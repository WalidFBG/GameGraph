from __future__ import annotations

from collections import Counter
from collections.abc import Iterable
from dataclasses import dataclass
from datetime import UTC, datetime, timedelta
from enum import StrEnum
from itertools import pairwise
from typing import Any

type Json = dict[str, Any]


class Link(StrEnum):
    IDENTITY = "identity"
    LOG = "log"
    LOGIT = "logit"


@dataclass(frozen=True)
class PredictedParameter:
    name: str
    link: Link


@dataclass(frozen=True)
class BernoulliDistribution:
    def predicted(self) -> tuple[PredictedParameter, ...]:
        return (PredictedParameter("probability", Link.LOGIT),)

    def given(self) -> tuple[str, ...]:
        return ()

    def to_json(self) -> Json:
        return {"name": "bernoulli"}


@dataclass(frozen=True)
class CategoricalDistribution:
    categories: tuple[str, ...]

    def __post_init__(self) -> None:
        if len(self.categories) < 2:
            raise ValueError(f"A categorical distribution needs at least 2 categories, got {list(self.categories)}")
        _require_distinct(list(self.categories))

    def predicted(self) -> tuple[PredictedParameter, ...]:
        return tuple(PredictedParameter(category, Link.IDENTITY) for category in self.categories[1:])

    def given(self) -> tuple[str, ...]:
        return ()

    def to_json(self) -> Json:
        return {"name": "categorical", "categories": list(self.categories)}


@dataclass(frozen=True)
class DiscreteJohnsonSuDistribution:
    def predicted(self) -> tuple[PredictedParameter, ...]:
        return (
            PredictedParameter("location", Link.IDENTITY),
            PredictedParameter("scale", Link.LOG),
            PredictedParameter("skew", Link.IDENTITY),
            PredictedParameter("tail", Link.LOG),
        )

    def given(self) -> tuple[str, ...]:
        return ("maximum",)

    def to_json(self) -> Json:
        return {"name": "discreteJohnsonSu"}


type DistributionDeclaration = BernoulliDistribution | CategoricalDistribution | DiscreteJohnsonSuDistribution


def distribution_from_json(json: Json) -> DistributionDeclaration:
    match json["name"]:
        case "bernoulli":
            return BernoulliDistribution()
        case "categorical":
            return CategoricalDistribution(tuple(json["categories"]))
        case "discreteJohnsonSu":
            return DiscreteJohnsonSuDistribution()
        case name:
            raise ValueError(f"Unknown distribution {name}")


class OptimiserName(StrEnum):
    LBFGS = "lbfgs"


@dataclass(frozen=True)
class LinearTerm:
    feature: str

    def to_json(self) -> Json:
        return {"basis": "linear", "feature": self.feature}

    @staticmethod
    def from_json(json: Json) -> LinearTerm:
        return LinearTerm(json["feature"])


@dataclass(frozen=True)
class LinearSplineTerm:
    feature: str
    knots: int

    def __post_init__(self) -> None:
        if self.knots < 2:
            raise ValueError(f"A linear spline of {self.feature} needs at least 2 knots, got {self.knots}")

    def to_json(self) -> Json:
        return {"basis": "linearSpline", "feature": self.feature, "knots": self.knots}

    @staticmethod
    def from_json(json: Json) -> LinearSplineTerm:
        return LinearSplineTerm(json["feature"], json["knots"])


type Term = LinearTerm | LinearSplineTerm


def term_from_json(json: Json) -> Term:
    match json["basis"]:
        case "linear":
            return LinearTerm.from_json(json)
        case "linearSpline":
            return LinearSplineTerm.from_json(json)
        case basis:
            raise ValueError(f"Unknown basis {basis}")


@dataclass(frozen=True)
class ModelDeclaration:
    distribution: DistributionDeclaration
    terms: dict[str, tuple[Term, ...]]
    given: dict[str, str]

    def __post_init__(self) -> None:
        _require_parameters(self.distribution, self.terms.keys(), self.given.keys())
        for terms in self.terms.values():
            _require_distinct([t.feature for t in terms])

    def features(self) -> set[str]:
        return {t.feature for terms in self.terms.values() for t in terms} | set(self.given.values())

    def to_json(self) -> Json:
        return {
            "distribution": self.distribution.to_json(),
            "terms": {name: [t.to_json() for t in terms] for name, terms in self.terms.items()},
            "given": dict(self.given),
        }

    @staticmethod
    def from_json(json: Json) -> ModelDeclaration:
        return ModelDeclaration(
            distribution_from_json(json["distribution"]),
            {name: tuple(term_from_json(t) for t in terms) for name, terms in json["terms"].items()},
            dict(json["given"]),
        )


