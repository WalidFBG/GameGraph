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

- every term is an interface call (`ITermColumns.Expand`), every predictor another (`IParameterValue`);
- a categorical node with K categories has K − 1 predictors over the same terms, so it expands the same spline columns K − 1 times;
- `Categorical.Sample` takes `exp` of every category twice, once for the total and once for the cumulative sum.

Of the 45 spline terms, 29 have 4 knots (3 columns); the large ones (16 to 24 knots) sit on the hottest nodes, and `ActionType` has 23 terms in each of its 3 predictors.

The benchmark's baseline for the plain walk: 2.47 s for 10,000 simulations (the median of ten runs) on the 22 threads of an Ultra 9 185H.

After step 1 (the exact fixes), on the same thread and fixture: 836 µs a simulation, of which the predictors are ~520 µs and the families ~180 µs; the benchmark's median is 1.37 s, 1.8 times as fast, with the digest unchanged.

## Order

1. **Exact fixes in `Model`**, digest unchanged: done. A node's distinct terms are expanded once into its design columns (`DesignColumns`), shared by all its predictors (the same values, summed in the same order); `Categorical.Sample` keeps the `exp` values it takes for the total; `DiscreteJohnsonSu.Sample` takes each normal CDF once. Terms are shared only where they are the same columns: a feature can be a spline in one predictor and linear in another (`ActionTime`'s `Bound` and `ScoreMargin`).
2. **The form of a spline term**, decided from that measurement. A standardised linear spline is piecewise linear in its feature, so each term reduces to finding the feature's segment, then one intercept and one slope (`a[segment] + b[segment] * x`), and a categorical node's predictors share the one lookup. It changes rounding, so it goes into `Model`, in its own pull request, with the digest re-pinned once on purpose, after checking that the simulations' output does not move. Done before the compiler, the compiler generates the short form once instead of being rewritten for it.
3. **The compiler**, one pull request: the features, the predictors in their final form, and calls to everything else. It comes with the plain-against-compiled digest, the benchmark of both, and the test that results do not depend on the number of threads.
4. **Generating a family's arithmetic**, only if the profile after compiling shows sampling dominating, in its own pull request.

The target: the compiled walk at least three times as fast as the plain walk was before step 1 (the benchmark's median under about 0.8 s). If steps 1 and 2 bring most of it, the compiler's remit shrinks accordingly.

## Decisions

1. **Exact**: the compiled walk gives bit-identical results to the plain walk. Arithmetic that changes rounding is never a compiler trick: it goes into the shared `Model` first, so both walks keep one arithmetic. Exact includes calling the same functions: generated code calls `Math.Min` and `Math.Max` where `Model` does, since a comparison written in their place differs for `-0.0` and `NaN`.
2. **Expression trees, built at start-up**: each node's code is an expression tree compiled once the fitted models are loaded, with the coefficients and knots as constants. A source generator cannot (the coefficients exist only at run time); generated C# compiled by Roslyn would be readable but needs a package and seconds to compile; emitted IL is unreadable. The features already are expression trees.
3. **One method a node**: a compiled node fuses its features, predictors, the call to its family's sampling and the call to its transition, and returns its outcome and the next node's index. The walk over those methods is ordinary C#. The whole game is not fused into one method: the digest's design (decision 6) needs the walk between nodes.
4. **Rules are called, not generated**: everything the plain walk does besides features and predictors is called from the generated code, never written again, so the two walks cannot drift and the compiled walk keeps every loud failure. That covers the families' sampling (made static and public), the choice node's check of its size against its candidates and its mapping of the chosen rows back to players (made static on `ChoiceNode`), and the walk's check that the next node is one of the game's. Only the features and predictors are generated.
5. **Transitions called directly**: each must be a static method (`Method.IsStatic` and no `Target`; a lambda compiles to an instance method of a hidden class even when it captures nothing), called as one; the compiler fails, when compiling, on one that is not. Nothing falls back to the plain walk: whatever the compiler does not support fails loudly when the game is compiled.
6. **What the digest compares**: a compiled node returns its outcome to the walk, and the walk, generic over a collector struct, hands each decision's node and outcome to it: in production the collector does nothing and the JIT removes it; the digest test's collector hashes them. The test and production run the same generated code. The plain and the compiled walk must give the same hash over nodes, outcomes and incidents; the existing digest, features included, keeps pinning the plain walk.
7. **Where it lives**: `CompiledGame<TState>` in `GameGraph.Simulation`, next to `FittedGame`, and `Simulator.Run` takes either: the plain walk stays for debugging (step node by node, break in a transition or a feature), for training data (the replay of observed games is never compiled) and as the reference. The benchmark times both.
8. **State unchanged**: `NbaState` stays plain classes and arrays. The older lineup features (the longest stint, a team that must change) still compute on read; they move to state only if the profile after compiling points at them, since all features together are 12%.

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
