from collections.abc import Sequence

import torch

from gamegraph.models import links
from gamegraph.models.contract import FittedPredictedParameter, Link, PredictedParameter
from gamegraph.models.terms import LinearColumns


class Predictor:
    def __init__(
        self, link: Link, terms: tuple[LinearColumns, ...], intercept: torch.Tensor, coefficients: torch.Tensor
    ) -> None:
        if coefficients.shape != (len(terms),):
            raise ValueError(
                f"{len(terms)} linear terms need {len(terms)} coefficients, got {tuple(coefficients.shape)}"
            )
        self.link = link
        self.terms = terms
        self.intercept = intercept
        self.coefficients = coefficients

    def design(self, features: torch.Tensor) -> torch.Tensor:
        if not self.terms:
            return features.new_zeros((features.shape[0], 0))
        return torch.cat([t.columns(features) for t in self.terms], dim=1)

    def value_of(self, design: torch.Tensor) -> torch.Tensor:
        return links.inverse(self.link, self.intercept + design @ self.coefficients)

    def to_fitted(self) -> FittedPredictedParameter:
        coefficients = self.coefficients.detach()
        return FittedPredictedParameter(
            self.link,
            float(self.intercept.detach()),
            tuple(t.to_fitted(coefficients[i : i + 1]) for i, t in enumerate(self.terms)),
        )

    @staticmethod
    def from_fitted(parameter: FittedPredictedParameter, features: Sequence[str]) -> "Predictor":
        return Predictor(
            parameter.link,
            tuple(LinearColumns.from_fitted(t, features) for t in parameter.terms),
            torch.tensor(parameter.intercept, dtype=torch.float64),
            torch.tensor([c for t in parameter.terms for c in t.coefficients], dtype=torch.float64),
        )

    @staticmethod
    def initial(
        parameter: PredictedParameter, features: Sequence[str], examples: torch.Tensor, start: float
    ) -> "Predictor":
        terms = tuple(LinearColumns.fit(t, features, examples) for t in parameter.terms)
        return Predictor(
            parameter.link,
            terms,
            torch.tensor(links.apply(parameter.link, start), dtype=torch.float64, requires_grad=True),
            torch.zeros(len(terms), dtype=torch.float64, requires_grad=True),
        )
