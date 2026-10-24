from datetime import UTC, datetime
from pathlib import Path

import torch

from gamegraph.models.contract import (
    FittedModel,
    Optimiser,
    OptimiserName,
    TrainingAndValidation,
    TrainingReport,
)
from gamegraph.models.contract_files import write_fitted_model, write_training_report
from gamegraph.models.model import Model
from gamegraph.models.training_data import Examples, TrainingData

VALIDATION_FRACTION = 0.2
MAX_ITERATIONS = 2000
MAX_EVALUATIONS = 4000
EXIT_GRADIENT = 1e-9
CHANGE_TOLERANCE = 1e-10
CONVERGED_GRADIENT = 1e-4


def train(data: TrainingData) -> tuple[FittedModel, TrainingReport]:
    training_games = len(data.games) - round(len(data.games) * VALIDATION_FRACTION)
    split = data.split(training_games)
    manifest = data.manifest
    model = Model.initial(manifest.model, manifest.features, split.training.features, split.training.outcomes)
    designs = model.designs(split.training.features)
    coefficients = model.coefficients()
    optimiser = torch.optim.LBFGS(
        coefficients,
        max_iter=MAX_ITERATIONS,
        max_eval=MAX_EVALUATIONS,
        tolerance_grad=EXIT_GRADIENT,
        tolerance_change=CHANGE_TOLERANCE,
        line_search_fn="strong_wolfe",
    )

    def closure() -> torch.Tensor:
        optimiser.zero_grad()
        loss = _loss(model, designs, split.training.outcomes)
        loss.backward()
        return loss

    optimiser.step(closure)
    state = optimiser.state[coefficients[0]]
    iterations, evaluations = int(state["n_iter"]), int(state["func_evals"])
    if iterations >= MAX_ITERATIONS or evaluations >= MAX_EVALUATIONS:
        raise RuntimeError(f"{manifest.node} did not converge in {iterations} iterations and {evaluations} evaluations")
    require_converged(manifest.node, model, designs, split.training.outcomes)

    report = TrainingReport(
        manifest.node,
        datetime.now(UTC),
        training_games,
        TrainingAndValidation[int](len(split.training.outcomes), len(split.validation.outcomes)),
        TrainingAndValidation[float](
            _negative_log_likelihood(model, split.training), _negative_log_likelihood(model, split.validation)
        ),
        Optimiser(OptimiserName.LBFGS, iterations),
    )
    return model.to_fitted(manifest.node), report


def write_training_result(directory: Path, fitted: FittedModel, report: TrainingReport) -> None:
    directory.mkdir(parents=True, exist_ok=True)
    write_fitted_model(directory / f"{fitted.node}.json", fitted)
    write_training_report(directory / f"{fitted.node}.training.json", report)


def require_converged(node: str, model: Model, designs: tuple[torch.Tensor, ...], outcomes: torch.Tensor) -> None:
    gradients = torch.autograd.grad(_loss(model, designs, outcomes), model.coefficients())
    largest = max(float(g.abs().max()) for g in gradients if g.numel() > 0)
    if largest > CONVERGED_GRADIENT:
        raise RuntimeError(
            f"{node} did not converge: its largest gradient is {largest:.3g}, above {CONVERGED_GRADIENT}"
        )


def _loss(model: Model, designs: tuple[torch.Tensor, ...], outcomes: torch.Tensor) -> torch.Tensor:
    return -model.log_prob_of(designs, outcomes).mean()


def _negative_log_likelihood(model: Model, examples: Examples) -> float:
    with torch.no_grad():
        return float(-model.log_prob(examples.features, examples.outcomes).mean())
