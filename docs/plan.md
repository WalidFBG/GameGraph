# Plan

## Goal

A simple, readable prototype that runs the full flow on real NBA data: augment, generate training data, train, simulate, validate. Start almost too simple, then grow one node or one feature at a time, keeping the whole flow working at every step.

## Constraints

- Plain .NET 10 and C#. Only .NET libraries (for example MathNet) where they genuinely help.
- The repository is public: no data is committed, and nothing is copied from other codebases. Findings and lessons are reused; code is written fresh.
- Every line should be understandable on its own. Prefer simple and explicit over clever.

## Step 1: the thinnest full flow

**The game.** Teams alternate possessions. Each possession scores a made field goal (worth 2 points) or not. Each team gets the number of possessions it had in the real game.

**The model.** One additive Bernoulli model for "possession scores a made field goal", with two features:

- the margin in the game's own score (game state, so simulating and generating training data compute it the same way);
- the team's made-field-goal rate per possession over its previous 20 games (game context).

**Reading the data.** How real games become possessions and outcomes, and where created data lives: see `data.md`.

**The flow.**

1. Read real games and walk them into possessions and outcomes.
2. Augment: build each game's context from games strictly before it.
3. Generate training data: walk each game, emit one row per possession.
4. Train the model on the rows.
5. Simulate held-out games; price the total and the winner.
6. Validate against what actually happened.
7. Round trip: simulate many games with the trained parameters, generate training data from them, retrain, and check the parameters come back.

The model is useless for pricing, but every part of the architecture exists and is tested.

## Validation

- **Round trip** (on the NBA graph itself): checks that simulation, training-data generation and training agree with each other. If it fails, the pipeline is wrong.
- **Against reality**: holdout log-loss and calibration per model; simulated against actual totals, margins and, later, player stats on held-out games. If this fails but the round trip passes, the model or the data is the problem.
- **Against the current production model**: a sanity check on averages only. It contains trading adjustments, so it is not the target.

## Augmentation

Augmentation runs side by side with training-data generation (and later the simulator), never as a separate file. Games are walked in start-time order; for each game, the context is read from the history of earlier games, training data is generated from the game or it is simulated with it, and only then is the game added to the history.

1. No looking ahead: guaranteed by the order of the walk, and a test checks it.
2. One code path for training and live pricing.
3. Small samples (rookies, trades, returns from injury) need explicit shrinkage or weighting, never hidden fixes.
4. Context enters the engine as constant per-game inputs next to the game state.

## Growing the NBA

1. Step 1 above (team level, one Bernoulli node).
2. Turnovers and rebounds: possession ends in a shot or a turnover; misses are rebounded.
3. Who: which player shoots and rebounds (categorical over the five on court, lineups taken from the real game); player-level context.
4. Possession length (a parametric family) and the game clock.
5. Compilation: once three decision kinds and three model families exist, so the compiler is built for real variety. Features become expressions the engine can read; every compiled node is proven identical to the plain walk by a digest test.
6. Shot types and points (2 or 3), fouls and free throws.
7. Substitutions: the hardest decision, done last.

Until compilation, two habits keep it cheap: rules stay static methods, and the game state stays plain arrays and numbers.

## Open questions

- Optimiser for the first models: plain gradient descent first, Adam once it is needed.
- Licence for the public repository.
