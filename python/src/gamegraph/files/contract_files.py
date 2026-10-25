import json
from functools import cache
from pathlib import Path
from typing import Any, cast

from jsonschema import Draft202012Validator, exceptions
from jsonschema.exceptions import ValidationError
from referencing import Registry, Resource

from gamegraph.contract import FittedModel, Json, TrainingDataManifest, TrainingReport
from gamegraph.files.paths import SCHEMAS

SCHEMA_VERSION = 1
TRAINING_DATA_MANIFEST = "training-data-manifest.schema.json"
FITTED_MODEL = "fitted-model.schema.json"
TRAINING_REPORT = "training-report.schema.json"


def read_training_data_manifest(path: Path) -> TrainingDataManifest:
    return TrainingDataManifest.from_json(_read(path, TRAINING_DATA_MANIFEST))


def write_training_data_manifest(path: Path, manifest: TrainingDataManifest) -> None:
    _write(path, TRAINING_DATA_MANIFEST, manifest.to_json())


def read_fitted_model(path: Path) -> FittedModel:
    return FittedModel.from_json(_read(path, FITTED_MODEL))


def write_fitted_model(path: Path, model: FittedModel) -> None:
    _write(path, FITTED_MODEL, model.to_json())


def read_training_report(path: Path) -> TrainingReport:
    return TrainingReport.from_json(_read(path, TRAINING_REPORT))


def write_training_report(path: Path, report: TrainingReport) -> None:
    _write(path, TRAINING_REPORT, report.to_json())


def write_training_result(directory: Path, fitted: FittedModel, report: TrainingReport) -> None:
    directory.mkdir(parents=True, exist_ok=True)
    write_fitted_model(directory / f"{fitted.node}.json", fitted)
    write_training_report(directory / f"{fitted.node}.training.json", report)


def _read(path: Path, schema: str) -> Json:
    document = json.loads(path.read_text(encoding="utf-8"), parse_constant=_reject_constant)
    _validate(document, schema, path)
    del document["schemaVersion"]
    return document


def _write(path: Path, schema: str, document: Json) -> None:
    versioned = {"schemaVersion": SCHEMA_VERSION, **document}
    _validate(versioned, schema, path)
    path.write_text(json.dumps(versioned, indent=2, allow_nan=False) + "\n", encoding="utf-8", newline="\n")


def _validate(document: Any, schema: str, path: Path) -> None:
    error = cast(ValidationError | None, exceptions.best_match(_validators()[schema].iter_errors(document)))
    if error is not None:
        raise ValueError(f"{path} does not match {schema}: {error.message} at {error.json_path}")


@cache
def _validators() -> dict[str, Draft202012Validator]:
    schemas = {p.name: json.loads(p.read_text(encoding="utf-8")) for p in SCHEMAS.glob("*.schema.json")}
    resources: list[tuple[str, Resource[Any]]] = [
        (schema["$id"], Resource[Any].from_contents(schema)) for schema in schemas.values()
    ]
    registry = Registry[Any]().with_resources(resources)
    return {name: Draft202012Validator(schema, registry=registry) for name, schema in schemas.items()}


def _reject_constant(constant: str) -> float:
    raise ValueError(f"{constant} is not a number")
