import math
import sys
from dataclasses import astuple, dataclass, fields
from pathlib import Path

import numpy as np
import polars as pl

from gamegraph.audit.buckets import assign, cuts, outcome_prefixes
from gamegraph.audit.runs import BUCKETS, csv_name, latest
from gamegraph.models.contract import CategoricalDistribution, TrainingDataManifest
from gamegraph.models.contract_files import read_training_data_manifest
from gamegraph.models.training_data import EXAMPLES, GAMES, MANIFEST, OUTCOME

GAME = "game"
GAMES_FILE = "games.parquet"
SIDES = ("real", "simulated")
REACHES = "reaches"
FEWEST_EXPECTED = 5
LARGE_Z = 3.0
CHANCE_OF_LARGE_Z = 0.0027
WORST_SHOWN = 5
QUARTILES = (0.25, 0.5, 0.75)
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


@dataclass(frozen=True)
class Failure:
    node: str
    feature: str
    lowest: float
    highest: float
    measure: str
    reference: float
    simulated: float
    z: float


type Sides = tuple[pl.DataFrame, pl.DataFrame]


def audit(run: Path, training_data: Path) -> None:
    games = pl.read_parquet(run / GAMES_FILE)
    files = sorted(f for f in run.glob("*.parquet") if f.name != GAMES_FILE)
    if not files:
        raise ValueError(f"No simulated decisions under {run}")
    output = run / BUCKETS
    failures: list[Failure] = []
    manifests: dict[str, TrainingDataManifest] = {}
    examples: dict[str, Sides] = {}
    sampling: list[dict[str, float | str | None]] = []
    for file in files:
        manifest = read_training_data_manifest(training_data / file.stem / MANIFEST)
        real = _real(training_data / file.stem, manifest, games.height)
        simulated = pl.read_parquet(file).select(GAME, *manifest.features, OUTCOME)
        manifests[manifest.node], examples[manifest.node] = manifest, (real, simulated)
        (output / manifest.node).mkdir(parents=True, exist_ok=True)
        tables = {REACHES: _table(manifest, real, simulated, None, games.height)}
        tables |= {f: _table(manifest, real, simulated, f, games.height) for f in manifest.features}
        node_failures: list[Failure] = []
        for name, table in tables.items():
            table.write_csv(output / manifest.node / csv_name(run, manifest.node, name))
            node_failures += _failures(manifest.node, name, table)
        _summarise(manifest.node, sum(_z_scores(t) for t in tables.values()), node_failures)
        failures += node_failures
        sampling += _sampling(manifest, pl.read_parquet(file))
    failures += _games(run, games, manifests, examples)
    failures += _sampled(run, pl.DataFrame(sampling))
    table = pl.DataFrame([astuple(f) for f in failures], schema=[f.name for f in fields(Failure)], orient="row")
    table.sort(pl.col("z").abs(), descending=True).write_csv(output / csv_name(run, "failures"))


def _real(directory: Path, manifest: TrainingDataManifest, game_count: int) -> pl.DataFrame:
    schema = {f: pl.Float64 for f in manifest.features} | {OUTCOME: pl.Int64}
    real = pl.read_csv(directory / EXAMPLES, schema=schema)
    counts = pl.read_csv(directory / GAMES)["examples"].to_numpy()
    if len(counts) != game_count or counts.sum() != real.height:
        raise ValueError(f"{directory} has {len(counts)} games and {real.height} examples, not {game_count} games")
    return real.with_columns(pl.Series(GAME, np.repeat(np.arange(len(counts)), counts)))


def _table(
    manifest: TrainingDataManifest, real: pl.DataFrame, simulated: pl.DataFrame, feature: str | None, game_count: int
) -> pl.DataFrame:
    if feature is None:
        sides = (real.with_columns(bucket=pl.lit(0)), simulated.with_columns(bucket=pl.lit(0)))
    else:
        edges = cuts(real[feature].to_numpy())
        sides = (
            real.with_columns(bucket=pl.Series(assign(real[feature].to_numpy(), edges))),
            simulated.with_columns(bucket=pl.Series(assign(simulated[feature].to_numpy(), edges))),
        )
    table = _reaches(sides, game_count).join(_outcomes(manifest, sides), on="bucket", how="left")
    if feature is not None:
        bounds = pl.concat([s.select("bucket", feature) for s in sides]).group_by("bucket")
        table = bounds.agg(lowest=pl.col(feature).min(), highest=pl.col(feature).max()).join(table, on="bucket")
    return table.sort("bucket").drop("bucket")


