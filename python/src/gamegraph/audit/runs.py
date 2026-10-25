from pathlib import Path

BUCKETS = "buckets"
PARTIAL = ".partial"


def latest(audits: Path) -> Path:
    runs = sorted(d for d in audits.iterdir() if d.is_dir() and not d.name.endswith(PARTIAL))
    if not runs:
        raise ValueError(f"No complete audit run under {audits}")
    return runs[-1]


def csv_name(run: Path, *parts: str) -> str:
    return "_".join([*parts, run.name]) + ".csv"
