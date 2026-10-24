import json
from pathlib import Path

import numpy as np
import torch

from gamegraph.models.contract import Distribution, FittedLinearTerm, FittedModel, FittedPredictedParameter, Link
from gamegraph.models.contract_files import write_fitted_model
from gamegraph.models.model import Model
from gamegraph.models.paths import GOLDEN
from gamegraph.models.synthetic import bernoulli
from gamegraph.models.training import train, write_training_result
from gamegraph.models.training_data import TrainingData, write_training_data

BERNOULLI_FEATURES = ("ScoreMargin", "ShotIsThree")

BERNOULLI = FittedModel(
    "Golden",
    Distribution.BERNOULLI,
    {
        "probability": FittedPredictedParameter(
            Link.LOGIT,
            -0.12,
            (
                FittedLinearTerm("ScoreMargin", 0.31, 8.4, (0.021,)),
                FittedLinearTerm("ShotIsThree", 0.38, 0.49, (-0.18,)),
            ),
        )
    },
)

BERNOULLI_CASES = [
    (0.0, 0.0, 1, 0.1),
    (0.0, 0.0, 0, 0.9),
    (0.0, 1.0, 1, 0.45),
    (0.0, 1.0, 0, 0.47),
    (-7.0, 1.0, 0, 0.05),
    (12.0, 0.0, 1, 0.999),
    (25.0, 1.0, 1, 0.5),
    (-25.0, 0.0, 0, 0.5),
    (3.5, 0.0, 1, 0.0),
    (-1.25, 1.0, 0, 0.75),
    (12000.0, 0.0, 1, 0.999999),
    (-12000.0, 1.0, 0, 0.000001),
]


def write(directory: Path) -> None:
    directory.mkdir(parents=True, exist_ok=True)
    write_fitted_model(directory / "fitted-model.json", BERNOULLI)
    features = torch.tensor([[margin, three] for margin, three, _, _ in BERNOULLI_CASES], dtype=torch.float64)
    outcomes = torch.tensor([outcome for _, _, outcome, _ in BERNOULLI_CASES])
    uniforms = torch.tensor([uniform for _, _, _, uniform in BERNOULLI_CASES], dtype=torch.float64)
    model = Model.from_fitted(BERNOULLI, BERNOULLI_FEATURES)
    (probability,) = model.parameter_values(features)
    log_probability = model.log_prob(features, outcomes)
    samples = model.sample(features, uniforms)
    if (uniforms == probability).any():
        raise ValueError("A golden uniform equals its probability, so its sample depends on rounding")
    cases: list[dict[str, object]] = [
        {
            "features": [float(value) for value in features[i]],
            "outcome": int(outcomes[i]),
            "probability": float(probability[i]),
            "logProbability": float(log_probability[i]),
            "uniform": float(uniforms[i]),
            "sample": int(samples[i]),
        }
        for i in range(len(BERNOULLI_CASES))
    ]
    document = {"features": list(BERNOULLI_FEATURES), "cases": cases}
    text = json.dumps(document, indent=2, allow_nan=False) + "\n"
    (directory / "cases.json").write_text(text, encoding="utf-8", newline="\n")


def parity_data() -> TrainingData:
    return bernoulli("Golden", games=40, rows_per_game=50, seed=11)


def parity_uniforms(rows: int) -> torch.Tensor:
    return torch.tensor(np.random.default_rng(12).random(rows), dtype=torch.float64)


def write_parity(directory: Path) -> None:
    data = parity_data()
    write_training_data(directory, data)
    fitted, report = train(data)
    write_training_result(directory, fitted, report)
    uniforms = parity_uniforms(data.manifest.examples)
    samples = Model.from_fitted(fitted, data.manifest.features).sample(data.examples.features, uniforms)
    lines = ["uniform,sample", *(f"{float(u)!r},{int(s)}" for u, s in zip(uniforms, samples, strict=True))]
    (directory / "samples.csv").write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")


if __name__ == "__main__":
    write(GOLDEN / "bernoulli")
    write_parity(GOLDEN / "bernoulli-training")
