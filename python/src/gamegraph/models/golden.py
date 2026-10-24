import json
from math import nextafter
from pathlib import Path

import numpy as np
import torch

from gamegraph.models import normal
from gamegraph.models.contract import (
    BernoulliDistribution,
    CategoricalDistribution,
    DiscreteJohnsonSuDistribution,
    FittedLinearSplineTerm,
    FittedLinearTerm,
    FittedModel,
    FittedPredictor,
)
from gamegraph.models.contract_files import write_fitted_model
from gamegraph.models.model import Model
from gamegraph.models.paths import GOLDEN
from gamegraph.models.synthetic import bernoulli, categorical, johnson_su
from gamegraph.models.training import train, write_training_result
from gamegraph.models.training_data import TrainingData, write_training_data

type Case = tuple[tuple[float, ...], int, float]

BERNOULLI = FittedModel(
    "Golden",
    BernoulliDistribution(),
    {
        "probability": FittedPredictor(
            -0.12,
            (
                FittedLinearTerm("Gaussian", 0.31, 8.4, (0.021,)),
                FittedLinearTerm("Indicator", 0.38, 0.49, (-0.18,)),
                FittedLinearSplineTerm(
                    "Uniform", 4, (0.0, 8.0, 16.0, 24.0), (4.1, 3.9, 2.2), (3.0, 3.5, 3.1), (0.12, -0.05, 0.2)
                ),
            ),
        )
    },
    {},
)

BERNOULLI_CASES: list[Case] = [
    ((0.0, 0.0, 12.0), 1, 0.1),
    ((0.0, 0.0, 12.0), 0, 0.9),
    ((0.0, 1.0, -3.0), 1, 0.45),
    ((0.0, 1.0, 0.0), 0, 0.47),
    ((-7.0, 1.0, 8.0), 0, 0.05),
    ((12.0, 0.0, 11.5), 1, 0.999),
    ((25.0, 1.0, 16.0), 1, 0.5),
    ((-25.0, 0.0, 20.0), 0, 0.5),
    ((3.5, 0.0, 24.0), 1, 0.0),
    ((-1.25, 1.0, 30.0), 0, 0.75),
    ((12000.0, 0.0, 5.0), 1, 0.999999),
    ((-12000.0, 1.0, 5.0), 0, 0.000001),
]

CATEGORICAL = FittedModel(
    "Golden",
    CategoricalDistribution(("first", "second", "third")),
    {
        "second": FittedPredictor(
            0.2,
            (
                FittedLinearTerm("Gaussian", 1.5, 4.0, (0.3,)),
                FittedLinearSplineTerm("Uniform", 3, (0.0, 12.0, 24.0), (5.0, 4.0), (4.5, 4.2), (0.25, -0.4)),
            ),
        ),
        "third": FittedPredictor(-0.8, (FittedLinearTerm("Gaussian", 1.5, 4.0, (-0.2,)),)),
    },
    {},
)

CATEGORICAL_CASES: list[Case] = [
    ((0.0, 6.0), 0, 0.1),
    ((0.0, 6.0), 1, 0.6),
    ((0.0, 6.0), 2, 0.95),
    ((5.0, 12.0), 1, 0.3),
    ((-5.0, 30.0), 2, 0.99),
    ((-5.0, -2.0), 0, 0.0),
    ((40.0, 18.0), 1, 0.5),
    ((-40.0, 0.0), 2, 0.2),
    ((2.0, 24.0), 0, 0.7),
]

JOHNSON_SU = FittedModel(
    "Golden",
    DiscreteJohnsonSuDistribution(),
    {
        "location": FittedPredictor(7.0, (FittedLinearTerm("Gaussian", 0.1, 1.2, (2.5,)),)),
        "scale": FittedPredictor(1.4, (FittedLinearTerm("Gaussian", 0.1, 1.2, (0.15,)),)),
        "skew": FittedPredictor(0.5, ()),
        "tail": FittedPredictor(0.3, ()),
    },
    {"maximum": "Bound"},
)

JOHNSON_SU_CASES: list[Case] = [
    ((0.0, 24.0), 5, 0.3),
    ((0.0, 24.0), 24, 0.99),
    ((0.0, 0.0), 0, 0.5),
    ((0.0, 1.0), 1, 0.7),
    ((1.0, 14.0), 9, 0.2),
    ((-30.0, 24.0), 0, 0.6),
    ((30.0, 10.0), 10, 0.4),
    ((-1.5, 3.0), 2, 0.05),
    ((2.0, 24.0), 12, 0.999),
    ((0.5, 24.0), 0, 0.0),
]

