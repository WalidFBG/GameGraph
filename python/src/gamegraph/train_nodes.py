import hashlib
import json
import shutil
import sys
import time
from functools import cache
from pathlib import Path

import torch

from gamegraph.files.contract_files import write_training_result
from gamegraph.files.training_data_files import EXAMPLES, GAMES, MANIFEST, read_training_data
from gamegraph.training.training import train

DIGEST = ".digest"
PACKAGE = Path(__file__).parent
UNTRACKED_PACKAGES = ("audit",)
UNTRACKED_MANIFEST_FIELDS = ("createdUtc",)


def train_nodes(training_data: Path, fitted_models: Path, retrain_all: bool = False) -> None:
    directories = sorted(d for d in training_data.iterdir() if d.is_dir())
    if not directories:
        raise ValueError(f"No node's training data under {training_data}")
    missing = [d.name for d in directories if not (d / MANIFEST).is_file()]
    if missing:
        raise ValueError(f"{missing} under {training_data} have no {MANIFEST}")
    staging = fitted_models.with_name(fitted_models.name + ".partial")
    if staging.exists():
        shutil.rmtree(staging)
    for directory in directories:
        node = directory.name
        digest = training_digest(directory)
        if not retrain_all and _recorded_digest(fitted_models, node) == digest:
            _keep(fitted_models, staging, node)
            print(f"{node}: unchanged, kept", flush=True)
            continue
        started = time.perf_counter()
        fitted, report = train(read_training_data(directory))
        if fitted.node != node:
            raise ValueError(f"The training data under {directory} is for {fitted.node}")
        write_training_result(staging, fitted, report)
        (staging / f"{node}{DIGEST}").write_text(digest + "\n", encoding="utf-8", newline="\n")
        chosen = report.chosen_attempt()
        failed = f", {len(report.failures)} failed attempts" if report.failures else ""
        print(
            f"{node}: {chosen.route.value} at ridge {chosen.ridge:g}, "
            f"validation {chosen.negative_log_likelihood.validation:.6f}, "
            f"{time.perf_counter() - started:.1f} s{failed}",
            flush=True,
        )
    if fitted_models.exists():
        shutil.rmtree(fitted_models)
    staging.rename(fitted_models)


def training_digest(directory: Path) -> str:
    manifest = json.loads((directory / MANIFEST).read_text(encoding="utf-8"))
    for field in UNTRACKED_MANIFEST_FIELDS:
        del manifest[field]
    digest = hashlib.sha256(trainer_digest().encode("utf-8"))
    digest.update(json.dumps(manifest, sort_keys=True).encode("utf-8"))
    for name in (EXAMPLES, GAMES):
        digest.update((directory / name).read_bytes())
    return digest.hexdigest()


@cache
def trainer_digest() -> str:
    digest = hashlib.sha256(torch.__version__.encode("utf-8"))
    for source in trainer_sources():
        digest.update(source.encode("utf-8"))
        digest.update((PACKAGE / source).read_bytes())
    return digest.hexdigest()


def trainer_sources() -> list[str]:
    return sorted(
        s.relative_to(PACKAGE).as_posix()
        for s in PACKAGE.rglob("*.py")
        if s.relative_to(PACKAGE).parts[0] not in UNTRACKED_PACKAGES
    )


def _recorded_digest(fitted_models: Path, node: str) -> str | None:
    path = fitted_models / f"{node}{DIGEST}"
    return path.read_text(encoding="utf-8").strip() if path.is_file() else None


def _keep(fitted_models: Path, staging: Path, node: str) -> None:
    staging.mkdir(parents=True, exist_ok=True)
    for suffix in (".json", ".training.json", DIGEST):
        shutil.copy2(fitted_models / f"{node}{suffix}", staging / f"{node}{suffix}")


def main() -> None:
    arguments = sys.argv[1:]
    retrain_all = "--all" in arguments
    paths = [a for a in arguments if a != "--all"]
    if len(paths) != 2 or len(arguments) - len(paths) > 1:
        raise SystemExit("Usage: gamegraph-train <training data> <fitted models> [--all]")
    train_nodes(Path(paths[0]), Path(paths[1]), retrain_all)
