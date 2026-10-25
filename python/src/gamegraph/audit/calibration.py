import sys
from dataclasses import astuple, dataclass, fields
from pathlib import Path

import numpy as np
import polars as pl

from gamegraph.models.contract import (
    BernoulliDistribution,
    CategoricalDistribution,
    DiscreteJohnsonSuDistribution,
    TrainingDataManifest,
)
from gamegraph.models.contract_files import read_training_data_manifest
from gamegraph.models.training_data import MANIFEST, OUTCOME

MOST_VALUES_FOR_CATEGORIES = 30
QUANTILE_BUCKETS = 20
FEWEST_EXPECTED = 5
LARGE_Z = 3.0
CHANCE_OF_LARGE_Z = 0.0027
WORST_SHOWN = 5
QUARTILES = (0.25, 0.5, 0.75)
BUCKETS = "buckets"
FAILURES = "failures"


@dataclass(frozen=True)
class Failure:
    node: str
    feature: str
    lowest: float
    highest: float
    outcome: str
    n: int
    observed: float
    predicted: float
    z: float


def audit(run: Path, training_data: Path) -> None:
    files = sorted(run.glob("*.parquet"))
    if not files:
        raise ValueError(f"No calibration files under {run}")
    failures: list[Failure] = []
    for file in files:
        manifest = read_training_data_manifest(training_data / file.stem / MANIFEST)
        examples = pl.read_parquet(file)
        output = run / BUCKETS / manifest.node
        output.mkdir(parents=True, exist_ok=True)
        node_failures: list[Failure] = []
        checked = 0
        for feature in manifest.features:
            table = _table(manifest, examples, feature)
            table.write_csv(output / f"{manifest.node}_{feature}_{run.name}.csv")
            checked += table.select(pl.col("^.*z$").is_not_null().sum()).sum_horizontal().item()
            node_failures += _failures(manifest.node, feature, table)
        failures += node_failures
        _summarise(manifest.node, checked, node_failures)
    table = pl.DataFrame([astuple(f) for f in failures], schema=[f.name for f in fields(Failure)], orient="row")
    table.sort(pl.col("z").abs(), descending=True).write_csv(run / BUCKETS / f"{FAILURES}_{run.name}.csv")


def _table(manifest: TrainingDataManifest, examples: pl.DataFrame, feature: str) -> pl.DataFrame:
    values = examples[feature].to_numpy()
    bucketed = examples.with_columns(bucket=pl.Series(_buckets(values)))
    grouped = bucketed.group_by("bucket").agg(
        pl.len().alias("n"), pl.col(feature).min().alias("lowest"), pl.col(feature).max().alias("highest")
    )
    match manifest.model.distribution:
        case BernoulliDistribution():
            outcomes = _shares(bucketed, {"": 1})
        case CategoricalDistribution(categories=categories):
            outcomes = _shares(bucketed, {f"{c} ": k for k, c in enumerate(categories)})
        case DiscreteJohnsonSuDistribution():
            outcomes = _times(bucketed)
    return grouped.join(outcomes, on="bucket").sort("bucket").drop("bucket")


def _buckets(values: np.ndarray) -> np.ndarray:
    distinct = np.unique(values)
    if len(distinct) <= MOST_VALUES_FOR_CATEGORIES:
        return np.searchsorted(distinct, values)
    cuts = np.unique(np.quantile(values, np.linspace(0, 1, QUANTILE_BUCKETS + 1)[1:-1], method="lower"))
    return np.searchsorted(cuts, values, side="right")


def _shares(examples: pl.DataFrame, outcomes: dict[str, int]) -> pl.DataFrame:
    sums = examples.group_by("bucket").agg(
        pl.len().alias("count"),
        *(
            expression
            for prefix, k in outcomes.items()
            for expression in (
                (pl.col(OUTCOME) == k).sum().alias(f"{prefix}observed"),
                pl.col(f"p{k}").sum().alias(f"{prefix}predicted"),
                (pl.col(f"p{k}") * (1 - pl.col(f"p{k}"))).sum().alias(f"{prefix}variance"),
            )
        ),
    )
    return sums.select(
        "bucket",
        *(expression for prefix in outcomes for expression in _compared(prefix, _enough_expected(prefix))),
    )


