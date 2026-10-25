from collections.abc import Sequence

import torch

from gamegraph.contract import PredictedParameter, PredictorFit, Term
from gamegraph.models import links
from gamegraph.models.terms import TermColumns, columns_from_fit, fit_columns


class Predictor:
    def __init__(
        self,
        parameter: PredictedParameter,
        terms: tuple[TermColumns, ...],
        intercept: torch.Tensor,
        coefficients: torch.Tensor,
    ) -> None:
        columns = sum(t.count for t in terms)
        if coefficients.shape != (columns,):
            raise ValueError(f"{columns} columns need {columns} coefficients, got {tuple(coefficients.shape)}")
        if not parameter.has_intercept and (intercept.requires_grad or float(intercept) != 0):
            raise ValueError(f"The {parameter.name} has no intercept, so its intercept is a fixed 0")
        self.parameter = parameter
        self.terms = terms
        self.intercept = intercept
        self.coefficients = coefficients

    def design(self, features: torch.Tensor) -> torch.Tensor:
        if not self.terms:
            return features.new_zeros((*features.shape[:-1], 0))
        return torch.cat([t.columns(features) for t in self.terms], dim=-1)

    def value_of(self, design: torch.Tensor) -> torch.Tensor:
        return links.inverse(self.parameter.link, self.intercept + design @ self.coefficients)

    def trainable(self) -> list[torch.Tensor]:
        return [self.intercept, self.coefficients] if self.parameter.has_intercept else [self.coefficients]

    def to_fit(self) -> PredictorFit:
        coefficients = self.coefficients.detach()
        starts = [sum(t.count for t in self.terms[:i]) for i in range(len(self.terms))]
        return PredictorFit(
            float(self.intercept.detach()),
            {t.feature: t.to_fit(coefficients[s : s + t.count]) for t, s in zip(self.terms, starts, strict=True)},
        )

    @staticmethod
    def from_fit(
        parameter: PredictedParameter, terms: tuple[Term, ...], fit: PredictorFit, features: Sequence[str]
    ) -> "Predictor":
        fits = [fit.terms[t.feature] for t in terms]
        return Predictor(
            parameter,
            tuple(columns_from_fit(t.feature, f, features) for t, f in zip(terms, fits, strict=True)),
            torch.tensor(fit.intercept, dtype=torch.float64),
            torch.tensor([c for f in fits for c in f.coefficients], dtype=torch.float64),
        )

    @staticmethod
    def initial(
        parameter: PredictedParameter,
        terms: tuple[Term, ...],
        features: Sequence[str],
        examples: torch.Tensor,
        starts: dict[str, float],
    ) -> "Predictor":
        columns = tuple(fit_columns(t, features, examples) for t in terms)
        intercept = (
            torch.tensor(links.apply(parameter.link, starts[parameter.name]), dtype=torch.float64, requires_grad=True)
            if parameter.has_intercept
            else torch.tensor(0.0, dtype=torch.float64)
        )
        return Predictor(
            parameter,
            columns,
            intercept,
            torch.zeros(sum(t.count for t in columns), dtype=torch.float64, requires_grad=True),
        )
