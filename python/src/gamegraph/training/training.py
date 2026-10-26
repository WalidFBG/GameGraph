from collections.abc import Sequence
from copy import deepcopy
from datetime import UTC, datetime

import torch

from gamegraph.contract import (
    Attempt,
    FailedAttempt,
    FittedModel,
    Route,
    TrainingAndValidation,
    TrainingReport,
)
from gamegraph.models.model import Model
from gamegraph.training_tensors import TrainingTensors

VALIDATION_FRACTION = 0.2
RIDGE_PATH = (1e-2, 1e-3, 1e-4, 1e-5, 0.0)
ROUTES = ((Route.DIRECT, (0.0,)), (Route.RIDGE_PATH, RIDGE_PATH))
MAX_ITERATIONS = 2000
MAX_EVALUATIONS = 4000
CHANGE_TOLERANCE = 1e-10
CONVERGED_GRADIENT = 1e-4


class NotConverged(RuntimeError):
    pass


def train(data: TrainingTensors) -> tuple[FittedModel, TrainingReport]:
    training_games = len(data.games) - round(len(data.games) * VALIDATION_FRACTION)
    split = data.split(training_games)
    manifest = data.manifest
    training, validation = split.training, split.validation
    start = Model.initial(manifest.model, manifest.features, training.features, training.outcomes)
    designs, validation_designs = start.designs(training.features), start.designs(validation.features)
    attempts: list[Attempt] = []
    failures: list[FailedAttempt] = []
    fits: list[FittedModel] = []
    for route, ridges in ROUTES:
        model = deepcopy(start)
        for ridge in ridges:
            try:
                iterations = fit(manifest.node, model, designs, training.outcomes, ridge)
            except NotConverged as failure:
                failures.append(FailedAttempt(route, ridge, str(failure)))
                break
            likelihood = TrainingAndValidation[float](
                _negative_log_likelihood(model, designs, training.outcomes),
                _negative_log_likelihood(model, validation_designs, validation.outcomes),
            )
            attempts.append(Attempt(route, ridge, likelihood, iterations))
            fits.append(model.to_fitted(manifest.node))
    if not attempts:
        raise NotConverged(f"Every attempt to fit {manifest.node} failed: " + "; ".join(f.reason for f in failures))
    chosen = choose(attempts)

    report = TrainingReport(
        manifest.node,
        datetime.now(UTC),
        training_games,
        TrainingAndValidation[int](len(training.outcomes), len(validation.outcomes)),
        chosen,
        tuple(attempts),
        tuple(failures),
    )
    return fits[chosen], report


def choose(attempts: Sequence[Attempt]) -> int:
    best: dict[float, int] = {}
    for i, attempt in enumerate(attempts):
        incumbent = best.get(attempt.ridge)
        if incumbent is None or _training(attempt) < _training(attempts[incumbent]):
            best[attempt.ridge] = i
    return min(best.values(), key=lambda i: attempts[i].negative_log_likelihood.validation)


def require_converged(
    node: str, model: Model, designs: tuple[torch.Tensor, ...], outcomes: torch.Tensor, ridge: float
) -> None:
    gradients = torch.autograd.grad(_loss(model, designs, outcomes, ridge), model.coefficients())
    largest = float(torch.cat([g.flatten() for g in gradients]).abs().max())
    if not largest <= CONVERGED_GRADIENT:
        raise NotConverged(
            f"{node} did not converge at ridge {ridge:g}: its largest gradient is {largest:.3g}, "
            f"above {CONVERGED_GRADIENT}"
        )


def fit(node: str, model: Model, designs: tuple[torch.Tensor, ...], outcomes: torch.Tensor, ridge: float) -> int:
    coefficients = model.coefficients()
    optimiser = torch.optim.LBFGS(
        coefficients,
        max_iter=MAX_ITERATIONS,
        max_eval=MAX_EVALUATIONS,
        tolerance_grad=CONVERGED_GRADIENT,
        tolerance_change=CHANGE_TOLERANCE,
        line_search_fn="strong_wolfe",
    )

    def closure() -> torch.Tensor:
        optimiser.zero_grad()
        loss = _loss(model, designs, outcomes, ridge)
        if not torch.isfinite(loss):
            raise NotConverged(f"{node} diverged at ridge {ridge:g}: its loss is {float(loss.detach())}")
        loss.backward()
        return loss

    optimiser.step(closure)
    state = optimiser.state[coefficients[0]]
    iterations, evaluations = int(state["n_iter"]), int(state["func_evals"])
    if iterations >= MAX_ITERATIONS or evaluations >= MAX_EVALUATIONS:
        raise NotConverged(
            f"{node} did not converge at ridge {ridge:g} in {iterations} iterations and {evaluations} evaluations"
        )
    require_converged(node, model, designs, outcomes, ridge)
    return iterations


def _loss(model: Model, designs: tuple[torch.Tensor, ...], outcomes: torch.Tensor, ridge: float) -> torch.Tensor:
    squared = sum(((p.coefficients**2).sum() for p in model.predictors), torch.zeros((), dtype=torch.float64))
    return -model.log_prob_from_designs(designs, outcomes).mean() + ridge * squared


def _training(attempt: Attempt) -> float:
    return attempt.negative_log_likelihood.training


def _negative_log_likelihood(model: Model, designs: tuple[torch.Tensor, ...], outcomes: torch.Tensor) -> float:
    with torch.no_grad():
        return float(-model.log_prob_from_designs(designs, outcomes).mean())
