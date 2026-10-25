import sys
from pathlib import Path

import polars as pl

from gamegraph.audit import findings, predicted
from gamegraph.audit.findings import Failure
from gamegraph.audit.runs import BUCKETS, csv_name, latest
from gamegraph.models.contract_files import read_training_data_manifest
from gamegraph.models.training_data import MANIFEST

CHECK = "calibration"


def audit(run: Path, training_data: Path) -> None:
    files = sorted(run.glob("*.parquet"))
    if not files:
        raise ValueError(f"No calibration files under {run}")
    found: list[Failure] = []
    for file in files:
        manifest = read_training_data_manifest(training_data / file.stem / MANIFEST)
        output = run / BUCKETS / manifest.node
        output.mkdir(parents=True, exist_ok=True)
        tables = predicted.tables(manifest, pl.read_parquet(file))
        node_found: list[Failure] = []
        for name, table in tables.items():
            table.write_csv(output / csv_name(run, manifest.node, name))
            node_found += findings.failures(CHECK, manifest.node, name, table, predicted.PREDICTED, predicted.OBSERVED)
        checked = sum(findings.z_scores(t) for t in tables.values())
        findings.summarise(manifest.node, checked, node_found, predicted.PREDICTED, predicted.OBSERVED)
        found += node_found
    findings.write(run / BUCKETS / csv_name(run, "failures"), found)


def main() -> None:
    if len(sys.argv) != 3:
        raise SystemExit("Usage: gamegraph-calibration <calibration audits> <training data>")
    audit(latest(Path(sys.argv[1])), Path(sys.argv[2]))