@dataclass(frozen=True)
class FittedLinearTerm:
    feature: str
    centre: float
    scale: float
    coefficients: tuple[float, ...]

    def declaration(self) -> LinearTerm:
        return LinearTerm(self.feature)

    def to_json(self) -> Json:
        return {
            "basis": "linear",
            "feature": self.feature,
            "centre": self.centre,
            "scale": self.scale,
            "coefficients": list(self.coefficients),
        }

    @staticmethod
    def from_json(json: Json) -> FittedLinearTerm:
        return FittedLinearTerm(json["feature"], json["centre"], json["scale"], tuple(json["coefficients"]))


@dataclass(frozen=True)
class FittedLinearSplineTerm:
    feature: str
    knots: int
    knot_positions: tuple[float, ...]
    centres: tuple[float, ...]
    scales: tuple[float, ...]
    coefficients: tuple[float, ...]

    def __post_init__(self) -> None:
        positions = self.knot_positions
        if not 2 <= len(positions) <= self.knots:
            raise ValueError(
                f"A linear spline of {self.feature} with {self.knots} knots has {len(positions)} positions"
            )
        if any(a >= b for a, b in pairwise(positions)):
            raise ValueError(f"The knots of {self.feature} are not strictly increasing: {list(positions)}")
        segments = len(positions) - 1
        if not len(self.centres) == len(self.scales) == len(self.coefficients) == segments:
            raise ValueError(f"A linear spline of {self.feature} needs {segments} centres, scales and coefficients")

    def declaration(self) -> LinearSplineTerm:
        return LinearSplineTerm(self.feature, self.knots)

    def to_json(self) -> Json:
        return {
            "basis": "linearSpline",
            "feature": self.feature,
            "knots": self.knots,
            "knotPositions": list(self.knot_positions),
            "centres": list(self.centres),
            "scales": list(self.scales),
            "coefficients": list(self.coefficients),
        }

    @staticmethod
    def from_json(json: Json) -> FittedLinearSplineTerm:
        return FittedLinearSplineTerm(
            json["feature"],
            json["knots"],
            tuple(json["knotPositions"]),
            tuple(json["centres"]),
            tuple(json["scales"]),
            tuple(json["coefficients"]),
        )


type FittedTerm = FittedLinearTerm | FittedLinearSplineTerm


def fitted_term_from_json(json: Json) -> FittedTerm:
    match json["basis"]:
        case "linear":
            return FittedLinearTerm.from_json(json)
        case "linearSpline":
            return FittedLinearSplineTerm.from_json(json)
        case basis:
            raise ValueError(f"Unknown basis {basis}")


@dataclass(frozen=True)
class FittedPredictor:
    intercept: float
    terms: tuple[FittedTerm, ...]

    def __post_init__(self) -> None:
        _require_distinct([t.feature for t in self.terms])

    def declaration(self) -> tuple[Term, ...]:
        return tuple(t.declaration() for t in self.terms)

    def to_json(self) -> Json:
        return {"intercept": self.intercept, "terms": [t.to_json() for t in self.terms]}

    @staticmethod
    def from_json(json: Json) -> FittedPredictor:
        return FittedPredictor(json["intercept"], tuple(fitted_term_from_json(t) for t in json["terms"]))


@dataclass(frozen=True)
class FittedModel:
    node: str
    distribution: DistributionDeclaration
    predictors: dict[str, FittedPredictor]
    given: dict[str, str]

    def __post_init__(self) -> None:
        _require_parameters(self.distribution, self.predictors.keys(), self.given.keys())

    def declaration(self) -> ModelDeclaration:
        return ModelDeclaration(
            self.distribution, {name: p.declaration() for name, p in self.predictors.items()}, dict(self.given)
        )

    def to_json(self) -> Json:
        return {
            "node": self.node,
            "distribution": self.distribution.to_json(),
            "predictors": {name: p.to_json() for name, p in self.predictors.items()},
            "given": dict(self.given),
        }

    @staticmethod
    def from_json(json: Json) -> FittedModel:
        return FittedModel(
            json["node"],
            distribution_from_json(json["distribution"]),
            {name: FittedPredictor.from_json(p) for name, p in json["predictors"].items()},
            dict(json["given"]),
        )


