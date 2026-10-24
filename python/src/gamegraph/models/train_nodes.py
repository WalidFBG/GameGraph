import sys
import time
from pathlib import Path

from gamegraph.models.training import train, write_training_result
from gamegraph.models.training_data import MANIFEST, read_training_data


def train_nodes(training_data: Path, fitted_models: Path) -> None:
    directories = sorted(d for d in training_data.iterdir() if (d / MANIFEST).is_file())
    if not directories:
        raise ValueError(f"No node's training data under {training_data}")
    for directory in directories:
        started = time.perf_counter()
        fitted, report = train(read_training_data(directory))
        write_training_result(fitted_models, fitted, report)
        chosen = report.chosen_attempt()
        failed = f", {len(report.failures)} failed attempts" if report.failures else ""
        print(
            f"{fitted.node}: {chosen.route.value} at ridge {chosen.ridge:g}, "
            f"validation {chosen.negative_log_likelihood.validation:.6f}, "
            f"{time.perf_counter() - started:.1f} s{failed}",
            flush=True,
        )


def main() -> None:
    if len(sys.argv) != 3:
        raise SystemExit("Usage: gamegraph-train <training data> <fitted models>")
    train_nodes(Path(sys.argv[1]), Path(sys.argv[2]))
