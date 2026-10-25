# Plan

## Goal

A simple, readable prototype that runs the full flow on real NBA data: augment, generate training data, train, simulate, validate. Start almost too simple, then grow one node or one feature at a time, keeping the whole flow working at every step.

## Constraints

- The engine, the sports and simulation are plain .NET 10 and C#, with .NET libraries (for example MathNet) only where they genuinely help. Training is Python (see `models.md`).
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
6. Validate against what actually happened: the audit (see "The audit").
7. Round trip: dropped, the audit covers it.

The model is useless for pricing, but every part of the architecture exists and is tested.

## Validation

- **The audit of the simulator and its nodes** (below): it covers what a round trip would (simulation, training-data generation and training agreeing with each other).
- **Against reality**: holdout log-loss and calibration per model; simulated against actual totals, margins and, later, player stats on held-out games.
- **Against the current production model**: a sanity check on averages only. It contains trading adjustments, so it is not the target.

## The audit

Two audits, split between C# and Python. The C# writes data and nothing else: each audit is an explicit xUnit fact in `src/Nba/GameGraph.Nba.Audit` (run only when asked for by name, never by a plain `dotnet test`), writing Parquet to `C:\Data\GameGraph\nba\audit\<audit>\<yyMMdd-HHmm>`; the project keeps the audits at its root, readers and writers in `Infrastructure`, the data written in `Definitions` and the logic in `Domain`. Python (`python/src/gamegraph/audit`) reads it, groups it and writes CSVs named `<node>_<feature>_<run>.csv` (so that two runs open side by side in Excel), with a summary per node; nothing passes or fails. Nothing names a node or a feature: those come from the training data's manifests and the fitted models, so a new node is audited as soon as it exists. `RecentGamesSimulationTests` stays the one test that asserts.

1. **Examples**: both audits work on each node's examples (features, outcome), every game at once, with no split. Real ones are read from the training-data files, so the audit sees exactly the rows the trainer saw; simulated ones come from the simulator through the same `TrainingDataCollector` that writes the training data.
2. **Buckets** (Python): a feature with at most 30 distinct values gets one bucket per value, any other up to 20 quantile buckets, never splitting equal values. A z-score per bucket and outcome uses the variance of a sum of independent Bernoullis, and is left out when fewer than 5 outcomes are expected; the summary sets the number of z-scores above 3 against the number chance alone gives, lists the worst, and `failures_<run>.csv` holds them all.
3. **The calibration audit**: does each fitted model follow its features? The C# writes every example with the C# model's probability of each outcome (`p0` to `pK−1`; for `ActionTime`, outcomes past a row's bound have probability 0), so that the translation from Python is audited too. Per node, feature and bucket, Python compares the observed shares with the predicted ones; `ActionTime` compares its mean and quartiles.
4. **The simulation audit**: does the simulator behave like the data? Every generated game is simulated once, with its own seed drawn from the root seed in game order, so runs repeat exactly: more simulations per game would only narrow the simulated side's noise, which is no larger than the real side's. Only the teams and each game's context (from the history of earlier games) come from the real game; every decision is sampled.
   - Node analysis: per node, and per feature bucket cut on the real data, how often the node is reached per game (a z-score on the per-game differences, since both sides are the same games) and its outcome shares, simulated against real. A node reached too often or too rarely points at the transitions or the state, not at the model.
   - Sampling: per node and outcome, the simulated share against the model's mean prediction, which match by construction unless sampling is wrong.
   - Game statistics: questions we choose, a short list at the top of the module: points, totals, the home margin, possessions and overtime from each game's incidents, and attempts, makes, free throws, fouls, rebounds and turnovers counted from the decisions; with the distributions of totals and margins.

## Augmentation

Augmentation runs side by side with training-data generation (and later the simulator), never as a separate file. Games are walked in start-time order; for each game, the context is read from the history of earlier games, training data is generated from the game or it is simulated with it, and only then is the game added to the history.

1. No looking ahead: guaranteed by the order of the walk, and a test checks it.
2. One code path for training and live pricing.
3. Small samples (rookies, trades, returns from injury) need explicit shrinkage or weighting, never hidden fixes.
4. Context enters the engine as constant per-game inputs next to the game state.

## Simulation (step 5)

