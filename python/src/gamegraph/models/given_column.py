from collections.abc import Sequence
from dataclasses import dataclass

import torch

from gamegraph.models.terms import column_of


@dataclass(frozen=True)
class GivenColumn:
    feature: str
    column: int

    def design(self, features: torch.Tensor) -> torch.Tensor:
        return features[:, self.column].unsqueeze(1)

    def value_of(self, design: torch.Tensor) -> torch.Tensor:
        return design[:, 0]

    @staticmethod
    def of(feature: str, features: Sequence[str]) -> "GivenColumn":
        return GivenColumn(feature, column_of(feature, features))
