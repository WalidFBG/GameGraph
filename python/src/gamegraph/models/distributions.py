import math

import torch

from gamegraph.contract import (
    BernoulliDistribution,
    CategoricalDistribution,
    ChoiceDistribution,
    DiscreteJohnsonSuDistribution,
    DistributionDeclaration,
)
from gamegraph.models import normal
from gamegraph.models.choice_shape import ChoiceShape

NARROW_OUTCOME = 1e-3
NARROW_RANGE = 1e-8
NO_SETS = -1e300


class Bernoulli:
    def log_prob(self, parameters: tuple[torch.Tensor, ...], outcomes: torch.Tensor) -> torch.Tensor:
        (probability,) = parameters
        self._require_outcomes(outcomes)
        is_one = outcomes == 1
        return torch.where(
            is_one,
            torch.log(torch.where(is_one, probability, 1.0)),
            torch.log1p(-torch.where(is_one, 0.0, probability)),
        )

    def sample(self, parameters: tuple[torch.Tensor, ...], uniforms: torch.Tensor) -> torch.Tensor:
        (probability,) = parameters
        _require_uniforms(uniforms)
        return (uniforms < probability).to(torch.int64)

    def initial_parameters(self, outcomes: torch.Tensor) -> dict[str, float]:
        self._require_outcomes(outcomes)
        mean = float(outcomes.to(torch.float64).mean())
        if mean in (0, 1):
            raise ValueError(f"Every outcome is {int(mean)}, so there is nothing to fit")
        return {"probability": mean}

    @staticmethod
    def _require_outcomes(outcomes: torch.Tensor) -> None:
        if not ((outcomes == 0) | (outcomes == 1)).all():
            raise ValueError("A Bernoulli outcome is 0 or 1")


class Categorical:
    def __init__(self, categories: tuple[str, ...]) -> None:
        self.categories = categories

    def log_prob(self, parameters: tuple[torch.Tensor, ...], outcomes: torch.Tensor) -> torch.Tensor:
        self._require_outcomes(outcomes)
        log_probabilities = torch.log_softmax(self._scores(parameters), dim=1)
        return log_probabilities.gather(1, outcomes.unsqueeze(1)).squeeze(1)

    def sample(self, parameters: tuple[torch.Tensor, ...], uniforms: torch.Tensor) -> torch.Tensor:
        _require_uniforms(uniforms)
        cumulative = torch.softmax(self._scores(parameters), dim=1).cumsum(dim=1)
        passed = (cumulative <= uniforms.unsqueeze(1)).sum(dim=1)
        return torch.clamp(passed, max=len(self.categories) - 1)

    def initial_parameters(self, outcomes: torch.Tensor) -> dict[str, float]:
        self._require_outcomes(outcomes)
        counts = torch.bincount(outcomes, minlength=len(self.categories))
        missing = [c for c, count in zip(self.categories, counts, strict=True) if count == 0]
        if missing:
            raise ValueError(f"Categories {missing} never occur, so their log-odds cannot be fitted")
        return {
            category: math.log(int(counts[k]) / int(counts[0])) for k, category in enumerate(self.categories) if k > 0
        }

    def _scores(self, parameters: tuple[torch.Tensor, ...]) -> torch.Tensor:
        if len(parameters) != len(self.categories) - 1:
            raise ValueError(f"{len(self.categories)} categories need {len(self.categories) - 1} log-odds")
        return torch.stack([torch.zeros_like(parameters[0]), *parameters], dim=1)

    def _require_outcomes(self, outcomes: torch.Tensor) -> None:
        if not ((outcomes >= 0) & (outcomes < len(self.categories))).all():
            raise ValueError(f"A categorical outcome is a category from 0 to {len(self.categories) - 1}")


class DiscreteJohnsonSu:
    def log_prob(self, parameters: tuple[torch.Tensor, ...], outcomes: torch.Tensor) -> torch.Tensor:
        location, scale, skew, tail, maximum = parameters
        _require_maximum(maximum)
        if not ((outcomes >= 0) & (outcomes <= maximum)).all():
            raise ValueError("A discrete Johnson SU outcome is a whole number from 0 to its maximum")

        def edge(x: torch.Tensor) -> torch.Tensor:
            return torch.asinh((x - location) / scale)

        k = outcomes.to(torch.float64)
        return _log_mass(skew, tail, edge(k), edge(k + 1)) - _log_mass(
            skew, tail, edge(torch.zeros_like(k)), edge(maximum + 1)
        )

    def sample(self, parameters: tuple[torch.Tensor, ...], uniforms: torch.Tensor) -> torch.Tensor:
        location, scale, skew, tail, maximum = parameters
        _require_maximum(maximum)
        _require_uniforms(uniforms)
        start = torch.asinh((0 - location) / scale)
        end = torch.asinh((maximum + 1 - location) / scale)
        low, high = skew + tail * start, skew + tail * end
        width = tail * (end - start)
        above = low > 0
        lower_side = normal.cdf(low) + uniforms * (normal.cdf(high) - normal.cdf(low))
        upper_side = normal.cdf(-low) - uniforms * (normal.cdf(-low) - normal.cdf(-high))
        quantile = torch.where(above, -normal.inverse_cdf(upper_side), normal.inverse_cdf(lower_side))
        narrow = _narrow(width, low + width / 2, NARROW_RANGE)
        edge = torch.where(narrow, start + uniforms * (end - start), (quantile - skew) / tail)
        times = location + scale * torch.sinh(edge)
        return torch.minimum(torch.clamp(torch.floor(times), min=0), maximum).to(torch.int64)

    def initial_parameters(self, outcomes: torch.Tensor) -> dict[str, float]:
        times = outcomes.to(torch.float64)
        spread = float(times.std(correction=0))
        if spread == 0:
            raise ValueError(f"Every outcome is {int(times[0])}, so there is nothing to fit")
        return {"location": float(times.mean()), "scale": spread, "skew": 0.0, "tail": 1.0}


