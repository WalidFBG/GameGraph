import json
import re
from collections.abc import Callable
from datetime import UTC, datetime
from pathlib import Path
from typing import Any

import pytest

from gamegraph.models.contract import (
    BernoulliDistribution,
    CategoricalDistribution,
    FittedLinearSplineTerm,
    FittedLinearTerm,
    FittedModel,
    FittedPredictedParameter,
    LinearSplineTerm,
    LinearTerm,
    Link,
    ModelDeclaration,
    Optimiser,
    OptimiserName,
    PredictedParameter,
    TrainingAndValidation,
    TrainingDataManifest,
    TrainingReport,
)
from gamegraph.models.contract_files import (
    read_fitted_model,
    read_training_data_manifest,
    read_training_report,
    write_fitted_model,
    write_training_data_manifest,
    write_training_report,
)

CREATED = datetime(2026, 10, 21, 12, 0, tzinfo=UTC)

PROBABILITY = FittedPredictedParameter(
    Link.LOGIT,
    -0.12,
    (
        FittedLinearTerm("Gaussian", 0.31, 8.4, (0.021,)),
        FittedLinearTerm("Indicator", 0.38, 0.49, (-0.18,)),
        FittedLinearSplineTerm("Uniform", 5, (0.0, 8.0, 24.0), (3.1, 6.2), (2.5, 4.4), (0.07, -0.02)),
    ),
)

FITTED = FittedModel("Example", BernoulliDistribution(), {"probability": PROBABILITY})

MANIFEST = TrainingDataManifest(
    "Example",
    "source",
    CREATED,
    ("Gaussian", "Indicator", "Uniform"),
    412345,
    FITTED.declaration(),
)

REPORT = TrainingReport(
    "Example",
    CREATED,
    3067,
    TrainingAndValidation[int](330112, 82233),
    TrainingAndValidation[float](0.6712, 0.6731),
    Optimiser(OptimiserName.LBFGS, 41),
)


def test_a_manifest_round_trips(tmp_path: Path) -> None:
    write_training_data_manifest(tmp_path / "manifest.json", MANIFEST)
    assert read_training_data_manifest(tmp_path / "manifest.json") == MANIFEST


def test_a_fitted_model_round_trips(tmp_path: Path) -> None:
    write_fitted_model(tmp_path / "model.json", FITTED)
    assert read_fitted_model(tmp_path / "model.json") == FITTED


def test_a_training_report_round_trips(tmp_path: Path) -> None:
    write_training_report(tmp_path / "report.json", REPORT)
    assert read_training_report(tmp_path / "report.json") == REPORT


def test_a_written_document_carries_the_schema_version(tmp_path: Path) -> None:
    write_fitted_model(tmp_path / "model.json", FITTED)
    assert json.loads((tmp_path / "model.json").read_text(encoding="utf-8"))["schemaVersion"] == 1


def test_a_fitted_model_gives_back_its_declaration() -> None:
    terms = (LinearTerm("Gaussian"), LinearTerm("Indicator"), LinearSplineTerm("Uniform", 5))
    assert FITTED.declaration() == ModelDeclaration(
        BernoulliDistribution(), {"probability": PredictedParameter(Link.LOGIT, terms)}
    )


type Document = dict[str, Any]


def _probability(document: Document) -> Document:
    return document["parameters"]["probability"]


BROKEN: dict[str, Callable[[Document], object]] = {
    "an extra field": lambda d: d.update(extra=1),
    "a missing node": lambda d: d.pop("node"),
    "another schema version": lambda d: d.update(schemaVersion=2),
    "an unknown distribution": lambda d: d["distribution"].update(name="poisson"),
    "a categorical distribution without categories": lambda d: d.update(distribution={"name": "categorical"}),
    "an unknown parameter": lambda d: d["parameters"].update(mean={}),
    "a link Bernoulli does not allow": lambda d: _probability(d).update(link="identity"),
    "a zero scale": lambda d: _probability(d)["terms"][0].update(scale=0),
    "two coefficients on a linear term": lambda d: _probability(d)["terms"][0].update(coefficients=[1, 2]),
    "an unknown basis": lambda d: _probability(d)["terms"][0].update(basis="spline"),
}


