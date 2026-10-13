# Plan

## Goal

A simple, readable prototype that runs the full flow on real NBA data: augment, label, train, simulate, validate. Start almost too simple, then grow one node or one feature at a time, keeping the whole flow working at every step.

## Constraints

- Plain .NET 10 and C#. Only .NET libraries (for example MathNet) where they genuinely help.
- The repository is public: no data is committed, and nothing is copied from other codebases. Findings and lessons are reused; code is written fresh.
- Every line should be understandable on its own. Prefer simple and explicit over clever.

## Step 1: the thinnest full flow

**The game.** Teams alternate possessions. Each possession is one shot, made or missed, worth 2 points. Each team gets the number of possessions it had in the real game.

**The model.** One additive Bernoulli model for "shot made", with two features:

- score margin (game state);
- the team's field-goal percentage over its previous 20 games (game context).

**The flow.**

1. Load real games and reduce each to its possessions and shot results.
2. Augment: build each game's context from games strictly before it.
3. Label: walk each game, emit one row per shot.
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

- How Sportradar events map to node outcomes, and what to do with games that do not fit (to start: drop them, counted and logged).
- Optimiser for the first models: plain gradient descent first, Adam once it is needed.
- Licence for the public repository.
