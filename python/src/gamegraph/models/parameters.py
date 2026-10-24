from collections.abc import Mapping, Sequence
from dataclasses import dataclass

import torch

from gamegraph.models.contract import (
    FittedParameter,
    FittedPredictedParameter,
    GivenParameter,
    Parameter,
    PredictedParameter,
)
from gamegraph.models.predictor import Predictor
from gamegraph.models.terms import column_of


@dataclass(frozen=True)
class GivenColumn:
    feature: str
    column: int

    def design(self, features: torch.Tensor) -> torch.Tensor:
        return features[:, self.column].unsqueeze(1)

    def value_of(self, design: torch.Tensor) -> torch.Tensor:
        return design[:, 0]

    def trainable(self) -> list[torch.Tensor]:
        return []

    def to_fitted(self) -> GivenParameter:
        return GivenParameter(self.feature)

    @staticmethod
    def from_parameter(parameter: GivenParameter, features: Sequence[str]) -> "GivenColumn":
        return GivenColumn(parameter.feature, column_of(parameter.feature, features))


type ParameterValue = Predictor | GivenColumn


def value_from_fitted(parameter: FittedParameter, features: Sequence[str]) -> ParameterValue:
    match parameter:
        case FittedPredictedParameter():
            return Predictor.from_fitted(parameter, features)
        case GivenParameter():
            return GivenColumn.from_parameter(parameter, features)


def initial_value(
    name: str, parameter: Parameter, features: Sequence[str], examples: torch.Tensor, starts: Mapping[str, float]
) -> ParameterValue:
    match parameter:
        case GivenParameter():
            return GivenColumn.from_parameter(parameter, features)
        case PredictedParameter():
            return Predictor.initial(parameter, features, examples, starts[name])