@pytest.mark.parametrize("change", BROKEN.values(), ids=BROKEN.keys())
def test_reading_a_fitted_model_that_breaks_the_schema_fails(
    tmp_path: Path, change: Callable[[Document], object]
) -> None:
    path = tmp_path / "model.json"
    write_fitted_model(path, FITTED)
    document = json.loads(path.read_text(encoding="utf-8"))
    change(document)
    path.write_text(json.dumps(document), encoding="utf-8")
    with pytest.raises(ValueError, match=re.escape("does not match fitted-model.schema.json")):
        read_fitted_model(path)


def test_reading_a_number_that_is_not_finite_fails(tmp_path: Path) -> None:
    path = tmp_path / "model.json"
    write_fitted_model(path, FITTED)
    path.write_text(path.read_text(encoding="utf-8").replace("-0.12", "NaN"), encoding="utf-8")
    with pytest.raises(ValueError, match="NaN is not a number"):
        read_fitted_model(path)


def test_writing_a_number_that_is_not_finite_fails(tmp_path: Path) -> None:
    probability = PROBABILITY
    broken = FittedModel(
        FITTED.node,
        FITTED.distribution,
        {"probability": FittedPredictedParameter(probability.link, float("nan"), probability.terms)},
    )
    with pytest.raises(ValueError):
        write_fitted_model(tmp_path / "model.json", broken)


def test_a_manifest_whose_model_uses_an_unknown_feature_fails() -> None:
    with pytest.raises(ValueError, match="not among its features"):
        TrainingDataManifest("Example", "source", CREATED, ("Gaussian",), 1, FITTED.declaration())


def test_a_manifest_with_a_feature_its_model_does_not_use_fails() -> None:
    with pytest.raises(ValueError, match="that its model does not use"):
        TrainingDataManifest(
            "Example",
            "source",
            CREATED,
            ("Gaussian", "Indicator", "Uniform", "Unused"),
            1,
            FITTED.declaration(),
        )


def test_a_spline_whose_knots_do_not_increase_fails() -> None:
    with pytest.raises(ValueError, match="not strictly increasing"):
        FittedLinearSplineTerm("x", 3, (0.0, 0.0, 1.0), (0.0, 0.0), (1.0, 1.0), (0.0, 0.0))


def test_a_spline_with_the_wrong_number_of_coefficients_fails() -> None:
    with pytest.raises(ValueError, match="needs 2 centres, scales and coefficients"):
        FittedLinearSplineTerm("x", 3, (0.0, 1.0, 2.0), (0.0, 0.0), (1.0, 1.0), (0.0,))


def test_a_categorical_model_round_trips(tmp_path: Path) -> None:
    probability = PROBABILITY
    categorical = FittedModel(
        "Example",
        CategoricalDistribution(("first", "second", "third")),
        {
            "second": FittedPredictedParameter(Link.IDENTITY, 0.4, probability.terms),
            "third": FittedPredictedParameter(Link.IDENTITY, -1.1, probability.terms[:1]),
        },
    )
    write_fitted_model(tmp_path / "model.json", categorical)
    assert read_fitted_model(tmp_path / "model.json") == categorical


def test_parameters_that_are_not_the_distribution_s_fail() -> None:
    with pytest.raises(ValueError, match="has parameters"):
        ModelDeclaration(CategoricalDistribution(("first", "second")), {"third": PredictedParameter(Link.IDENTITY, ())})


def test_a_categorical_distribution_with_one_category_fails() -> None:
    with pytest.raises(ValueError, match="at least 2 categories"):
        CategoricalDistribution(("first",))


def test_the_same_term_twice_fails() -> None:
    with pytest.raises(ValueError, match="appear more than once"):
        PredictedParameter(Link.LOGIT, (LinearTerm("Gaussian"), LinearTerm("Gaussian")))


def test_a_time_that_is_not_utc_fails() -> None:
    with pytest.raises(ValueError, match="not in UTC"):
        TrainingDataManifest(
            "Example",
            "source",
            datetime(2026, 10, 21),
            (),
            1,
            ModelDeclaration(BernoulliDistribution(), {"probability": PredictedParameter(Link.LOGIT, ())}),
        ).to_json()
