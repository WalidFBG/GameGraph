# Architecture

## The idea

A sport is a graph of decisions. At each decision (a node) something is chosen: whether a possession scores, who shoots, how many seconds a possession lasts. After each choice, the sport's rules update the game state and say which node comes next.

## The walk

The engine performs the walk, in both modes. At every node it:

1. computes the node's features from the game state, using the sport's feature definitions: one row for a vector node, or one row per candidate for a choice node (who shoots: the candidates come from the state, and each feature reads the state and a candidate), whose outcome is the chosen row;
2. asks the **outcome decider** for the node's outcome, given the state and the features;
3. hands the node, the state, the features and the outcome to the **decision collector**;
4. calls the node's **transition**, the sport's rule for the node: it updates the game state with the outcome, reports to the state's **incident collector** whatever the rules say happened (for example, a possession ended with 2 points), and returns the next node.

It repeats this until a transition returns the end of the game.

The walk has two outputs, for two audiences:

| | Decisions | Incidents |
|---|---|---|
| What | node, features and outcome, for every model decision | sport-specific facts, for example a possession's side, period and points |
| When | at every node | at the points the sport's rules choose, for example the end of a possession |
| Received by | a decision collector | an incident collector |
| Defined by | the engine | the sport: its incident types and its collector interface (`INbaIncidentCollector`) are its public contract |
| Seen by | only this solution: training-data generation, diagnostics | the outside world, which aggregates incidents into whatever it prices |

| | Simulate | Generate training data |
|---|---|---|
| Outcome decider | samples from the node's model | reads the observed outcome from a real game, as the sport says how |
| Decision collector | does nothing | the recording decision collector: records node, features and outcome as training examples |
| Incident collector | the outside world's, one per simulation | ignores incidents |

Who provides what:

- **The sport**: the game state (which holds the incident collector, so the engine never sees incidents), the transitions (update the state with an outcome, report incidents, return the next node), the feature definitions, its incident types and collector interface, and how to read observed outcomes from its data.
- **The engine**: the walk itself. The only thing that differs between modes is where each node's outcome comes from and who receives the two outputs.
- **The recording decision collector**: it only stores what the walk hands it. It never tracks the score, applies a rule, computes a feature or writes a file. Simulating normally passes the empty collector; the simulation audit passes the recording one to see the simulated decisions.

So features, rules and state updates are the same code in both modes. Training data is built by exactly the code that later simulates.

## Projects and dependencies

The core is pure: values in, values out, no file system, serialisation or console. Only the edge does I/O, and references point from the edge to the core, never the other way.

- **Core**: Contract, Engine, Models, Simulation, Nba, Nba.Augmentation, Nba.TrainingDataGeneration.
- **Edge**: Contract.Files, Nba.Sportradar (it adapts an external format), Host, the QA and test projects; in Python, `gamegraph.files`, `gamegraph.train_nodes` and the audit.

Sport-agnostic:

```
GameGraph.Engine ──────► GameGraph.Contract
GameGraph.Models ──────► GameGraph.Contract
GameGraph.Simulation ──► GameGraph.Engine, GameGraph.Models
GameGraph.Contract.Files ──► GameGraph.Contract
GameGraph.Host ──► GameGraph.Contract.Files, GameGraph.Nba.Sportradar, GameGraph.Nba.TrainingDataGeneration
```

Training is Python (`python/`); it shares only files with the C#, written and read against the schemas in `schemas/`.

The NBA, in `src/Nba/`:

```
GameGraph.Host
  ├── GameGraph.Nba.Sportradar ──────────────► GameGraph.Nba ──► GameGraph.Engine, GameGraph.Contract
  └── GameGraph.Nba.TrainingDataGeneration
        ├── GameGraph.Nba.Augmentation ──────► GameGraph.Nba
        ├── GameGraph.Nba
        └── GameGraph.Engine

GameGraph.Nba.Tests.QualityAssurance
  ├── GameGraph.Nba.Sportradar
  ├── GameGraph.Nba.Augmentation
  ├── GameGraph.Contract.Files
  ├── GameGraph.Simulation
  ├── GameGraph.Models
  ├── GameGraph.Nba
  └── GameGraph.Engine
```

