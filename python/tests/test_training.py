from pathlib import Path

import numpy as np
import pytest
import torch

from gamegraph.models.contract_files import read_fitted_model, read_training_report
from gamegraph.models.distributions import DiscreteJohnsonSu
from gamegraph.models.model import Model
from gamegraph.models.synthetic import (
    BERNOULLI_FEATURES,
    CATEGORICAL_FEATURES,
    bernoulli,
    bernoulli_logit,
    categorical,
    categorical_log_odds,
    johnson_su,
    johnson_su_parameters,
)
from gamegraph.models.training import train, write_training_result
from gamegraph.models.training_data import Examples, TrainingData, read_training_data, write_training_data


def synthetic(games: int = 1000) -> TrainingData:
    return bernoulli("Synthetic", games, rows_per_game=200, seed=7)


def test_training_recovers_a_known_shape(tmp_path: Path) -> None:
    write_training_data(tmp_path / "data", synthetic())
    fitted, report = train(read_training_data(tmp_path / "data"))
    grid = np.array([(m, t, c) for m in (0.0, 3.0, 6.0) for t in (0.0, 1.0) for c in (2.0, 9.0, 15.0, 22.0)])
    model = Model.from_fitted(fitted, BERNOULLI_FEATURES)
    (probability,) = model.parameter_values(torch.tensor(grid, dtype=torch.float64))
    fitted_logit = torch.logit(probability).numpy()
    assert np.abs(fitted_logit - bernoulli_logit(grid[:, 0], grid[:, 1], grid[:, 2])).max() < 0.05
    assert report.training_games == 800
    assert (report.examples.training, report.examples.validation) == (160_000, 40_000)
    assert 0 < report.negative_log_likelihood.training < np.log(2)
    assert report.optimiser.iterations > 0


def test_training_recovers_known_log_odds() -> None:
    fitted, report = train(categorical("Synthetic", games=1000, rows_per_game=200, seed=9))
    grid = np.array([(m, c) for m in (0.0, 3.0, 6.0) for c in (2.0, 9.0, 15.0, 22.0)])
    model = Model.from_fitted(fitted, CATEGORICAL_FEATURES)
    second, third = model.parameter_values(torch.tensor(grid, dtype=torch.float64))
    expected = categorical_log_odds(grid[:, 0], grid[:, 1])
    assert np.abs(second.numpy() - expected[:, 0]).max() < 0.08
    assert np.abs(third.numpy() - expected[:, 1]).max() < 0.08
    assert report.training_games == 800


def test_training_recovers_a_known_discrete_johnson_su() -> None:
    fitted, _ = train(johnson_su("Synthetic", games=1000, rows_per_game=200, seed=19))
    model = Model.from_fitted(fitted, ("Gaussian", "Bound"))
    times = torch.arange(25)
    for pace in (-1.0, 0.0, 1.0):
        features = torch.tensor([[pace, 24.0]] * 25, dtype=torch.float64)
        fitted_probabilities = torch.exp(model.log_prob(features, times))
        true_parameters = johnson_su_parameters(features[:, 0], features[:, 1])
        true_probabilities = torch.exp(DiscreteJohnsonSu().log_prob(true_parameters, times))
        assert float((fitted_probabilities - true_probabilities).abs().max()) < 0.005


def test_a_training_result_is_written_as_contract_files(tmp_path: Path) -> None:
    fitted, report = train(synthetic(games=50))
    write_training_result(tmp_path, fitted, report)
    assert read_fitted_model(tmp_path / "Synthetic.json") == fitted
    assert read_training_report(tmp_path / "Synthetic.training.json") == report


def test_training_data_round_trips(tmp_path: Path) -> None:
    data = synthetic(games=3)
    write_training_data(tmp_path, data)
    read = read_training_data(tmp_path)
    assert read.manifest == data.manifest
    assert read.games == data.games
    assert torch.equal(read.examples.features, data.examples.features)
    assert torch.equal(read.examples.outcomes, data.examples.outcomes)


def test_reading_examples_with_the_wrong_header_fails(tmp_path: Path) -> None:
    write_training_data(tmp_path, synthetic(games=3))
    examples = tmp_path / "examples.csv"
    examples.write_text(
        examples.read_text(encoding="utf-8").replace("Gaussian,Indicator", "Indicator,Gaussian", 1), encoding="utf-8"
    )
    with pytest.raises(ValueError, match="header"):
        read_training_data(tmp_path)


def test_reading_fewer_examples_than_the_manifest_says_fails(tmp_path: Path) -> None:
    write_training_data(tmp_path, synthetic(games=3))
    examples = tmp_path / "examples.csv"
    lines = examples.read_text(encoding="utf-8").splitlines(keepends=True)
    examples.write_text("".join(lines[:-1]), encoding="utf-8")
    with pytest.raises(ValueError, match="the manifest says"):
        read_training_data(tmp_path)


def test_games_out_of_start_time_order_fail() -> None:
    data = synthetic(games=3)
    with pytest.raises(ValueError, match="start-time order"):
        TrainingData(data.manifest, tuple(reversed(data.games)), data.examples)


def test_a_feature_constant_in_training_fails() -> None:
    data = synthetic(games=10)
    features = data.examples.features.clone()
    features[:, 1] = 1.0
    with pytest.raises(ValueError, match="Indicator is constant"):
        train(TrainingData(data.manifest, data.games, Examples(features, data.examples.outcomes)))


def test_outcomes_that_are_all_the_same_fail() -> None:
    data = synthetic(games=10)
    outcomes = torch.ones_like(data.examples.outcomes)
    with pytest.raises(ValueError, match="nothing to fit"):
        train(TrainingData(data.manifest, data.games, Examples(data.examples.features, outcomes)))
