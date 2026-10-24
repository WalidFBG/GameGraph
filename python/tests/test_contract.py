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
    DiscreteJohnsonSuDistribution,
    FittedModel,
    LinearFit,
    LinearSplineFit,
    LinearSplineTerm,
    LinearTerm,
    ModelDeclaration,
    Optimiser,
    OptimiserName,
    PredictorFit,
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

TERMS = (LinearTerm("Gaussian"), LinearTerm("Indicator"), LinearSplineTerm("Uniform", 5))

DECLARATION = ModelDeclaration(BernoulliDistribution(), {"probability": TERMS}, {})

PROBABILITY = PredictorFit(
    -0.12,
    {
        "Gaussian": LinearFit(0.31, 8.4, (0.021,)),
        "Indicator": LinearFit(0.38, 0.49, (-0.18,)),
        "Uniform": LinearSplineFit((0.0, 8.0, 24.0), (3.1, 6.2), (2.5, 4.4), (0.07, -0.02)),
    },
)

FITTED = FittedModel("Example", DECLARATION, {"probability": PROBABILITY})

MANIFEST = TrainingDataManifest(
    "Example",
    "source",
    CREATED,
    ("Gaussian", "Indicator", "Uniform"),
    412345,
    DECLARATION,
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


type Document = dict[str, Any]


def _probability(document: Document) -> Document:
    return document["fit"]["probability"]


BROKEN: dict[str, Callable[[Document], object]] = {
    "an extra field": lambda d: d.update(extra=1),
    "a missing node": lambda d: d.pop("node"),
    "another schema version": lambda d: d.update(schemaVersion=2),
    "an unknown distribution": lambda d: d["model"]["distribution"].update(name="poisson"),
    "a categorical distribution without categories": lambda d: d["model"].update(distribution={"name": "categorical"}),
    "a link in the file": lambda d: _probability(d).update(link="identity"),
    "a given parameter that is not a feature name": lambda d: d["model"].update(given={"maximum": 3}),
    "a zero scale": lambda d: _probability(d)["terms"]["Gaussian"].update(scale=0),
    "two coefficients on a linear term": lambda d: _probability(d)["terms"]["Gaussian"].update(coefficients=[1, 2]),
    "an unknown basis": lambda d: d["model"]["terms"]["probability"][0].update(basis="spline"),
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


FITTED_BROKEN: dict[str, tuple[Callable[[Document], object], str]] = {
    "an unknown parameter": (
        lambda d: d["fit"].update(mean=_probability(d)),
        "mean is not a declared predicted parameter",
    ),
    "a missing parameter": (lambda d: d["fit"].pop("probability"), "predicts ['probability'], but its fit has []"),
    "an undeclared term": (
        lambda d: _probability(d)["terms"].update(Extra=_probability(d)["terms"]["Gaussian"]),
        "Extra is not a declared term",
    ),
    "a missing term": (lambda d: _probability(d)["terms"].pop("Indicator"), "fitted ['Gaussian', 'Uniform']"),
    "more knot positions than knots": (
        lambda d: d["model"]["terms"]["probability"][2].update(knots=2),
        "with 2 knots has 3 positions",
    ),
}


@pytest.mark.parametrize(("change", "message"), FITTED_BROKEN.values(), ids=FITTED_BROKEN.keys())
def test_reading_a_fit_that_does_not_match_its_declaration_fails(
    tmp_path: Path, change: Callable[[Document], object], message: str
) -> None:
    path = tmp_path / "model.json"
    write_fitted_model(path, FITTED)
    document = json.loads(path.read_text(encoding="utf-8"))
    change(document)
    path.write_text(json.dumps(document), encoding="utf-8")
    with pytest.raises(ValueError, match=re.escape(message)):
        read_fitted_model(path)


def test_a_spline_fit_on_a_linear_term_fails() -> None:
    terms = {**PROBABILITY.terms, "Gaussian": PROBABILITY.terms["Uniform"]}
    with pytest.raises(ValueError, match="The fit of Gaussian does not match its basis"):
        FittedModel("Example", DECLARATION, {"probability": PredictorFit(-0.12, terms)})


def test_reading_a_number_that_is_not_finite_fails(tmp_path: Path) -> None:
    path = tmp_path / "model.json"
    write_fitted_model(path, FITTED)
    path.write_text(path.read_text(encoding="utf-8").replace("-0.12", "NaN"), encoding="utf-8")
    with pytest.raises(ValueError, match="NaN is not a number"):
        read_fitted_model(path)


def test_writing_a_number_that_is_not_finite_fails(tmp_path: Path) -> None:
    broken = FittedModel(FITTED.node, DECLARATION, {"probability": PredictorFit(float("nan"), PROBABILITY.terms)})
    with pytest.raises(ValueError):
        write_fitted_model(tmp_path / "model.json", broken)


def test_a_manifest_whose_model_uses_an_unknown_feature_fails() -> None:
    with pytest.raises(ValueError, match="not among its features"):
        TrainingDataManifest("Example", "source", CREATED, ("Gaussian",), 1, DECLARATION)


def test_a_manifest_with_a_feature_its_model_does_not_use_fails() -> None:
    with pytest.raises(ValueError, match="that its model does not use"):
        TrainingDataManifest(
            "Example",
            "source",
            CREATED,
            ("Gaussian", "Indicator", "Uniform", "Unused"),
            1,
            DECLARATION,
        )


def test_a_spline_whose_knots_do_not_increase_fails() -> None:
    with pytest.raises(ValueError, match="not strictly increasing"):
        LinearSplineFit((0.0, 0.0, 1.0), (0.0, 0.0), (1.0, 1.0), (0.0, 0.0))


def test_a_spline_with_the_wrong_number_of_coefficients_fails() -> None:
    with pytest.raises(ValueError, match="needs 2 centres, scales and coefficients"):
        LinearSplineFit((0.0, 1.0, 2.0), (0.0, 0.0), (1.0, 1.0), (0.0,))


def test_a_categorical_model_round_trips(tmp_path: Path) -> None:
    categorical = FittedModel(
        "Example",
        ModelDeclaration(
            CategoricalDistribution(("first", "second", "third")), {"second": TERMS, "third": TERMS[:1]}, {}
        ),
        {"second": PROBABILITY, "third": PredictorFit(-1.1, {"Gaussian": LinearFit(0.31, 8.4, (0.5,))})},
    )
    write_fitted_model(tmp_path / "model.json", categorical)
    assert read_fitted_model(tmp_path / "model.json") == categorical


def test_a_johnson_su_model_with_a_given_maximum_round_trips(tmp_path: Path) -> None:
    johnson_su = FittedModel(
        "Example",
        ModelDeclaration(
            DiscreteJohnsonSuDistribution(),
            {"location": TERMS[:1], "scale": (), "skew": (), "tail": ()},
            {"maximum": "Bound"},
        ),
        {
            "location": PredictorFit(9.0, {"Gaussian": LinearFit(0.31, 8.4, (2.0,))}),
            "scale": PredictorFit(1.5, {}),
            "skew": PredictorFit(0.2, {}),
            "tail": PredictorFit(0.4, {}),
        },
    )
    write_fitted_model(tmp_path / "model.json", johnson_su)
    read = read_fitted_model(tmp_path / "model.json")
    assert read == johnson_su
    assert read.model.features() == {"Gaussian", "Bound"}


def test_predicted_parameters_that_are_not_the_distribution_s_fail() -> None:
    with pytest.raises(ValueError, match=re.escape("predicts ['second'], got ['third']")):
        ModelDeclaration(CategoricalDistribution(("first", "second")), {"third": ()}, {})


def test_a_predicted_parameter_given_instead_fails() -> None:
    with pytest.raises(ValueError, match=re.escape("predicts ['probability'], got []")):
        ModelDeclaration(BernoulliDistribution(), {}, {"probability": "Gaussian"})


def test_a_missing_given_parameter_fails() -> None:
    terms = {"location": (), "scale": (), "skew": (), "tail": ()}
    with pytest.raises(ValueError, match=re.escape("is given ['maximum'], got []")):
        ModelDeclaration(DiscreteJohnsonSuDistribution(), terms, {})


def test_a_categorical_distribution_with_one_category_fails() -> None:
    with pytest.raises(ValueError, match="at least 2 categories"):
        CategoricalDistribution(("first",))


def test_the_same_term_twice_fails() -> None:
    with pytest.raises(ValueError, match="appear more than once"):
        ModelDeclaration(BernoulliDistribution(), {"probability": (LinearTerm("Gaussian"), LinearTerm("Gaussian"))}, {})


def test_a_time_that_is_not_utc_fails() -> None:
    with pytest.raises(ValueError, match="not in UTC"):
        TrainingDataManifest(
            "Example",
            "source",
            datetime(2026, 10, 21),
            (),
            1,
            ModelDeclaration(BernoulliDistribution(), {"probability": ()}, {}),
        ).to_json()
