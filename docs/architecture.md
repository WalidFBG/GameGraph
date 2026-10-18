# Architecture

## The idea

A sport is a graph of decisions. At each decision (a node) something is chosen: whether a possession scores, who shoots, how many seconds a possession lasts. After each choice, the sport's rules update the game state and say which node comes next.

## The walk

The engine performs the walk, in both modes. At every node it:

1. computes the node's features from the game state, using the sport's feature definitions;
2. asks the **outcome decider** for the node's outcome;
3. hands the features and the outcome to the **sink**;
4. calls the node's **transition**, the sport's rule for the node: it updates the game state with the outcome and returns the next node.

It repeats this until a transition returns the end of the game.

| | Simulate | Generate training data |
|---|---|---|
| Outcome decider | samples from the node's model | reads the observed outcome from a real game, as the sport says how |
| Sink | records the game's events, for pricing | the training-data collector: copies features and outcome, and stores them as training data |

Who provides what:

- **The sport**: the game state, the transitions (update the state with an outcome, return the next node), the feature definitions, and how to read observed outcomes from its data.
- **The engine**: the walk itself. The only thing that differs between modes is where each node's outcome comes from.
- **The training-data collector**: nothing but a sink. It never tracks the score, applies a rule or computes a feature.

So features, rules and state updates are the same code in both modes. Training data is built by exactly the code that later simulates.

## Projects and dependencies

Sport-agnostic:

```
GameGraph.Host
  ├── GameGraph.TrainingDataGeneration ───► GameGraph.Engine ──► GameGraph.ModelFamilies
  └── GameGraph.Training ─────────────────────────────────────► GameGraph.ModelFamilies
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
```

- **ModelFamilies** knows nothing about sports, games or the engine. Each model family provides: evaluate (parameters and features to a distribution), log-likelihood, and gradient.
- **Engine** owns the walk, the outcome-decider and sink interfaces, and (later) sampling from model families. It knows nothing about any sport and nothing about training.
- **Training** orchestrates training: reads training data, fits each model's parameters, writes them. It knows nothing about the engine.
- **TrainingDataGeneration** holds the training-data collector (a sink for the engine's walk that stores what it is handed) and writes the training data to file.
- **Nba** is the sport: its state, rules, features, and what an observed game looks like (possessions by side and period). It knows no data source and no training data.
- **Nba.Sportradar** reads Sportradar's ValidatedData into observed games. It is the only project that knows Sportradar.
- **Nba.Augmentation** keeps each team's history from observed games, taken in start-time order, and gives each game its context.
- **Nba.TrainingDataGeneration** walks the observed games: context, then the engine's walk with the observed outcome decider, then the history advances.
- **Host** wires them together.

Engine and Training never reference each other. They share only the model families, which is what guarantees that what is trained is what runs.

## Inputs to a node

A node's features can read two kinds of input, both held in the game state:

- **In-game values**: change during the game (score, clock, possession).
- **Game context**: fixed for the whole game, computed before tip-off from historical data (augmentation, for example a team's scoring over its last 20 games).

Context is never written to file. It is computed side by side with the walk: games are taken in start-time order, each is walked with the context from earlier games, and only then is it added to the history.

## Lines drawn from day one

- The engine performs the walk in both modes; game logic lives only in the sport's rules.
- Model families are separate from the engine.
- An outcome decider (sample or observe) is the only difference between simulating and generating training data.
- Every random draw comes from a seeded stream; every run is reproducible.
- When the data and the rules disagree, fail loudly.

## Not yet

- No compilation and no speed work until the full flow runs and its output is pinned by a test.
- No generality without a second use: one decision kind and one model family until a second is needed.
