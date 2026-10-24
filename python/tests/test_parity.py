from pathlib import Path

import pytest
import torch

from gamegraph.models import golden
from gamegraph.models.contract_files import read_fitted_model, read_training_report
from gamegraph.models.model import Model
from gamegraph.models.paths import GOLDEN
from gamegraph.models.training_data import read_training_data, write_training_data

PARITY = GOLDEN / "bernoulli-training"


def test_the_parity_training_data_is_current(tmp_path: Path) -> None:
    write_training_data(tmp_path, golden.parity_data())
    for name in ("manifest.json", "examples.csv", "games.csv"):
        assert (tmp_path / name).read_bytes() == (PARITY / name).read_bytes()


def test_the_parity_model_reproduces_its_report() -> None:
    data = read_training_data(PARITY)
    report = read_training_report(PARITY / "Golden.training.json")
    model = Model.from_fitted(read_fitted_model(PARITY / "Golden.json"), data.manifest.features)
    split = data.split(report.training_games)
    with torch.no_grad():
        training = float(-model.log_prob(split.training.features, split.training.outcomes).mean())
        validation = float(-model.log_prob(split.validation.features, split.validation.outcomes).mean())
    assert training == pytest.approx(report.negative_log_likelihood.training, rel=1e-12)
    assert validation == pytest.approx(report.negative_log_likelihood.validation, rel=1e-12)


def test_the_parity_samples_are_the_model_s_samples() -> None:
    data = read_training_data(PARITY)
    model = Model.from_fitted(read_fitted_model(PARITY / "Golden.json"), data.manifest.features)
    lines = (PARITY / "samples.csv").read_text(encoding="utf-8").splitlines()
    assert lines[0] == "uniform,sample"
    uniforms = torch.tensor([float(line.split(",")[0]) for line in lines[1:]], dtype=torch.float64)
    expected = torch.tensor([int(line.split(",")[1]) for line in lines[1:]])
    assert torch.equal(uniforms, golden.parity_uniforms(data.manifest.examples))
    assert torch.equal(model.sample(data.examples.features, uniforms), expected)
