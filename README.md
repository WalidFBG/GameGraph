# GameGraph

A sport-agnostic way to model a sport once and use that one definition for everything:

- **Simulate**: Monte Carlo games for pricing.
- **Generate training data**: walk real games and turn every decision into a training example.
- **Train**: fit the models those rows are for.

A sport is described as a graph of decisions (a shot, who shoots, how long a possession lasts) plus plain C# rules that update the game state. The engine walks that graph. The only difference between simulating and generating training data is where each decision's outcome comes from: drawn from a model, or read from what actually happened. Training and simulation use the same model code, so what is trained is exactly what runs.

The NBA is the first (and for now only) sport.

## Status

In progress. See `docs/plan.md`, and `docs/models.md` for the new models (trained in Python, evaluated in C#).

## Layout

| Path | What it holds |
|---|---|
| `src/GameGraph.ModelFamilies` | Models as GLMs: terms (a feature and its basis), linear predictors, and families (Bernoulli, Categorical) |
| `src/GameGraph.Models` | The new models (GAMLSS), evaluated and sampled in C#; being built on its own (`docs/models.md`) |
| `python/` | Training in Python, for the new models; being built on its own (`docs/models.md`) |
| `schemas/` | The contract between C# and Python: JSON Schemas for the training data, fitted models and training reports |
| `testdata/` | Test data only: reference outputs (golden files) written by `python/tests/golden.py` (`uv run python tests/golden.py`) and checked by both test suites |
| `src/GameGraph.Engine` | The walk: state, nodes, outcome deciders, run loop, randomness, decision collectors |
| `src/GameGraph.Parameters` | Trained parameters by node, predictor and term, and their file format |
| `src/GameGraph.Simulation` | Simulates a game many times: samples each decision from its model, reports incidents |
| `src/GameGraph.Training` | Orchestrates training: reads training data, fits parameters, writes them |
| `src/GameGraph.TrainingDataGeneration` | Collects what the engine's walk hands it and writes it as training data |
| `src/Nba/GameGraph.Nba` | The NBA: state, rules, features, observed games |
| `src/Nba/GameGraph.Nba.Sportradar` | Reads Sportradar ValidatedData into observed games |
| `src/Nba/GameGraph.Nba.Augmentation` | Team history and each game's context |
| `src/Nba/GameGraph.Nba.TrainingDataGeneration` | Walks observed games into training data |
| `src/Nba/GameGraph.Nba.QualityAssurance` | Analyses as tests: simulate real games, compare, write CSVs |
| `src/GameGraph.Host` | Command line: generate training data, train, simulate |
| `docs/` | Architecture, plan, data, lessons, and the proposed possession tree (`possession-tree.html`) |

## Build

```
dotnet build GameGraph.slnx
dotnet test --solution GameGraph.slnx
```

The Python, from `python/`, needs [uv](https://docs.astral.sh/uv/), which installs Python 3.13 and the packages:

```
uv sync
uv run pytest
uv run ruff check
uv run ruff format --check
uv run pyright
```

The C# requires the .NET 10 SDK, and access to the OddsFactory NuGet feed for `OddsFactory.Sports.Data.Contracts`. The vulnerability audit uses nuget.org only (`auditSources` in `nuget.config`), so builds do not depend on the feed's audit data.

## Data

No real data is stored in this repository; `testdata/` holds only synthetic test data. Real game data stays on your machine; its location is set in `src/GameGraph.Host/Program.cs`.
