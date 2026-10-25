import math
from pathlib import Path

import golden
import pytest
import torch

from gamegraph.models import links
from gamegraph.models.contract import LinearFit, LinearSplineFit, LinearSplineTerm, Link
from gamegraph.models.distributions import Bernoulli, Categorical, DiscreteJohnsonSu
from gamegraph.models.model import Model
from gamegraph.models.terms import LinearColumns, LinearSplineColumns

BERNOULLI_FEATURES = golden.CASES["bernoulli"][1]
CATEGORICAL = Categorical(("a", "b", "c"))


def tensor(values: object) -> torch.Tensor:
    return torch.tensor(values, dtype=torch.float64)


def assert_close(actual: torch.Tensor, expected: object) -> None:
    torch.testing.assert_close(actual, torch.tensor(expected, dtype=actual.dtype), rtol=1e-15, atol=0)


def test_a_linear_term_centres_and_scales_its_feature() -> None:
    term = LinearColumns.from_fit("b", LinearFit(1.0, 2.0, (0.5,)), ("a", "b"))
    assert_close(term.columns(tensor([[9.0, 5.0], [9.0, -1.0]])), [[2.0], [-1.0]])


def test_a_linear_spline_term_measures_each_segment_and_is_flat_beyond_its_end_knots() -> None:
    term = LinearSplineColumns.from_fit(
        "x", LinearSplineFit((0.0, 10.0, 20.0), (0.0, 0.0), (1.0, 1.0), (0.0, 0.0)), ("x",)
    )
    assert_close(
        term.columns(tensor([[-5.0], [0.0], [4.0], [10.0], [15.0], [20.0], [99.0]])),
        [[0.0, 0.0], [0.0, 0.0], [4.0, 0.0], [10.0, 0.0], [10.0, 5.0], [10.0, 10.0], [10.0, 10.0]],
    )


def test_a_linear_spline_places_its_knots_at_quantiles_of_the_distinct_values() -> None:
    values = tensor([[v] for v in [0.0] * 50 + [1.0, 2.0, 3.0] + [24.0] * 47])
    term = LinearSplineColumns.fit(LinearSplineTerm("x", 5), ("x",), values)
    assert term.knot_positions == (0.0, 1.0, 2.0, 3.0, 24.0)
    assert term.count == 4


def test_a_linear_spline_with_fewer_distinct_values_than_knots_merges_repeats() -> None:
    values = tensor([[v] for v in [0.0] * 60 + [1.0] * 40])
    term = LinearSplineColumns.fit(LinearSplineTerm("x", 5), ("x",), values)
    assert term.knot_positions == (0.0, 1.0)
    assert term.count == 1


def test_a_linear_spline_of_a_constant_feature_fails() -> None:
    with pytest.raises(ValueError, match="constant"):
        LinearSplineColumns.fit(LinearSplineTerm("x", 5), ("x",), tensor([[3.0]] * 10))


def test_a_linear_term_whose_feature_is_missing_fails() -> None:
    with pytest.raises(ValueError, match="No feature c"):
        LinearColumns.from_fit("c", LinearFit(0.0, 1.0, (0.5,)), ("a", "b"))


def test_the_inverse_logit_is_the_sigmoid() -> None:
    assert_close(links.inverse(Link.LOGIT, tensor([0.0, math.log(3)])), [0.5, 0.75])


def test_bernoulli_gives_the_log_probability_of_each_outcome() -> None:
    log_prob = Bernoulli().log_prob((tensor([0.25, 0.25]),), torch.tensor([1, 0]))
    assert_close(log_prob, [math.log(0.25), math.log(0.75)])


def test_bernoulli_has_a_finite_gradient_when_a_probability_rounds_to_0_or_1() -> None:
    eta = tensor([40.0, -800.0, 0.0]).requires_grad_()
    log_probability = Bernoulli().log_prob((torch.sigmoid(eta),), torch.tensor([1, 0, 0]))
    log_probability.sum().backward()
    assert_close(log_probability.detach(), [0.0, 0.0, math.log(0.5)])
    assert eta.grad is not None
    assert_close(eta.grad, [0.0, 0.0, -0.5])


