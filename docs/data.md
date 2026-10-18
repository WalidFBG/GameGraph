# Data

## Source

`C:\Data\Basketball\ValidatedData\PlayByPlay`: validated Sportradar play-by-play, one JSON file per game, read with the `OddsFactory.Sports.Data.Contracts` types. It is read only, never written.

## Reading a game (step 1)

Training-data generation walks each game's events in order and skips anything step 1 does not use.

1. Only `RegularSeason` games are used, for training data and for history alike. Pre-season, post-season, play-in and in-season tournament games are rejected and counted.
2. A game is used only if its validation summary reports no invalid events. Others are rejected and counted. (The game-level status field is missing in some 2025 games; the summary flag is always present.)
3. Possession is decided by who acts, not by the data's `teamInPossession` field, which flips for single events. The acting team of an event is:
   - field-goal attempt: the shooter's team;
   - turnover: the team of the player who committed it; a team turnover (no player) uses that event's `teamInPossession`, the only place the field is used;
   - free throw of type `Regular`: the shooter's team.
4. Consecutive actions by the same team form one possession. A change of acting team, or the end of a period, ends it. So offensive rebounds and and-ones stay inside their possession.
5. Free throws of other types (`Technical`, `Flagrant`, `ClearPath`) are ignored: they can be taken by the defending team in the middle of a possession.
6. Every other event is ignored in step 1 (rebounds, fouls, lineup changes, timeouts, reviews, jump balls).
7. A possession's outcome is 1 if it contains at least one made field goal, 0 otherwise. A second made field goal in one possession is legitimate (and-one, missed free throw, offensive rebound, another basket); step 1 ignores it.
8. Checks that fail a game (rejected, counted by reason, never silently): an action whose team cannot be determined, and a difference of more than 4 possessions between the two teams.

## Data GameGraph creates

Lives in `C:\Data\GameGraph\nba\`:

| Folder | What |
|---|---|
| `training-data\<node>` | Training data for one model: `examples.csv`, `games.csv` and `manifest.json` |
| `parameters` | Per model: `<node>.json`, the parameters (intercept and weights by feature name), and `<node>.training.json`, the training run (source, date, best and last step, training and validation NLL at the best step) |
| `simulations` | Simulation outputs: one CSV per simulated game, one row per simulation |
| `quality-assurance\<test>` | Output of the quality-assurance tests, for example one row per simulation of the latest game |

- Parameters, settings and summaries are JSON; training data is CSV to start.
- Each training-data folder has a `manifest.json`: node, source data, date, feature names, example count and how many games were generated, short of history or rejected, by reason.
- `examples.csv` holds one row per example, games one after another in start-time order; `games.csv` holds one row per game (id, start time, number of examples), so examples are grouped by game without repeating the game on every row.
- Training splits by game: the latest 20% of games by start time are the validation set.
- Augmentation values are never written: they are computed in memory as the season is walked (see `plan.md`).
- Paths are set in `src/GameGraph.Host/Program.cs`.
