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
6. Validate against what actually happened: deferred; it will be an audit of the simulator and its nodes, designed when it is needed.
7. Round trip: dropped, the audit covers it.

The model is useless for pricing, but every part of the architecture exists and is tested.

## Validation

- **Audit of the simulator and its nodes**: deferred, designed when it is needed; it covers what a round trip would (simulation, training-data generation and training agreeing with each other).
- **Against reality**: holdout log-loss and calibration per model; simulated against actual totals, margins and, later, player stats on held-out games.
- **Against the current production model**: a sanity check on averages only. It contains trading adjustments, so it is not the target.

## Augmentation

Augmentation runs side by side with training-data generation (and later the simulator), never as a separate file. Games are walked in start-time order; for each game, the context is read from the history of earlier games, training data is generated from the game or it is simulated with it, and only then is the game added to the history.

1. No looking ahead: guaranteed by the order of the walk, and a test checks it.
2. One code path for training and live pricing.
3. Small samples (rookies, trades, returns from injury) need explicit shrinkage or weighting, never hidden fixes.
4. Context enters the engine as constant per-game inputs next to the game state.

## Simulation (step 5)

Built in two pieces: first the engine change (decision collector and incident collector, incidents reported by the NBA's transitions), then simulation itself.

1. **Parameters**: `GameGraph.Parameters` is the contract between training and simulation (intercept and weights by feature name, and the file format). Callers read the files; simulation lines the weights up with the node's features and fails on any missing or extra name.
2. **Randomness**: one root seed constant; each simulation gets its own seed, drawn from the root up front, so results do not depend on scheduling. `System.Random(seed)` sits behind a small class of our own, to be replaced by xoshiro256** (seeded through SplitMix64) with the speed work, before outputs are pinned by a digest test.
3. **Count**: 10,000 simulations per game.
4. **The simulation index**: each incident carries the index of its simulation (`SimulationIndex`), read from the game state, so one incident collector receives all simulations.
5. **The real schedule**: each simulation plays the real game's periods and possessions per period, overtime included; every possession's outcome is sampled. This leaks the real game into the price (overtime is known in advance, so is the pace) and is accepted until step 4 brings the clock; ties are possible and counted separately.
6. **Quality assurance**: analyses are tests in `src/Nba/GameGraph.Nba.QualityAssurance` (xUnit v3), each with its own season loop. The first simulates the latest generated game, writes one row per simulation (simulation index, home points, away points) to `quality-assurance`, and prints home win, away win, tie and mean total next to the actual result.

## Growing the NBA

A proposal for a deeper possession tree, with every node's model family, outcome type and transitions, is in `possession-tree.html`.

1. Step 1 above (team level, one Bernoulli node).
2. Turnovers and rebounds: possession ends in a shot or a turnover; misses are rebounded.
3. Who: which player shoots and rebounds (categorical over the five on court, lineups taken from the real game); player-level context.
4. Possession length (a parametric family) and the game clock.
5. Compilation: once three decision kinds and three model families exist, so the compiler is built for real variety. Features become expressions the engine can read; every compiled node is proven identical to the plain walk by a digest test.
6. Shot types and points (2 or 3), fouls and free throws.
7. Substitutions: the hardest decision, done last.

Until compilation, two habits keep it cheap: rules stay static methods, and the game state stays plain arrays and numbers.

## Model families and data shapes

Agreed now, built when the second model family arrives (step 2) and when choices arrive (step 3). The current code is the "vector features, Bernoulli outcome" corner of this design.

1. **Linear predictor split out**: intercept plus weighted features becomes its own piece; a family adds its link and distribution (Bernoulli, Poisson, Gamma) on top.
2. **A model-family interface**: parameter count, negative log-likelihood, gradient and later sampling. `GradientDescent` fits through it instead of calling `Logistic`.
3. **Scaling and the parameters file follow the linear predictor's layout**: intercept, then one weight per feature; extra family parameters (a Gamma's shape) come after.
4. **Each node declares its family** in the sport's spec; the manifest records it and the trainer picks the family from it.
5. **The outcome type belongs to the family**: `bool` for Bernoulli, `int` for categorical and counts, `double` for continuous. Nodes are typed by it (`Node<TState, TOutcome>` under a common `Node<TState>`); each node runs its own step, so outcomes are never boxed or stored as `double`.
6. **Two feature shapes, not a generic one**:
   - vector: one feature set per decision; flat `double[]` of n × f plus a typed outcome array;
   - choice: one feature set per candidate (who shoots); flat `double[]` of all candidates × f, an offset per decision and the chosen index (long format, one CSV row per candidate).
7. **Speed without our own codegen**: families are structs and the trainer is generic over them (`where TFamily : struct`), so the JIT specialises and inlines each family; families work on whole spans of flat arrays, ready for SIMD (`System.Numerics.Tensors`).

## Open questions

- Regularisation (required, a vital part of training): which penalty (L2 first, L1 if sparsity is wanted), which feature scale it applies to (scaled or raw, since the fit is no longer scale-invariant once penalised), and its strength chosen on the validation set. Needed before player-level models.
- Whether games outside the regular season should feed the history (they do not yet): a team's last 20 games currently skip its playoff games.
- Optimiser for the first models: plain gradient descent first, Adam once it is needed.
- Licence for the public repository.