def test_bernoulli_samples_one_below_the_probability() -> None:
    samples = Bernoulli().sample((tensor([0.25, 0.25, 0.25]),), tensor([0.0, 0.2499, 0.25]))
    assert torch.equal(samples, torch.tensor([1, 1, 0]))


def test_bernoulli_rejects_an_outcome_that_is_not_0_or_1() -> None:
    with pytest.raises(ValueError, match="0 or 1"):
        Bernoulli().log_prob((tensor([0.5]),), torch.tensor([2]))


def test_bernoulli_rejects_a_uniform_outside_0_to_1() -> None:
    with pytest.raises(ValueError, match=r"\[0, 1\)"):
        Bernoulli().sample((tensor([0.5]),), tensor([1.0]))


def test_the_model_combines_terms_link_and_distribution() -> None:
    model = Model.from_fitted(golden.BERNOULLI, BERNOULLI_FEATURES)
    rows = [[4.0, 1.0, 10.0], [-2.0, 0.0, -1.0]]
    segments = [[8.0, 2.0, 0.0], [0.0, 0.0, 0.0]]
    expected = [
        1
        / (
            1
            + math.exp(
                -(
                    -0.12
                    + 0.021 * (margin - 0.31) / 8.4
                    - 0.18 * (three - 0.38) / 0.49
                    + 0.12 * (segment[0] - 4.1) / 3.0
                    - 0.05 * (segment[1] - 3.9) / 3.5
                    + 0.2 * (segment[2] - 2.2) / 3.1
                )
            )
        )
        for (margin, three, _), segment in zip(rows, segments, strict=True)
    ]
    (probability,) = model.parameter_values(tensor(rows))
    assert_close(probability, expected)
    assert_close(model.log_prob(tensor(rows), torch.tensor([1, 0])), [math.log(expected[0]), math.log(1 - expected[1])])


def test_categorical_gives_the_log_probability_of_each_category() -> None:
    log_odds = (tensor([math.log(2.0)]), tensor([math.log(3.0)]))
    log_prob = CATEGORICAL.log_prob((log_odds[0].repeat(3), log_odds[1].repeat(3)), torch.tensor([0, 1, 2]))
    assert_close(log_prob, [math.log(1 / 6), math.log(2 / 6), math.log(3 / 6)])


def test_categorical_samples_the_first_category_whose_cumulative_probability_exceeds_the_uniform() -> None:
    log_odds = (tensor([math.log(2.0)] * 5), tensor([math.log(3.0)] * 5))
    samples = CATEGORICAL.sample(log_odds, tensor([0.0, 0.16, 0.17, 0.49, 0.51]))
    assert torch.equal(samples, torch.tensor([0, 0, 1, 1, 2]))


def test_categorical_rejects_an_outcome_outside_its_categories() -> None:
    with pytest.raises(ValueError, match="from 0 to 2"):
        CATEGORICAL.log_prob((tensor([0.0]), tensor([0.0])), torch.tensor([3]))


def test_categorical_starts_from_the_log_odds_of_the_category_shares() -> None:
    starts = CATEGORICAL.initial_parameters(torch.tensor([0, 0, 1, 2, 2, 2]))
    assert starts == pytest.approx({"b": math.log(1 / 2), "c": math.log(3 / 2)})


def test_categorical_fails_when_a_category_never_occurs() -> None:
    with pytest.raises(ValueError, match="never occur"):
        CATEGORICAL.initial_parameters(torch.tensor([0, 2, 2]))


def johnson_su_parameters(rows: int, maximum: float) -> tuple[torch.Tensor, ...]:
    def column(value: float) -> torch.Tensor:
        return torch.full((rows,), value, dtype=torch.float64)

    return column(6.0), column(4.0), column(0.5), column(1.3), column(maximum)


def test_the_discrete_johnson_su_probabilities_add_up_to_1_up_to_the_maximum() -> None:
    log_prob = DiscreteJohnsonSu().log_prob(johnson_su_parameters(11, 10.0), torch.arange(11))
    assert float(torch.exp(log_prob).sum()) == pytest.approx(1.0, rel=1e-13)


