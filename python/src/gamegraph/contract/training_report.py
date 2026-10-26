from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime
from enum import StrEnum

from gamegraph.contract.json_document import Json
from gamegraph.contract.utc import utc_from_text, utc_to_text


class Route(StrEnum):
    DIRECT = "direct"
    RIDGE_PATH = "ridgePath"


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
class FailedAttempt:
    route: Route
    ridge: float
    reason: str

    def to_json(self) -> Json:
        return {"route": self.route.value, "ridge": self.ridge, "reason": self.reason}

    @staticmethod
    def from_json(json: Json) -> FailedAttempt:
        return FailedAttempt(Route(json["route"]), json["ridge"], json["reason"])


@dataclass(frozen=True)
class TrainingReport:
    node: str
    created_utc: datetime
    training_games: int
    examples: TrainingAndValidation[int]
    chosen: int
    attempts: tuple[Attempt, ...]
    failures: tuple[FailedAttempt, ...]

    def __post_init__(self) -> None:
        if not 0 <= self.chosen < len(self.attempts):
            raise ValueError(f"{self.node} chose attempt {self.chosen} of {len(self.attempts)}")

    def chosen_attempt(self) -> Attempt:
        return self.attempts[self.chosen]

    def to_json(self) -> Json:
        return {
            "node": self.node,
            "createdUtc": utc_to_text(self.created_utc),
            "trainingGames": self.training_games,
            "examples": self.examples.to_json(),
            "chosen": self.chosen,
            "attempts": [a.to_json() for a in self.attempts],
            "failures": [f.to_json() for f in self.failures],
        }

    @staticmethod
    def from_json(json: Json) -> TrainingReport:
        return TrainingReport(
            json["node"],
            utc_from_text(json["createdUtc"]),
            json["trainingGames"],
            TrainingAndValidation[int].from_json(json["examples"]),
            json["chosen"],
            tuple(Attempt.from_json(a) for a in json["attempts"]),
            tuple(FailedAttempt.from_json(f) for f in json["failures"]),
        )