class Choice:
    def log_prob(self, parameters: tuple[torch.Tensor, ...], outcomes: torch.Tensor) -> torch.Tensor:
        utility, sizes = parameters
        shape = ChoiceShape.of(sizes)
        shape.require_outcomes(outcomes)
        safe = torch.where(shape.candidate, utility, 0.0)
        return torch.where(shape.chosen(outcomes), safe, 0.0).sum(dim=1) - _log_sets(safe, shape.candidate, shape.size)

    def sample(self, parameters: tuple[torch.Tensor, ...], uniforms: torch.Tensor) -> torch.Tensor:
        utility, sizes = parameters
        shape = ChoiceShape.of(sizes)
        candidate, size = shape.candidate, shape.size
        _require_uniforms(uniforms)
        safe = torch.where(candidate, utility, 0.0)
        suffixes = _log_suffix_sets(safe, candidate, int(size.max()))
        remaining, uniform = size.clone(), uniforms.clone()
        outcome = torch.zeros_like(size)
        for c in range(utility.shape[1]):
            rest = suffixes[:, c + 1].gather(1, (remaining - 1).clamp(min=0).unsqueeze(1)).squeeze(1)
            whole = suffixes[:, c].gather(1, remaining.unsqueeze(1)).squeeze(1)
            include = torch.where(candidate[:, c] & (remaining > 0), torch.exp(safe[:, c] + rest - whole), 0.0)
            taken = uniform < include
            uniform = torch.where(taken, uniform / include, (uniform - include) / (1 - include))
            outcome = outcome | (taken.to(torch.int64) << c)
            remaining = remaining - taken.to(torch.int64)
        if (remaining != 0).any():
            raise RuntimeError("A choice sampled fewer candidates than its size")
        return outcome

    def initial_parameters(self, outcomes: torch.Tensor) -> dict[str, float]:
        return {}


type Distribution = Bernoulli | Categorical | DiscreteJohnsonSu | Choice


def distribution_for(declaration: DistributionDeclaration) -> Distribution:
    match declaration:
        case BernoulliDistribution():
            return Bernoulli()
        case CategoricalDistribution():
            return Categorical(declaration.categories)
        case DiscreteJohnsonSuDistribution():
            return DiscreteJohnsonSu()
        case ChoiceDistribution():
            return Choice()


def _log_sets(utility: torch.Tensor, candidate: torch.Tensor, size: torch.Tensor) -> torch.Tensor:
    return _log_suffix_sets(utility, candidate, int(size.max()))[:, 0].gather(1, size.unsqueeze(1)).squeeze(1)


def _log_suffix_sets(utility: torch.Tensor, candidate: torch.Tensor, largest: int) -> torch.Tensor:
    decisions, candidates = utility.shape
    empty = torch.zeros(decisions, 1, dtype=utility.dtype)
    none = torch.full((decisions, largest), NO_SETS, dtype=utility.dtype)
    suffixes = [torch.cat([empty, none], dim=1)]
    for c in reversed(range(candidates)):
        after = suffixes[0]
        grown = torch.cat([after[:, :1], torch.logaddexp(after[:, 1:], utility[:, c : c + 1] + after[:, :-1])], dim=1)
        suffixes.insert(0, torch.where(candidate[:, c : c + 1], grown, after))
    return torch.stack(suffixes, dim=1)


def _log_mass(skew: torch.Tensor, tail: torch.Tensor, start: torch.Tensor, end: torch.Tensor) -> torch.Tensor:
    low, high = skew + tail * start, skew + tail * end
    width = tail * (end - start)
    middle = low + width / 2
    narrow = _narrow(width, middle, NARROW_OUTCOME)
    wide = _log_mass_between(torch.where(narrow, -1.0, low), torch.where(narrow, 0.0, high))
    narrow_width = torch.where(narrow, width, 1.0)
    midpoint = (
        normal.log_density(middle) + torch.log(narrow_width) + torch.log1p((middle**2 - 1) * narrow_width**2 / 24)
    )
    return torch.where(narrow, midpoint, wide)


def _narrow(width: torch.Tensor, middle: torch.Tensor, threshold: float) -> torch.Tensor:
    return width * torch.clamp(middle.abs(), min=1.0) < threshold


def _log_mass_between(low: torch.Tensor, high: torch.Tensor) -> torch.Tensor:
    above = low > 0
    lower = torch.where(above, -high, low)
    upper = torch.where(above, -low, high)
    log_upper = normal.log_cdf(upper)
    return log_upper + torch.log1p(-torch.exp(normal.log_cdf(lower) - log_upper))


def _require_maximum(maximum: torch.Tensor) -> None:
    if not ((maximum >= 0) & (maximum == torch.round(maximum))).all():
        raise ValueError("A maximum is a whole number of 0 or more")


def _require_uniforms(uniforms: torch.Tensor) -> None:
    if not ((uniforms >= 0) & (uniforms < 1)).all():
        raise ValueError("A uniform is in [0, 1)")