def _reaches(sides: Sides, game_count: int) -> pl.DataFrame:
    real, simulated = (s.group_by("bucket", GAME).len(name=side) for s, side in zip(sides, SIDES, strict=True))
    per_game = (
        real.join(simulated, on=["bucket", GAME], how="full", coalesce=True)
        .fill_null(0)
        .with_columns(pl.col(*SIDES).cast(pl.Int64))
        .with_columns(difference=pl.col("simulated") - pl.col("real"))
    )
    sums = per_game.group_by("bucket").agg(
        pl.col("real").sum(),
        pl.col("simulated").sum(),
        pl.col("difference").sum(),
        (pl.col("difference") ** 2).sum().alias("square"),
    )
    difference = pl.col("difference") / game_count
    variance = (pl.col("square") - pl.col("difference") ** 2 / game_count) / (game_count - 1)
    return sums.select(
        "bucket",
        (pl.col("real") / game_count).alias(f"{REACHES} real"),
        (pl.col("simulated") / game_count).alias(f"{REACHES} simulated"),
        difference.alias(f"{REACHES} difference"),
        pl.when(variance > 0).then(difference / (variance / game_count).sqrt()).alias(f"{REACHES} z"),
    )


def _outcomes(manifest: TrainingDataManifest, sides: Sides) -> pl.DataFrame:
    prefixes = outcome_prefixes(manifest.model.distribution)
    if prefixes is None:
        return _means(sides)
    real, simulated = (
        s.group_by("bucket").agg(
            pl.len().alias(f"{side} n"),
            *((pl.col(OUTCOME) == k).sum().alias(f"{side} {prefix}count") for prefix, k in prefixes.items()),
        )
        for s, side in zip(sides, SIDES, strict=True)
    )
    counts = real.join(simulated, on="bucket", how="full", coalesce=True).fill_null(0)
    columns: list[pl.Expr] = []
    for prefix in prefixes:
        shares = [pl.col(f"{side} {prefix}count") / pl.col(f"{side} n") for side in SIDES]
        pooled = sum(pl.col(f"{side} {prefix}count") for side in SIDES) / sum(pl.col(f"{side} n") for side in SIDES)
        fewest = pl.min_horizontal(pl.col("real n"), pl.col("simulated n"))
        enough = (fewest * pooled >= FEWEST_EXPECTED) & (fewest * (1 - pooled) >= FEWEST_EXPECTED)
        error = (pooled * (1 - pooled) * (1 / pl.col("real n") + 1 / pl.col("simulated n"))).sqrt()
        columns += _compared(prefix, shares[0], shares[1], error, enough)
    return counts.select("bucket", *columns)


def _means(sides: Sides) -> pl.DataFrame:
    real, simulated = (
        s.group_by("bucket").agg(
            pl.len().alias(f"{side} n"),
            pl.col(OUTCOME).mean().alias(f"{side} mean"),
            pl.col(OUTCOME).var().alias(f"{side} variance"),
        )
        for s, side in zip(sides, SIDES, strict=True)
    )
    error = sum((pl.col(f"{side} variance") / pl.col(f"{side} n") for side in SIDES), pl.lit(0.0)).sqrt()
    enough = pl.min_horizontal(pl.col("real n"), pl.col("simulated n")) >= FEWEST_EXPECTED
    return real.join(simulated, on="bucket", how="full", coalesce=True).select(
        "bucket", *_compared("mean ", pl.col("real mean"), pl.col("simulated mean"), error, enough)
    )


