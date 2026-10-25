from pathlib import Path

import golden
import pytest
import torch

from gamegraph.files.contract_files import read_fitted_model, read_training_report
from gamegraph.files.training_data_files import read_training_data, write_training_data
from gamegraph.models.model import Model

SETS = golden.parity_sets()


@pytest.mark.parametrize("name", SETS.keys())
def test_the_parity_training_data_is_current(tmp_path: Path, name: str) -> None:
    write_training_data(tmp_path, SETS[name])
    for file in ("manifest.json", "examples.csv", "games.csv"):
        assert (tmp_path / file).read_bytes() == (golden.TESTDATA / name / file).read_bytes()


@pytest.mark.parametrize("name", SETS.keys())
def test_the_parity_model_reproduces_its_report(name: str) -> None:
    data = read_training_data(golden.TESTDATA / name)
    report = read_training_report(golden.TESTDATA / name / "Golden.training.json")
    model = Model.from_fitted(read_fitted_model(golden.TESTDATA / name / "Golden.json"), data.manifest.features)
    split = data.split(report.training_games)
    with torch.no_grad():
        training = float(-model.log_prob(split.training.features, split.training.outcomes).mean())
        validation = float(-model.log_prob(split.validation.features, split.validation.outcomes).mean())
    likelihood = report.chosen_attempt().negative_log_likelihood
    assert training == pytest.approx(likelihood.training, rel=1e-12)
    assert validation == pytest.approx(likelihood.validation, rel=1e-12)


@pytest.mark.parametrize("name", SETS.keys())
def test_the_parity_samples_are_the_model_s_samples(name: str) -> None:
    data = read_training_data(golden.TESTDATA / name)
    model = Model.from_fitted(read_fitted_model(golden.TESTDATA / name / "Golden.json"), data.manifest.features)
    lines = (golden.TESTDATA / name / "samples.csv").read_text(encoding="utf-8").splitlines()
    assert lines[0] == "uniform,sample"
    uniforms = torch.tensor([float(line.split(",")[0]) for line in lines[1:]], dtype=torch.float64)
    expected = torch.tensor([int(line.split(",")[1]) for line in lines[1:]])
    assert torch.equal(uniforms, golden.parity_uniforms(data.manifest.examples))
    assert torch.equal(model.sample(data.examples.features, uniforms), expected)
