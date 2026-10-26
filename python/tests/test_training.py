from pathlib import Path
from typing import Any

import numpy as np
import pytest
import torch
from synthetic import (
    BERNOULLI_FEATURES,
    CATEGORICAL_FEATURES,
    CHOICE_FEATURES,
    bernoulli,
    bernoulli_logit,
    categorical,
    categorical_log_odds,
    choice,
    choice_utility,
    intercept_only,
    johnson_su,
    johnson_su_parameters,
)

from gamegraph.contract import Attempt, Route, TrainingAndValidation
from gamegraph.files.contract_documents import read_fitted_model, read_training_report, write_training_result
from gamegraph.files.training_data_directory import read_training_data, write_training_data
from gamegraph.models.distributions import DiscreteJohnsonSu
from gamegraph.models.model import Model
from gamegraph.training import training
from gamegraph.training.training import (
    RIDGE_PATH,
    NotConverged,
    choose,
    require_converged,
    train,
)
from gamegraph.training_tensors import Examples, TrainingTensors


def synthetic(games: int = 1000) -> TrainingTensors:
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
    assert 0 < report.chosen_attempt().negative_log_likelihood.training < np.log(2)


def test_training_recovers_known_log_odds() -> None:
    fitted, report = train(categorical("Synthetic", games=1000, rows_per_game=200, seed=9))
    grid = np.array([(m, c) for m in (0.0, 3.0, 6.0) for c in (2.0, 9.0, 15.0, 22.0)])
    model = Model.from_fitted(fitted, CATEGORICAL_FEATURES)
    second, third = model.parameter_values(torch.tensor(grid, dtype=torch.float64))
    expected = categorical_log_odds(grid[:, 0], grid[:, 1])
    assert np.abs(second.numpy() - expected[:, 0]).max() < 0.08
    assert np.abs(third.numpy() - expected[:, 1]).max() < 0.08
    assert report.training_games == 800


def test_training_recovers_known_utilities_up_to_a_constant(tmp_path: Path) -> None:
    write_training_data(tmp_path / "data", choice("Synthetic", games=1000, decisions_per_game=100, seed=5))
    fitted, report = train(read_training_data(tmp_path / "data"))
    share = np.log(np.array([[0.05, 0.1, 0.2, 0.3, 0.35], [0.02, 0.08, 0.15, 0.25, 0.5]]))
    indicator = np.array([[0.0, 1.0, 0.0, 1.0, 0.0], [1.0, 0.0, 0.0, 0.0, 1.0]])
    size = np.ones_like(share)
    model = Model.from_fitted(fitted, CHOICE_FEATURES)
    features = torch.tensor(np.stack([share, indicator, size], axis=2), dtype=torch.float64)
    utility, _ = model.parameter_values(features)
    expected = choice_utility(share, indicator)
    difference = utility.numpy() - expected
    assert np.abs(difference - difference.mean(axis=1, keepdims=True)).max() < 0.08
    assert "intercept" not in fitted.to_json()["fit"]["utility"]
    assert (report.examples.training, report.examples.validation) == (80_000, 20_000)


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


def test_a_model_without_terms_trains_to_the_rate_it_saw(tmp_path: Path) -> None:
    write_training_data(tmp_path / "data", intercept_only("Tip", games=100, rows_per_game=20, seed=5, probability=0.3))
    data = read_training_data(tmp_path / "data")
    fitted, _ = train(data)
    seen = float(data.split(80).training.outcomes.double().mean())
    rate = float(torch.sigmoid(torch.tensor(fitted.fit["probability"].intercept)))
    assert fitted.fit["probability"].terms == {}
    assert rate == pytest.approx(seen, abs=training.CONVERGED_GRADIENT)


def test_the_starting_model_has_not_converged() -> None:
    data = synthetic(games=50)
    model = Model.initial(data.manifest.model, data.manifest.features, data.examples.features, data.examples.outcomes)
    with pytest.raises(RuntimeError, match="Synthetic did not converge at ridge 0: its largest gradient is"):
        require_converged("Synthetic", model, model.designs(data.examples.features), data.examples.outcomes, 0.0)


def test_an_attempt_whose_loss_is_not_finite_fails_at_once(monkeypatch: pytest.MonkeyPatch) -> None:
    data = synthetic(games=50)
    model = Model.initial(data.manifest.model, data.manifest.features, data.examples.features, data.examples.outcomes)

    def not_a_number(*_: object) -> torch.Tensor:
        return torch.tensor(float("nan"), requires_grad=True)

    monkeypatch.setattr(training, "_loss", not_a_number)
    with pytest.raises(NotConverged, match="Synthetic diverged at ridge 0: its loss is nan"):
        training.fit("Synthetic", model, model.designs(data.examples.features), data.examples.outcomes, 0.0)


def test_a_model_whose_gradient_is_not_a_number_has_not_converged() -> None:
    data = synthetic(games=50)
    model = Model.initial(data.manifest.model, data.manifest.features, data.examples.features, data.examples.outcomes)
    with torch.no_grad():
        model.coefficients()[1][0] = float("nan")
    with pytest.raises(RuntimeError, match="Synthetic did not converge at ridge 0: its largest gradient is nan"):
        require_converged("Synthetic", model, model.designs(data.examples.features), data.examples.outcomes, 0.0)


