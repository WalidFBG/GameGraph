from collections.abc import Sequence

import torch

from gamegraph.models.contract import DistributionDeclaration, FittedModel, ModelDeclaration
from gamegraph.models.distributions import distribution_for
from gamegraph.models.given_column import GivenColumn
from gamegraph.models.predictor import Predictor


class Model:
    def __init__(
        self, distribution: DistributionDeclaration, predictors: tuple[Predictor, ...], given: tuple[GivenColumn, ...]
    ) -> None:
        if len(predictors) != len(distribution.predicted()) or len(given) != len(distribution.given()):
            raise ValueError(
                f"{distribution} needs {len(distribution.predicted())} predictors and {len(distribution.given())} "
                f"given columns, got {len(predictors)} and {len(given)}"
            )
        self.distribution = distribution
        self._implementation = distribution_for(distribution)
        self.predictors = predictors
        self.given = given

    def designs(self, features: torch.Tensor) -> tuple[torch.Tensor, ...]:
        if features.dtype != torch.float64:
            raise ValueError(f"Features must be float64, got {features.dtype}")
        return tuple(p.design(features) for p in (*self.predictors, *self.given))

    def parameter_values_of(self, designs: tuple[torch.Tensor, ...]) -> tuple[torch.Tensor, ...]:
        return tuple(p.value_of(d) for p, d in zip((*self.predictors, *self.given), designs, strict=True))

    def log_prob_of(self, designs: tuple[torch.Tensor, ...], outcomes: torch.Tensor) -> torch.Tensor:
        return self._implementation.log_prob(self.parameter_values_of(designs), outcomes)

    def parameter_values(self, features: torch.Tensor) -> tuple[torch.Tensor, ...]:
        return self.parameter_values_of(self.designs(features))

    def log_prob(self, features: torch.Tensor, outcomes: torch.Tensor) -> torch.Tensor:
        return self.log_prob_of(self.designs(features), outcomes)

    def sample(self, features: torch.Tensor, uniforms: torch.Tensor) -> torch.Tensor:
        return self._implementation.sample(self.parameter_values(features), uniforms)

    def coefficients(self) -> list[torch.Tensor]:
        return [tensor for p in self.predictors for tensor in p.trainable()]

    def to_fitted(self, node: str) -> FittedModel:
        distribution = self.distribution
        return FittedModel(
            node,
            distribution,
            {p.name: q.to_fitted() for p, q in zip(distribution.predicted(), self.predictors, strict=True)},
            {name: g.feature for name, g in zip(distribution.given(), self.given, strict=True)},
        )

    @staticmethod
    def from_fitted(fitted: FittedModel, features: Sequence[str]) -> "Model":
        distribution = fitted.distribution
        return Model(
            distribution,
            tuple(Predictor.from_fitted(p.link, fitted.predictors[p.name], features) for p in distribution.predicted()),
            tuple(GivenColumn.of(fitted.given[name], features) for name in distribution.given()),
        )

    @staticmethod
    def initial(
        declaration: ModelDeclaration, features: Sequence[str], examples: torch.Tensor, outcomes: torch.Tensor
    ) -> "Model":
        distribution = declaration.distribution
        starts = distribution_for(distribution).initial_parameters(outcomes)
        return Model(
            distribution,
            tuple(
                Predictor.initial(p.link, declaration.terms[p.name], features, examples, starts[p.name])
                for p in distribution.predicted()
            ),
            tuple(GivenColumn.of(declaration.given[name], features) for name in distribution.given()),
        )