def _compared(prefix: str, real: pl.Expr, simulated: pl.Expr, error: pl.Expr, enough: pl.Expr) -> list[pl.Expr]:
    return [
        real.alias(f"{prefix}real"),
        simulated.alias(f"{prefix}simulated"),
        (simulated - real).alias(f"{prefix}difference"),
        pl.when(enough & (error > 0)).then((simulated - real) / error).alias(f"{prefix}z"),
    ]


def _sampling(manifest: TrainingDataManifest, simulated: pl.DataFrame) -> list[dict[str, float | str | None]]:
    prefixes = outcome_prefixes(manifest.model.distribution)
    if prefixes is None:
        outcomes = [int(c[1:]) for c in simulated.columns if c.startswith("p") and c[1:].isdigit()]
        mean = pl.sum_horizontal([pl.col(f"p{k}") * k for k in outcomes])
        square = pl.sum_horizontal([pl.col(f"p{k}") * k * k for k in outcomes])
        sums = simulated.select(
            pl.col(OUTCOME).sum().alias("observed"),
            mean.sum().alias("expected"),
            (square - mean**2).sum().alias("variance"),
        )
        return [_sampled_row(manifest.node, "mean", simulated.height, sums.row(0, named=True))]
    rows: list[dict[str, float | str | None]] = []
    for prefix, k in prefixes.items():
        probability = pl.col(f"p{k}")
        sums = simulated.select(
            (pl.col(OUTCOME) == k).sum().alias("observed"),
            probability.sum().alias("expected"),
            (probability * (1 - probability)).sum().alias("variance"),
        )
        rows.append(_sampled_row(manifest.node, prefix.strip() or "share", simulated.height, sums.row(0, named=True)))
    return rows


def _sampled_row(node: str, measure: str, n: int, sums: dict[str, float]) -> dict[str, float | str | None]:
    observed, expected, variance = sums["observed"], sums["expected"], sums["variance"]
    return {
        "node": node,
        "measure": measure,
        "n": n,
        "predicted": expected / n,
        "simulated": observed / n,
        "difference": (observed - expected) / n,
        "z": (observed - expected) / math.sqrt(variance) if variance > 0 else None,
    }


def _sampled(run: Path, sampling: pl.DataFrame) -> list[Failure]:
    sampling.write_csv(run / BUCKETS / csv_name(run, "sampling"))
    failures = [
        Failure(r["node"], "sampling", math.nan, math.nan, r["measure"], r["predicted"], r["simulated"], r["z"])
        for r in sampling.filter(pl.col("z").abs() > LARGE_Z).iter_rows(named=True)
    ]
    _summarise("sampling", int(sampling["z"].is_not_null().sum()), failures)
    return failures


def _games(
    run: Path, games: pl.DataFrame, manifests: dict[str, TrainingDataManifest], examples: dict[str, Sides]
) -> list[Failure]:
    values = _per_game(games, manifests, examples)
    rows = [_statistic(name, real, simulated) for name, (real, simulated) in values.items()]
    statistics = pl.DataFrame(rows)
    statistics.write_csv(run / BUCKETS / csv_name(run, "games"))
    failures = [
        Failure("games", r["statistic"], math.nan, math.nan, "mean", r["real"], r["simulated"], r["z"])
        for r in statistics.filter(pl.col("z").abs() > LARGE_Z).iter_rows(named=True)
    ]
    checked = int(statistics["z"].is_not_null().sum())
    for name, width in BIN_WIDTHS.items():
        distribution = _distribution(*values[name], width)
        distribution.write_csv(run / BUCKETS / csv_name(run, name.replace(" ", "-")))
        failures += _failures("games", name, distribution)
        checked += _z_scores(distribution)
    _summarise("games", checked, failures)
    return failures