- **Contract** is the contract with Python as records with their invariants: a model's declaration, the fitted model, the training-data manifest, and a node's training data (its games, each with its examples; a choice's example holds one row of features per candidate, and its outcome is the chosen row). It references nothing and knows no file format.
- **Contract.Files** reads and writes the contract's files: the manifest and fitted model as JSON validated against the schemas (the polymorphic records get their discriminators here, not from attributes), the training data as CSV, and the training report, which only the parity tests read. It is the only C# that knows the formats.
- **Models** knows nothing about sports, games, the engine or files (see `models.md`): the forward pass, a fitted model's parameter values, log-probability and samples, from the node's features.
- **Engine** owns the walk and the outcome-decider and decision-collector interfaces, and the recording decision collector (a decision collector that records each node's examples, game by game, as it is handed them, in either mode). Each node declares its model over the sport's features (a distribution, the terms of each predicted parameter, each a feature with its basis, and the feature of each given parameter) and takes its features from it, so the two cannot disagree. A game numbers its nodes, and a node belongs to one game. It knows nothing about any sport, nothing about files and nothing about how training works.
- **Simulation** binds the fitted models to a game once (`FittedGame`) and simulates it many times: each simulation gets its own seed and a fresh state, every decision is sampled from its node's fitted model, decisions are discarded, and incidents go to the incident collector the caller puts in each simulation's state. It fails if a fitted model was declared differently from its node; it reads no files.
- **Training** is Python: it reads each node's training data and writes its fitted model and training report (`uv run gamegraph-train`).
- **Nba** is the sport: its state, rules, features, and what an observed game looks like (possessions by side and period, each action with its clock, and the free throws outside the play). Its incidents are the possession and the awarded free throw; the tree is `possession-tree.html`. It knows no data source and no training data.
- **Nba.Sportradar** reads Sportradar's ValidatedData into observed games. It is the only project that knows Sportradar.
- **Nba.Augmentation** keeps each team's history from observed games, taken in start-time order, and gives each game its context.
- **Nba.TrainingDataGeneration** walks the observed games: context, then the engine's walk with the observed outcome decider, then the history advances.
- **Nba.Tests.QualityAssurance** holds analyses as tests (simulate real games, check the simulations, write CSVs to `quality-assurance`) and the audits (explicit facts that write Parquet to `audit`, see `plan.md`): entry points at its root, readers and writers in `Infrastructure`, the data written in `Definitions`, the logic in `Domain`.
- **Host** wires them together: it walks the season, then gives each node's examples their manifest (the node's features and declared model) and writes them.

The engine and training never meet: they share only files. What guarantees that what is trained is what runs is the parity tests: C# recomputes the training report's likelihoods from the same files and fitted models, and draws the same samples, to 1e-12.

## Inputs to a node

A node's features can read two kinds of input, both held in the game state:

- **In-game values**: change during the game (score, clock, possession).
- **Game context**: fixed for the whole game, computed before tip-off from historical data (augmentation, for example a team's scoring over its last 20 games).

Context is never written to file. It is computed side by side with the walk: games are taken in start-time order, each is walked with the context from earlier games, and only then is it added to the history.

In production, augmentation runs one game at a time: the latest histories are saved after each game and loaded before the next. The season loop over many games exists only inside this solution, for training-data generation and analysis.

## Lines drawn from day one

- The engine performs the walk in both modes; game logic lives only in the sport's rules.
- Models are separate from the engine.
- An outcome decider (sample or observe) is the only difference between simulating and generating training data.
- Every random draw comes from a seeded stream; every run is reproducible.
- When the data and the rules disagree, fail loudly.
- The outside world sees only incidents: never decisions, features or state.

## Not yet

- No compilation and no speed work until the full flow runs and its output is pinned by a test.
- No generality without a use: each new model family, outcome type or feature shape arrives with the node that needs it.