def test_training_fits_directly_and_along_the_ridge_path_and_chooses_among_the_attempts() -> None:
    fitted, report = train(synthetic(games=50))
    assert [(a.route, a.ridge) for a in report.attempts] == [
        (Route.DIRECT, 0.0),
        *((Route.RIDGE_PATH, ridge) for ridge in RIDGE_PATH),
    ]
    assert report.chosen == choose(report.attempts)
    model = Model.from_fitted(fitted, synthetic(games=50).manifest.features)
    training = synthetic(games=50).split(report.training_games).training
    likelihood = -model.log_prob(training.features, training.outcomes).mean()
    assert float(likelihood) == pytest.approx(report.chosen_attempt().negative_log_likelihood.training, rel=1e-12)


def attempt(route: Route, ridge: float, training: float, validation: float) -> Attempt:
    return Attempt(route, ridge, TrainingAndValidation[float](training, validation), 1)


def test_at_one_ridge_the_better_optimum_on_training_wins_even_if_validation_prefers_the_other() -> None:
    attempts = [attempt(Route.DIRECT, 0.0, 2.70, 2.80), attempt(Route.RIDGE_PATH, 0.0, 2.75, 2.75)]
    assert choose(attempts) == 0


def test_across_ridges_validation_chooses() -> None:
    attempts = [
        attempt(Route.DIRECT, 0.0, 2.70, 2.80),
        attempt(Route.RIDGE_PATH, 1e-2, 2.74, 2.76),
        attempt(Route.RIDGE_PATH, 0.0, 2.75, 2.75),
    ]
    assert choose(attempts) == 1


def test_an_attempt_that_does_not_converge_is_reported_and_its_route_stopped(monkeypatch: pytest.MonkeyPatch) -> None:
    original = training.fit
    calls: list[None] = []

    def failing_at_the_third_attempt(*arguments: Any) -> int:
        calls.append(None)
        if len(calls) == 3:
            raise NotConverged("stalled")
        return original(*arguments)

    monkeypatch.setattr(training, "fit", failing_at_the_third_attempt)
    _, report = train(synthetic(games=50))
    assert [(a.route, a.ridge) for a in report.attempts] == [(Route.DIRECT, 0.0), (Route.RIDGE_PATH, RIDGE_PATH[0])]
    assert [(f.route, f.ridge, f.reason) for f in report.failures] == [(Route.RIDGE_PATH, RIDGE_PATH[1], "stalled")]


def test_training_fails_when_every_attempt_fails(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(training, "MAX_ITERATIONS", 1)
    with pytest.raises(
        NotConverged, match="Every attempt to fit Synthetic failed: Synthetic did not converge at ridge 0"
    ):
        train(synthetic(games=50))


def test_the_fitted_model_carries_its_manifest_s_declaration() -> None:
    data = synthetic(games=50)
    fitted, _ = train(data)
    assert fitted.model == data.manifest.model


def test_a_training_result_is_written_as_contract_documents(tmp_path: Path) -> None:
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


def test_choice_training_data_round_trips_one_row_per_candidate(tmp_path: Path) -> None:
    data = choice("Synthetic", games=3, decisions_per_game=4, seed=1)
    write_training_data(tmp_path, data)
    lines = (tmp_path / "examples.csv").read_text(encoding="utf-8").splitlines()
    assert lines[0] == "decision,Share,Indicator,Size,outcome"
    assert len(lines) == 1 + int((data.examples.features[:, :, 2] > 0).sum())
    assert sum(line.endswith(",1") for line in lines[1:]) == int(data.examples.features[:, 0, 2].sum())
    read = read_training_data(tmp_path)
    assert read.manifest == data.manifest
    assert torch.equal(read.examples.features, data.examples.features)
    assert torch.equal(read.examples.outcomes, data.examples.outcomes)


def test_reading_a_decision_that_chooses_more_candidates_than_its_size_fails(tmp_path: Path) -> None:
    write_training_data(tmp_path, choice("Synthetic", games=3, decisions_per_game=4, seed=1))
    examples = tmp_path / "examples.csv"
    lines = examples.read_text(encoding="utf-8").splitlines()
    first = [line.split(",") for line in lines[1:] if line.startswith("0,")]
    for row in first:
        row[-1] = "1"
    rest = [line for line in lines[1:] if not line.startswith("0,")]
    examples.write_text("\n".join([lines[0], *(",".join(r) for r in first), *rest]) + "\n", encoding="utf-8")
    with pytest.raises(ValueError, match="as many candidates as its size"):
        read_training_data(tmp_path)


def test_reading_decisions_out_of_order_fails(tmp_path: Path) -> None:
    write_training_data(tmp_path, choice("Synthetic", games=3, decisions_per_game=4, seed=1))
    examples = tmp_path / "examples.csv"
    lines = examples.read_text(encoding="utf-8").splitlines()
    examples.write_text("\n".join([lines[0], *lines[1:][::-1]]) + "\n", encoding="utf-8")
    with pytest.raises(ValueError, match="from 0 in order"):
        read_training_data(tmp_path)


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
        TrainingTensors(data.manifest, tuple(reversed(data.games)), data.examples)


def test_a_feature_constant_in_training_fails() -> None:
    data = synthetic(games=10)
    features = data.examples.features.clone()
    features[:, 1] = 1.0
    with pytest.raises(ValueError, match="Indicator is constant"):
        train(TrainingTensors(data.manifest, data.games, Examples(features, data.examples.outcomes)))


def test_outcomes_that_are_all_the_same_fail() -> None:
    data = synthetic(games=10)
    outcomes = torch.ones_like(data.examples.outcomes)
    with pytest.raises(ValueError, match="nothing to fit"):
        train(TrainingTensors(data.manifest, data.games, Examples(data.examples.features, outcomes)))
