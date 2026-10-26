import numpy as np
import polars as pl

from gamegraph.audit.buckets import Means, Shares, assign, comparison, cuts
from gamegraph.audit.findings import FEWEST_EXPECTED, Z
from gamegraph.contract import TrainingDataManifest
from gamegraph.files.training_data_directory import OUTCOME

ALL = "all"
OBSERVED = "observed"
PREDICTED = "predicted"
QUARTILES = (0.25, 0.5, 0.75)


def tables(manifest: TrainingDataManifest, scored: pl.DataFrame) -> dict[str, pl.DataFrame]:
    found = {ALL: _table(manifest, scored.with_columns(bucket=pl.lit(0)), None)}
    for feature in manifest.features:
        values = scored[feature].to_numpy()
        found[feature] = _table(manifest, scored.with_columns(bucket=pl.Series(assign(values, cuts(values)))), feature)
    return found


def outcomes(scored: pl.DataFrame) -> list[int]:
    return sorted(int(c[1:]) for c in scored.columns if c.startswith("p") and c[1:].isdigit())


def _table(manifest: TrainingDataManifest, bucketed: pl.DataFrame, feature: str | None) -> pl.DataFrame:
    sizes = [pl.len().alias("n")]
    if feature is not None:
        sizes += [pl.col(feature).min().alias("lowest"), pl.col(feature).max().alias("highest")]
    match comparison(manifest.model.distribution):
        case Shares(prefixes=prefixes):
            compared = _shares(bucketed, prefixes)
        case Means():
            compared = _times(bucketed)
    return bucketed.group_by("bucket").agg(sizes).join(compared, on="bucket").sort("bucket").drop("bucket")


def _shares(bucketed: pl.DataFrame, prefixes: dict[str, int]) -> pl.DataFrame:
    sums = bucketed.group_by("bucket").agg(
        pl.len().alias("count"),
        *(
            expression
            for prefix, k in prefixes.items()
            for expression in (
                (pl.col(OUTCOME) == k).sum().alias(f"{prefix}{OBSERVED}"),
                pl.col(f"p{k}").sum().alias(f"{prefix}{PREDICTED}"),
                (pl.col(f"p{k}") * (1 - pl.col(f"p{k}"))).sum().alias(f"{prefix}variance"),
            )
        ),
    )
    return sums.select(
        "bucket",
        *(expression for prefix in prefixes for expression in _compared(prefix, _enough_expected(prefix))),
    )


def _enough_expected(prefix: str) -> pl.Expr:
    predicted = pl.col(f"{prefix}{PREDICTED}")
    return (predicted >= FEWEST_EXPECTED) & (pl.col("count") - predicted >= FEWEST_EXPECTED)


def _times(bucketed: pl.DataFrame) -> pl.DataFrame:
    values = outcomes(bucketed)
    mean = pl.sum_horizontal([pl.col(f"p{k}") * k for k in values])
    square = pl.sum_horizontal([pl.col(f"p{k}") * k * k for k in values])
    sums = (
        bucketed.with_columns(mean.alias("mean"), (square - mean * mean).alias("variance"))
        .group_by("bucket")
        .agg(
            pl.len().alias("count"),
            pl.col(OUTCOME).sum().alias(f"mean {OBSERVED}"),
            pl.col("mean").sum().alias(f"mean {PREDICTED}"),
            pl.col("variance").sum().alias("mean variance"),
            *(pl.col(OUTCOME).quantile(q, "lower").alias(f"q{q:g} {OBSERVED}") for q in QUARTILES),
            *(pl.col(f"p{k}").sum().alias(f"total p{k}") for k in values),
        )
    )
    probabilities = sums.select(f"total p{k}" for k in values).to_numpy()
    cumulative = np.cumsum(probabilities, axis=1) / probabilities.sum(axis=1, keepdims=True)
    predicted_quartiles = {
        f"q{q:g} {PREDICTED}": np.array(values)[np.argmax(cumulative >= q, axis=1)] for q in QUARTILES
    }
    return sums.with_columns(**{name: pl.Series(v) for name, v in predicted_quartiles.items()}).select(
        "bucket",
        *_compared("mean ", pl.col("count") >= FEWEST_EXPECTED),
        *(pl.col(f"q{q:g} {side}") for q in QUARTILES for side in (OBSERVED, PREDICTED)),
    )


def _compared(prefix: str, enough: pl.Expr) -> tuple[pl.Expr, ...]:
    count = pl.col("count")
    observed, predicted = pl.col(f"{prefix}{OBSERVED}"), pl.col(f"{prefix}{PREDICTED}")
    variance = pl.col(f"{prefix}variance")
    return (
        (observed / count).alias(f"{prefix}{OBSERVED}"),
        (predicted / count).alias(f"{prefix}{PREDICTED}"),
        ((observed - predicted) / count).alias(f"{prefix}difference"),
        pl.when(enough & (variance > 0)).then((observed - predicted) / variance.sqrt()).alias(f"{prefix}{Z}"),
    )
