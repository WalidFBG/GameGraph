import math
from pathlib import Path

import numpy as np
import polars as pl

from gamegraph.audit import findings, predicted
from gamegraph.audit.buckets import Means, Shares, assign, comparison, cuts
from gamegraph.audit.findings import FEWEST_EXPECTED, Finding
from gamegraph.audit.runs import BUCKETS, csv_name
from gamegraph.contract import TrainingDataManifest
from gamegraph.files.contract_documents import read_training_data_manifest
from gamegraph.files.training_data_directory import DECISION, EXAMPLES, GAMES, MANIFEST, OUTCOME

GAME = "game"
SIDES = ("real", "simulated")
REACHES = "reaches"
SIMULATION = "simulation"
SAMPLING = "sampling"
QUARTILES = (0.25, 0.5, 0.75)


type Sides = tuple[pl.DataFrame, pl.DataFrame]


def node_failures(
    run: Path, training_data: Path, files: list[Path], game_count: int
) -> tuple[list[Finding], dict[str, TrainingDataManifest], dict[str, Sides]]:
    if not files:
        raise ValueError(f"No simulated decisions under {run}")
    output = run / BUCKETS
    failures: list[Finding] = []
    manifests: dict[str, TrainingDataManifest] = {}
    examples: dict[str, Sides] = {}
    for file in files:
        manifest = read_training_data_manifest(training_data / file.stem / MANIFEST)
        real = _real(training_data / file.stem, manifest, game_count)
        simulated = pl.read_parquet(file).select(GAME, *manifest.features, OUTCOME)
        manifests[manifest.node], examples[manifest.node] = manifest, (real, simulated)
        (output / manifest.node).mkdir(parents=True, exist_ok=True)
        tables = {REACHES: _table(manifest, real, simulated, None, game_count)}
        tables |= {f: _table(manifest, real, simulated, f, game_count) for f in manifest.features}
        node_failures: list[Finding] = []
        for name, table in tables.items():
            table.write_csv(output / manifest.node / csv_name(run, manifest.node, name))
            node_failures += findings.failures(SIMULATION, manifest.node, name, table, *SIDES)
        findings.summarise(manifest.node, sum(findings.z_scores(t) for t in tables.values()), node_failures, *SIDES)
        failures += node_failures
        failures += _sampling(run, manifest, pl.read_parquet(file))
    return failures, manifests, examples


def _real(directory: Path, manifest: TrainingDataManifest, game_count: int) -> pl.DataFrame:
    choice = manifest.model.distribution.chooses_among_candidates()
    schema = ({DECISION: pl.Int64} if choice else {}) | {f: pl.Float64 for f in manifest.features} | {OUTCOME: pl.Int64}
    real = pl.read_csv(directory / EXAMPLES, schema=schema)
    counts = pl.read_csv(directory / GAMES)["examples"].to_numpy()
    examples = real[DECISION].n_unique() if choice else real.height
    if len(counts) != game_count or counts.sum() != examples:
        raise ValueError(f"{directory} has {len(counts)} games and {examples} examples, not {game_count} games")
    game_of_example = np.repeat(np.arange(len(counts)), counts)
    games = game_of_example[real[DECISION].to_numpy()] if choice else game_of_example
    return real.with_columns(pl.Series(GAME, games)).drop(DECISION, strict=False)


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
    match comparison(manifest.model.distribution):
        case Shares(prefixes=prefixes):
            return _shares(sides, prefixes)
        case Means():
            return _means(sides)


def _shares(sides: Sides, prefixes: dict[str, int]) -> pl.DataFrame:
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


def _sampling(run: Path, manifest: TrainingDataManifest, simulated: pl.DataFrame) -> list[Finding]:
    tables = predicted.tables(manifest, simulated)
    found: list[Finding] = []
    for name, table in tables.items():
        table.write_csv(run / BUCKETS / manifest.node / csv_name(run, manifest.node, name, SAMPLING))
        found += findings.failures(SAMPLING, manifest.node, name, table, predicted.PREDICTED, predicted.OBSERVED)
    checked = sum(findings.z_scores(t) for t in tables.values())
    findings.summarise(f"{manifest.node} sampling", checked, found, predicted.PREDICTED, "sampled")
    return found


def statistic(name: str, real: np.ndarray, simulated: np.ndarray) -> dict[str, float | str | None]:
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


def distribution(real: np.ndarray, simulated: np.ndarray, width: int) -> pl.DataFrame:
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
