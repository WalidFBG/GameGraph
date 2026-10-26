from __future__ import annotations

from dataclasses import dataclass
from enum import StrEnum
from typing import ClassVar

from gamegraph.contract.json_document import Json
from gamegraph.contract.names import require_distinct


class Link(StrEnum):
    IDENTITY = "identity"
    LOG = "log"
    LOGIT = "logit"


@dataclass(frozen=True)
class PredictedParameter:
    name: str
    link: Link
    has_intercept: bool


@dataclass(frozen=True)
class BernoulliDistribution:
    def predicted(self) -> tuple[PredictedParameter, ...]:
        return (PredictedParameter("probability", Link.LOGIT, has_intercept=True),)

    def given(self) -> tuple[str, ...]:
        return ()

    def chooses_among_candidates(self) -> bool:
        return False

    def to_json(self) -> Json:
        return {"name": "bernoulli"}


@dataclass(frozen=True)
class CategoricalDistribution:
    categories: tuple[str, ...]

    def __post_init__(self) -> None:
        if len(self.categories) < 2:
            raise ValueError(f"A categorical distribution needs at least 2 categories, got {list(self.categories)}")
        require_distinct(list(self.categories))

    def predicted(self) -> tuple[PredictedParameter, ...]:
        return tuple(
            PredictedParameter(category, Link.IDENTITY, has_intercept=True) for category in self.categories[1:]
        )

    def given(self) -> tuple[str, ...]:
        return ()

    def chooses_among_candidates(self) -> bool:
        return False

    def to_json(self) -> Json:
        return {"name": "categorical", "categories": list(self.categories)}


@dataclass(frozen=True)
class DiscreteJohnsonSuDistribution:
    def predicted(self) -> tuple[PredictedParameter, ...]:
        return (
            PredictedParameter("location", Link.IDENTITY, has_intercept=True),
            PredictedParameter("scale", Link.LOG, has_intercept=True),
            PredictedParameter("skew", Link.IDENTITY, has_intercept=True),
            PredictedParameter("tail", Link.LOG, has_intercept=True),
        )

    def given(self) -> tuple[str, ...]:
        return ("maximum",)

    def chooses_among_candidates(self) -> bool:
        return False

    def to_json(self) -> Json:
        return {"name": "discreteJohnsonSu"}


@dataclass(frozen=True)
class ChoiceDistribution:
    SIZE: ClassVar[str] = "size"

    def predicted(self) -> tuple[PredictedParameter, ...]:
        return (PredictedParameter("utility", Link.IDENTITY, has_intercept=False),)

    def given(self) -> tuple[str, ...]:
        return (ChoiceDistribution.SIZE,)

    def chooses_among_candidates(self) -> bool:
        return True

    def to_json(self) -> Json:
        return {"name": "choice"}


type DistributionDeclaration = (
    BernoulliDistribution | CategoricalDistribution | DiscreteJohnsonSuDistribution | ChoiceDistribution
)


def distribution_from_json(json: Json) -> DistributionDeclaration:
    match json["name"]:
        case "bernoulli":
            return BernoulliDistribution()
        case "categorical":
            return CategoricalDistribution(tuple(json["categories"]))
        case "discreteJohnsonSu":
            return DiscreteJohnsonSuDistribution()
        case "choice":
            return ChoiceDistribution()
        case name:
            raise ValueError(f"Unknown distribution {name}")
