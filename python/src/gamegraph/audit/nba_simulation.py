import math
import sys
from dataclasses import dataclass
from pathlib import Path

import numpy as np
import polars as pl

from gamegraph.audit import findings, simulation
from gamegraph.audit.findings import LARGE_Z, Finding
from gamegraph.audit.runs import BUCKETS, csv_name, latest
from gamegraph.contract import CategoricalDistribution, TrainingDataManifest
from gamegraph.files.training_data_directory import OUTCOME

GAMES_FILE = "games.parquet"
PLAYERS_FILE = "players.parquet"
GAMES_CHECK = "games"
PLAYERS_CHECK = "players"
SECONDS_WIDTH = 300
PLAYER_GROUPS = (
    ("starters started", 1, 1),
    ("starters benched", 1, 0),
    ("bench started", 0, 1),
    ("bench benched", 0, 0),
)
PLAYER_COUNTS = (
    "points",
    "attempts",
    "threes",
    "free throws",
    "rebounds",
    "assists",
    "steals",
    "blocks",
    "turnovers",
    "fouls",
)
FOUL_OUT = 6
REGULATION_PERIODS = 4
BIN_WIDTHS = {"total points": 10, "home margin": 5}


@dataclass(frozen=True)
class Count:
    name: str
    node: str
    outcome: str | int | None = None
    feature: tuple[str, float] | None = None


COUNTS = (
    Count("two-point attempts", "ActionType", "TwoPointAttempt"),
    Count("three-point attempts", "ActionType", "ThreePointAttempt"),
    Count("turnovers", "ActionType", "Turnover"),
    Count("non-shooting fouls", "ActionType", "NonShootingFoul"),
    Count("shooting fouls", "FouledOnShot", 1),
    Count("two-point makes", "ShotMade", 1, ("ShotIsThree", 0)),
    Count("three-point makes", "ShotMade", 1, ("ShotIsThree", 1)),
    Count("free throws", "FreeThrowMade"),
    Count("free throws made", "FreeThrowMade", 1),
    Count("offensive rebounds", "OffensiveRebound", 1),
    Count("awarded free throws", "AwardedFreeThrowMade"),
)


def audit(run: Path, training_data: Path) -> None:
    games = pl.read_parquet(run / GAMES_FILE)
    files = sorted(f for f in run.glob("*.parquet") if f.name not in (GAMES_FILE, PLAYERS_FILE))
    failures, manifests, examples = simulation.node_failures(run, training_data, files, games.height)
    failures += _games(run, games, manifests, examples)
    failures += _players(run, pl.read_parquet(run / PLAYERS_FILE))
    findings.write(run / BUCKETS / csv_name(run, "failures"), failures)


def _games(
    run: Path, games: pl.DataFrame, manifests: dict[str, TrainingDataManifest], examples: dict[str, simulation.Sides]
) -> list[Finding]:
    values = _per_game(games, manifests, examples)
    rows = [simulation.statistic(name, real, simulated) for name, (real, simulated) in values.items()]
    statistics = pl.DataFrame(rows)
    statistics.write_csv(run / BUCKETS / csv_name(run, "games"))
    failures = [
        Finding(GAMES_CHECK, GAMES_CHECK, r["statistic"], math.nan, math.nan, "mean", r["real"], r["simulated"], r["z"])
        for r in statistics.filter(pl.col("z").abs() > LARGE_Z).iter_rows(named=True)
    ]
    checked = int(statistics["z"].is_not_null().sum())
    for name, width in BIN_WIDTHS.items():
        distribution = simulation.distribution(*values[name], width)
        distribution.write_csv(run / BUCKETS / csv_name(run, name.replace(" ", "-")))
        failures += findings.failures(GAMES_CHECK, GAMES_CHECK, name, distribution, *simulation.SIDES)
        checked += findings.z_scores(distribution)
    findings.summarise(GAMES_CHECK, checked, failures, *simulation.SIDES)
    return failures


