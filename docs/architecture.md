# Architecture

## The idea

A sport is a graph of decisions. At each decision (a node) something is chosen: whether a shot goes in, who takes it, how many seconds a possession lasts. After each choice, the sport's rules update the game state and say which node comes next.

The engine walks this graph. What it does at each node depends on the mode:

| Mode | Where the outcome comes from | What is emitted |
|---|---|---|
| Simulate | Drawn from the node's model | Game events, for pricing |
| Label | Read from a real game's record | A row: the node's features and the observed outcome |

Everything else (features, rules, state updates) is the same code in both modes. This is the point of the design: training rows are built by exactly the code that later simulates.

## Projects and dependencies

```
GameGraph.Host
  ├── GameGraph.Nba ──► GameGraph.Engine ──► GameGraph.Models
  └── GameGraph.Training ─────────────────► GameGraph.Models
```

- **Models** knows nothing about sports or the engine. Each model family provides: evaluate (parameters and features to a distribution), log-likelihood, and gradient.
- **Engine** uses Models to evaluate and sample. It knows nothing about any sport and nothing about training.
- **Training** uses Models to fit parameters to labelled rows. It knows nothing about the engine.
- **Nba** is a sport: its state, rules, features, the mapping from real data to node outcomes, and augmentation.
- **Host** wires them together.

Engine and Training never reference each other. They share only the model families, which is what guarantees that what is trained is what runs.

## Inputs to a node

A node's features can read two kinds of input:

- **Game state**: changes during the game (score, clock, possession).
- **Game context**: fixed for the whole game, computed before tip-off from historical data (augmentation, for example a team's shooting over its last 20 games).

Context is built by one piece of code for both training (historical games) and live pricing, and only ever from data before the game starts.

## Lines drawn from day one

- Models are separate from the engine.
- An outcome source (sample or observe) is the only difference between simulating and labelling.
- Game context is separate from game state.
- Every random draw comes from a seeded stream; every run is reproducible.
- When the data and the rules disagree, fail loudly.

## Not yet

- No compilation and no speed work until the full flow runs and its output is pinned by a test.
- No generality without a second use: one decision kind and one model family until a second is needed.
