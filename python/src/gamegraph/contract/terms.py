from __future__ import annotations

from dataclasses import dataclass
from itertools import pairwise

from gamegraph.contract.json_document import Json


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