@dataclass(frozen=True)
class TrainingDataManifest:
    node: str
    source: str
    created_utc: datetime
    features: tuple[str, ...]
    examples: int
    model: ModelDeclaration

    def __post_init__(self) -> None:
        _require_distinct(list(self.features))
        missing = self.model.features() - set(self.features)
        if missing:
            raise ValueError(f"The model of {self.node} uses {sorted(missing)}, which are not among its features")
        unused = set(self.features) - self.model.features()
        if unused:
            raise ValueError(f"{self.node} has features {sorted(unused)} that its model does not use")

    def to_json(self) -> Json:
        return {
            "node": self.node,
            "source": self.source,
            "createdUtc": utc_to_text(self.created_utc),
            "features": list(self.features),
            "examples": self.examples,
            "model": self.model.to_json(),
        }

    @staticmethod
    def from_json(json: Json) -> TrainingDataManifest:
        return TrainingDataManifest(
            json["node"],
            json["source"],
            utc_from_text(json["createdUtc"]),
            tuple(json["features"]),
            json["examples"],
            ModelDeclaration.from_json(json["model"]),
        )


@dataclass(frozen=True)
class TrainingAndValidation[T]:
    training: T
    validation: T

    def to_json(self) -> Json:
        return {"training": self.training, "validation": self.validation}

    @classmethod
    def from_json(cls, json: Json) -> TrainingAndValidation[T]:
        return cls(json["training"], json["validation"])


@dataclass(frozen=True)
class Optimiser:
    name: OptimiserName
    iterations: int

    def to_json(self) -> Json:
        return {"name": self.name.value, "iterations": self.iterations}

    @staticmethod
    def from_json(json: Json) -> Optimiser:
        return Optimiser(OptimiserName(json["name"]), json["iterations"])


@dataclass(frozen=True)
class TrainingReport:
    node: str
    created_utc: datetime
    training_games: int
    examples: TrainingAndValidation[int]
    negative_log_likelihood: TrainingAndValidation[float]
    optimiser: Optimiser

    def to_json(self) -> Json:
        return {
            "node": self.node,
            "createdUtc": utc_to_text(self.created_utc),
            "trainingGames": self.training_games,
            "examples": self.examples.to_json(),
            "negativeLogLikelihood": self.negative_log_likelihood.to_json(),
            "optimiser": self.optimiser.to_json(),
        }

    @staticmethod
    def from_json(json: Json) -> TrainingReport:
        return TrainingReport(
            json["node"],
            utc_from_text(json["createdUtc"]),
            json["trainingGames"],
            TrainingAndValidation[int].from_json(json["examples"]),
            TrainingAndValidation[float].from_json(json["negativeLogLikelihood"]),
            Optimiser.from_json(json["optimiser"]),
        )


def _require_parameters(distribution: DistributionDeclaration, predicted: Iterable[str], given: Iterable[str]) -> None:
    expected, actual = sorted(p.name for p in distribution.predicted()), sorted(predicted)
    if actual != expected:
        raise ValueError(f"{distribution} predicts {expected}, got {actual}")
    expected, actual = sorted(distribution.given()), sorted(given)
    if actual != expected:
        raise ValueError(f"{distribution} is given {expected}, got {actual}")


def _require_distinct(names: list[str]) -> None:
    repeated = sorted(name for name, count in Counter(names).items() if count > 1)
    if repeated:
        raise ValueError(f"{repeated} appear more than once")


def utc_to_text(moment: datetime) -> str:
    if moment.tzinfo != UTC:
        raise ValueError(f"{moment} is not in UTC")
    return moment.isoformat().replace("+00:00", "Z")


def utc_from_text(text: str) -> datetime:
    moment = datetime.fromisoformat(text)
    if moment.utcoffset() != timedelta(0):
        raise ValueError(f"{text} is not in UTC")
    return moment.astimezone(UTC)
