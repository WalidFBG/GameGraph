# Models

A redesign of `GameGraph.ModelFamilies` as GAMLSS (generalised additive models for location, scale and shape): every parameter of a distribution gets its own additive predictor and its own link. Built on its own (steps 1–11 below), and since step 12 the models the engine runs on; `GameGraph.ModelFamilies` is gone.

## How it is built

1. **Training is Python**, with automatic differentiation (PyTorch): only the log-likelihood is written, the gradients of every distribution, link, spline and penalty come for free, and mature optimisers (L-BFGS, Adam) come with it. The experiments and the trainer are the same code.
2. **C# keeps the forward pass**: a new project, `GameGraph.Models`, holds terms, links, each distribution's probabilities and sampling, and reading the parameters file. It does not train. It is built on its own and not hooked up to anything until it is good; `GameGraph.ModelFamilies` and the C# trainer stay as they are meanwhile, and the swap is a later step.
3. **The files are the boundary**, as today: C# generates the training data (the engine computes every feature, Python never recomputes one), Python reads it and writes the parameters files and a training report, C# reads the parameters to simulate.
4. **Parity tests are the contract** between the two:
   - likelihood: C# recomputes the training report's training and validation negative log-likelihood from the same files and parameters, and must agree to 1e-12 relative; this covers the parameters file, terms, knots, links and every distribution's probabilities;
   - sampling: given fixed uniforms, C# draws the outcomes Python's inverse distribution function gives.
5. **Only contracts are public** in `GameGraph.Models`; everything else is internal.
6. **Unit tests** on both sides, once the structure is agreed.

## The contract between C# and Python

Two documents cross the boundary, plus the training data:

- C# to Python: the training data (`examples.csv`, `games.csv`, with the feature names as the header) and the manifest, which holds the model declaration (the distribution; the terms of each predicted parameter, with each term's basis and number of knots; the feature of each given parameter) and the feature names;
- Python to C#: the fitted model and the training report (likelihoods, steps). The fitted model is the manifest's declaration, copied as it is, and the fit: per predicted parameter its intercept and, per term by feature, what fitting found (knot positions, centres and scales, coefficients). Neither side has a second, fitted copy of the declaration's types; pairing a fit with its declaration checks that the parameters, the terms and each term's basis match, and C#'s parity tests check that the fitted model's declaration is its manifest's.

Two kinds of duplication, handled differently:

1. **Shape** (fields, names, types, which kinds are allowed): a JSON Schema per document, checked into `schemas/` (`model.schema.json` for the declaration and its parts, `training-data-manifest.schema.json`, `fitted-model.schema.json`, `training-report.schema.json`), is the contract. Types are written by hand on both sides, and every document either side reads or writes is validated against the schema at run time, failing loudly:
   - C#: classes and records, validated with `JsonSchema.Net`;
   - Python: dataclasses, validated with the `jsonschema` package;
   - every document carries a `schemaVersion`, and a file from another version is rejected with a clear message.