def _players(run: Path, players: pl.DataFrame) -> list[Finding]:
    rows: list[dict[str, float | str | None]] = []
    errors: dict[str, float] = {}
    for name, real_started, simulated_started in PLAYER_GROUPS:
        group = players.filter(
            (pl.col("real started") == real_started) & (pl.col("simulated started") == simulated_started)
        )
        real, simulated = (group[f"{side} seconds"].to_numpy() for side in simulation.SIDES)
        rows.append(simulation.statistic(name, real, simulated))
        errors[name] = float(np.abs(simulated - real).mean())
    for count in PLAYER_COUNTS:
        real, simulated = (players[f"{side} {count}"].to_numpy() for side in simulation.SIDES)
        rows.append(simulation.statistic(count, real, simulated))
        errors[count] = float(np.abs(simulated - real).mean())
    real, simulated = ((players[f"{side} fouls"] >= FOUL_OUT).cast(pl.Float64).to_numpy() for side in simulation.SIDES)
    rows.append(simulation.statistic("fouled out", real, simulated))
    errors["fouled out"] = float(np.abs(simulated - real).mean())
    statistics = pl.DataFrame(rows)
    statistics.write_csv(run / BUCKETS / csv_name(run, PLAYERS_CHECK))
    failures = [
        Finding(
            PLAYERS_CHECK, PLAYERS_CHECK, r["statistic"], math.nan, math.nan, "mean", r["real"], r["simulated"], r["z"]
        )
        for r in statistics.filter(pl.col("z").abs() > LARGE_Z).iter_rows(named=True)
    ]
    checked = int(statistics["z"].is_not_null().sum())
    real_seconds, simulated_seconds = (players[f"{side} seconds"].to_numpy() for side in simulation.SIDES)
    distribution = simulation.distribution(real_seconds, simulated_seconds, SECONDS_WIDTH)
    distribution.write_csv(run / BUCKETS / csv_name(run, "player-seconds"))
    failures += findings.failures(PLAYERS_CHECK, PLAYERS_CHECK, "seconds", distribution, *simulation.SIDES)
    checked += findings.z_scores(distribution)
    findings.summarise(PLAYERS_CHECK, checked, failures, *simulation.SIDES)
    for row in rows:
        name = str(row["statistic"])
        error = errors[name]
        print(f"  {name}: real {row['real']:.4g}, simulated {row['simulated']:.4g}, mean absolute error {error:.4g}")
    return failures


def _per_game(
    games: pl.DataFrame, manifests: dict[str, TrainingDataManifest], examples: dict[str, simulation.Sides]
) -> dict[str, tuple[np.ndarray, np.ndarray]]:
    values: dict[str, tuple[np.ndarray, np.ndarray]] = {}
    for name, column in (
        ("home points", "home points"),
        ("away points", "away points"),
        ("possessions", "possessions"),
    ):
        values[name] = (games[f"real {column}"].to_numpy(), games[f"simulated {column}"].to_numpy())
    home, away = values["home points"], values["away points"]
    values["total points"] = (home[0] + away[0], home[1] + away[1])
    values["home margin"] = (home[0] - away[0], home[1] - away[1])
    real_periods, simulated_periods = (games[f"{side} periods"].to_numpy() for side in simulation.SIDES)
    values["overtime"] = (
        (real_periods > REGULATION_PERIODS).astype(np.int64),
        (simulated_periods > REGULATION_PERIODS).astype(np.int64),
    )
    for count in COUNTS:
        real, simulated = (
            _counted(count, manifests[count.node], frame, games.height) for frame in examples[count.node]
        )
        values[count.name] = (real, simulated)
    return values


def _counted(count: Count, manifest: TrainingDataManifest, examples: pl.DataFrame, game_count: int) -> np.ndarray:
    selected = examples
    if count.outcome is not None:
        selected = selected.filter(pl.col(OUTCOME) == _outcome_value(count.outcome, manifest))
    if count.feature is not None:
        selected = selected.filter(pl.col(count.feature[0]) == count.feature[1])
    return np.bincount(selected[simulation.GAME].to_numpy(), minlength=game_count)


def _outcome_value(outcome: str | int, manifest: TrainingDataManifest) -> int:
    if isinstance(outcome, int):
        return outcome
    distribution = manifest.model.distribution
    if not isinstance(distribution, CategoricalDistribution):
        raise ValueError(f"{manifest.node} has no categories, so it has no outcome {outcome}")
    return distribution.categories.index(outcome)


def main() -> None:
    if len(sys.argv) != 3:
        raise SystemExit("Usage: gamegraph-simulation <simulation audits> <training data>")
    audit(latest(Path(sys.argv[1])), Path(sys.argv[2]))
