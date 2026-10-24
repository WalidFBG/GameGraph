# Models

A redesign of `GameGraph.ModelFamilies` as GAMLSS (generalised additive models for location, scale and shape): every parameter of a distribution gets its own additive predictor and its own link. Nothing is built yet.

## How it is built

1. **Training is Python**, with automatic differentiation (PyTorch): only the log-likelihood is written, the gradients of every distribution, link, spline and penalty come for free, and mature optimisers (L-BFGS, Adam) come with it. The experiments and the trainer are the same code.
2. **C# keeps the forward pass**: a new project, `GameGraph.Models`, holds terms, links, each distribution's probabilities and sampling, and reading the parameters file. It does not train. It is built on its own and not hooked up to anything until it is good; `GameGraph.ModelFamilies` and the C# trainer stay as they are meanwhile, and the swap is a later step.
3. **The files are the boundary**, as today: C# generates the training data (the engine computes every feature, Python never recomputes one), Python reads it and writes the parameters files and a training report, C# reads the parameters to simulate.
4. **Parity tests are the contract** between the two:
   - likelihood: C# recomputes the training report's training and validation negative log-likelihood from the same files and parameters, and must agree to about 1e-9; this covers the parameters file, terms, knots, links and every distribution's probabilities;
   - sampling: given fixed uniforms, C# draws the outcomes Python's inverse distribution function gives.
5. **Only contracts are public** in `GameGraph.Models`; everything else is internal.
6. **Unit tests** on both sides, once the structure is agreed.

## The contract between C# and Python

Two documents cross the boundary, plus the training data:

- C# to Python: the training data (`examples.csv`, `games.csv`, with the feature names as the header) and the manifest, which holds the model declaration (the distribution; per parameter its link and terms, with each term's basis and number of knots, or its given feature) and the feature names;
- Python to C#: the fitted model (the declaration and what fitting found: knots, centres and scales, coefficients) and the training report (likelihoods, steps).

Two kinds of duplication, handled differently:

1. **Shape** (fields, names, types, which kinds are allowed): a JSON Schema per document, checked into `schemas/` (for example `model-declaration.schema.json` and `fitted-model.schema.json`), is the contract. Types are written by hand on both sides and checked against it:
   - C#: records; a test exports their schema (`JsonSchemaExporter`, built into .NET) and fails if it differs from the checked-in one;
   - Python: dataclasses; every file it reads or writes is validated against the same schema (the `jsonschema` package), in tests and at run time, failing loudly;
   - every document carries a `schemaVersion`, and a file from another version is rejected with a clear message.