CASES: dict[str, tuple[FittedModel, tuple[str, ...], list[Case]]] = {
    "bernoulli": (BERNOULLI, ("Gaussian", "Indicator", "Uniform"), BERNOULLI_CASES),
    "categorical": (CATEGORICAL, ("Gaussian", "Uniform"), CATEGORICAL_CASES),
    "johnson-su": (JOHNSON_SU, ("Gaussian", "Bound"), JOHNSON_SU_CASES),
}

NORMAL_POINTS = [
    -40.0,
    -38.0,
    -30.0,
    -20.0,
    -10.0,
    -5.0,
    -3.0,
    -2.0,
    -1.5,
    -1.0,
    -0.5,
    -0.1,
    0.0,
    0.1,
    0.5,
    1.0,
    1.5,
    2.0,
    3.0,
    5.0,
    8.0,
    10.0,
    20.0,
    38.0,
    40.0,
]
NORMAL_PROBABILITIES = [
    1e-300,
    1e-100,
    1e-20,
    1e-10,
    1e-5,
    0.001,
    0.02,
    0.075,
    0.1,
    0.25,
    0.5,
    0.6,
    0.9,
    0.925,
    0.975,
    0.999,
    1 - 1e-10,
    1 - 1e-15,
]


def write_cases(directory: Path, fitted: FittedModel, feature_names: tuple[str, ...], cases: list[Case]) -> None:
    directory.mkdir(parents=True, exist_ok=True)
    write_fitted_model(directory / "fitted-model.json", fitted)
    features = torch.tensor([case[0] for case in cases], dtype=torch.float64)
    outcomes = torch.tensor([case[1] for case in cases])
    uniforms = torch.tensor([case[2] for case in cases], dtype=torch.float64)
    model = Model.from_fitted(fitted, feature_names)
    parameters = model.parameter_values(features)
    log_probability = model.log_prob(features, outcomes)
    samples = model.sample(features, uniforms)
    for nudged in ([nextafter(c[2], 1) for c in cases], [max(nextafter(c[2], 0), 0.0) for c in cases]):
        if not torch.equal(model.sample(features, torch.tensor(nudged, dtype=torch.float64)), samples):
            raise ValueError("A golden uniform sits on a category boundary, so its sample depends on rounding")
    document = {
        "features": list(feature_names),
        "cases": [
            {
                "features": [float(value) for value in features[i]],
                "outcome": int(outcomes[i]),
                "parameters": [float(p[i]) for p in parameters],
                "logProbability": float(log_probability[i]),
                "uniform": float(uniforms[i]),
                "sample": int(samples[i]),
            }
            for i in range(len(cases))
        ],
    }
    text = json.dumps(document, indent=2, allow_nan=False) + "\n"
    (directory / "cases.json").write_text(text, encoding="utf-8", newline="\n")


def write_normal(directory: Path) -> None:
    directory.mkdir(parents=True, exist_ok=True)
    points = torch.tensor(NORMAL_POINTS, dtype=torch.float64)
    probabilities = torch.tensor(NORMAL_PROBABILITIES, dtype=torch.float64)
    document = {
        "cdf": [
            {"z": float(z), "cdf": float(c), "logCdf": float(lc)}
            for z, c, lc in zip(points, normal.cdf(points), normal.log_cdf(points), strict=True)
        ],
        "inverseCdf": [
            {"p": float(p), "z": float(z)}
            for p, z in zip(probabilities, normal.inverse_cdf(probabilities), strict=True)
        ],
    }
    text = json.dumps(document, indent=2, allow_nan=False) + "\n"
    (directory / "cases.json").write_text(text, encoding="utf-8", newline="\n")


def parity_sets() -> dict[str, TrainingData]:
    return {
        "bernoulli-training": bernoulli("Golden", games=40, rows_per_game=50, seed=11),
        "categorical-training": categorical("Golden", games=40, rows_per_game=50, seed=13),
        "johnson-su-training": johnson_su("Golden", games=40, rows_per_game=50, seed=17),
    }


def parity_uniforms(rows: int) -> torch.Tensor:
    return torch.tensor(np.random.default_rng(12).random(rows), dtype=torch.float64)


def write_parity(directory: Path, data: TrainingData) -> None:
    write_training_data(directory, data)
    fitted, report = train(data)
    write_training_result(directory, fitted, report)
    uniforms = parity_uniforms(data.manifest.examples)
    samples = Model.from_fitted(fitted, data.manifest.features).sample(data.examples.features, uniforms)
    lines = ["uniform,sample", *(f"{float(u)!r},{int(s)}" for u, s in zip(uniforms, samples, strict=True))]
    (directory / "samples.csv").write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")


if __name__ == "__main__":
    write_normal(GOLDEN / "normal")
    for name, (fitted, feature_names, cases) in CASES.items():
        write_cases(GOLDEN / name, fitted, feature_names, cases)
    for name, data in parity_sets().items():
        write_parity(GOLDEN / name, data)