def test_the_discrete_johnson_su_is_a_floored_normal_transform() -> None:
    location, scale, skew, tail, maximum = johnson_su_parameters(1, 24.0)

    def cdf(x: float) -> float:
        return 0.5 * math.erfc(-(0.5 + 1.3 * math.asinh((x - 6.0) / 4.0)) / math.sqrt(2))

    expected = (cdf(4) - cdf(3)) / (cdf(25) - cdf(0))
    log_prob = DiscreteJohnsonSu().log_prob((location, scale, skew, tail, maximum), torch.tensor([3]))
    assert float(torch.exp(log_prob)[0]) == pytest.approx(expected, rel=1e-12)


def test_the_discrete_johnson_su_is_uniform_when_its_outcomes_are_narrower_than_the_precision_of_z() -> None:
    def column(value: float) -> torch.Tensor:
        return torch.full((29,), value, dtype=torch.float64)

    parameters = column(0.0), column(5e6), column(14.2), column(3e-9), column(28.0)
    log_prob = DiscreteJohnsonSu().log_prob(parameters, torch.arange(29))
    torch.testing.assert_close(torch.exp(log_prob), torch.full((29,), 1 / 29, dtype=torch.float64), rtol=1e-9, atol=0)


def test_the_discrete_johnson_su_samples_uniformly_when_its_whole_range_is_narrower_than_the_precision_of_z() -> None:
    def column(value: float) -> torch.Tensor:
        return torch.full((3,), value, dtype=torch.float64)

    parameters = column(0.0), column(5e6), column(14.2), column(3e-9), column(28.0)
    samples = DiscreteJohnsonSu().sample(parameters, tensor([0.01, 0.5, 0.99]))
    assert samples.tolist() == [0, 14, 28]


def test_the_discrete_johnson_su_of_a_narrow_outcome_is_its_midpoint_density_and_curvature() -> None:
    location, scale, skew, tail, maximum = 6.0, 4.0, 0.5, 1.3e-3, 24.0

    def cdf(x: float) -> float:
        return 0.5 * math.erfc(-(skew + tail * math.asinh((x - location) / scale)) / math.sqrt(2))

    expected = (cdf(4) - cdf(3)) / (cdf(25) - cdf(0))
    parameters = tuple(tensor([v]) for v in (location, scale, skew, tail, maximum))
    log_prob = DiscreteJohnsonSu().log_prob(parameters, torch.tensor([3]))
    assert float(torch.exp(log_prob)[0]) == pytest.approx(expected, rel=1e-10)


def test_the_discrete_johnson_su_samples_whole_numbers_within_its_maximum() -> None:
    uniforms = torch.linspace(0, 0.999, 500, dtype=torch.float64)
    samples = DiscreteJohnsonSu().sample(johnson_su_parameters(500, 10.0), uniforms)
    assert int(samples.min()) == 0
    assert int(samples.max()) == 10
    assert bool((samples[1:] >= samples[:-1]).all())


def test_the_discrete_johnson_su_rejects_an_outcome_above_its_maximum() -> None:
    with pytest.raises(ValueError, match="from 0 to its maximum"):
        DiscreteJohnsonSu().log_prob(johnson_su_parameters(1, 10.0), torch.tensor([11]))


def test_the_discrete_johnson_su_rejects_a_maximum_that_is_not_a_whole_number() -> None:
    with pytest.raises(ValueError, match="whole number"):
        DiscreteJohnsonSu().log_prob(johnson_su_parameters(1, 10.5), torch.tensor([3]))


def test_the_model_rejects_features_that_are_not_float64() -> None:
    model = Model.from_fitted(golden.BERNOULLI, BERNOULLI_FEATURES)
    with pytest.raises(ValueError, match="float64"):
        model.parameter_values(torch.zeros((1, 2), dtype=torch.float32))


def test_the_normal_golden_file_is_current(tmp_path: Path) -> None:
    golden.write_normal(tmp_path)
    assert (tmp_path / "cases.json").read_bytes() == (golden.TESTDATA / "normal" / "cases.json").read_bytes()


@pytest.mark.parametrize("name", golden.CASES.keys())
def test_the_golden_files_are_current(tmp_path: Path, name: str) -> None:
    golden.write_cases(tmp_path, *golden.CASES[name])
    for file in ("fitted-model.json", "cases.json"):
        assert (tmp_path / file).read_bytes() == (golden.TESTDATA / name / file).read_bytes()
