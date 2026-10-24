from __future__ import annotations

from collections import Counter
from dataclasses import dataclass
from datetime import UTC, datetime, timedelta
from enum import StrEnum
from typing import Any

type Json = dict[str, Any]


class Distribution(StrEnum):
    BERNOULLI = "bernoulli"


class Link(StrEnum):
    LOGIT = "logit"


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
class PredictedParameter:
    link: Link
    terms: tuple[LinearTerm, ...]

    def __post_init__(self) -> None:
        _require_distinct([t.feature for t in self.terms])

    def to_json(self) -> Json:
        return {"source": "predicted", "link": self.link.value, "terms": [t.to_json() for t in self.terms]}

    @staticmethod
    def from_json(json: Json) -> PredictedParameter:
        return PredictedParameter(Link(json["link"]), tuple(LinearTerm.from_json(t) for t in json["terms"]))


@dataclass(frozen=True)
class ModelDeclaration:
    distribution: Distribution
    parameters: dict[str, PredictedParameter]

    def features(self) -> set[str]:
        return {t.feature for p in self.parameters.values() for t in p.terms}

    def to_json(self) -> Json:
        return {
            "distribution": self.distribution.value,
            "parameters": {name: p.to_json() for name, p in self.parameters.items()},
        }

    @staticmethod
    def from_json(json: Json) -> ModelDeclaration:
        return ModelDeclaration(
            Distribution(json["distribution"]),
            {name: PredictedParameter.from_json(p) for name, p in json["parameters"].items()},
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
class FittedPredictedParameter:
    link: Link
    intercept: float
    terms: tuple[FittedLinearTerm, ...]

    def __post_init__(self) -> None:
        _require_distinct([t.feature for t in self.terms])

    def declaration(self) -> PredictedParameter:
        return PredictedParameter(self.link, tuple(t.declaration() for t in self.terms))

    def to_json(self) -> Json:
        return {
            "source": "predicted",
            "link": self.link.value,
            "intercept": self.intercept,
            "terms": [t.to_json() for t in self.terms],
        }

    @staticmethod
    def from_json(json: Json) -> FittedPredictedParameter:
        return FittedPredictedParameter(
            Link(json["link"]), json["intercept"], tuple(FittedLinearTerm.from_json(t) for t in json["terms"])
        )


@dataclass(frozen=True)
class FittedModel:
    node: str
    distribution: Distribution
    parameters: dict[str, FittedPredictedParameter]

    def declaration(self) -> ModelDeclaration:
        return ModelDeclaration(self.distribution, {name: p.declaration() for name, p in self.parameters.items()})

    def to_json(self) -> Json:
        return {
            "node": self.node,
            "distribution": self.distribution.value,
            "parameters": {name: p.to_json() for name, p in self.parameters.items()},
        }

    @staticmethod
    def from_json(json: Json) -> FittedModel:
        return FittedModel(
            json["node"],
            Distribution(json["distribution"]),
            {name: FittedPredictedParameter.from_json(p) for name, p in json["parameters"].items()},
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
