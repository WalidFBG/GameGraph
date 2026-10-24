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

1. **Parameters**: `GameGraph.Parameters` is the contract between training and simulation (the family, and per predictor an intercept and each term's coefficients, and the file format). Callers read the files; simulation rebuilds the model from them and fails if a term's feature is not one of the node's features.
2. **Randomness**: one root seed constant; each simulation gets its own seed, drawn from the root up front, so results do not depend on scheduling. `System.Random(seed)` sits behind a small class of our own, to be replaced by xoshiro256** (seeded through SplitMix64) with the speed work, before outputs are pinned by a digest test.
3. **Count**: 10,000 simulations per game.
4. **The simulation index**: each incident carries the index of its simulation (`SimulationIndex`), read from the game state, so one incident collector receives all simulations. Superseded: each simulation's state now holds its own incident collector, so incidents carry no index.
5. **The real schedule**: each simulation plays the real game's periods and possessions per period, overtime included; every possession's outcome is sampled. This leaks the real game into the price (overtime is known in advance, so is the pace) and is accepted until step 4 brings the clock; ties are possible and counted separately.
6. **Quality assurance**: analyses are tests in `src/Nba/GameGraph.Nba.QualityAssurance` (xUnit v3), each with its own season loop. The first simulates the latest generated game, writes one row per simulation (simulation index, home points, away points) to `quality-assurance`, and prints home win, away win, tie and mean total next to the actual result.

## Growing the NBA

The next model is the team-level possession tree in `possession-tree.html`: every node's model family, outcome type, transitions, clock rules and incidents.

1. Step 1 above (team level, one Bernoulli node): done.
2. The tree without the clock, on the real schedule: action types, shots, fouls, free throws and rebounds (the Categorical family; rules with their own state). Whether a non-shooting foul gives free throws is a Bernoulli node (`InBonus`) rather than the bonus rule, because the tree does not generate every team foul. Fouls come now because about one possession in eight has free throws: most are a fouled missed shot, which the data records as a shooting foul and free throws, with no shot.
3. The tree's clock: `ClockCheck`, `PeriodRunsOut`, `ActionTime`, `PeriodEnds` and the clock features (the truncated Count family); removes the schedule leak. It also removes a stopgap of step 2: without a clock, a possession the period ends in the middle of (a buzzer scramble, about one period in two) cannot be walked, so the reader drops it and counts it (`PossessionsCutByPeriodEnd`); with the clock it is kept.
4. Compilation: once these families exist, so the compiler is built for real variety (see below).
5. Players and substitutions: who shoots, rebounds, turns it over and is fouled (the Choice family), with substitutions so that the five on court come from the simulation, not the real game.

Until compilation, two habits keep it cheap: rules stay static methods, and the game state stays plain arrays and numbers.

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
  - `Categorical` allocates no small arrays per call.
- **Parallel simulations**: each simulation already has its own seed, so simulations run in parallel, one walk at a time per thread. Each simulation's state holds its own incident collector, writing into its own preallocated slot, so they need no locks. A test checks that the results do not depend on the number of threads.

## Open questions

- Regularisation (required, a vital part of training): which penalty (L2 first, L1 if sparsity is wanted), which feature scale it applies to (scaled or raw, since the fit is no longer scale-invariant once penalised), and its strength chosen on the validation set. Needed before player-level models.
- Whether games outside the regular season should feed the history (they do not yet): a team's last 20 games currently skip its playoff games.
- Optimiser: plain gradient descent converges slowly on some nodes (`InBonus` hits the step cap). Training moves to Python, where L-BFGS and Adam come with PyTorch.
- Licence for the public repository.
