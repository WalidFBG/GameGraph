import math
from dataclasses import astuple, dataclass, fields
from pathlib import Path

import polars as pl

FEWEST_EXPECTED = 5
LARGE_Z = 3.0
CHANCE_OF_LARGE_Z = 0.0027
WORST_SHOWN = 5
Z = "z"


@dataclass(frozen=True)
class Finding:
    check: str
    node: str
    table: str
    lowest: float
    highest: float
    measure: str
    expected: float
    actual: float
    z: float


def z_scores(table: pl.DataFrame) -> int:
    return table.select(pl.col(f"^.*{Z}$").is_not_null().sum()).sum_horizontal().item()


def failures(check: str, node: str, name: str, table: pl.DataFrame, expected: str, actual: str) -> list[Finding]:
    found: list[Finding] = []
    for column in (c for c in table.columns if c.endswith(Z)):
        prefix = column[: -len(Z)]
        for row in table.filter(pl.col(column).abs() > LARGE_Z).iter_rows(named=True):
            found.append(
                Finding(
                    check,
                    node,
                    name,
                    row.get("lowest", math.nan),
                    row.get("highest", math.nan),
                    prefix.strip() or "share",
                    row[f"{prefix}{expected}"],
                    row[f"{prefix}{actual}"],
                    row[column],
                )
            )
    return found


def summarise(name: str, checked: int, found: list[Finding], expected: str, actual: str) -> None:
    print(f"{name}: {checked} z-scores, |z| > {LARGE_Z:g}: {len(found)} ({checked * CHANCE_OF_LARGE_Z:.1f} by chance)")
    for f in sorted(found, key=lambda f: -abs(f.z))[:WORST_SHOWN]:
        print(
            f"  {f.table}{_range(f.lowest, f.highest)}, {f.measure}: "
            f"{expected} {f.expected:.4g}, {actual} {f.actual:.4g}, z {f.z:.1f}"
        )


def write(path: Path, found: list[Finding]) -> None:
    table = pl.DataFrame([astuple(f) for f in found], schema=[f.name for f in fields(Finding)], orient="row")
    table.sort(pl.col(Z).abs(), descending=True).write_csv(path)


def _range(lowest: float, highest: float) -> str:
    if math.isnan(lowest):
        return ""
    return f" {lowest:g}" if lowest == highest else f" {lowest:g} to {highest:g}"
