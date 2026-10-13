# GameGraph

A sport-agnostic way to model a sport once and use that one definition for everything:

- **Simulate**: Monte Carlo games for pricing.
- **Label**: walk real games and turn every decision into a training row.
- **Train**: fit the models those rows are for.

A sport is described as a graph of decisions (a shot, who shoots, how long a possession lasts) plus plain C# rules that update the game state. The engine walks that graph. The only difference between simulating and labelling is where each decision's outcome comes from: drawn from a model, or read from what actually happened. Training and simulation use the same model code, so what is trained is exactly what runs.

The NBA is the first (and for now only) sport.

## Status

Planning. See `docs/plan.md`.

## Layout

| Path | What it holds |
|---|---|
| `src/GameGraph.Models` | Model families: evaluate, log-likelihood, gradient |
| `src/GameGraph.Engine` | The walk: state, nodes, outcome sources, run loop, randomness, sinks |
| `src/GameGraph.Training` | Labelled rows, optimiser, training loop, evaluation |
| `src/GameGraph.Nba` | The NBA: state, rules, features, data loading, augmentation |
| `src/GameGraph.Host` | Command line: label, train, simulate |
| `docs/` | Architecture, plan, lessons |

## Build

```
dotnet build GameGraph.slnx
```

Requires the .NET 10 SDK.

## Data

No data is stored in this repository. Real game data stays on your machine; its location is passed in as configuration.
