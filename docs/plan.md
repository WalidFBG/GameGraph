# Plan

## Goal

A simple, readable prototype that runs the full flow on real NBA data: augment, label, train, simulate, validate. Start almost too simple, then grow one node or one feature at a time, keeping the whole flow working at every step.

## Constraints

- Plain .NET 10 and C#. Only .NET libraries (for example MathNet) where they genuinely help.
- The repository is public: no data is committed, and nothing is copied from other codebases. Findings and lessons are reused; code is written fresh.
- Every line should be understandable on its own. Prefer simple and explicit over clever.

## Step 1: the thinnest full flow

**The game.** Teams alternate possessions. Each possession ends in a made field goal (worth 2 points) or not. Each team gets the number of possessions it had in the real game.

**The model.** One additive Bernoulli model for "possession ends in a made field goal", with two features:

- the margin in the game's own score (game state, so simulating and labelling compute it the same way);
- the team's made-field-goal rate per possession over its previous 20 games (game context).

**Reading the data.** The source is `C:\Data\Basketball\ValidatedData\PlayByPlay` (validated Sportradar play-by-play, one JSON file per game). It is read only, never written. The labeller walks each game's events in order and skips anything step 1 does not use.

1. Only games with status `Valid` are used. Others are skipped, counted by status.
2. Possession is decided by who acts, not by the data's `teamInPossession` field, which flips for single events. The acting team of an event is:
   - field-goal attempt: the shooter's team;
   - turnover: the team of the player who committed it;
   - free throw of type `Regular`: the shooter's team.
3. Consecutive actions by the same team form one possession. A change of acting team starts a new one. So offensive rebounds and and-ones stay inside their possession.
4. Free throws of other types (`Technical`, `Flagrant`, `ClearPath`) are ignored: they can be taken by the defending team in the middle of a possession.
5. Every other event is ignored in step 1 (rebounds, fouls, lineup changes, timeouts, reviews, jump balls, period ends).
6. A possession's outcome is 1 if it contains a made field goal, 0 otherwise. A possession with more than one made field goal fails the game.
7. Checks that fail a game (skipped, counted by reason, never silently): an action whose team cannot be determined, more than one made field goal in a possession, and a difference of more than a few possessions between the two teams.
8. The summary over all games (games used and skipped by reason, possessions per team per game, made rate) is checked before anything is built on top.

Unknowns to settle while building: how team turnovers (no player) name their team, and the right tolerance for the possession difference.

Data GameGraph creates lives in `C:\Data\GameGraph
ba\` (`context`, `rows\<model>`, `parameters`, `simulations`). Parameters, settings and summaries are JSON; labelled rows are CSV to start. Each dataset folder has a `manifest.json`: source data, GameGraph commit, seed, date and row count. Paths come from configuration.

**The flow.**

1. Read real games and walk them into possessions and outcomes.
2. Augment: build each game's context from games strictly before it.
3. Label: walk each game, emit one row per possession.
4. Train the model on the rows.
5. Simulate held-out games; price the total and the winner.
6. Validate against what actually happened.
7. Round trip: simulate many games with the trained parameters, label them, retrain, and check the parameters come back.

The model is useless for pricing, but every part of the architecture exists and is tested.

## Validation

- **Round trip** (on the NBA graph itself): checks that simulation, labelling and training agree with each other. If it fails, the pipeline is wrong.
- **Against reality**: holdout log-loss and calibration per model; simulated against actual totals, margins and, later, player stats on held-out games. If this fails but the round trip passes, the model or the data is the problem.
- **Against the current production model**: a sanity check on averages only. It contains trading adjustments, so it is not the target.

## Augmentation

1. No looking ahead: context only uses data from before tip-off, and a test checks it.
2. One code path for training and live pricing.
3. Small samples (rookies, trades, returns from injury) need explicit shrinkage or weighting, never hidden fixes.
4. Context enters the engine as constant per-game inputs next to the game state.

## Growing the NBA

1. Step 1 above (team level, one Bernoulli node).
2. Turnovers and rebounds: possession ends in a shot or a turnover; misses are rebounded.
3. Who: which player shoots and rebounds (categorical over the five on court, lineups taken from the real game); player-level context.
4. Shot types and points (2 or 3), fouls and free throws.
5. Possession length (a parametric family) and the game clock.
6. Substitutions: the hardest decision, done last.
7. Compilation and speed, each step proven identical to the plain version by a digest test.

## Open questions

- Optimiser for the first models: plain gradient descent first, Adam once it is needed.
- Licence for the public repository.
