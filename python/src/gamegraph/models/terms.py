from collections.abc import Sequence
from dataclasses import dataclass

import torch

from gamegraph.models.contract import FittedLinearTerm, LinearTerm


@dataclass(frozen=True)
class LinearColumns:
    feature: str
    column: int
    centre: float
    scale: float

    def columns(self, features: torch.Tensor) -> torch.Tensor:
        return ((features[:, self.column] - self.centre) / self.scale).unsqueeze(1)

    def to_fitted(self, coefficients: torch.Tensor) -> FittedLinearTerm:
        return FittedLinearTerm(self.feature, self.centre, self.scale, tuple(float(c) for c in coefficients))

    @staticmethod
    def from_fitted(term: FittedLinearTerm, features: Sequence[str]) -> "LinearColumns":
        return LinearColumns(term.feature, column_of(term.feature, features), term.centre, term.scale)

    @staticmethod
    def fit(term: LinearTerm, features: Sequence[str], examples: torch.Tensor) -> "LinearColumns":
        column = column_of(term.feature, features)
        values = examples[:, column]
        scale = float(values.std(correction=0))
        if scale == 0:
            raise ValueError(f"{term.feature} is constant in the training examples and cannot be fitted")
        return LinearColumns(term.feature, column, float(values.mean()), scale)


def column_of(feature: str, features: Sequence[str]) -> int:
    if feature not in features:
        raise ValueError(f"No feature {feature} among {list(features)}")
    return features.index(feature)