def _per_game(
    games: pl.DataFrame, manifests: dict[str, TrainingDataManifest], examples: dict[str, Sides]
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
    real_periods, simulated_periods = (games[f"{side} periods"].to_numpy() for side in SIDES)
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
    return np.bincount(selected[GAME].to_numpy(), minlength=game_count)


def _outcome_value(outcome: str | int, manifest: TrainingDataManifest) -> int:
    if isinstance(outcome, int):
        return outcome
    distribution = manifest.model.distribution
    if not isinstance(distribution, CategoricalDistribution):
        raise ValueError(f"{manifest.node} has no categories, so it has no outcome {outcome}")
    return distribution.categories.index(outcome)


def _statistic(name: str, real: np.ndarray, simulated: np.ndarray) -> dict[str, float | str | None]:
    difference = simulated - real
    error = difference.std(ddof=1) / math.sqrt(len(difference))
    row: dict[str, float | str | None] = {
        "statistic": name,
        "real": float(real.mean()),
        "simulated": float(simulated.mean()),
        "difference": float(difference.mean()),
        "z": float(difference.mean() / error) if error > 0 else None,
        "real sd": float(real.std(ddof=1)),
        "simulated sd": float(simulated.std(ddof=1)),
    }
    for q in QUARTILES:
        row[f"q{q:g} real"] = float(np.quantile(real, q))
        row[f"q{q:g} simulated"] = float(np.quantile(simulated, q))
    return row


def _distribution(real: np.ndarray, simulated: np.ndarray, width: int) -> pl.DataFrame:
    lowest = min(real.min(), simulated.min()) // width * width
    bins = [np.bincount((v - lowest) // width) for v in (real, simulated)]
    size = max(len(b) for b in bins)
    real_counts, simulated_counts = (np.pad(b, (0, size - len(b))) for b in bins)
    games = len(real)
    pooled = (real_counts + simulated_counts) / (2 * games)
    error = np.sqrt(pooled * (1 - pooled) * 2 / games)
    enough = (games * pooled >= FEWEST_EXPECTED) & (games * (1 - pooled) >= FEWEST_EXPECTED) & (error > 0)
    z = np.where(enough, (simulated_counts - real_counts) / games / np.where(error > 0, error, 1), np.nan)
    starts = lowest + width * np.arange(size)
    return pl.DataFrame(
        {
            "lowest": starts.astype(np.float64),
            "highest": (starts + width - 1).astype(np.float64),
            "real": real_counts / games,
            "simulated": simulated_counts / games,
            "difference": (simulated_counts - real_counts) / games,
            "z": z,
        }
    ).with_columns(pl.col("z").fill_nan(None))


def _z_scores(table: pl.DataFrame) -> int:
    return table.select(pl.col("^.*z$").is_not_null().sum()).sum_horizontal().item()


def _failures(node: str, feature: str, table: pl.DataFrame) -> list[Failure]:
    failures: list[Failure] = []
    for column in (c for c in table.columns if c.endswith("z")):
        prefix = column[: -len("z")]
        for row in table.filter(pl.col(column).abs() > LARGE_Z).iter_rows(named=True):
            failures.append(
                Failure(
                    node,
                    feature,
                    row.get("lowest", math.nan),
                    row.get("highest", math.nan),
                    prefix.strip() or "share",
                    row[f"{prefix}real"],
                    row[f"{prefix}simulated"],
                    row[column],
                )
            )
    return failures


def _summarise(node: str, checked: int, failures: list[Failure]) -> None:
    print(
        f"{node}: {checked} z-scores, |z| > {LARGE_Z:g}: {len(failures)} ({checked * CHANCE_OF_LARGE_Z:.1f} by chance)"
    )
    for f in sorted(failures, key=lambda f: -abs(f.z))[:WORST_SHOWN]:
        print(
            f"  {f.feature}{_range(f.lowest, f.highest)}, {f.measure}: "
            f"{f.reference:.4g} against simulated {f.simulated:.4g}, z {f.z:.1f}"
        )


def _range(lowest: float, highest: float) -> str:
    if math.isnan(lowest):
        return ""
    return f" {lowest:g}" if lowest == highest else f" {lowest:g} to {highest:g}"


def main() -> None:
    if len(sys.argv) != 3:
        raise SystemExit("Usage: gamegraph-simulation <simulation audits> <training data>")
    audit(latest(Path(sys.argv[1])), Path(sys.argv[2]))
