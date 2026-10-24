# GameGraph

A sport-agnostic way to model a sport once and use that one definition for everything:

- **Simulate**: Monte Carlo games for pricing.
- **Generate training data**: walk real games and turn every decision into a training example.
- **Train**: fit the models those rows are for.

A sport is described as a graph of decisions (a shot, who shoots, how long a possession lasts) plus plain C# rules that update the game state. The engine walks that graph. The only difference between simulating and generating training data is where each decision's outcome comes from: drawn from a model, or read from what actually happened. Training (Python) and simulation (C#) meet only in files, and parity tests check that C# computes exactly what Python fitted.

The NBA is the first (and for now only) sport.

## Status

In progress. See `docs/plan.md`, and `docs/models.md` for the models (trained in Python, evaluated in C#).

## Layout

| Path | What it holds |
|---|---|
| `src/GameGraph.Models` | The models (GAMLSS, `docs/models.md`): the contract with Python, and evaluating and sampling fitted models |
| `python/` | Training, in Python: reads each node's training data, writes its fitted model and report |
| `schemas/` | The contract between C# and Python: JSON Schemas for the training data, fitted models and training reports |
| `testdata/` | Test data only: reference outputs (golden files) written by `python/tests/golden.py` (`uv run python tests/golden.py`) and checked by both test suites |
| `src/GameGraph.Engine` | The walk: state, nodes, outcome deciders, run loop, randomness, decision collectors |
| `src/GameGraph.Simulation` | Simulates a game many times: samples each decision from its node's fitted model, reports incidents |
| `src/GameGraph.TrainingDataGeneration` | Collects what the engine's walk hands it and writes it as training data |
| `src/Nba/GameGraph.Nba` | The NBA: state, rules, features, observed games |
| `src/Nba/GameGraph.Nba.Sportradar` | Reads Sportradar ValidatedData into observed games |
| `src/Nba/GameGraph.Nba.Augmentation` | Team history and each game's context |
| `src/Nba/GameGraph.Nba.TrainingDataGeneration` | Walks observed games into training data |
| `src/Nba/GameGraph.Nba.Tests.QualityAssurance` | Analyses as tests: simulate real games, compare, write CSVs |
| `src/Tests` | Unit tests: `GameGraph.Engine.Tests.Unit` (the walk, on a toy game) and `GameGraph.Models.Tests.Unit` (the forward pass and the contract) |
| `src/GameGraph.Host` | Command line: generates the training data |
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

To train every node, from the training data the Host writes to the fitted models simulation reads:

```
uv run gamegraph-train <training data> <fitted models>
```

The C# requires the .NET 10 SDK, and access to the OddsFactory NuGet feed for `OddsFactory.Sports.Data.Contracts`. The vulnerability audit uses nuget.org only (`auditSources` in `nuget.config`), so builds do not depend on the feed's audit data.

## Data

No real data is stored in this repository; `testdata/` holds only synthetic test data. Real game data stays on your machine; its location is set in `src/GameGraph.Host/Program.cs`.
