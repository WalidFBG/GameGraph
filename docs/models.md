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

- C# to Python: the training data (`examples.csv`, `games.csv`, with the feature names as the header) and the manifest, which holds the model declaration (the distribution; per parameter its link and terms, with each term's basis and number of knots, or its given feature) and the feature names;
- Python to C#: the fitted model (the declaration and what fitting found: knots, centres and scales, coefficients) and the training report (likelihoods, steps).

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
4. **Fitting-only steps live only in Python**: knots at quantiles, scales, the split, the optimiser and, later, penalties. C# reads their results from the file.
5. **Float64 throughout**, so the parity tests can agree to 1e-12 relative.
6. **Small closed sets of plain classes** for bases, links and distributions; no plugin registries, deep hierarchies or code that inspects types to decide what to do.

The layers:

- `terms`: `Linear`, `LinearSpline`; fitted from data (knots, centre, scale), then expand a column into columns;
- `links`: `Identity`, `Log`, `Logit`, each its inverse;
- `distributions`: `log_prob(parameters, outcomes)` and `sample(parameters, uniforms)`, given parameters arriving like any other;
- `model`: a distribution and, per parameter, a link and terms or a given feature; coefficients by parameter and term;
- `training`: reading training data, the split, the optimiser, writing the fitted model and the report.

## Build steps

Thin slices, each end to end, so the contract and the parity tests exist from the first slice and later slices grow them. Not hooked up to the engine; that is a later step.

Layout: `schemas/` (the contract, JSON Schema), `python/` (the trainer, `src` layout, package `gamegraph.models`), `src/GameGraph.Models` and `src/GameGraph.Models.Tests` (the C# forward pass), `golden/models/` (reference outputs written by Python and checked by both test suites, regenerated by a script and reviewed as diffs). Python tooling: `uv`, Python 3.13, `ruff`, `pyright` (strict), `pytest`, PyTorch on the CPU. The schema is written by hand; both sides validate every document they write, and every golden file, against it in tests (C#: `JsonSchema.Net`; Python: `jsonschema`). Unions carry an explicit kind (`basis`, `source`); `schemaVersion` starts at 1.

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

- [ ] 11. Regularisation and pruning: a smoothness penalty for splines, a penalty for linear terms, their strength chosen on the validation set, and how a term is dropped; designed first.
- [ ] 12. The hook-up: `GameGraph.Models` replaces `GameGraph.ModelFamilies` in the engine, Python replaces the C# trainer, the old code goes.
- [ ] 13. The clock (`clock.md`) on the new stack.

## The structure

From the bottom up; each layer knows only the one below it.

1. **Term**: a feature and a basis, expanding one raw value into columns. Two bases, a closed set:
   - linear, `x`: only where a feature should obviously stay linear (indicators, for example);
   - linear spline: the default (see below).
2. **Additive predictor**: η = intercept + the sum of coefficients times the columns of its own terms.
3. **Link**: a closed set (identity, log, logit). The link takes a parameter to η (logit(p) = η); its inverse takes η to the parameter (p = sigmoid(η)). Each link gives its inverse; automatic differentiation supplies the derivatives in training, and C# needs none.
4. **Distribution**: knows only its parameters (p; the log-odds; location, scale, skew, tail and maximum), never η, links or features. It gives the negative log-likelihood of an outcome and a sample from one uniform number; in Python, automatic differentiation gives the derivatives.
5. **Model**: a distribution and, per parameter, where its value comes from:
   - **predicted**: an additive predictor and a link, with coefficients to fit;
   - **given**: read straight from a feature, with no coefficients (the bound of a truncated distribution).

   Training differentiates the whole model at once; C# only evaluates it.

"Family" goes: in a GLM it means a distribution and its link, which is exactly what this splits.

## Distributions

1. **Bernoulli**: p, logit link.
2. **Categorical**: K − 1 log-odds against a reference category, each with an identity link and its own terms. The softmax lives inside the distribution, because the probabilities must add up to 1 together.
3. **A distribution for `ActionTime`**, chosen by experiment (see below): a count of seconds from 0 to a bound that is given, not predicted (the shot clock or the period clock, whichever is shorter). The bound is a given parameter, so no distribution needs a special argument.

One interface for all distributions: every one is a probability over outcomes given parameters, and training and simulation need the same operations from each.

## Training

1. **L-BFGS** (PyTorch, strong Wolfe line search, the whole training set at every step) on the mean negative log-likelihood; automatic differentiation gives the gradient.
2. **It stops when the mean negative log-likelihood changes by less than 1e-10 between iterations**, at most 2,000 iterations and 4,000 evaluations; not converging fails loudly and writes nothing. A gradient test (1e-9) is kept but never decides: on a million rows the gradient settles above it.
3. **The split**: the latest 20% of games by start time are validation; the report records how many games are training, so C# never reimplements the rule.
4. **Starting point**: zero coefficients and each intercept at the link of a simple estimate (the mean outcome, the category shares, the mean and spread of the times).

## Numerics

1. **Log-space differences**: the probability of a range is computed as log[Φ(b) − Φ(a)] from log Φ, on the upper side when both points are above the median, so neither tail underflows.
2. **Φ is exp(log Φ) in both languages**: torch's `ndtr` cancels to exactly 0 below about z = −8 (it computes 0.5·(1 + erf)), which made JSU sampling wrong when the mass sat far below 0; `log_ndtr` is accurate.
3. **log(1 + x) is our own in C#**: .NET's `double.LogP1` evaluates log(1 + x) naively and loses up to about 10 digits for small x; `LogOnePlus` uses log(u)·x/(u − 1) with u = 1 + x.
4. **Golden tolerances**: 1e-12 relative for model values; the normal functions 1e-13 relative (1e-12 for Φ, which comes through an exponential), ignoring values below 1e-300, which no model needs and subnormals cannot hold precisely.

## Linear splines

1. Knots at quantiles of the data.
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
2. **A nearly flat direction in the real `ActionTime` fit**: after convergence the largest gradient stays at about 1e-6 while the fit creeps on, most likely parameters trading off (location against skew, scale against tail) or overlapping features (`Bound`, `LogBound`, `PeriodClockBinds`); step 11's penalties should pin it down.
3. **Speed**: links and bases are closed sets and distributions are pure functions of their parameters, so the compiler plan in `plan.md` still holds. If larger models make L-BFGS slow, Newton's method with the exact Hessian (from automatic differentiation) is the next candidate.
