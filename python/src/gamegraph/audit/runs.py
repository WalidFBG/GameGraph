from pathlib import Path

BUCKETS = "buckets"


def latest(audits: Path) -> Path:
    runs = sorted(d for d in audits.iterdir() if d.is_dir())
    if not runs:
        raise ValueError(f"No audit run under {audits}")
    return runs[-1]


def csv_name(run: Path, *parts: str) -> str:
    return "_".join([*parts, run.name]) + ".csv"