2. **Meaning** (what a spline with these knots computes, what a link does, a distribution's probabilities): written in both languages by nature, and checked numerically by the parity tests.

Training data stays CSV; Parquet is the next step if loading it in Python becomes slow.

## The Python

It mirrors the C# layers (terms, links, distributions, model), so the parity tests compare like with like, layer by layer, under the same names in the files. It differs where Python should:

1. **Vectorised**: every operation works on whole columns of examples, never one example at a time; no loop over examples in Python. The abstraction then costs a few dozen tensor operations per step, nothing next to the arithmetic.
2. **No hand-written gradients**: a distribution writes its log-probability; PyTorch differentiates the whole model.
3. **Built once, expanded once**: knots and scales are fixed during fitting, so each term's columns are computed once before the optimiser starts, and each parameter's terms are joined into one design matrix; a step is one matrix product per parameter, then the distribution.
4. **Fitting-only steps live only in Python**: knots at quantiles, scales, the split, early stopping, penalties. C# reads their results from the file.
5. **Float64 throughout**, so the parity tests can agree to about 1e-9.
6. **Small closed sets of plain classes** for bases, links and distributions; no plugin registries, deep hierarchies or code that inspects types to decide what to do.

The layers:

- `terms`: `Linear`, `LinearSpline`; fitted from data (knots, centre, scale), then expand a column into columns;
- `links`: `Identity`, `Log`, `Logit`, each its inverse;
- `distributions`: `log_prob(parameters, outcome, given)`, and for plots and the parity tests `pmf` and sampling from a uniform;
- `model`: a distribution and, per parameter, a link and terms or a given feature; coefficients by parameter and term;
- `training`: reading training data, the split, the optimiser, early stopping, writing the fitted model and the report.

## Build steps

Thin slices, each end to end, so the contract and the parity tests exist from the first slice and later slices grow them. Not hooked up to the engine; that is a later step.

Layout: `schemas/` (the contract, JSON Schema), `python/` (the trainer, `src` layout, package `gamegraph.models`), `src/GameGraph.Models` and `src/GameGraph.Models.Tests` (the C# forward pass), `golden/models/` (reference outputs written by Python and checked by both test suites, regenerated by a script and reviewed as diffs). Python tooling: `uv`, Python 3.13, `ruff`, `pyright` (strict), `pytest`, PyTorch on the CPU. The schema is written by hand; both sides validate every document they write, and every golden file, against it in tests (C#: `JsonSchema.Net`; Python: `jsonschema`). Unions carry an explicit kind (`basis`, `source`); `schemaVersion` starts at 1.

- [x] 1. Skeleton: the folders, `pyproject.toml` with the tools, `GameGraph.Models` in the solution, a README section on running both; no logic.
- [x] 2. Schema v1 for Bernoulli with linear terms: training data (manifest, examples, games), fitted model, training report; reviewed on its own before any code.
- [x] 3. Types on both sides (Python dataclasses, C# records): read, write, validate against the schema, round-trip tests.
- [x] 4. The forward pass in Python and C#: `Linear`, `Identity` and `Logit`, Bernoulli's probability and sampling, the model; unit tests; the first golden files, checked by C#.
- [x] 5. Python training: reading training data, the split by game, L-BFGS, writing the fitted model and the report; recovering known coefficients from synthetic data.
- [ ] 6. Parity: C# recomputes the report's likelihoods from the same files to about 1e-9, and draws the same outcomes from fixed uniforms. The first slice is complete.
- [ ] 7. `LinearSpline`: knots at quantiles, flat beyond the end knots, centre and scale fitted in Python; through the schema, both sides, golden files and parity.
- [ ] 8. Categorical: K − 1 log-odds, each with its own terms; through the same path.
- [ ] 9. JSU: four predicted parameters, the given bound, floor edges; the normal distribution function and its inverse in C# (our own or MathNet); through the same path.
- [ ] 10. `ActionTime` on real data in the new stack: the extraction writes the contract's training-data format, the trainer fits JSU, the plots are redrawn from the fitted model.

## The structure

From the bottom up; each layer knows only the one below it.

1. **Term**: a feature and a basis, expanding one raw value into columns. Two bases, a closed set:
   - linear, `x`: only where a feature should obviously stay linear (indicators, for example);
   - linear spline: the default (see below).
2. **Additive predictor**: η = intercept + the sum of coefficients times the columns of its own terms.
3. **Link**: a closed set (identity, log, logit). The link takes a parameter to η (logit(p) = η); its inverse takes η to the parameter (p = sigmoid(η)). Each link gives the inverse and its derivative.
4. **Distribution**: knows only its parameters (p; μ and r), never η, links or features. It gives the negative log-likelihood of an outcome and a sample from one uniform number; in Python, automatic differentiation gives the derivatives.
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
2. **Data-dependent structure**: knots (quantiles) and scaling (centres and scales) come from the training data, so they are fitted before the coefficients and recorded in the parameters file next to them. What the node declares is then the number of knots, not the knots.
3. **Which quantiles**, and how many knots by default.
4. **The Python's practicalities**: package layout, typing, tests, its environment, and how it is run next to the C#.
5. **The public contracts**: exactly which types are public in `GameGraph.Models` (the model's declaration, its fitted parameters, an example), and the parameters file as the contract with Python.
6. **Regularisation**: the smoothness penalty for splines and a penalty for linear terms, chosen on the validation set.
7. **Speed**: links and bases are closed sets and distributions are pure functions of their parameters, so the compiler plan in `plan.md` still holds.
