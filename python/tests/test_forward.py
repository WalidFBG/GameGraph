import math
from pathlib import Path

import pytest
import torch

from gamegraph.models import golden, links
from gamegraph.models.contract import FittedLinearTerm, Link
from gamegraph.models.distributions import Bernoulli
from gamegraph.models.model import Model
from gamegraph.models.paths import GOLDEN
from gamegraph.models.terms import LinearColumns


def tensor(values: object) -> torch.Tensor:
    return torch.tensor(values, dtype=torch.float64)


def assert_close(actual: torch.Tensor, expected: object) -> None:
    torch.testing.assert_close(actual, torch.tensor(expected, dtype=actual.dtype), rtol=1e-15, atol=0)


def test_a_linear_term_centres_and_scales_its_feature() -> None:
    term = LinearColumns.from_fitted(FittedLinearTerm("b", 1.0, 2.0, (0.5,)), ("a", "b"))
    assert_close(term.columns(tensor([[9.0, 5.0], [9.0, -1.0]])), [[2.0], [-1.0]])


def test_a_linear_term_whose_feature_is_missing_fails() -> None:
    with pytest.raises(ValueError, match="No feature c"):
        LinearColumns.from_fitted(FittedLinearTerm("c", 0.0, 1.0, (0.5,)), ("a", "b"))


def test_the_inverse_logit_is_the_sigmoid() -> None:
    assert_close(links.inverse(Link.LOGIT, tensor([0.0, math.log(3)])), [0.5, 0.75])


def test_bernoulli_gives_the_log_probability_of_each_outcome() -> None:
    log_prob = Bernoulli.log_prob((tensor([0.25, 0.25]),), torch.tensor([1, 0]))
    assert_close(log_prob, [math.log(0.25), math.log(0.75)])


def test_bernoulli_samples_one_below_the_probability() -> None:
    samples = Bernoulli.sample((tensor([0.25, 0.25, 0.25]),), tensor([0.0, 0.2499, 0.25]))
    assert torch.equal(samples, torch.tensor([1, 1, 0]))


def test_bernoulli_rejects_an_outcome_that_is_not_0_or_1() -> None:
    with pytest.raises(ValueError, match="0 or 1"):
        Bernoulli.log_prob((tensor([0.5]),), torch.tensor([2]))


def test_bernoulli_rejects_a_uniform_outside_0_to_1() -> None:
    with pytest.raises(ValueError, match=r"\[0, 1\)"):
        Bernoulli.sample((tensor([0.5]),), tensor([1.0]))


def test_the_model_combines_terms_link_and_distribution() -> None:
    model = Model.from_fitted(golden.BERNOULLI, golden.BERNOULLI_FEATURES)
    rows = [[4.0, 1.0], [-2.0, 0.0]]
    expected = [
        1 / (1 + math.exp(-(-0.12 + 0.021 * (margin - 0.31) / 8.4 - 0.18 * (three - 0.38) / 0.49)))
        for margin, three in rows
    ]
    (probability,) = model.parameter_values(tensor(rows))
    assert_close(probability, expected)
    assert_close(model.log_prob(tensor(rows), torch.tensor([1, 0])), [math.log(expected[0]), math.log(1 - expected[1])])


def test_the_model_rejects_features_that_are_not_float64() -> None:
    model = Model.from_fitted(golden.BERNOULLI, golden.BERNOULLI_FEATURES)
    with pytest.raises(ValueError, match="float64"):
        model.parameter_values(torch.zeros((1, 2), dtype=torch.float32))


def test_the_golden_files_are_current(tmp_path: Path) -> None:
    golden.write(tmp_path)
    for name in ("fitted-model.json", "cases.json"):
        assert (tmp_path / name).read_text(encoding="utf-8") == (GOLDEN / "bernoulli" / name).read_text(
            encoding="utf-8"
        )
