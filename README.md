# GameGraph

GameGraph models a sport as a graph of decisions (who shoots, does it go in, how long did the action take) and uses that one definition for three jobs:

- **Simulate** games many times, for pricing.
- **Generate training data** by replaying real games through the same graph: every decision becomes a training example.
- **Train** each decision's model, in Python.

The NBA is the only sport so far. Open [`docs/graph.html`](docs/graph.html) in a browser to watch a simulated game walk through its graph, or to make the decisions yourself.

## Docs

| | |
|---|---|
| [`docs/graph.html`](docs/graph.html) | The NBA graph, interactive: every node, what it asks, what it reads, how often each answer comes up and where it leads |
| [`docs/how-it-works.md`](docs/how-it-works.md) | The walk, simulation against training data, and how C# and Python share the models |
| [`docs/models.md`](docs/models.md) | The models and how they are trained |
| [`docs/data.md`](docs/data.md) | Where the data comes from and where everything is written |

## Data

The real games are Sportradar's validated play-by-play, in the quants blob storage under `data-nba`. Copy them to `C:\Data\Basketball\ValidatedData\PlayByPlay`, where the Host and the audits read them (see [`docs/data.md`](docs/data.md)). The tests and the benchmark need no data: they run on the fixture in `testdata/`.

## Build and test

Everything is built, tested and run in Release.

```
dotnet build -c Release GameGraph.slnx
dotnet test -c Release --solution GameGraph.slnx
```

From `python/`, with [uv](https://docs.astral.sh/uv/):

```
uv sync
uv run pytest
uv run ruff check
uv run ruff format --check
uv run pyright
```

## Run

1. **Generate the training data** from the real games:

   ```
   dotnet run -c Release --project src/GameGraph.Host
   ```

2. **Train** every node whose training data or trainer changed since its last fit (`--all` retrains every node), from `python/`:

   ```
   uv run gamegraph-train C:\Data\GameGraph\nba\training-data C:\Data\GameGraph\nba\fitted-models [--all]
   ```

3. **Check the models.** Two audits write data, and two Python analyses read their latest run. The calibration audit compares each node's predicted probabilities with the training data; the simulation audit simulates every training game once and compares decisions, games and box scores with the real ones:

   ```
   src/Nba/GameGraph.Nba.Tests.QualityAssurance/bin/Release/net10.0/GameGraph.Nba.Tests.QualityAssurance.exe -class GameGraph.Nba.Tests.QualityAssurance.CalibrationAudit -explicit only
   uv run gamegraph-calibration C:\Data\GameGraph\nba\audit\calibration C:\Data\GameGraph\nba\training-data
   src/Nba/GameGraph.Nba.Tests.QualityAssurance/bin/Release/net10.0/GameGraph.Nba.Tests.QualityAssurance.exe -class GameGraph.Nba.Tests.QualityAssurance.SimulationAudit -explicit only
   uv run gamegraph-simulation C:\Data\GameGraph\nba\audit\simulation C:\Data\GameGraph\nba\training-data
   ```

4. **Time the simulator**: 10,000 simulations of the fixture game on all cores, on the plain and the compiled walk:

   ```
   dotnet run -c Release --project src/Nba/GameGraph.Nba.Benchmark
   ```

5. **Refresh the fixture** after the nodes change (the digest test fails until you do, then pin its new digest):

   ```
   src/Nba/GameGraph.Nba.Tests.QualityAssurance/bin/Release/net10.0/GameGraph.Nba.Tests.QualityAssurance.exe -class GameGraph.Nba.Tests.QualityAssurance.SimulationFixtureRefresh -explicit only
   ```

## Where to start in the code

- **The NBA's graph**: `src/Nba/GameGraph.Nba/Graph`, in `NbaNodes.cs` (each node and its model), `NbaTransitions.cs` (the rules between nodes) and `NbaFeatures.cs`.
- **The walk**: `src/GameGraph.Engine/GameWalker.cs`.
- **Simulating a game**: `Simulator.Run` in `src/GameGraph.Simulation`, given a `FittedGame` (the plain walk) or a `CompiledGame` (the fast one).
- **Training**: `python/src/gamegraph/train_nodes.py`.

## Data in this repository

No real game data. `testdata/` holds synthetic test data, and one derived fixture (`testdata/nba-simulation`): the fitted models' coefficients and one game's player rates, by index, with no ids or names.
