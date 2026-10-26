from dataclasses import dataclass
from typing import ClassVar

import torch


@dataclass(frozen=True)
class ChoiceShape:
    MOST_CANDIDATES: ClassVar[int] = 31

    candidate: torch.Tensor
    size: torch.Tensor

    @staticmethod
    def of(sizes: torch.Tensor) -> "ChoiceShape":
        if sizes.shape[1] > ChoiceShape.MOST_CANDIDATES:
            raise ValueError(f"A choice has at most {ChoiceShape.MOST_CANDIDATES} candidates, got {sizes.shape[1]}")
        candidate = sizes > 0
        size = torch.where(candidate, sizes, 0.0).amax(dim=1)
        if not (
            candidate.any(dim=1).all() and (torch.where(candidate, sizes, size.unsqueeze(1)) == size.unsqueeze(1)).all()
        ):
            raise ValueError("Every candidate of a choice has the same size, and padding rows have size 0")
        if not (torch.equal(size, torch.round(size)) and (size <= candidate.sum(dim=1)).all()):
            raise ValueError("A choice's size is a whole number of its candidates")
        return ChoiceShape(candidate, size.to(torch.int64))

    def chosen(self, outcomes: torch.Tensor) -> torch.Tensor:
        return (outcomes.unsqueeze(1) >> torch.arange(self.candidate.shape[1])) & 1 == 1

    def require_outcomes(self, outcomes: torch.Tensor) -> None:
        chosen = self.chosen(outcomes)
        rows = self.candidate.shape[1]
        if (outcomes < 0).any() or (outcomes >> rows != 0).any() or (chosen & ~self.candidate).any():
            raise ValueError("A choice's outcome is a set of its candidates' rows")
        if not torch.equal(chosen.sum(dim=1), self.size):
            raise ValueError("A choice chooses as many candidates as its size")