def _enough_expected(prefix: str) -> pl.Expr:
    predicted = pl.col(f"{prefix}predicted")
    return (predicted >= FEWEST_EXPECTED) & (pl.col("count") - predicted >= FEWEST_EXPECTED)


def _times(examples: pl.DataFrame) -> pl.DataFrame:
    outcomes = sorted(int(c[1:]) for c in examples.columns if c.startswith("p") and c[1:].isdigit())
    mean = pl.sum_horizontal([pl.col(f"p{k}") * k for k in outcomes])
    square = pl.sum_horizontal([pl.col(f"p{k}") * k * k for k in outcomes])
    sums = (
        examples.with_columns(mean.alias("mean"), (square - mean * mean).alias("variance"))
        .group_by("bucket")
        .agg(
            pl.len().alias("count"),
            pl.col(OUTCOME).sum().alias("mean observed"),
            pl.col("mean").sum().alias("mean predicted"),
            pl.col("variance").sum().alias("mean variance"),
            *(pl.col(OUTCOME).quantile(q, "lower").alias(f"q{q:g} observed") for q in QUARTILES),
            *(pl.col(f"p{k}").sum().alias(f"total p{k}") for k in outcomes),
        )
    )
    probabilities = sums.select(f"total p{k}" for k in outcomes).to_numpy()
    cumulative = np.cumsum(probabilities, axis=1) / probabilities.sum(axis=1, keepdims=True)
    predicted_quartiles = {
        f"q{q:g} predicted": np.array(outcomes)[np.argmax(cumulative >= q, axis=1)] for q in QUARTILES
    }
    return sums.with_columns(**{name: pl.Series(values) for name, values in predicted_quartiles.items()}).select(
        "bucket",
        *_compared("mean ", pl.col("count") >= FEWEST_EXPECTED),
        *(pl.col(f"q{q:g} {side}") for q in QUARTILES for side in ("observed", "predicted")),
    )


def _compared(prefix: str, enough: pl.Expr) -> tuple[pl.Expr, ...]:
    count = pl.col("count")
    observed, predicted = pl.col(f"{prefix}observed"), pl.col(f"{prefix}predicted")
    z = (observed - predicted) / pl.col(f"{prefix}variance").sqrt()
    return (
        (observed / count).alias(f"{prefix}observed"),
        (predicted / count).alias(f"{prefix}predicted"),
        ((observed - predicted) / count).alias(f"{prefix}difference"),
        pl.when(enough & (pl.col(f"{prefix}variance") > 0)).then(z).otherwise(None).alias(f"{prefix}z"),
    )


def _failures(node: str, feature: str, table: pl.DataFrame) -> list[Failure]:
    failures: list[Failure] = []
    for column in (c for c in table.columns if c.endswith("z")):
        prefix = column[: -len("z")]
        for row in table.filter(pl.col(column).abs() > LARGE_Z).iter_rows(named=True):
            failures.append(
                Failure(
                    node,
                    feature,
                    row["lowest"],
                    row["highest"],
                    prefix.strip() or "yes",
                    row["n"],
                    row[f"{prefix}observed"],
                    row[f"{prefix}predicted"],
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
            f"  {f.feature} {_range(f.lowest, f.highest)}, {f.outcome}: "
            f"observed {f.observed:.4g}, predicted {f.predicted:.4g}, n {f.n}, z {f.z:.1f}"
        )


def _range(lowest: float, highest: float) -> str:
    return f"{lowest:g}" if lowest == highest else f"{lowest:g} to {highest:g}"


def latest(calibration: Path) -> Path:
    runs = sorted(d for d in calibration.iterdir() if d.is_dir())
    if not runs:
        raise ValueError(f"No calibration audit under {calibration}")
    return runs[-1]


def main() -> None:
    if len(sys.argv) != 3:
        raise SystemExit("Usage: gamegraph-calibration <calibration audits> <training data>")
    audit(latest(Path(sys.argv[1])), Path(sys.argv[2]))
