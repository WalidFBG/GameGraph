# Compilation

The plan for making simulation fast, and the record of each optimisation decision. Training is not compiled: it is Python, reading the training-data files, so it is independent of the engine and its speed comes from PyTorch.

## Where the time goes

Measured on the simulation fixture (`testdata/nba-simulation`), one thread, the plain walk, 2026-10-26: about 1.3 ms and 2,957 decisions a simulation (timers add some overhead to the split).

| Part | µs a simulation |
|---|---|
| The predictors | ~980 |
| The families | ~185 |
| Features | ~155 |
| The walk and the transitions | ~130 |

About 80% is the models, and of that 84% is the predictors: generating the families' arithmetic (order, step 4) is not needed. The dearest nodes are the most frequent: `ActionTime` and `ActionType` (234 a game each, ~183 µs each), `Substitutions`, `AwardedFreeThrow` and the two timeouts (240 to 280 a game each, 70 to 130 µs each), then `Shooter`. Three costs show in the code:

- every term was an interface call (`ITermColumns.Expand`), every parameter another (`IParameterValue`; both gone by step 2);
- a categorical node with K categories has K − 1 predictors over the same terms, so it expands the same spline columns K − 1 times;
- `Categorical.Sample` takes `exp` of every category twice, once for the total and once for the cumulative sum.

Of the 45 spline terms, 29 have 4 knots (3 columns); the large ones (16 to 24 knots) sit on the hottest nodes, and `ActionType` has 23 terms in each of its 3 predictors.

The benchmark's baseline for the plain walk: 2.47 s for 10,000 simulations (the median of ten runs) on the 22 threads of an Ultra 9 185H.

| After | µs a simulation, one thread | Predictors | Families | Benchmark median |
|---|---|---|---|---|
| Nothing (the baseline) | 1,310 | ~980 | ~185 | 2.47 s |
| Step 1, the exact fixes | 836 | ~520 | ~180 | 1.37 s |
| Step 2, the piecewise form | 715 | ~355 | ~135 | 1.08 s |
| Step 3, the compiler (compiled walk) | ~395 | | | 0.54 s |

## Order