2. **Meaning** (what a spline with these knots computes, what a link does, a distribution's probabilities): written in both languages by nature, and checked numerically by the parity tests.

Training data stays CSV; Parquet is the next step if loading it in Python becomes slow.

## The Python

It mirrors the C# layers (terms, links, distributions, model), so the parity tests compare like with like, layer by layer, under the same names in the files. It differs where Python should:

1. **Vectorised**: every operation works on whole columns of examples, never one example at a time; no loop over examples in Python. The abstraction then costs a few dozen tensor operations per step, nothing next to the arithmetic.
2. **No hand-written gradients**: a distribution writes its log-probability; PyTorch differentiates the whole model.
3. **Built once, expanded once**: knots and scales are fixed during fitting, so each term's columns are computed once before the optimiser starts, and each parameter's terms are joined into one design matrix; a step is one matrix product per parameter, then the distribution.
4. **Fitting-only steps live only in Python**: knot positions, scales, the split, the optimiser and, later, penalties. C# reads their results from the file.
5. **Float64 throughout**, so the parity tests can agree to 1e-12 relative.
6. **Small closed sets of plain classes** for bases, links and distributions; no plugin registries, deep hierarchies or code that inspects types to decide what to do.

The layers:

- `terms`: `Linear`, `LinearSpline`; fitted from data (knots, centre, scale), then expand a column into columns;
- `links`: `Identity`, `Log`, `Logit`, each its inverse;
- `distributions`: `log_prob(parameters, outcomes)` and `sample(parameters, uniforms)`, given parameters arriving like any other;
- `model`: a distribution, a predictor per predicted parameter and a column per given one; coefficients by parameter and term;
- `training`: reading training data, the split, the optimiser, writing the fitted model and the report.

## Build steps

Thin slices, each end to end, so the contract and the parity tests exist from the first slice and later slices grow them. Not hooked up to the engine; that is a later step.

Layout: `schemas/` (the contract, JSON Schema), `python/` (the trainer, `src` layout, package `gamegraph.models`), `src/GameGraph.Models` and `src/Tests/GameGraph.Models.Tests.Unit` (the C# forward pass), `testdata/` (test data only: reference outputs, the golden files, written by `python/tests/golden.py` from hand-made models and synthetic data, checked by both test suites, regenerated by that script and reviewed as diffs). Python tooling: `uv`, Python 3.13, `ruff`, `pyright` (strict), `pytest`, PyTorch on the CPU. The schema is written by hand; both sides validate every document they write, and every golden file, against it in tests (C#: `JsonSchema.Net`; Python: `jsonschema`). Unions carry an explicit kind (`basis`, and the distribution's `name`); `schemaVersion` starts at 1.

- [x] 1. Skeleton: the folders, `pyproject.toml` with the tools, `GameGraph.Models` in the solution, a README section on running both; no logic.
- [x] 2. Schema v1 for Bernoulli with linear terms: training data (manifest, examples, games), fitted model, training report; reviewed on its own before any code.
- [x] 3. Types on both sides (Python dataclasses, C# records): read, write, validate against the schema, round-trip tests.
- [x] 4. The forward pass in Python and C#: `Linear`, `Logit`, Bernoulli's probability and sampling, the model; unit tests; the first golden files, checked by C#.
- [x] 5. Python training: reading training data, the split by game, L-BFGS, writing the fitted model and the report; recovering known coefficients from synthetic data.
- [x] 6. Parity: C# recomputes the report's likelihoods from the same files to 1e-12 relative, and draws the same outcomes from fixed uniforms. The first slice is complete.
- [x] 7. `LinearSpline`: knots at quantiles, flat beyond the end knots, centre and scale fitted in Python; through the schema, both sides, golden files and parity.
- [x] 8. Categorical: K − 1 log-odds, each with its own terms; through the same path.
- [x] 9. JSU: four predicted parameters, the given bound, floor edges; the normal distribution function and its inverse in C# (our own); through the same path.
- [x] 10. `ActionTime` on real data in the new stack: the extraction writes the contract's training-data format, the trainer fits JSU, the plots are redrawn from the fitted model. About a million action times (802,653 training, 202,299 validation): written in 2.5 s, read in 1.1 s, trained in 48 s (493 iterations); validation negative log-likelihood 2.76916 against the SciPy experiment's 2.76947, and the predicted distributions lie on top of each other except in the thin, two-humped rows where the period clock binds. The data and plots stay outside the repository (`C:\Data\GameGraph\nba\analysis\action-time`); the extraction is still a scratchpad script, so this fit is a check, not a model in use.

Next, each its own pull request:

- [x] 11. Regularisation (see "Regularisation" below), in two pieces; pruning was dropped (see the ideas):
  - [x] 11a. The baseline: the real `ActionTime` data rewritten in the current format, with splines on location and scale. It found two things. Knots at quantiles of the data collapsed on `Bound` (now quantiles of the distinct values). And every unpenalised fit so far, step 10's included, stopped in a poor local optimum: fitted along a ridge path, the same model reaches a validation negative log-likelihood of 2.75543 against 2.76940, and on 100 games the cold fit diverges to NaN.
  - [x] 11b. Two routes: a direct fit and a ridge path from strong to no ridge, warm-started; the better optimum on training loss within a strength, the strength on validation. Checked on every real node (five Bernoulli, `ActionType`, `ActionTime`), on all their games and on 100: the direct fit and the path's end agree wherever the loss is convex, a ridge is chosen only where validation gains (by 0.0001 to 0.0003 on Bernoulli and Categorical nodes, 0.014 on `ActionTime`), and every node but `ActionTime` trains in under 4 s.
- [x] 12. The hook-up: `GameGraph.Models` replaces `GameGraph.ModelFamilies` in the engine, Python replaces the C# trainer (`uv run gamegraph-train`), the old code goes, and a transition returns the next node or the game's end. A pure migration: every node keeps its distribution and features, with linear terms. End to end, the six NBA nodes train in 6 s, and the latest game's 10,000 simulations match the old stack's closely (home win 49.9% against 50.8%, mean total 234.0 against 235.2). The gap is drift, not a fault: the validation games have more offensive rebounds (28.6% against 27.4%) and three-point attempts than the training games, the new fits match the training games, and the old trainer's early stopping on validation had leaned towards the later games. `InBonus` gains most (validation 0.1224 against 0.1295), since the old trainer never converged on it.
- [x] 13. The clock on the new stack (the tree as built: `possession-tree.html`).
  - [x] 13a. Terms over features: `ActionTime`'s terms differ by parameter, so a node declares its terms over the sport's `Feature` objects (`FeatureTerm.Linear`, `FeatureTerm.Spline`) and its given parameters as features, and takes its features from them in the order they first appear. The two can no longer disagree; the training data is unchanged.
  - [x] 13b. Reading the clock: each observed action keeps its period clock; games whose clock runs backwards (none) or whose periods 2 to 4 break the alternating rule (1 game, not the 9% the quick script guessed) are rejected. From a period's start to its first action takes over 24 seconds 3.6% of the time, between consecutive actions 2.7%: 13c's late-action rule must cover both.
  - [x] 13c1. The reader keeps what the period end cuts short: possessions whose last action does not end them, and misses at 0:00 the period ends before their rebound (a rebound at 0:00 counts only if play follows). Training-data generation drops them until 13c2.
  - [x] 13c2. The graph: the clocks in the state, `PeriodRunsOut` (the only way a period ends: asked when the period clock is below the shot clock plus 4 seconds, and after a miss at 0), `ActionTime`, `JumpBall` (the first model without terms), overtime, and free throws outside the play (`AwardedFreeThrow`, `AwardedFreeThrowMade`: without them the score missed about 1.2 points a game, enough to get overtime wrong in 136 games); the schedule goes. 3,825 games generated, 7 discarded because the rules and the data disagree. The latest game simulates 199.3 possessions on average against its real 198. `ActionTime` trains in 9 minutes, and its direct fit and the path's last step fail to converge.
  - [x] 13d. End to end. Training stops at our verdict (the largest gradient at most 1e-4) and fails an attempt whose loss is not finite: `ActionTime`'s direct fit had converged, crept on along a flat direction, jumped to NaN and spent its evaluations there; it now trains in 111 s, not 547, with the same chosen fit. `RecentGamesSimulationTests` simulates the latest 200 generated games (all in validation): possessions 200.2 against 198.1, overtime 2.5% against 2.5%, a home-win Brier score of 0.225 (0.250 for always 50%), but totals 4.8 points low (227.5 against 232.3) and no home advantage (margin 0.4 against 2.8). It fails if possessions move by more than 2%, overtime leaves 1% to 4%, or totals move by more than 8 points.

## The structure

From the bottom up; each layer knows only the one below it.

1. **Term**: a feature and a basis, expanding one raw value into columns. Two bases, a closed set:
   - linear, `x`: only where a feature should obviously stay linear (indicators, for example);
   - linear spline: the default (see below).
2. **Additive predictor**: η = intercept + the sum of coefficients times the columns of its own terms.
3. **Link**: a closed set (identity, log, logit). The link takes a parameter to η (logit(p) = η); its inverse takes η to the parameter (p = sigmoid(η)). Each link gives its inverse; automatic differentiation supplies the derivatives in training, and C# needs none.
4. **Distribution**: computes from its parameters (p; the log-odds; location, scale, skew, tail and maximum), never from η or features. It gives the negative log-likelihood of an outcome and a sample from one uniform number; in Python, automatic differentiation gives the derivatives. Its declaration fixes, for each parameter, where the value comes from, because only the distribution knows each parameter's domain:
   - **predicted**, with its link: an additive predictor with coefficients to fit, whose terms the model declares;
   - **given**: read straight from a feature the model names, with no coefficients (the bound of a truncated distribution).

   The values reach the distribution in that order, predicted then given.
5. **Model**: a distribution, the terms of each predicted parameter and the feature of each given one. The file holds no links, because the distribution fixes them; if a choice of links is ever wanted, it comes back as a real choice that the distribution checks.

   Training differentiates the whole model at once; C# only evaluates it.

"Family" goes: in a GLM it means a distribution and one link, while here the distribution fixes a link per parameter.

## Distributions

1. **Bernoulli**: p, logit link.
2. **Categorical**: K − 1 log-odds against a reference category, each with an identity link and its own terms. The softmax lives inside the distribution, because the probabilities must add up to 1 together.
3. **Discrete Johnson SU (JSU)**, for `ActionTime`, chosen by experiment (see below): a whole number of seconds from 0 to a bound that is given, not predicted (the shot clock plus 4 seconds or the period clock, whichever is shorter), so no distribution needs a special argument.
   - Four predicted parameters, each with its own terms and link: location (identity), scale (log), skew (identity), tail (log). With Z standard normal, the continuous time is location + scale · sinh((Z − skew) / tail).
   - Floor edges: second k is the probability of [k, k + 1), and the distribution is truncated to [0, n + 1) for a bound n.
   - Sampling inverts the distribution function with one uniform, never outside 0 to the bound, at a fixed cost: the normal distribution function twice (the two ends), its inverse once, and a sinh. .NET has neither, so C# has its own: Cody's rational approximations for erfc and the scaled erfcx (so log Φ stays accurate deep into the lower tail) and Wichura's AS241 for the inverse, pinned against torch by golden files.

One interface for all distributions: every one is a probability over outcomes given parameters, and training and simulation need the same operations from each.

## Training

1. **L-BFGS** (PyTorch, strong Wolfe line search, the whole training set at every step) on the mean negative log-likelihood; automatic differentiation gives the gradient.
2. **Torch's loop runs until the largest gradient falls below 1e-4, our verdict below**, or the mean negative log-likelihood changes by less than 1e-10 between iterations, at most 2,000 iterations and 4,000 evaluations. Running on past the verdict only risks a step along a nearly flat direction: on `ActionTime` with the clock, the direct fit reached it after about 1,050 evaluations, crept on, jumped to coefficients of 1e113 and spent its remaining evaluations on NaN. An attempt whose loss is not finite fails at once.
3. **The verdict is ours**: torch also stops silently when it stalls (a direction that does not descend, a step that moves nothing, a line search that gives up), so afterwards the largest gradient of the loss must be below 1e-4, or the attempt fails (see "Regularisation"). The columns are standardised and the loss is a mean, so 1e-4 means the same for every coefficient; on `ActionTime` the gradient is 0.35 at the start and 3.3e-6 at the end.
4. **The split**: the latest 20% of games by start time are validation; the report records how many games are training, so C# never reimplements the rule.
5. **Starting point**: zero coefficients and each intercept at the link of a simple estimate (the mean outcome, the category shares, the mean and spread of the times).

## Regularisation

Training chooses how much to shrink each node's model, separately for each node and with nothing to set; the strengths tried are constants in the code, like the tolerances.

1. **Two routes**: the loss is the mean negative log-likelihood plus a ridge strength times the sum of squared coefficients (not intercepts; the columns are standardised, so one strength means the same for every term). Each model is fitted twice from the same starting point: directly, with no ridge, and along a ridge path (1e-2, 1e-3, 1e-4, 1e-5, 0, each fit warm-started from the last). Every fit is an attempt.
2. **Two rules choose among them**, because they answer two questions: at one ridge strength, the attempts are fits of the same objective from different starts, so the lower training loss is the better optimum (only ridge 0 has two, the direct fit and the path's end, and there the training loss is the whole objective); across strengths, how much to shrink is model selection, and the best validation negative log-likelihood decides. Validation is spent only on the strength.
3. **Why two routes**: where the loss is convex (Bernoulli and Categorical with their links), both routes end at the same optimum and the path only chooses how much to shrink. The JSU likelihood is not convex, and neither route always wins: on `ActionTime` the direct fit stops in a poor optimum (validation 2.76940) and the path reaches a better one (2.75543); on synthetic JSU data the path walks into the normal limit (scale and tail without bound) and the direct fit finds the true parameters. On little data the direct fit can also diverge (scale and tail to 0, chasing the two humps of the rows where the period clock binds).
4. **A failed attempt is reported, not fatal**: an attempt that does not converge is listed in the report's failures with its reason and never chosen, and the rest of its route is skipped, since it would start from a failed fit. Training fails only when every attempt fails.
5. **Training games only**: the chosen attempt is the final model. Its validation score is the selection score, the best of five strengths, so it is biased low as an estimate on new games; there is no untouched test split.
6. **In the report**: every attempt (its route, ridge, likelihoods and iterations), every failure with its reason, and which attempt was chosen.

## Numerics

1. **Log-space differences**: the probability of a range is computed as log[Φ(b) − Φ(a)] from log Φ, on the upper side when both points are above the median, so neither tail underflows.
2. **Φ is exp(log Φ) in both languages**: torch's `ndtr` cancels to exactly 0 below about z = −8 (it computes 0.5·(1 + erf)), which made JSU sampling wrong when the mass sat far below 0; `log_ndtr` is accurate.
3. **log(1 + x) is our own in C#**: .NET's `double.LogP1` evaluates log(1 + x) naively and loses up to about 10 digits for small x; `LogOnePlus` uses log(u)·x/(u − 1) with u = 1 + x.
4. **Golden tolerances**: 1e-12 relative for model values; the normal functions 1e-13 relative (1e-12 for Φ, which comes through an exponential), ignoring values below 1e-300, which no model needs and subnormals cannot hold precisely.

## Linear splines

1. Knots at evenly spaced quantiles of the feature's distinct values, each the nearest actual value (so a feature with fewer distinct values than knots gets a knot at each, and never two segments with the same data), not quantiles of the data: a feature with heavily repeated values would otherwise lose its knots to them (`Bound` is 24 in most rows and 14 in most others, so 8 knots at quantiles of the data merged into 0, 14 and 24, leaving one straight line below 14, where the period clock binds). For a continuous feature it is almost the same.
2. Flat beyond the end knots: the value is clamped to the first and last knot before it is expanded.
3. The smoothness penalty (on changes of slope between knots) comes later; the basics first.

## Agreed

1. Coefficients are organised by parameter, then by term; a flat vector exists only inside the optimiser.
2. Scaling lives in the terms: each term records the centre and scale of its columns, and coefficients stay on that scale, so there is no converting back after training.
3. Outcomes stay `int` until a continuous distribution (a Normal, for example) arrives.
4. Unit tests:
   - links: the inverse;
   - distributions: known values, probabilities adding up to 1, sampled frequencies against probabilities;
   - terms: expansion;
   - the model: coefficients round-tripping through the file;
   - the trainer (Python): recovering known coefficients from synthetic data.

## Ideas and open questions

1. **The distribution for `ActionTime`**: an action never takes more than 24 seconds, so the bound is at most 24. Tested in Python on about a million real action times (latest 20% of games for validation; the same linear features for every candidate; plots of actual against predicted by situation in `C:\Data\GameGraph\nba\analysis\action-time`):
   - truncated Poisson: far too narrow; out;
   - beta-binomial: validation negative log-likelihood 2.796, too wide and falling away before 24; out;
   - truncated negative binomial: 2.773, good in normal play, but it cannot be narrower than a Poisson, so it misses sharp peaks (the fast break after a live-ball turnover);
   - **discretised truncated JSU (Johnson's SU: location, scale, skew and tail)**: 2.769, the best, and it fits the sharp peaks; chosen, with floor edges (second k is the probability of [k, k + 1), truncated to [0, n + 1)), which fit as well as rounding and are simpler.

   What no distribution fits is structure: spikes at 0 (putbacks, a quarter of fouls) and the two humps when the period clock binds (acting early or holding for the last shot). Candidates: a hurdle node ("acts immediately") or richer features, and the `PeriodRunsOut` gate.
2. **A nearly flat direction in the real `ActionTime` fit**: after convergence the largest gradient stays at about 1e-6 while the fit creeps on, most likely parameters trading off (location against skew, scale against tail) or overlapping features (`Bound`, `LogBound`, `PeriodClockBinds`); 11a found the likelier cause: the fit had stopped in a poor local optimum, and a ridge path reaches a better one.
3. **A smoothness penalty for splines** (the squared changes of slope between neighbouring segments): measured on `ActionTime`, it gave no validation gain at any strength, and the ridge path already tames wild slopes. If splines wiggle on small nodes, it comes back; unlike ridge, it pulls a stiff spline to a straight line rather than a flat one.
4. **Pruning**, dropped from step 11. A group lasso (a term kept or pruned whole), fitted by accelerated proximal gradient descent, converged badly on 100 games of `ActionTime`: the JSU loss is badly conditioned, so it hit its step cap at one strength and crept at the others, and on all games it would take about an hour. If pruning comes back, a cheaper route: rank the terms once by their Wald statistics from the exact Hessian at the chosen fit, drop them in that order, and refit nested sets warm-started, choosing on validation with the full model still a candidate.
5. **The distribution comparison again**: the candidates for `ActionTime` (above) were each fitted cold, so each may have stopped in a poor optimum; refit with a ridge path, the ranking could change.
6. **JSU's limits at infinity**: on data with no single peak (synthetic outcomes only 0 or the maximum), the scale and tail intercepts run off together (to about 1e34 and 1e30) towards a near-uniform limit; the ridge leaves intercepts free, and the gradient test passes because the loss is flat out there. The same normal-like limit is where the ridge path stopped on synthetic JSU data. Fitting scale ÷ tail and 1 ÷ tail instead of scale and tail would put those limits at finite values.
7. **Refitting on all games**: training only on the earlier 80% means the final model never sees the newest games, which in sport are often the most relevant (rules and styles drift). Step 12 measured it: the validation games have 28.6% offensive rebounds against the training games' 27.4%, and more three-point attempts, which lowers the simulated total of the latest game by about a point. If that matters, refit once on all games with the chosen strengths and terms, and report validation from the selection run.
8. **A test split**: the latest 10% of games, say, on which nothing is chosen, would give an unbiased score; it matters once validation scores compare distributions or feature sets (idea 5).
9. **Knots on dense integer features**: quantiles of the distinct values spread knots evenly in value, so a feature like seconds remaining (every value present, skewed density) gets as many segments in its sparse tails as in its dense middle. Alternatives: quantiles of the data with each value's weight capped at 1/(knots − 1) of the rows, or quantiles of the data with mass points kept once and the freed knots placed on the rest. Compare on validation once the hook-up brings the real features.
10. **Fits that name their data**: neither the fitted model nor the report records which training data it came from, so fits older than regenerated data go unnoticed. The manifest's `createdUtc` in the report would let simulation and the QA test name the data they ran on.
11. **Speed**: links and bases are closed sets and distributions are pure functions of their parameters, so the compiler plan in `plan.md` still holds. If larger models make L-BFGS slow, Newton's method with the exact Hessian (from automatic differentiation) is the next candidate.
12. **Home advantage**: no feature says which team is at home, so simulated home margins are 2.4 points short of real ones on the latest 200 games and home wins 51.2% against 58.0%. A home indicator on the scoring nodes is the obvious first try.
13. **Totals run low**: 4.8 points a game on the latest 200 games, with 1% more possessions, so too few points per possession. The models train on the earliest 80% of games and the late season scores more (step 12 found the same drift), but how much of the gap that explains is unmeasured; refitting on all games (idea 7) would tell.
