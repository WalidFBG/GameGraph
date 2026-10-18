# Data

## Source

`C:\Data\Basketball\ValidatedData\PlayByPlay`: validated Sportradar play-by-play, one JSON file per game, read with the `OddsFactory.Sports.Data.Contracts` types. It is read only, never written.

## Reading a game (step 1)

The labeller walks each game's events in order and skips anything step 1 does not use.

1. A game is used only if its validation summary reports no invalid events. Others are skipped and counted. (The game-level status field is missing in some 2025 games; the summary flag is always present.)
2. Possession is decided by who acts, not by the data's `teamInPossession` field, which flips for single events. The acting team of an event is:
   - field-goal attempt: the shooter's team;
   - turnover: the team of the player who committed it; a team turnover (no player) uses that event's `teamInPossession`, the only place the field is used;
   - free throw of type `Regular`: the shooter's team.
3. Consecutive actions by the same team form one possession. A change of acting team, or the end of a period, ends it. So offensive rebounds and and-ones stay inside their possession.
4. Free throws of other types (`Technical`, `Flagrant`, `ClearPath`) are ignored: they can be taken by the defending team in the middle of a possession.
5. Every other event is ignored in step 1 (rebounds, fouls, lineup changes, timeouts, reviews, jump balls).
6. A possession's outcome is 1 if it contains at least one made field goal, 0 otherwise. A second made field goal in one possession is legitimate (and-one, missed free throw, offensive rebound, another basket); step 1 ignores it and the summary counts it.
7. Checks that fail a game (skipped, counted by reason, never silently): an action whose team cannot be determined, and a difference of more than 4 possessions between the two teams.
8. The summary over all games (games used and skipped by reason, possessions per team per game, made rate) is checked before anything is built on top.

## Data GameGraph creates

Lives in `C:\Data\GameGraph\nba\`:

| Folder | What |
|---|---|
| `context` | Pre-game context per game (augmentation) |
| `rows\<model>` | Labelled rows, one dataset per model |
| `parameters` | Trained parameters, one file per model |
| `simulations` | Simulation outputs and pricing |

- Parameters, settings and summaries are JSON; labelled rows are CSV to start.
- Each dataset folder has a `manifest.json`: source data, GameGraph commit, seed, date and row count.
- Paths come from configuration.
