import csv
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path

import numpy as np
import torch

from gamegraph.models.contract import TrainingAndValidation, TrainingDataManifest, utc_from_text, utc_to_text
from gamegraph.models.contract_files import read_training_data_manifest, write_training_data_manifest

MANIFEST = "manifest.json"
EXAMPLES = "examples.csv"
GAMES = "games.csv"
DECISION = "decision"
OUTCOME = "outcome"
GAMES_HEADER = ["game", "startUtc", "examples"]


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
class TrainingData:
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


def read_training_data(directory: Path) -> TrainingData:
    manifest = read_training_data_manifest(directory / MANIFEST)
    path = directory / EXAMPLES
    choice = manifest.model.distribution.chooses_among_candidates()
    _require_header(path, [*([DECISION] if choice else []), *manifest.features, OUTCOME])
    table = np.loadtxt(path, delimiter=",", skiprows=1, ndmin=2, dtype=np.float64)
    outcomes = table[:, -1]
    if not np.array_equal(outcomes, np.round(outcomes)):
        raise ValueError(f"{path} has an outcome that is not a whole number")
    examples = _choices(path, table) if choice else Examples(_tensor(table[:, :-1]), _whole(outcomes))
    if examples.outcomes.shape[0] != manifest.examples:
        raise ValueError(f"{path} has {examples.outcomes.shape[0]} examples, the manifest says {manifest.examples}")
    return TrainingData(manifest, _read_games(directory / GAMES), examples)


def _choices(path: Path, table: np.ndarray) -> Examples:
    decisions, chosen = table[:, 0].astype(np.int64), table[:, -1]
    count = int(decisions[-1]) + 1 if len(decisions) else 0
    candidates = len(decisions) // count if count else 0
    if not np.array_equal(decisions, np.repeat(np.arange(count), candidates)):
        raise ValueError(
            f"{path} does not number its decisions from 0 in order with the same number of candidates in each, "
            "which is all the trainer reads yet"
        )
    chosen = chosen.reshape(count, candidates)
    if not (((chosen == 0) | (chosen == 1)).all() and (chosen.sum(axis=1) == 1).all()):
        raise ValueError(f"{path} has a decision that does not choose exactly one candidate")
    features = table[:, 1:-1].reshape(count, candidates, table.shape[1] - 2)
    return Examples(_tensor(features), _whole(chosen.argmax(axis=1)))


def _tensor(values: np.ndarray) -> torch.Tensor:
    return torch.tensor(values, dtype=torch.float64)


def _whole(values: np.ndarray) -> torch.Tensor:
    return torch.tensor(values.astype(np.int64), dtype=torch.int64)


def write_training_data(directory: Path, data: TrainingData) -> None:
    directory.mkdir(parents=True, exist_ok=True)
    write_training_data_manifest(directory / MANIFEST, data.manifest)
    features, outcomes = data.examples.features.numpy(), data.examples.outcomes.numpy()
    if data.manifest.model.distribution.chooses_among_candidates():
        decisions, candidates = int(data.examples.features.shape[0]), int(data.examples.features.shape[1])
        chosen = (np.arange(candidates)[None, :] == outcomes[:, None]).astype(np.int64)
        table = np.column_stack(
            [np.repeat(np.arange(decisions), candidates), features.reshape(decisions * candidates, -1), chosen.ravel()]
        )
        header, formats = [DECISION, *data.manifest.features, OUTCOME], ["%d"]
    else:
        table = np.column_stack([features, outcomes])
        header, formats = [*data.manifest.features, OUTCOME], []
    with (directory / EXAMPLES).open("w", encoding="utf-8", newline="") as file:
        np.savetxt(
            file,
            table,
            delimiter=",",
            fmt=formats + ["%.17g"] * len(data.manifest.features) + ["%d"],
            header=",".join(header),
            comments="",
            newline="\n",
        )
    with (directory / GAMES).open("w", encoding="utf-8", newline="") as file:
        writer = csv.writer(file, lineterminator="\n")
        writer.writerow(GAMES_HEADER)
        writer.writerows([g.id, utc_to_text(g.start_utc), g.examples] for g in data.games)


def _read_games(path: Path) -> tuple[Game, ...]:
    _require_header(path, GAMES_HEADER)
    with path.open(encoding="utf-8", newline="") as file:
        rows = list(csv.reader(file))[1:]
    if any(len(row) != len(GAMES_HEADER) for row in rows):
        raise ValueError(f"{path} has a row without {len(GAMES_HEADER)} columns")
    return tuple(Game(row[0], utc_from_text(row[1]), int(row[2])) for row in rows)


def _require_header(path: Path, expected: list[str]) -> None:
    with path.open(encoding="utf-8", newline="") as file:
        header = next(csv.reader(file))
    if header != expected:
        raise ValueError(f"The header of {path} is {header}, expected {expected}")
