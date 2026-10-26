# Models

Every node's model is a distribution whose parameters each have their own additive predictor and link (a GAMLSS: generalised additive model for location, scale and shape). A predictor is an intercept plus a coefficient for each column of its terms; a term is a feature with a basis.

## Terms

- **Linear**: the feature itself, centred and scaled.
- **Linear spline**: piecewise linear in the feature, with knots at evenly spaced quantiles of its distinct values, flat beyond the first and last knot.

## Distributions

| Distribution | Outcome | Parameters | Example |
|---|---|---|---|
| Bernoulli | yes or no | the probability (logit link) | `ShotMade` |
| Categorical | one of K categories | K − 1 log-odds against the first category | `ActionType` |
| Discrete Johnson SU | whole seconds from 0 to a bound | location, scale, skew and tail; the bound is given by the state, not predicted | `ActionTime` |
| Choice | a set of k of a decision's candidates | each candidate's utility; the size k is given | `Shooter` (one of five), `SubstitutedOn` (k of the bench) |

A choice gives a set the probability of its utilities' product among every set of its size; for one candidate that is a softmax over the candidates.

## Training

- **Data**: each node's training data is split by game: the latest 20% of games by start time are the validation set.
- **Loss**: the mean negative log-likelihood, minimised with L-BFGS on the whole training set, from zero coefficients and intercepts at simple estimates.
- **Shrinkage**: each model is fitted twice, directly and along a ridge path from strong to no ridge (each fit starting from the last); the strength with the best validation likelihood is kept. Nothing needs setting.
- **Convergence**: a fit counts only if its largest gradient ends below 1e-4; a fit that fails is reported and never chosen.
- **What it writes**: per node, the fitted model (the declaration, and per parameter its intercept and each term's fit) and a training report (every attempt, its likelihoods, the chosen one).
- **Retraining**: a node keeps its fitted model while the digest recorded with it still matches (its training data, the trainer's source and the torch version); `--all` retrains every node.
