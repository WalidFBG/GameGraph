from dataclasses import dataclass
from datetime import datetime

import torch

from gamegraph.contract import TrainingAndValidation, TrainingDataManifest


@dataclass(frozen=True)
class Game:
    id: str
    start_utc: datetime
    examples: int


@dataclass(frozen=True)
class Examples:
    features: torch.Tensor
    outcomes: torch.Tensor


@dataclass(frozen=True)
class TrainingTensors:
    manifest: TrainingDataManifest
    games: tuple[Game, ...]
    examples: Examples

    def __post_init__(self) -> None:
        rows = self.manifest.examples
        shape = tuple(self.examples.features.shape)
        candidates = shape[1:-1] if self.manifest.model.distribution.chooses_among_candidates() else ()
        if len(shape) < 2 or shape != (rows, *candidates, len(self.manifest.features)):
            raise ValueError(
                f"{self.manifest.node} has {rows} examples of {len(self.manifest.features)} features, "
                f"got features of shape {shape}"
            )
        if tuple(self.examples.outcomes.shape) != (rows,):
            raise ValueError(
                f"{self.manifest.node} has {rows} examples, got {self.examples.outcomes.shape[0]} outcomes"
            )
        if sum(g.examples for g in self.games) != rows:
            raise ValueError(
                f"The games of {self.manifest.node} hold {sum(g.examples for g in self.games)} examples, not {rows}"
            )
        if any(a.start_utc > b.start_utc for a, b in zip(self.games, self.games[1:], strict=False)):
            raise ValueError(f"The games of {self.manifest.node} are not in start-time order")

    def split(self, training_games: int) -> TrainingAndValidation[Examples]:
        if not 0 < training_games < len(self.games):
            raise ValueError(f"{len(self.games)} games cannot be split after game {training_games}")
        rows = sum(g.examples for g in self.games[:training_games])
        features, outcomes = self.examples.features, self.examples.outcomes
        return TrainingAndValidation[Examples](
            Examples(features[:rows], outcomes[:rows]), Examples(features[rows:], outcomes[rows:])
        )
