from collections.abc import Sequence
from dataclasses import dataclass
from typing import cast

import torch

from gamegraph.models.contract import LinearFit, LinearSplineFit, LinearSplineTerm, LinearTerm, Term, TermFit


@dataclass(frozen=True)
class LinearColumns:
    feature: str
    column: int
    centre: float
    scale: float

    @property
    def count(self) -> int:
        return 1

    def columns(self, features: torch.Tensor) -> torch.Tensor:
        return ((features[..., self.column] - self.centre) / self.scale).unsqueeze(-1)

    def to_fit(self, coefficients: torch.Tensor) -> LinearFit:
        return LinearFit(self.centre, self.scale, _floats(coefficients))

    @staticmethod
    def from_fit(feature: str, fit: LinearFit, features: Sequence[str]) -> "LinearColumns":
        return LinearColumns(feature, column_of(feature, features), fit.centre, fit.scale)

    @staticmethod
    def fit(term: LinearTerm, features: Sequence[str], examples: torch.Tensor) -> "LinearColumns":
        column = column_of(term.feature, features)
        centres, scales = _centres_and_scales(term.feature, examples[..., column].reshape(-1, 1))
        return LinearColumns(term.feature, column, centres[0], scales[0])


@dataclass(frozen=True)
class LinearSplineColumns:
    feature: str
    column: int
    knot_positions: tuple[float, ...]
    centres: tuple[float, ...]
    scales: tuple[float, ...]

    @property
    def count(self) -> int:
        return len(self.knot_positions) - 1

    def columns(self, features: torch.Tensor) -> torch.Tensor:
        raw = _segments(features[..., self.column], self.knot_positions)
        return (raw - torch.tensor(self.centres, dtype=raw.dtype)) / torch.tensor(self.scales, dtype=raw.dtype)

    def to_fit(self, coefficients: torch.Tensor) -> LinearSplineFit:
        return LinearSplineFit(self.knot_positions, self.centres, self.scales, _floats(coefficients))

    @staticmethod
    def from_fit(feature: str, fit: LinearSplineFit, features: Sequence[str]) -> "LinearSplineColumns":
        return LinearSplineColumns(feature, column_of(feature, features), fit.knot_positions, fit.centres, fit.scales)

    @staticmethod
    def fit(term: LinearSplineTerm, features: Sequence[str], examples: torch.Tensor) -> "LinearSplineColumns":
        column = column_of(term.feature, features)
        values = examples[..., column].flatten()
        distinct = cast(torch.Tensor, torch.unique(values))
        quantiles = torch.quantile(
            distinct, torch.linspace(0, 1, term.knots, dtype=values.dtype), interpolation="nearest"
        )
        positions = _floats(cast(torch.Tensor, torch.unique(quantiles)))
        if len(positions) < 2:
            raise ValueError(f"{term.feature} is constant in the training examples and cannot be fitted")
        centres, scales = _centres_and_scales(term.feature, _segments(values, positions))
        return LinearSplineColumns(term.feature, column, positions, centres, scales)


type TermColumns = LinearColumns | LinearSplineColumns


def columns_from_fit(feature: str, fit: TermFit, features: Sequence[str]) -> TermColumns:
    match fit:
        case LinearFit():
            return LinearColumns.from_fit(feature, fit, features)
        case LinearSplineFit():
            return LinearSplineColumns.from_fit(feature, fit, features)


def fit_columns(term: Term, features: Sequence[str], examples: torch.Tensor) -> TermColumns:
    match term:
        case LinearTerm():
            return LinearColumns.fit(term, features, examples)
        case LinearSplineTerm():
            return LinearSplineColumns.fit(term, features, examples)


def column_of(feature: str, features: Sequence[str]) -> int:
    if feature not in features:
        raise ValueError(f"No feature {feature} among {list(features)}")
    return features.index(feature)


def _segments(values: torch.Tensor, knot_positions: tuple[float, ...]) -> torch.Tensor:
    knots = torch.tensor(knot_positions, dtype=values.dtype)
    lower, upper = knots[:-1], knots[1:]
    return torch.minimum(torch.maximum(values.unsqueeze(-1), lower), upper) - lower


def _centres_and_scales(feature: str, columns: torch.Tensor) -> tuple[tuple[float, ...], tuple[float, ...]]:
    scales = columns.std(dim=0, correction=0)
    if (scales == 0).any():
        raise ValueError(f"{feature} is constant in the training examples and cannot be fitted")
    return _floats(columns.mean(dim=0)), _floats(scales)


def _floats(values: torch.Tensor) -> tuple[float, ...]:
    return tuple(float(v) for v in values.detach())
