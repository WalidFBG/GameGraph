import json
from dataclasses import replace
from math import log, nextafter
from pathlib import Path

import numpy as np
import torch
from synthetic import START, bernoulli, categorical, choice, johnson_su

from gamegraph.contract import (
    BernoulliDistribution,
    CategoricalDistribution,
    ChoiceDistribution,
    DiscreteJohnsonSuDistribution,
    FittedModel,
    Json,
    LinearFit,
    LinearSplineFit,
    LinearSplineTerm,
    LinearTerm,
    ModelDeclaration,
    PredictorFit,
)
from gamegraph.files.contract_documents import write_fitted_model, write_training_result
from gamegraph.files.paths import REPOSITORY
from gamegraph.files.training_data_directory import write_training_data
from gamegraph.models import normal
from gamegraph.models.model import Model
from gamegraph.training.training import train
from gamegraph.training_tensors import TrainingTensors

TESTDATA = REPOSITORY / "testdata"

type Case = tuple[tuple[float, ...] | tuple[tuple[float, ...], ...], int, float]

BERNOULLI = FittedModel(
    "Golden",
    ModelDeclaration(
        BernoulliDistribution(),
        {"probability": (LinearTerm("Gaussian"), LinearTerm("Indicator"), LinearSplineTerm("Uniform", 4))},
        {},
    ),
    {
        "probability": PredictorFit(
            -0.12,
            {
                "Gaussian": LinearFit(0.31, 8.4, (0.021,)),
                "Indicator": LinearFit(0.38, 0.49, (-0.18,)),
                "Uniform": LinearSplineFit(
                    (0.0, 8.0, 16.0, 24.0), (4.1, 3.9, 2.2), (3.0, 3.5, 3.1), (0.12, -0.05, 0.2)
                ),
            },
        )
    },
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
    ModelDeclaration(
        CategoricalDistribution(("first", "second", "third")),
        {"second": (LinearTerm("Gaussian"), LinearSplineTerm("Uniform", 3)), "third": (LinearTerm("Gaussian"),)},
        {},
    ),
    {
        "second": PredictorFit(
            0.2,
            {
                "Gaussian": LinearFit(1.5, 4.0, (0.3,)),
                "Uniform": LinearSplineFit((0.0, 12.0, 24.0), (5.0, 4.0), (4.5, 4.2), (0.25, -0.4)),
            },
        ),
        "third": PredictorFit(-0.8, {"Gaussian": LinearFit(1.5, 4.0, (-0.2,))}),
    },
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
    ModelDeclaration(
        DiscreteJohnsonSuDistribution(),
        {"location": (LinearTerm("Gaussian"),), "scale": (LinearTerm("Gaussian"),), "skew": (), "tail": ()},
        {"maximum": "Bound"},
    ),
    {
        "location": PredictorFit(7.0, {"Gaussian": LinearFit(0.1, 1.2, (2.5,))}),
        "scale": PredictorFit(1.4, {"Gaussian": LinearFit(0.1, 1.2, (0.15,))}),
        "skew": PredictorFit(0.5, {}),
        "tail": PredictorFit(0.3, {}),
    },
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

JOHNSON_SU_NARROW = FittedModel(
    "Golden",
    ModelDeclaration(
        DiscreteJohnsonSuDistribution(),
        {
            "location": (LinearTerm("Location"),),
            "scale": (LinearTerm("LogScale"),),
            "skew": (LinearTerm("Skew"),),
            "tail": (LinearTerm("LogTail"),),
        },
        {"maximum": "Bound"},
    ),
    {
        name: PredictorFit(0.0, {feature: LinearFit(0.0, 1.0, (1.0,))})
        for name, feature in (("location", "Location"), ("scale", "LogScale"), ("skew", "Skew"), ("tail", "LogTail"))
    },
)

UNIFORM_LIMIT = (0.0, log(5e6), 14.2, log(3e-9), 28.0)
BELOW_THE_NARROW_OUTCOME = (6.0, log(4.0), 0.5, log(4.6e-3), 24.0)
ABOVE_THE_NARROW_OUTCOME = (6.0, log(4.0), 0.5, log(4.8e-3), 24.0)

JOHNSON_SU_NARROW_CASES: list[Case] = [
    (UNIFORM_LIMIT, 0, 0.01),
    (UNIFORM_LIMIT, 13, 0.5),
    (UNIFORM_LIMIT, 28, 0.99),
    (BELOW_THE_NARROW_OUTCOME, 3, 0.3),
    (ABOVE_THE_NARROW_OUTCOME, 3, 0.3),
]

CHOICE_FEATURES = ("Share", "Rest", "Size")
CHOICE = FittedModel(
    "Golden",
    ModelDeclaration(
        ChoiceDistribution(), {"utility": (LinearTerm("Share"), LinearSplineTerm("Rest", 3))}, {"size": "Size"}
    ),
    {
        "utility": PredictorFit(
            0.0,
            {
                "Share": LinearFit(-1.6, 0.7, (0.9,)),
                "Rest": LinearSplineFit((0.0, 6.0, 12.0), (2.5, 1.0), (2.2, 1.6), (-0.3, 0.15)),
            },
        )
    },
)

FIVE = ((-1.2, 0.0), (-1.6, 3.0), (-2.3, 7.0), (-1.0, 12.0), (-3.0, 1.0))


def choosing(size: int, candidates: tuple[tuple[float, float], ...]) -> tuple[tuple[float, ...], ...]:
    return tuple((*candidate, float(size)) for candidate in candidates)


CHOICE_CASES: list[Case] = [
    (choosing(1, FIVE), 0b00001, 0.1),
    (choosing(1, FIVE), 0b01000, 0.9),
    (choosing(2, ((-1.6, 2.0),) * 5), 0b00101, 0.55),
    (choosing(2, ((0.0, 20.0), (-9.0, -4.0), (-1.4, 6.0), (-0.7, 9.0))), 0b1001, 0.97),
    (choosing(1, ((40.0, 0.0), (-40.0, 0.0), (-1.4, 6.0), (-0.7, 9.0), (-2.0, 0.0))), 0b10000, 0.3),
    (choosing(3, (*FIVE, (-0.5, 4.0))), 0b110010, 0.42),
    (choosing(3, FIVE[:3]), 0b111, 0.6),
]

CASES: dict[str, tuple[FittedModel, tuple[str, ...], list[Case]]] = {
    "bernoulli": (BERNOULLI, ("Gaussian", "Indicator", "Uniform"), BERNOULLI_CASES),
    "categorical": (CATEGORICAL, ("Gaussian", "Uniform"), CATEGORICAL_CASES),
    "johnson-su": (JOHNSON_SU, ("Gaussian", "Bound"), JOHNSON_SU_CASES),
    "johnson-su-narrow": (
        JOHNSON_SU_NARROW,
        ("Location", "LogScale", "Skew", "LogTail", "Bound"),
        JOHNSON_SU_NARROW_CASES,
    ),
    "choice": (CHOICE, CHOICE_FEATURES, CHOICE_CASES),
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
    model = Model.from_fitted(fitted, feature_names)
    document = {"features": list(feature_names), "cases": [_case(model, *case) for case in cases]}
    text = json.dumps(document, indent=2, allow_nan=False) + "\n"
    (directory / "cases.json").write_text(text, encoding="utf-8", newline="\n")


def _case(
    model: Model, values: tuple[float, ...] | tuple[tuple[float, ...], ...], outcome: int, uniform: float
) -> Json:
    features = torch.tensor([values], dtype=torch.float64)
    outcomes = torch.tensor([outcome])
    sample = model.sample(features, torch.tensor([uniform], dtype=torch.float64))
    for nudged in (nextafter(uniform, 1), max(nextafter(uniform, 0), 0.0)):
        if not torch.equal(model.sample(features, torch.tensor([nudged], dtype=torch.float64)), sample):
            raise ValueError("A golden uniform sits on a category boundary, so its sample depends on rounding")
    return {
        "features": [float(value) for value in features[0].flatten()],
        "outcome": outcome,
        "parameters": [float(p) for p in torch.stack(model.parameter_values(features), dim=-1)[0].flatten()],
        "logProbability": float(model.log_prob(features, outcomes)[0]),
        "uniform": uniform,
        "sample": int(sample[0]),
    }


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


def parity_sets() -> dict[str, TrainingTensors]:
    return {
        "bernoulli-training": bernoulli("Golden", games=40, rows_per_game=50, seed=11),
        "categorical-training": categorical("Golden", games=40, rows_per_game=50, seed=13),
        "johnson-su-training": johnson_su("Golden", games=40, rows_per_game=50, seed=17),
        "choice-training": choice("Golden", games=40, decisions_per_game=50, seed=19),
    }


def parity_uniforms(rows: int) -> torch.Tensor:
    return torch.tensor(np.random.default_rng(12).random(rows), dtype=torch.float64)


def write_parity(directory: Path, data: TrainingTensors) -> None:
    write_training_data(directory, data)
    fitted, report = train(data)
    write_training_result(directory, fitted, replace(report, created_utc=START))
    uniforms = parity_uniforms(data.manifest.examples)
    samples = Model.from_fitted(fitted, data.manifest.features).sample(data.examples.features, uniforms)
    lines = ["uniform,sample", *(f"{float(u)!r},{int(s)}" for u, s in zip(uniforms, samples, strict=True))]
    (directory / "samples.csv").write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")


if __name__ == "__main__":
    write_normal(TESTDATA / "normal")
    for name, (fitted, feature_names, cases) in CASES.items():
        write_cases(TESTDATA / name, fitted, feature_names, cases)
    for name, data in parity_sets().items():
        write_parity(TESTDATA / name, data)
