# Data

## The source

Sportradar's validated play-by-play, one JSON file per game, from the quants blob storage under `data-nba`, copied to `C:\Data\Basketball\ValidatedData\PlayByPlay` and read with the `OddsFactory.Sports.Data.Contracts` types and never written. Only regular-season games with no invalid events are used, for the training data and for the teams' and players' history alike.

Each game is read into possessions and actions the graph can play, then replayed through the graph. A game the reader cannot turn into possessions, or whose events the rules cannot follow, is rejected whole; generating the training data prints how many games were used and how many were rejected, by reason.

## What GameGraph writes

Everything goes to `C:\Data\GameGraph\nba\`:

| Folder | What |
|---|---|
| `training-data\<node>` | One node's training data: `examples.csv` (one row per example, or per candidate for a choice), `games.csv` (each game's id, start and number of examples) and `manifest.json` (the node's features and declared model) |
| `fitted-models` | Per node: `<node>.json` (the fitted model), `<node>.training.json` (the training report) and `<node>.digest` (what the fit was made from) |
| `audit\calibration`, `audit\simulation` | The audits' runs, one folder per run, as Parquet |

The formats are described by the JSON Schemas in `schemas/`. The game context (each team's and player's history) is computed in memory as the games are walked, and never written.
