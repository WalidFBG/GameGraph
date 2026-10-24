# Models

A redesign of `GameGraph.ModelFamilies` as GAMLSS (generalised additive models for location, scale and shape): every parameter of a distribution gets its own additive predictor and its own link. Built on its own (steps 1–10 below) and not yet used by the engine.

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

Layout: `schemas/` (the contract, JSON Schema), `python/` (the trainer, `src` layout, package `gamegraph.models`), `src/GameGraph.Models` and `src/GameGraph.Models.Tests` (the C# forward pass), `testdata/` (test data only: reference outputs, the golden files, written by `python/tests/golden.py` from hand-made models and synthetic data, checked by both test suites, regenerated by that script and reviewed as diffs). Python tooling: `uv`, Python 3.13, `ruff`, `pyright` (strict), `pytest`, PyTorch on the CPU. The schema is written by hand; both sides validate every document they write, and every golden file, against it in tests (C#: `JsonSchema.Net`; Python: `jsonschema`). Unions carry an explicit kind (`basis`, and the distribution's `name`); `schemaVersion` starts at 1.

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

- [ ] 11. Regularisation and pruning (see "Regularisation and pruning" below), in three pieces:
  - [x] 11a. The baseline: the real `ActionTime` data rewritten in the current format, with splines on location and scale. It found two things. Knots at quantiles of the data collapsed on `Bound` (now quantiles of the distinct values). And every unpenalised fit so far, step 10's included, stopped in a poor local optimum: fitted along a ridge path, the same model reaches a validation negative log-likelihood of 2.75543 against 2.76940, and on 100 games the cold fit diverges to NaN.
  - [ ] 11b. Fitting by a ridge path: every model fitted from strong to no ridge, warm-started, the point chosen on validation.
  - [ ] 11c. Pruning: the group lasso path, relaxed refits with the chosen ridge, pruned terms left out of the fit, the point chosen on validation.
- [ ] 12. The hook-up: `GameGraph.Models` replaces `GameGraph.ModelFamilies` in the engine, Python replaces the C# trainer, the old code goes.
- [ ] 13. The clock (`clock.md`) on the new stack.

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
3. **A distribution for `ActionTime`**, chosen by experiment (see below): a count of seconds from 0 to a bound that is given, not predicted (the shot clock or the period clock, whichever is shorter). The bound is a given parameter, so no distribution needs a special argument.

One interface for all distributions: every one is a probability over outcomes given parameters, and training and simulation need the same operations from each.

## Training

1. **L-BFGS** (PyTorch, strong Wolfe line search, the whole training set at every step) on the mean negative log-likelihood; automatic differentiation gives the gradient.
2. **Torch's loop runs until the mean negative log-likelihood changes by less than 1e-10 between iterations**, at most 2,000 iterations and 4,000 evaluations; it also exits early if the largest gradient falls below 1e-9, which on a million rows it never does.
3. **The verdict is ours**: torch also stops silently when it stalls (a direction that does not descend, a step that moves nothing, a line search that gives up), so afterwards the largest gradient of the mean negative log-likelihood must be below 1e-4, or training fails loudly and writes nothing. The columns are standardised and the loss is a mean, so 1e-4 means the same for every coefficient; on `ActionTime` the gradient is 0.35 at the start and 3.3e-6 at the end.
4. **The split**: the latest 20% of games by start time are validation; the report records how many games are training, so C# never reimplements the rule.
5. **Starting point**: zero coefficients and each intercept at the link of a simple estimate (the mean outcome, the category shares, the mean and spread of the times).

## Regularisation and pruning

What a person decides is only what could matter: each node declares every plausible feature, mostly as splines. Training decides what does, for each node separately and with nothing to set; the ranges searched are constants in the code, like the tolerances.

1. **Fitting by a ridge path**: the loss is the mean negative log-likelihood plus a strength times the sum of squared coefficients (not intercepts; the columns are standardised, so one strength means the same for every term). Every model is fitted at 1e-2, 1e-3, 1e-4, 1e-5 and 0 in turn, each fit warm-started from the last, and the point with the best validation negative log-likelihood is kept. The JSU likelihood is not convex, and from the starting point a cold fit can stop in a poor local optimum or, on little data, diverge (scale and tail to 0, chasing the two humps of the rows where the period clock binds); coming down from a strong ridge avoids both. On `ActionTime` (800,000 rows) validation chooses 0 and reaches 2.75543 against the cold fit's 2.76940, in about 170 s against 64 s; on 100 of its games it chooses 1e-3.
2. **The ridge holds** for what follows: pruning's refits keep the chosen strength.
3. **Pruning by a group lasso**: a strength times the sum, over terms, of √(columns) times the length of the term's coefficients, so a term (all of a spline's segments together) is kept or pruned whole. Its kink at zero is what makes exact zeros, so it is fitted by accelerated proximal gradient descent, not L-BFGS.
4. **The path**: from the smallest strength that prunes every term (exact, from the gradient of the intercept-only model) down geometrically, about 10 points, each warm-started from the last.
5. **Relaxed refits**: at each point the lasso only chooses the terms; L-BFGS refits the kept terms with the chosen ridge and no lasso, so coefficients are not shrunk and the gradient verdict still decides convergence. The point with the best validation negative log-likelihood is the model.
6. **Training games only**: the chosen refit is the final model, so the report's validation stays honest.
7. **In the files**: a pruned term is left out of the fit (the declaration stays the manifest's), C# skips a declared term without a fit, and the report records the strengths, the path and the pruned terms.

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
4. **The distribution comparison again**: the candidates for `ActionTime` (above) were each fitted cold, so each may have stopped in a poor optimum; refit with a ridge path, the ranking could change.
5. **Refitting on all games**: training only on the earlier 80% means the final model never sees the newest games, which in sport are often the most relevant (rules and styles drift). If that matters, refit once on all games with the chosen strengths and terms, and report validation from the selection run.
6. **Speed**: links and bases are closed sets and distributions are pure functions of their parameters, so the compiler plan in `plan.md` still holds. If larger models make L-BFGS slow, Newton's method with the exact Hessian (from automatic differentiation) is the next candidate.
