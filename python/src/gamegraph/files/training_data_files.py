import csv
from pathlib import Path

import numpy as np
import torch

from gamegraph.contract import ChoiceDistribution, TrainingDataManifest, utc_from_text, utc_to_text
from gamegraph.files.contract_files import read_training_data_manifest, write_training_data_manifest
from gamegraph.training.training_data import Examples, Game, TrainingData

MANIFEST = "manifest.json"
EXAMPLES = "examples.csv"
GAMES = "games.csv"
DECISION = "decision"
OUTCOME = "outcome"
GAMES_HEADER = ["game", "startUtc", "examples"]


def read_training_data(directory: Path) -> TrainingData:
    manifest = read_training_data_manifest(directory / MANIFEST)
    path = directory / EXAMPLES
    choice = manifest.model.distribution.chooses_among_candidates()
    _require_header(path, [*([DECISION] if choice else []), *manifest.features, OUTCOME])
    table = np.loadtxt(path, delimiter=",", skiprows=1, ndmin=2, dtype=np.float64)
    outcomes = table[:, -1]
    if not np.array_equal(outcomes, np.round(outcomes)):
        raise ValueError(f"{path} has an outcome that is not a whole number")
    examples = (
        _choices(path, table, _size_column(manifest)) if choice else Examples(_tensor(table[:, :-1]), _whole(outcomes))
    )
    if examples.outcomes.shape[0] != manifest.examples:
        raise ValueError(f"{path} has {examples.outcomes.shape[0]} examples, the manifest says {manifest.examples}")
    return TrainingData(manifest, _read_games(directory / GAMES), examples)


def _choices(path: Path, table: np.ndarray, size_column: int) -> Examples:
    decisions, chosen = table[:, 0].astype(np.int64), table[:, -1].astype(np.int64)
    count = int(decisions[-1]) + 1 if len(decisions) else 0
    if len(decisions) and not (decisions[0] == 0 and np.isin(np.diff(decisions), (0, 1)).all()):
        raise ValueError(f"{path} does not number its decisions from 0 in order")
    candidates = np.bincount(decisions, minlength=count)
    if (candidates > ChoiceDistribution.MOST_CANDIDATES).any():
        raise ValueError(f"{path} has a decision with more than {ChoiceDistribution.MOST_CANDIDATES} candidates")
    starts = np.concatenate([[0], np.cumsum(candidates)[:-1]])
    position = np.arange(len(decisions)) - starts[decisions]
    sizes = table[:, 1 + size_column]
    if not ((chosen == 0) | (chosen == 1)).all():
        raise ValueError(f"{path} has a candidate whose outcome is not 0 or 1")
    if not np.array_equal(sizes, np.bincount(decisions, weights=chosen, minlength=count)[decisions]):
        raise ValueError(f"{path} has a decision that does not choose as many candidates as its size")
    features = np.zeros((count, int(candidates.max(initial=0)), table.shape[1] - 2))
    features[decisions, position] = table[:, 1:-1]
    outcomes = np.bincount(decisions, weights=chosen << position, minlength=count)
    return Examples(_tensor(features), _whole(outcomes))


def _size_column(manifest: TrainingDataManifest) -> int:
    return manifest.features.index(manifest.model.given[ChoiceDistribution.SIZE])


def _tensor(values: np.ndarray) -> torch.Tensor:
    return torch.tensor(values, dtype=torch.float64)


def _whole(values: np.ndarray) -> torch.Tensor:
    return torch.tensor(values.astype(np.int64), dtype=torch.int64)


def write_training_data(directory: Path, data: TrainingData) -> None:
    directory.mkdir(parents=True, exist_ok=True)
    write_training_data_manifest(directory / MANIFEST, data.manifest)
    features, outcomes = data.examples.features.numpy(), data.examples.outcomes.numpy()
    if data.manifest.model.distribution.chooses_among_candidates():
        decisions, candidates = int(features.shape[0]), int(features.shape[1])
        real = features[:, :, _size_column(data.manifest)] > 0
        chosen = (outcomes[:, None] >> np.arange(candidates)[None, :]) & 1
        decision = np.broadcast_to(np.arange(decisions)[:, None], (decisions, candidates))
        table = np.column_stack([decision[real], features[real], chosen[real]])
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
