from collections.abc import Sequence

import torch

from gamegraph.models import links
from gamegraph.models.contract import FittedPredictor, Link, Term
from gamegraph.models.terms import TermColumns, columns_from_fitted, fit_columns


class Predictor:
    def __init__(
        self, link: Link, terms: tuple[TermColumns, ...], intercept: torch.Tensor, coefficients: torch.Tensor
    ) -> None:
        columns = sum(t.count for t in terms)
        if coefficients.shape != (columns,):
            raise ValueError(f"{columns} columns need {columns} coefficients, got {tuple(coefficients.shape)}")
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

    def trainable(self) -> list[torch.Tensor]:
        return [self.intercept, self.coefficients]

    def to_fitted(self) -> FittedPredictor:
        coefficients = self.coefficients.detach()
        starts = [sum(t.count for t in self.terms[:i]) for i in range(len(self.terms))]
        return FittedPredictor(
            float(self.intercept.detach()),
            tuple(t.to_fitted(coefficients[s : s + t.count]) for t, s in zip(self.terms, starts, strict=True)),
        )

    @staticmethod
    def from_fitted(link: Link, predictor: FittedPredictor, features: Sequence[str]) -> "Predictor":
        return Predictor(
            link,
            tuple(columns_from_fitted(t, features) for t in predictor.terms),
            torch.tensor(predictor.intercept, dtype=torch.float64),
            torch.tensor([c for t in predictor.terms for c in t.coefficients], dtype=torch.float64),
        )

    @staticmethod
    def initial(
        link: Link, terms: tuple[Term, ...], features: Sequence[str], examples: torch.Tensor, start: float
    ) -> "Predictor":
        columns = tuple(fit_columns(t, features, examples) for t in terms)
        return Predictor(
            link,
            columns,
            torch.tensor(links.apply(link, start), dtype=torch.float64, requires_grad=True),
            torch.zeros(sum(t.count for t in columns), dtype=torch.float64, requires_grad=True),
        )