Built in two pieces: first the engine change (decision collector and incident collector, incidents reported by the NBA's transitions), then simulation itself.

1. **Parameters**: `GameGraph.Parameters` is the contract between training and simulation (the family, and per predictor an intercept and each term's coefficients, and the file format). Callers read the files; simulation rebuilds the model from them and fails if a term's feature is not one of the node's features.
2. **Randomness**: one root seed constant; each simulation gets its own seed, drawn from the root up front, so results do not depend on scheduling. `System.Random(seed)` sits behind a small class of our own, to be replaced by xoshiro256** (seeded through SplitMix64) with the speed work, before outputs are pinned by a digest test.
3. **Count**: 10,000 simulations per game.
4. **The simulation index**: each incident carries the index of its simulation (`SimulationIndex`), read from the game state, so one incident collector receives all simulations. Superseded: each simulation's state now holds its own incident collector, so incidents carry no index.
5. **The real schedule**: each simulation plays the real game's periods and possessions per period, overtime included; every possession's outcome is sampled. This leaks the real game into the price (overtime is known in advance, so is the pace) and is accepted until step 4 brings the clock; ties are possible and counted separately.
6. **Quality assurance**: analyses are tests in `src/Nba/GameGraph.Nba.Tests.QualityAssurance` (xUnit v3), each with its own season loop. The first simulates the latest generated game, writes one row per simulation (simulation index, home points, away points) to `quality-assurance`, and prints home win, away win, tie and mean total next to the actual result.

## Growing the NBA

The next model is the team-level possession tree in `possession-tree.html`: every node's model family, outcome type, transitions, clock rules and incidents.

1. Step 1 above (team level, one Bernoulli node): done.
2. The tree without the clock, on the real schedule: action types, shots, fouls, free throws and rebounds (the Categorical family; rules with their own state). Whether a non-shooting foul gives free throws is a Bernoulli node (`InBonus`) rather than the bonus rule, because the tree does not generate every team foul. Fouls come now because about one possession in eight has free throws: most are a fouled missed shot, which the data records as a shooting foul and free throws, with no shot.
3. The tree's clock (step 13 in `models.md`): done. `PeriodRunsOut` ends periods, `JumpBall` starts the game and every overtime, `ActionTime` is a discrete Johnson SU, and free throws outside the play keep the score right; the schedule leak is gone, and possessions the period cuts short are kept (`PossessionsCutByPeriodEnd` only counts them now).
4. The audit (see "The audit"): first, so that every later step is judged by it.
5. The team graph's gaps, one at a time:
   - the ball after a flagrant, take or clear-path foul: the fouled team keeps it in reality, not in a simulation (a correctness bug);
   - late-game fouling: the trailing team fouls to stop the clock, which shapes close finishes and the totals and margins near the line;
   - timeouts: they stop the clock and advance the ball late in games.
6. Players and substitutions, so that the game is player-driven: who shoots, rebounds, turns it over and is fouled (the Choice family), and the five on court come from the simulation, not the real game. One slice at a time:
   1. the Choice family (one feature row per candidate) and the lineup state, read from the data (starting lineups, lineup changes and player availability are in it);
   2. one player node, who shoots, end to end;
   3. the other player nodes, with the finer action types that need players (live-ball steals, assists, blocks);
   4. starters and substitutions last, as repeated single choices from a shrinking pool; until then simulations take the real lineups, a leak accepted as the real schedule was before the clock.

   Per-player history brings small samples (rookies, trades, returns from injury), which need explicit shrinkage (see "Augmentation").
7. Compilation (see below), once players have brought their family, the feature row per candidate and the lineup state: a compiler built before them would be rewritten for them.

Each step adds its nodes, reshapes the simulator where they need it, adds a family if they need one, and then goes through training data, training, simulation and the audit.

Until compilation, two habits keep it cheap: rules stay static methods, and the game state stays plain arrays and numbers.

Once the graph is complete, and not before, comes the tuning: richer context (the opponent's defence, pace, a rate per node rather than points per possession for all, rest and travel; with players, much of it becomes the strength of the five on court), the drift (idea 7 in `models.md`), and whether games outside the regular season feed the history or the training data.

### Building step 2, piece by piece

Each piece builds and runs on its own and is reviewed before the next.

1. Models as GLMs: terms, linear predictors and families (`Bernoulli`, `Categorical`) working on η; the trainer, the parameters file and the simulator go through the model: done.
2. Each node declares its model (family and terms); the manifest records it; the trainer and the simulator build the model from it: done.
3. Features as expressions (`Expression<Func<TState, double>>`, compiled once for the plain walk), while there are only two features: done.
4. Reading the data: an observed possession becomes a sequence of actions (two, three, turnover, non-shooting foul), with fouled or not, made or missed, the free throws and the offensive rebound.
5. The NBA graph: the new nodes, state, features, transitions and incidents: done (the six decisions of the tree without the clock; first features are the score margin and team scoring rate plus each node's own state; the possession incident is the only incident yet).
6. End to end: training data, training and the quality-assurance simulation on the new graph: done. `InBonus` stops at the 200-step cap and `FouledOnShot` close to it, so plain gradient descent is already too slow for some nodes (see the optimiser question).

## Model families and data shapes

What the C# models are today; items 1 to 4 are built. `models.md` replaces this: GAMLSS, trained in Python, evaluated and sampled in C#.

1. **Models are GLMs**: terms (a feature and its basis) expand into design columns; each linear predictor is an intercept plus a coefficient per column; the family (link and distribution) works only on η. Every coefficient belongs to one term of one predictor, so nothing has to be inferred from a layout.
2. **A model-family interface** (`IModelFamily`): the number of predictors, the negative log-likelihood, its gradient with respect to η, and sampling from one uniform number. The gradient with respect to the coefficients is the chain rule, done once by the model.
3. **Scaling works on design columns**: training standardises each column and converts the coefficients back afterwards, the same way for every basis. A family's own parameters (a count's dispersion) will sit outside the predictors and are not rescaled.
4. **Each node declares its model** (family and terms) in the sport's spec; the manifest records it and training and simulation build the same model from it.
5. **Outcomes stay `int` until the first `double`**: Bernoulli (0 or 1), categories and counts are all whole numbers, and each family rejects outcomes outside its range, loudly. Typed outcomes (`bool`, `int`, `double`) arrive with the first continuous outcome, when integers can no longer hold it: one node class per outcome type under a common base, and one decide and one collect method per type, so nothing is cast or boxed. Training data keeps 0/1 until then.
6. **Two feature shapes, not a generic one**:
   - vector: one feature set per decision; flat `double[]` of n × f plus a typed outcome array;
   - choice: one feature set per candidate (who shoots); flat `double[]` of all candidates × f, an offset per decision and the chosen index (long format, one CSV row per candidate).
7. **Splines as a second basis**: `SplineTerm(feature, knots)` expands a feature into `x, max(0, x − k₁), …` columns, and training data keeps raw features. Families, predictors and scaling do not change; the parameters file records each spline term's knots next to its coefficients.
8. **Speed without our own codegen**: for readability, `Model` holds its family behind `IModelFamily` for now, so the family's arithmetic is an interface call and cannot be inlined. Simulation loses nothing by it, because compilation writes each node's family arithmetic out directly. Training is not compiled: its speed work makes the trainer generic over the family's struct again (`where TFamily : struct, IModelFamily`, one dispatch from the family's name to its struct, in the trainer only), together with spans of flat arrays, no allocation per example and SIMD (`System.Numerics.Tensors`).

## Compilation

- **Training does not walk the graph**: it is Python, reading the training-data files, so it is independent of the engine and its speed comes from PyTorch.
- **Simulation**: a compiled node fuses its features, predictor, family sampling and transition into one method, with its parameters as constants; the plain walk stays as the reference, and a digest test proves the two identical.
- **What keeps it possible**: rules are static methods, the state is plain arrays and numbers, families are pure functions of parameters, features and one uniform number, and features are expressions over the state (no calls into arbitrary methods) so a compiler can read them.
- **Bases stay a closed set** (linear, spline): a compiler generates each basis's arithmetic directly, so no term may hide its logic behind an arbitrary `ITerm` implementation.
- **Plain-walk costs to remove on the way**:
  - nodes become integer indices into the game's node array, so the walk and the sampling decider find nodes and parameters by index: no dictionary by node, and no search of the node list at every step (an index outside the array still fails loudly): done, through `FittedGame`;
  - the walk allocates no features array per step: done;
  - `Categorical` allocates no small arrays per call: done, it works on spans and `Model` puts the parameters on the stack.
- **Parallel simulations**: each simulation already has its own seed, so simulations run in parallel, one walk at a time per thread. Each simulation's state holds its own incident collector, writing into its own preallocated slot, so they need no locks. A test checks that the results do not depend on the number of threads.

## Open questions

- Whether games outside the regular season should feed the history or the training data: only regular-season games are read, for both (settled once the graph is complete).
- Licence for the public repository.
