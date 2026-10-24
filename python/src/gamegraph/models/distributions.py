import math

import torch

from gamegraph.models import normal
from gamegraph.models.contract import (
    BernoulliDistribution,
    CategoricalDistribution,
    DiscreteJohnsonSuDistribution,
    DistributionDeclaration,
)


class Bernoulli:
    def log_prob(self, parameters: tuple[torch.Tensor, ...], outcomes: torch.Tensor) -> torch.Tensor:
        (probability,) = parameters
        self._require_outcomes(outcomes)
        return torch.where(outcomes == 1, torch.log(probability), torch.log1p(-probability))

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

        def z(x: torch.Tensor) -> torch.Tensor:
            return skew + tail * torch.asinh((x - location) / scale)

        k = outcomes.to(torch.float64)
        return _log_mass_between(z(k), z(k + 1)) - _log_mass_between(z(torch.zeros_like(k)), z(maximum + 1))

    def sample(self, parameters: tuple[torch.Tensor, ...], uniforms: torch.Tensor) -> torch.Tensor:
        location, scale, skew, tail, maximum = parameters
        _require_maximum(maximum)
        _require_uniforms(uniforms)
        low = skew + tail * torch.asinh((0 - location) / scale)
        high = skew + tail * torch.asinh((maximum + 1 - location) / scale)
        above = low > 0
        lower_side = normal.cdf(low) + uniforms * (normal.cdf(high) - normal.cdf(low))
        upper_side = normal.cdf(-low) - uniforms * (normal.cdf(-low) - normal.cdf(-high))
        quantile = torch.where(above, -normal.inverse_cdf(upper_side), normal.inverse_cdf(lower_side))
        times = location + scale * torch.sinh((quantile - skew) / tail)
        return torch.minimum(torch.clamp(torch.floor(times), min=0), maximum).to(torch.int64)

    def initial_parameters(self, outcomes: torch.Tensor) -> dict[str, float]:
        times = outcomes.to(torch.float64)
        spread = float(times.std(correction=0))
        if spread == 0:
            raise ValueError(f"Every outcome is {int(times[0])}, so there is nothing to fit")
        return {"location": float(times.mean()), "scale": spread, "skew": 0.0, "tail": 1.0}


type Distribution = Bernoulli | Categorical | DiscreteJohnsonSu


def distribution_for(declaration: DistributionDeclaration) -> Distribution:
    match declaration:
        case BernoulliDistribution():
            return Bernoulli()
        case CategoricalDistribution():
            return Categorical(declaration.categories)
        case DiscreteJohnsonSuDistribution():
            return DiscreteJohnsonSu()


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
