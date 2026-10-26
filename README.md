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
| `src/GameGraph.Contract` | The contract with Python: model declarations, fitted models, the training-data manifest and training data, as records with their invariants |
| `src/GameGraph.Contract.Files` | Reads and writes the contract's files, validated against the schemas: the only C# that knows their formats |
| `src/GameGraph.Models` | The models (GAMLSS, `docs/models.md`): evaluating and sampling fitted models |
| `python/` | Training, in Python: reads each node's training data, writes its fitted model and report (`gamegraph.contract`, `gamegraph.files`, `gamegraph.models`, `gamegraph.training`) |
| `schemas/` | The contract between C# and Python: JSON Schemas for the training data, fitted models and training reports |
| `testdata/` | Test data only: reference outputs (golden files) written by `python/tests/golden.py` (`uv run python tests/golden.py`) and checked by both test suites, and the simulation fixture (`nba-simulation`: the latest game's context and the fitted models) that the digest test and the benchmark simulate |
| `src/GameGraph.Engine` | The walk: state, nodes, outcome deciders, run loop, randomness, decision collectors, among them the one that records training examples |
| `src/GameGraph.Simulation` | Simulates a game many times, on the plain walk or compiled (`CompiledGame`): samples each decision from its node's fitted model, reports incidents |
| `src/Nba/GameGraph.Nba` | The NBA: state, rules, features, observed games |
| `src/Nba/GameGraph.Nba.Sportradar` | Reads Sportradar ValidatedData into observed games |
| `src/Nba/GameGraph.Nba.Augmentation` | Team history and each game's context |
| `src/Nba/GameGraph.Nba.TrainingDataGeneration` | Walks observed games into training data |
| `src/Nba/GameGraph.Nba.Fixture` | Reads and writes the simulation fixture in `testdata/nba-simulation` |
| `src/Nba/GameGraph.Nba.Tests.Unit` | The digest test (a fixed number of simulations of the fixture game, every decision and incident hashed and pinned), the replay's tests on hand-made games, and the compiled walk's tests: its parameters bit for bit, its outcomes against the plain walk's, and one thread against many |
| `src/Nba/GameGraph.Nba.Benchmark` | Times runs of 10,000 simulations of the fixture game on all threads, plain and compiled (Release only) |
| `src/Nba/GameGraph.Nba.Tests.QualityAssurance` | The audits (`docs/plan.md`), explicit facts that only write data: the calibration audit writes every example with the C# model's probabilities, the simulation audit every simulated decision and game; `SimulationFixtureRefresh` rewrites the simulation fixture from the latest game and the fitted models. Entry points at the root, readers and writers in `Infrastructure`, the data written in `Definitions`, the logic in `Domain` |
| `python/src/gamegraph/audit` | Analyses of the audits' data: calibration and simulation buckets, game statistics |
| `src/Tests` | Unit tests: `GameGraph.Engine.Tests.Unit` (the walk, on a toy game), `GameGraph.Models.Tests.Unit` (the forward pass and the parity with Python), `GameGraph.Contract.Files.Tests.Unit` (the contract and its files) and `GameGraph.Simulation.Tests.Unit` (the random numbers) |
| `src/GameGraph.Host` | Command line: generates the training data and writes it |
| `docs/` | Architecture, plan, compilation (the speed work and its decisions), data, lessons, and the possession tree as built (`possession-tree.html`) |

## Build

Everything is built, tested and run in Release, so every timing and every output comes from the build that is benchmarked:

```
dotnet build -c Release GameGraph.slnx
dotnet test -c Release --solution GameGraph.slnx
dotnet run -c Release --project src/Nba/GameGraph.Nba.Benchmark
```

The Python, from `python/`, needs [uv](https://docs.astral.sh/uv/), which installs Python 3.13 and the packages:

```
uv sync
uv run pytest
uv run ruff check
uv run ruff format --check
uv run pyright
```

To train the nodes, from the training data the Host writes to the fitted models simulation reads:

```
uv run gamegraph-train <training data> <fitted models> [--all]
```

A node keeps its fitted model when the digest recorded with it (`<node>.digest`) still matches: its training data (examples, games and manifest, apart from when it was written), the trainer's source and the torch version. The rest are trained; `--all` retrains every node regardless.

The audits run only when asked for by name; each analysis reads its audit's latest run:

```
src/Nba/GameGraph.Nba.Tests.QualityAssurance/bin/Release/net10.0/GameGraph.Nba.Tests.QualityAssurance.exe -class GameGraph.Nba.Tests.QualityAssurance.CalibrationAudit -explicit only
uv run gamegraph-calibration <calibration audits> <training data>
src/Nba/GameGraph.Nba.Tests.QualityAssurance/bin/Release/net10.0/GameGraph.Nba.Tests.QualityAssurance.exe -class GameGraph.Nba.Tests.QualityAssurance.SimulationAudit -explicit only
uv run gamegraph-simulation <simulation audits> <training data>
src/Nba/GameGraph.Nba.Tests.QualityAssurance/bin/Release/net10.0/GameGraph.Nba.Tests.QualityAssurance.exe -class GameGraph.Nba.Tests.QualityAssurance.SimulationFixtureRefresh -explicit only
```

The C# requires the .NET 10 SDK, and access to the OddsFactory NuGet feed for `OddsFactory.Sports.Data.Contracts`. The vulnerability audit uses nuget.org only (`auditSources` in `nuget.config`), so builds do not depend on the feed's audit data.

## Data

No real game data is stored in this repository. `testdata/` holds synthetic test data, and one derived fixture: the fitted models (coefficients only) and one game's context (each player's rates and shares, by index, with no ids or names). Real game data stays on your machine; its location is set in `src/GameGraph.Host/Program.cs`.
