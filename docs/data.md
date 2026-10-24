# Data

## Source

`C:\Data\Basketball\ValidatedData\PlayByPlay`: validated Sportradar play-by-play, one JSON file per game, read with the `OddsFactory.Sports.Data.Contracts` types. It is read only, never written.

## Reading a game

The reader walks each game's events in order and turns them into possessions, each a sequence of actions the possession tree can produce (see `possession-tree.html`). Anything it cannot read that way rejects the game, counted by reason, never silently.

1. Only `RegularSeason` games are used, for training data and for history alike. Pre-season, post-season, play-in and in-season tournament games are rejected and counted.
2. A game is used only if its validation summary reports no invalid events. Others are rejected and counted. (The game-level status field is missing in some 2025 games; the summary flag is always present.)
3. **Actions**:
   - a field-goal attempt is a two or a three, by the shooter's team;
   - a turnover, by the team of the player who committed it (a team turnover uses the event's `teamInPossession`); shot-clock violations are turnovers;
   - a `Personal` foul committed by the defence is a non-shooting foul: 2 free throws means in the bonus, 0 a side-out (the possession carries on).
4. **Shooting fouls**: a missed fouled shot is not recorded as a shot; the `Shooting` foul with 2 or 3 free throws stands in for it (a two or a three, fouled, missed). A `Shooting` foul with 1 free throw right after a made shot is an and-one (fouled, made).
5. **Free throws**: only `Regular` free throws count, and they belong to the action waiting for them, checked by the shooter's team. They are not matched by their foul's id: when a technical is called at the same moment, the shooting foul's free throws carry the technical's id.
6. **Rebounds**: only the rebound after a missed shot, or after a missed last free throw, counts. The dead-ball team rebounds after earlier free throws, and bookkeeping rebounds after a made shot, are skipped.
7. **Loose-ball fouls**: a `Personal` foul while a rebound is still loose gives the rebound to the team that drew it; the foul itself is then that team's non-shooting foul.
8. **Ignored**: `Technical`, `Flagrant`, `Offensive` (the turnover covers it) and `NotSet` fouls, and their free throws; transition take fouls (a `Personal` foul with 1 free throw: the fouled team keeps the ball), and their free throw, about 1 point every 3 or 4 games; `Personal` fouls by the team with the ball; every other event (lineup changes, timeouts, reviews, jump balls).
9. **The clock**: each action keeps the seconds left in its period when it happened, from the event that started it (the shot, the turnover or the foul; an and-one keeps its shot's). The data has no shot clock; the rules rebuild it.
10. **Possessions**: a possession ends with a turnover, a made shot (and its and-one), a made last free throw, a defensive rebound, or a non-shooting foul in the bonus whose last free throw is made; an offensive rebound or a side-out continues it. The next possession is the other team's.
11. **Possessions cut by the end of a period** (about one period in two: buzzer scrambles and bookkeeping rebounds) are dropped and counted. A stopgap until the clock, which models them.
12. **Rejections**: an action whose team cannot be determined, a shot type that is neither two nor three, an unexpected foul (for example a `Shooting` foul with 0 free throws), a free throw nothing was waiting for, an action that interrupts another (a shot while free throws or a rebound are pending), the same team starting two possessions in a row, a possession that ends without an ending action, an unknown rebound type, an action whose clock is later than the one before it in its period, a difference of more than 4 possessions between the two teams, and periods 2 to 4 not starting by the alternating rule (the team that did not start the first period starts the second and third, the one that did starts the fourth: 1 game in about 4,000). About 5% of the regular-season games with valid events are rejected.
13. The quality-assurance test `ObservedActionsTests` prints the summary: games by rejection reason, possessions, actions by type, fouled and made rates, free throws, rebounds, and the seconds from a period's start to its first action and between consecutive actions.

## Data GameGraph creates

Lives in `C:\Data\GameGraph\nba\`:

| Folder | What |
|---|---|
| `training-data\<node>` | Training data for one model: `examples.csv`, `games.csv` and `manifest.json` |
| `fitted-models` | Per model: `<node>.json`, the fitted model (the declaration, and per predicted parameter its intercept and each term's fit), and `<node>.training.json`, the training report (every attempt, its likelihoods, and the chosen one); both as the schemas in `schemas/` say |
| `simulations` | Simulation outputs: one CSV per simulated game, one row per simulation |
| `quality-assurance\<test>` | Output of the quality-assurance tests, for example one row per simulation of the latest game |

- Fitted models, reports, settings and summaries are JSON; training data is CSV to start.
- Each training-data folder has a `manifest.json`, as `schemas/training-data-manifest.schema.json` says: node, source data, date, feature names, example count, and the model the node declares. The tally (how many games were generated, short of history or rejected, by reason) is printed when the data is generated.
- `examples.csv` holds one row per example, games one after another in start-time order; `games.csv` holds one row per game (id, start time, number of examples), so examples are grouped by game without repeating the game on every row.
- Training splits by game: the latest 20% of games by start time are the validation set.
- Augmentation values are never written: they are computed in memory as the season is walked (see `plan.md`).
- Paths are set in `src/GameGraph.Host/Program.cs`.