1. **Exact fixes in `Model`**, digest unchanged: done. A node's distinct terms were expanded once into its design columns (`DesignColumns`, gone again in step 2), shared by all its predictors (the same values, summed in the same order); `Categorical.Sample` keeps the `exp` values it takes for the total; `DiscreteJohnsonSu.Sample` takes each normal CDF once. Terms are shared only where they are the same columns: a feature can be a spline in one predictor and linear in another (`ActionTime`'s `Bound` and `ScoreMargin`).
2. **The form of a term**: done. A standardised linear spline is piecewise linear in its feature, so each spline term is a lookup of the feature's segment (a `KnotLookup`, shared by every term on the same feature and knots, so a categorical node's predictors share it) and one intercept and one slope for that segment (`a[segment] + b[segment] * x`); a linear term folds its centre and scale into its slope and the intercept. The columns are gone: each term's centring and scaling is folded into its coefficients when the model is loaded. It changes rounding: over the 885,669 decisions of 300 simulations of the fixture, the parameter values differ from step 1's by at most 5e-14 absolute (3e-12 on `ActionTime`'s values in the thousands), the parity tests with Python still hold to 1e-12, and not one sampled outcome of the digest's 1,000 simulations changed, so the digest needed no re-pin.
3. **The compiler**: done (`CompiledGame`). The features, the predictors in their final form, and calls to everything else (decisions below), with the bit-for-bit parameter test, the plain-against-compiled digest, the test that results do not depend on the number of threads, and the benchmark of both. Compiling the game takes 0.06 s. On one thread the compiled walk takes ~395 µs a simulation against the plain walk's ~665 µs (the plain walk itself gained from the struct step); the benchmark's median is 0.54 s against the plain walk's 1.04 s, 4.6 times as fast as the baseline, past the target. A planted error of 1e-15 in one intercept fails the bit-for-bit test at the first decision and passes the outcome digest, as decision 8 expected.
4. **Generating a family's arithmetic**, only if the profile after compiling shows sampling dominating, in its own pull request.

After the compiler, the families are the largest part of a simulation: replayed on recorded parameters, ~200 to 235 µs of ~400, of which the choices ~115 µs (`Shooter` 37, `Rebounder` 17, `SubstitutedOn` 16), the discrete Johnson SU ~40 µs (`ActionTime`, 170 ns a sample) and the categoricals ~50 µs; allocation is negligible (7 KB a simulation). So the families' algorithms come before their code:

5. **A choice of one**: done. `Choice.Sample` takes a choice of one candidate (`Shooter`, `Rebounder`, `Assister`, `Fouler` and the like) as a softmax over the candidates in their order, one `exp` each, instead of the general choice of k (a table of log-sums, an `exp` and a log a cell). It picks the same candidate for the same uniform; only rounding differs, and the parity tests with Python and the digest are unchanged. The benchmark's compiled median, before and after on the same machine back to back: 0.55 s to 0.49 s.
6. **The normal functions** of the discrete Johnson SU (`Normal.Cdf` is `exp` of `LogCdf`), next.

The target: the compiled walk at least three times as fast as the plain walk was before step 1 (the benchmark's median under about 0.8 s). If steps 1 and 2 bring most of it, the compiler's remit shrinks accordingly.

## Decisions

1. **Exact**: the compiled walk gives bit-identical results to the plain walk. Arithmetic that changes rounding is never a compiler trick: it goes into the shared `Model` first, so both walks keep one arithmetic. Exact includes calling the same functions: generated code calls `Math.Min`, `Math.Max` and `Links.Inverse` where `Model` does, since a comparison or a link written in their place could differ (for `-0.0` and `NaN`, say).
2. **Expression trees, built at start-up**: each node's code is an expression tree compiled once the fitted models are loaded, with the coefficients and knots as constants. A source generator cannot (the coefficients exist only at run time); generated C# compiled by Roslyn would be readable but needs a package and seconds to compile; emitted IL is unreadable. The features already are expression trees. Expression trees cannot hold spans or `stackalloc`, so generated code writes parameters into an array (decision 6).
3. **One method a node, for its outcome**: a compiled node computes its features, its predictors and its family's sample, and returns the outcome. The transition is not generated: the walk calls the node's own `Apply`, which already holds the transitions, the choice node's mapping of chosen rows back to players and its loud checks, so the two walks cannot drift there. The walk and the transitions are ~70 µs of 715; once the predictors are compiled that may be 20 to 25% of what is left, and transitions can still be generated later if a profile asks. The whole game is not fused into one method: the walk between nodes is what the tests observe (decision 8).
4. **One walk, two steps**: `GameWalker.Walk` is one loop, generic over a struct step (`IWalkStep<TState>`) that decides a node's outcome; the loop then applies it and checks that the next node is one of the game's. The plain step writes the features, asks the outcome decider and hands the features to the decision collector, so training data and the audits keep their interfaces; the compiled step calls the compiled node by its index and hands the node and outcome to a collector struct. The loop, its end and its checks exist once, and the plain walk gets struct dispatch too.
5. **Rules are called, not generated**: everything the plain walk does besides features and predictors is called from the generated code or the walk, never written again, so the compiled walk keeps every loud failure: the families' sampling (decision 6), the choice node's check of its size against its candidates (made static on `ChoiceNode`, called by both walks), the transitions and the next-node check (decision 3). Nothing falls back to the plain walk: whatever the compiler does not support fails loudly when the game is compiled.
6. **Families called through `Model`, from one buffer**: `Model` gains an internal method (visible to `GameGraph.Simulation` only) that samples its family from parameters generated code has written into an array, so the families are never generated. The array is sized once, when the game is compiled, to the largest node's need: a vector node's parameter count, or two parameters per candidate up to `ChoiceDistribution.MostCandidates` for a choice node; the generated choice loop checks its candidates against that maximum and fails loudly beyond it. One array per simulation, so parallel simulations share nothing.
7. **Inside a node**:
   - each feature is computed once into a local (`Feature.Inline` substitutes the node's state into the feature's expression; `CandidateFeature.Inline` the state and the candidate);
   - each knot lookup (the clamp with `Math.Min` and `Math.Max`, then the segment) is computed once, and the segments' intercepts and slopes are constant arrays;
   - each predictor sums its intercept, then its linear terms, then its spline terms, in exactly `Predictor`'s order; the expression is built in `GameGraph.Models`, next to `Predictor` and `KnotLookup`, so the order is written beside the code it must match;
   - a choice node gets a generated loop over its candidates, one row of parameters each;
   - **every decision draws exactly one uniform, in walk order, in both walks**, including a choice with one possible outcome (of none, or of every candidate): skipping that draw would shift every later decision and show only as a hash mismatch.
8. **What the tests compare** (`Nba.Tests.Unit`): equal outcomes do not prove equal arithmetic (step 2 moved every parameter value by up to 5e-14, and no sampled outcome changed), so three checks:
   - **the parameters, bit for bit**: each node's parameter code is one expression, compiled on its own for the test and inside the node for production; a collector on the plain walk calls it with each decision's state (before `Apply`), so the inlined features are checked too, and requires the doubles to equal `Model.ParameterValues` exactly, on every decision of the fixture's simulations;
   - **the walk, end to end**: the plain and the compiled walk give the same hash over nodes, outcomes and incidents for 1,000 simulations; the existing digest, features included, keeps pinning the plain walk;
   - **the threads**: each simulation's hash is the same whether the 1,000 run one after another or in parallel, which catches any shared buffer.
9. **Where it lives**: `CompiledGame<TState>` in `GameGraph.Simulation`, next to `FittedGame`, and `Simulator.Run` takes either: the plain walk stays for debugging (step node by node, break in a transition or a feature), for training data (the replay of observed games is never compiled) and as the reference. The benchmark times both.
10. **State unchanged**: `NbaState` stays plain classes and arrays. The older lineup features (the longest stint, a team that must change) still compute on read; they move to state only if the profile after compiling points at them.

## Expected gain of the compiler

A prediction for the benchmark to confirm or refute, against 715 µs a simulation on one thread after step 2:

| Part | Now (µs) | Compiled (µs) | What goes |
|---|---|---|---|
| Predictors | ~355 | ~140 | the call per predictor, loops over term arrays, nested array indexing and bounds checks; coefficients and knots become constants |
| Features | ~155 | ~105 | one delegate call per feature (about 10 per decision × 2,957 decisions) and the writes to the features array |
| Families | ~135 | ~135 | nothing: they are called, not generated |
| Walk and transitions | ~70 | ~70 | nothing: `Apply` is reused |
| **Total** | **~715** | **~450** | about 1.6 times on one thread; a benchmark median of about 0.65 to 0.75 s against 1.07 s |

A result well outside that range says something in itself: for example, that the predictors were bound by memory rather than by calls.

The result: ~395 µs on one thread (predicted ~450) and a median of 0.54 s (predicted 0.65 to 0.75 s), a little better than predicted on both.

## Risks

- **Large methods**: the JIT compiles a very large method without optimising it and tracks only a limited number of locals in registers, so a straight-line node with a local per column could run slower with nothing reporting it. The short form of the predictors (order, step 2) keeps the methods small; if a node is still slow after compiling, its method size is the first thing to check.

## Generic by construction

The compiler never names a node. It reads what every node declares: its features (expressions), its terms (a closed set of bases: linear, spline), its family (a closed set: Bernoulli, categorical, choice, discrete Johnson SU), its transition (a static method) and its fitted coefficients. A new node, feature or player node compiles as soon as it is declared; compiler code is needed only for a new basis, a new family or a new node shape (today a vector of features per decision, or a row per candidate). That is why bases and families stay closed sets: no term may hide its logic behind an arbitrary implementation.

What keeps it possible: rules are static methods, the state is plain arrays and numbers, families are pure functions of parameters and one uniform number, and features are expressions over the state.

## Done on the way

- Nodes are integer indices into the game's node array, so the walk and the sampling decider find nodes and models by index (an index outside the array still fails loudly), through `FittedGame`.
- The walk allocates no features array per step.
- `Categorical` allocates no small arrays per call: it works on spans and `Model` puts the parameters on the stack.
- Random numbers come from xoshiro256** (`SeededRandom`, seeded through SplitMix64), and the root draws each simulation's seed through SplitMix64.
- Parallel simulations: each simulation has its own seed and its own state, with its own incident collector, so simulations run one walk at a time per thread with no locks.
- The digest test pins the plain walk; the benchmark times runs of 10,000 simulations on all threads.
