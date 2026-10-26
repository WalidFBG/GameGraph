from __future__ import annotations

from dataclasses import dataclass

from gamegraph.contract.distributions import PredictedParameter
from gamegraph.contract.json_document import Json
from gamegraph.contract.model_declaration import ModelDeclaration
from gamegraph.contract.terms import (
    LinearFit,
    LinearSplineFit,
    LinearSplineTerm,
    LinearTerm,
    Term,
    TermFit,
    term_fit_from_json,
)


@dataclass(frozen=True)
class PredictorFit:
    intercept: float
    terms: dict[str, TermFit]

    def to_json(self, parameter: PredictedParameter) -> Json:
        terms = {"terms": {feature: f.to_json() for feature, f in self.terms.items()}}
        return {"intercept": self.intercept} | terms if parameter.has_intercept else terms

    @staticmethod
    def from_json(parameter: PredictedParameter, terms: tuple[Term, ...], json: Json) -> PredictorFit:
        declared = {t.feature: t for t in terms}
        if ("intercept" in json) != parameter.has_intercept:
            raise ValueError(
                f"The fit of {parameter.name} {'has' if 'intercept' in json else 'has no'} intercept, "
                f"but the parameter {'has one' if parameter.has_intercept else 'has none'}"
            )
        return PredictorFit(
            json["intercept"] if parameter.has_intercept else 0.0,
            {
                feature: term_fit_from_json(_declared(declared, feature, "term"), f)
                for feature, f in json["terms"].items()
            },
        )


@dataclass(frozen=True)
class FittedModel:
    node: str
    model: ModelDeclaration
    fit: dict[str, PredictorFit]

    def __post_init__(self) -> None:
        if sorted(self.fit) != sorted(self.model.terms):
            raise ValueError(f"{self.node} predicts {sorted(self.model.terms)}, but its fit has {sorted(self.fit)}")
        for parameter in self.model.distribution.predicted():
            intercept = self.fit[parameter.name].intercept
            if not parameter.has_intercept and intercept != 0:
                raise ValueError(f"The {parameter.name} of {self.node} has no intercept, but its fit has {intercept}")
        for name, terms in self.model.terms.items():
            fits = self.fit[name].terms
            if sorted(fits) != sorted(t.feature for t in terms):
                raise ValueError(
                    f"The {name} of {self.node} has terms {[t.feature for t in terms]}, fitted {sorted(fits)}"
                )
            for term in terms:
                _require_fit_of(term, fits[term.feature])

    def to_json(self) -> Json:
        return {
            "node": self.node,
            "model": self.model.to_json(),
            "fit": {p.name: self.fit[p.name].to_json(p) for p in self.model.distribution.predicted()},
        }

    @staticmethod
    def from_json(json: Json) -> FittedModel:
        model = ModelDeclaration.from_json(json["model"])
        parameters = {p.name: p for p in model.distribution.predicted()}
        return FittedModel(
            json["node"],
            model,
            {
                name: PredictorFit.from_json(
                    _declared(parameters, name, "predicted parameter"),
                    _declared(model.terms, name, "predicted parameter"),
                    f,
                )
                for name, f in json["fit"].items()
            },
        )


def _require_fit_of(term: Term, fit: TermFit) -> None:
    match term, fit:
        case LinearTerm(), LinearFit():
            pass
        case LinearSplineTerm(), LinearSplineFit():
            if len(fit.knot_positions) > term.knots:
                raise ValueError(
                    f"A linear spline of {term.feature} with {term.knots} knots has {len(fit.knot_positions)} positions"
                )
        case _:
            raise ValueError(f"The fit of {term.feature} does not match its basis")


def _declared[T](declared: dict[str, T], name: str, kind: str) -> T:
    if name not in declared:
        raise ValueError(f"{name} is not a declared {kind}")
    return declared[name]
