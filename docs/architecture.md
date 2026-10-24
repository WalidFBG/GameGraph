# Architecture

## The idea

A sport is a graph of decisions. At each decision (a node) something is chosen: whether a possession scores, who shoots, how many seconds a possession lasts. After each choice, the sport's rules update the game state and say which node comes next.

## The walk

The engine performs the walk, in both modes. At every node it:

1. computes the node's features from the game state, using the sport's feature definitions;
2. asks the **outcome decider** for the node's outcome, given the state and the features;
3. hands the node, the state, the features and the outcome to the **decision collector**;
4. calls the node's **transition**, the sport's rule for the node: it updates the game state with the outcome, reports to the **incident collector** whatever the rules say happened (for example, a possession ended with 2 points), and returns the next node.

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
| Decision collector | does nothing | the training-data collector: stores node, features and outcome as training data |
| Incident collector | the outside world's | ignores incidents |

Who provides what:

- **The sport**: the game state, the transitions (update the state with an outcome, report incidents, return the next node), the feature definitions, its incident types and collector interface, and how to read observed outcomes from its data.
- **The engine**: the walk itself. The only thing that differs between modes is where each node's outcome comes from and who receives the two outputs.
- **The training-data collector**: nothing but a decision collector. It never tracks the score, applies a rule or computes a feature.

So features, rules and state updates are the same code in both modes. Training data is built by exactly the code that later simulates.

## Projects and dependencies

Sport-agnostic:

```
GameGraph.Host
  ├── GameGraph.TrainingDataGeneration ───► GameGraph.Engine ──► GameGraph.ModelFamilies
  └── GameGraph.Training ─────────────────────────────────────► GameGraph.ModelFamilies

GameGraph.Training ──► GameGraph.Parameters ──► GameGraph.ModelFamilies
GameGraph.Simulation ──► GameGraph.Engine, GameGraph.ModelFamilies, GameGraph.Parameters
```

The NBA, in `src/Nba/`:

```
GameGraph.Host
  ├── GameGraph.Nba.Sportradar ──────────────► GameGraph.Nba ──► GameGraph.Engine
  └── GameGraph.Nba.TrainingDataGeneration
        ├── GameGraph.Nba.Augmentation ──────► GameGraph.Nba
        ├── GameGraph.TrainingDataGeneration
        ├── GameGraph.Nba
        └── GameGraph.Engine

GameGraph.Nba.QualityAssurance (tests)
  ├── GameGraph.Nba.Sportradar
  ├── GameGraph.Nba.Augmentation
  ├── GameGraph.Simulation
  ├── GameGraph.Parameters
  ├── GameGraph.Nba
  └── GameGraph.Engine
```

- **ModelFamilies** knows nothing about sports, games or the engine. A model is a generalised linear model in three parts:
  - **terms**: a feature and a basis (linear now, splines later), which expand the feature's value into design columns, one coefficient each;
  - **linear predictors**: η = intercept + the sum of coefficients times design columns; a family needs one (Bernoulli) or several (Categorical: one per category but the reference);
  - **the family** (a struct implementing `IModelFamily`): link and distribution only. From the predictors' η it gives the negative log-likelihood of an outcome, its gradient with respect to η, and a sample from one uniform number. It never sees features or coefficients.

  The loops use only a `Model` (family and terms, bound to the feature columns) and a flat `double[]` of coefficients, which only `Model` knows how to lay out. Files hold both as data, and `Model` converts each way:

  | | In memory | In a file | To file | From file |
  |---|---|---|---|---|
  | Structure | `Model` | `ModelDescription` (manifest) | `model.Describe()` | `description.Create(features)` |
  | Coefficients | `double[]` | `PredictorCoefficients[]` (parameters file) | `model.Describe(parameters)` | `model.Parameters(coefficients)` |

  Each node declares its family and terms in the sport's spec, and builds its `Model` from its own features. Training-data generation writes the structure to the manifest; training recreates the `Model` from it; simulation uses the node's `Model` and fails if the parameters were trained for a different model.
- **Engine** owns the walk, the outcome-decider and decision-collector interfaces, and (later) sampling from model families. It knows nothing about any sport and nothing about training.
- **Parameters** is the contract between training and simulation: each node's family and the named form of its parameters (per predictor, an intercept and each term's feature, basis and coefficients), and their file format.
- **Simulation** simulates a game many times: each simulation gets its own seed and a fresh state, every decision is sampled from its model with the trained parameters, decisions are discarded, and incidents go to the caller's incident collector. It lines the parameters up with each node's features and fails on any mismatch; it reads no files.
- **Training** orchestrates training: reads training data, fits each model's parameters, writes them. It knows nothing about the engine. It moves to Python (see `models.md`), reading the same training-data files and writing the same parameters files.
- **TrainingDataGeneration** holds the training-data collector (a decision collector for the engine's walk that stores what it is handed) and writes the training data to file.
- **Nba** is the sport: its state, rules, features, and what an observed game looks like (possessions by side and period). It knows no data source and no training data.
- **Nba.Sportradar** reads Sportradar's ValidatedData into observed games. It is the only project that knows Sportradar.
- **Nba.Augmentation** keeps each team's history from observed games, taken in start-time order, and gives each game its context.
- **Nba.TrainingDataGeneration** walks the observed games: context, then the engine's walk with the observed outcome decider, then the history advances.
- **Nba.QualityAssurance** holds analyses as tests: simulate real games, check the simulations, write CSVs to `quality-assurance`.
- **Host** wires them together.

Engine and Training never reference each other. They share only the model families, which is what guarantees that what is trained is what runs. Once training is Python, parity tests give that guarantee instead: C# recomputes the training report's likelihoods from the same files and parameters, and must agree.

## Inputs to a node

A node's features can read two kinds of input, both held in the game state:

- **In-game values**: change during the game (score, clock, possession).
- **Game context**: fixed for the whole game, computed before tip-off from historical data (augmentation, for example a team's scoring over its last 20 games).

Context is never written to file. It is computed side by side with the walk: games are taken in start-time order, each is walked with the context from earlier games, and only then is it added to the history.

In production, augmentation runs one game at a time: the latest histories are saved after each game and loaded before the next. The season loop over many games exists only inside this solution, for training-data generation and analysis.

## Lines drawn from day one

- The engine performs the walk in both modes; game logic lives only in the sport's rules.
- Model families are separate from the engine.
- An outcome decider (sample or observe) is the only difference between simulating and generating training data.
- Every random draw comes from a seeded stream; every run is reproducible.
- When the data and the rules disagree, fail loudly.
- The outside world sees only incidents: never decisions, features or state.

## Not yet

- No compilation and no speed work until the full flow runs and its output is pinned by a test.
- No generality without a use: each new model family, outcome type or feature shape arrives with the node that needs it.
