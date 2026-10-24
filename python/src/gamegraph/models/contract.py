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


class Route(StrEnum):
    DIRECT = "direct"
    RIDGE_PATH = "ridgePath"


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
class LinearFit:
    centre: float
    scale: float
    coefficients: tuple[float, ...]

    def to_json(self) -> Json:
        return {"centre": self.centre, "scale": self.scale, "coefficients": list(self.coefficients)}

    @staticmethod
    def from_json(json: Json) -> LinearFit:
        return LinearFit(json["centre"], json["scale"], tuple(json["coefficients"]))


@dataclass(frozen=True)
class LinearSplineFit:
    knot_positions: tuple[float, ...]
    centres: tuple[float, ...]
    scales: tuple[float, ...]
    coefficients: tuple[float, ...]

    def __post_init__(self) -> None:
        positions = self.knot_positions
        if len(positions) < 2:
            raise ValueError(f"A linear spline needs at least 2 knot positions, got {list(positions)}")
        if any(a >= b for a, b in pairwise(positions)):
            raise ValueError(f"The knots {list(positions)} are not strictly increasing")
        segments = len(positions) - 1
        if not len(self.centres) == len(self.scales) == len(self.coefficients) == segments:
            raise ValueError(
                f"A linear spline with {len(positions)} knots needs {segments} centres, scales and coefficients"
            )

    def to_json(self) -> Json:
        return {
            "knotPositions": list(self.knot_positions),
            "centres": list(self.centres),
            "scales": list(self.scales),
            "coefficients": list(self.coefficients),
        }

    @staticmethod
    def from_json(json: Json) -> LinearSplineFit:
        return LinearSplineFit(
            tuple(json["knotPositions"]), tuple(json["centres"]), tuple(json["scales"]), tuple(json["coefficients"])
        )


type TermFit = LinearFit | LinearSplineFit


def term_fit_from_json(term: Term, json: Json) -> TermFit:
    match term:
        case LinearTerm():
            return LinearFit.from_json(json)
        case LinearSplineTerm():
            return LinearSplineFit.from_json(json)


@dataclass(frozen=True)
class PredictorFit:
    intercept: float
    terms: dict[str, TermFit]

    def to_json(self) -> Json:
        return {"intercept": self.intercept, "terms": {feature: f.to_json() for feature, f in self.terms.items()}}

    @staticmethod
    def from_json(terms: tuple[Term, ...], json: Json) -> PredictorFit:
        declared = {t.feature: t for t in terms}
        return PredictorFit(
            json["intercept"],
            {
                feature: term_fit_from_json(_declared(declared, feature, "term"), f)
                for feature, f in json["terms"].items()
            },
        )


@dataclass(frozen=True)
class FittedModel:
    node: str
    model: ModelDeclaration
    fit: dict[str, PredictorFit]

    def __post_init__(self) -> None:
        if sorted(self.fit) != sorted(self.model.terms):
            raise ValueError(f"{self.node} predicts {sorted(self.model.terms)}, but its fit has {sorted(self.fit)}")
        for name, terms in self.model.terms.items():
            fits = self.fit[name].terms
            if sorted(fits) != sorted(t.feature for t in terms):
                raise ValueError(
                    f"The {name} of {self.node} has terms {[t.feature for t in terms]}, fitted {sorted(fits)}"
                )
            for term in terms:
                _require_fit_of(term, fits[term.feature])

    def to_json(self) -> Json:
        return {
            "node": self.node,
            "model": self.model.to_json(),
            "fit": {name: f.to_json() for name, f in self.fit.items()},
        }

    @staticmethod
    def from_json(json: Json) -> FittedModel:
        model = ModelDeclaration.from_json(json["model"])
        return FittedModel(
            json["node"],
            model,
            {
                name: PredictorFit.from_json(_declared(model.terms, name, "predicted parameter"), f)
                for name, f in json["fit"].items()
            },
        )


def _require_fit_of(term: Term, fit: TermFit) -> None:
    match term, fit:
        case LinearTerm(), LinearFit():
            pass
        case LinearSplineTerm(), LinearSplineFit():
            if len(fit.knot_positions) > term.knots:
                raise ValueError(
                    f"A linear spline of {term.feature} with {term.knots} knots has {len(fit.knot_positions)} positions"
                )
        case _:
            raise ValueError(f"The fit of {term.feature} does not match its basis")


def _declared[T](declared: dict[str, T], name: str, kind: str) -> T:
    if name not in declared:
        raise ValueError(f"{name} is not a declared {kind}")
    return declared[name]


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
class Attempt:
    route: Route
    ridge: float
    negative_log_likelihood: TrainingAndValidation[float]
    iterations: int

    def to_json(self) -> Json:
        return {
            "route": self.route.value,
            "ridge": self.ridge,
            "negativeLogLikelihood": self.negative_log_likelihood.to_json(),
            "iterations": self.iterations,
        }

    @staticmethod
    def from_json(json: Json) -> Attempt:
        return Attempt(
            Route(json["route"]),
            json["ridge"],
            TrainingAndValidation[float].from_json(json["negativeLogLikelihood"]),
            json["iterations"],
        )


@dataclass(frozen=True)
class Failure:
    route: Route
    ridge: float
    reason: str

    def to_json(self) -> Json:
        return {"route": self.route.value, "ridge": self.ridge, "reason": self.reason}

    @staticmethod
    def from_json(json: Json) -> Failure:
        return Failure(Route(json["route"]), json["ridge"], json["reason"])


@dataclass(frozen=True)
class TrainingReport:
    node: str
    created_utc: datetime
    training_games: int
    examples: TrainingAndValidation[int]
    negative_log_likelihood: TrainingAndValidation[float]
    route: Route
    ridge: float
    attempts: tuple[Attempt, ...]
    failures: tuple[Failure, ...]
    optimiser: Optimiser

    def to_json(self) -> Json:
        return {
            "node": self.node,
            "createdUtc": utc_to_text(self.created_utc),
            "trainingGames": self.training_games,
            "examples": self.examples.to_json(),
            "negativeLogLikelihood": self.negative_log_likelihood.to_json(),
            "route": self.route.value,
            "ridge": self.ridge,
            "attempts": [a.to_json() for a in self.attempts],
            "failures": [f.to_json() for f in self.failures],
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
            Route(json["route"]),
            json["ridge"],
            tuple(Attempt.from_json(a) for a in json["attempts"]),
            tuple(Failure.from_json(f) for f in json["failures"]),
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
