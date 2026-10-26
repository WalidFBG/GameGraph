from collections.abc import Sequence

import torch

from gamegraph.contract import ChoiceDistribution, FittedModel, ModelDeclaration
from gamegraph.models.choice_shape import ChoiceShape
from gamegraph.models.distributions import distribution_for
from gamegraph.models.given_column import GivenColumn
from gamegraph.models.predictor import Predictor
from gamegraph.models.terms import column_of


class Model:
    def __init__(
        self, declaration: ModelDeclaration, predictors: tuple[Predictor, ...], given: tuple[GivenColumn, ...]
    ) -> None:
        distribution = declaration.distribution
        if len(predictors) != len(distribution.predicted()) or len(given) != len(distribution.given()):
            raise ValueError(
                f"{distribution} needs {len(distribution.predicted())} predictors and {len(distribution.given())} "
                f"given columns, got {len(predictors)} and {len(given)}"
            )
        self.declaration = declaration
        self._implementation = distribution_for(distribution)
        self.predictors = predictors
        self.given = given

    def designs(self, features: torch.Tensor) -> tuple[torch.Tensor, ...]:
        if features.dtype != torch.float64:
            raise ValueError(f"Features must be float64, got {features.dtype}")
        if self.declaration.distribution.chooses_among_candidates():
            if features.dim() != 3 or features.shape[-1] == 0:
                raise ValueError(
                    "A choice needs features of decisions, candidates and at least one feature, "
                    f"got {tuple(features.shape)}"
                )
        elif features.dim() != 2:
            raise ValueError(f"Features must be examples by features, got {tuple(features.shape)}")
        return tuple(p.design(features) for p in (*self.predictors, *self.given))

    def parameter_values_from_designs(self, designs: tuple[torch.Tensor, ...]) -> tuple[torch.Tensor, ...]:
        return tuple(p.value_of(d) for p, d in zip((*self.predictors, *self.given), designs, strict=True))

    def log_prob_from_designs(self, designs: tuple[torch.Tensor, ...], outcomes: torch.Tensor) -> torch.Tensor:
        return self._implementation.log_prob(self.parameter_values_from_designs(designs), outcomes)

    def parameter_values(self, features: torch.Tensor) -> tuple[torch.Tensor, ...]:
        return self.parameter_values_from_designs(self.designs(features))

    def log_prob(self, features: torch.Tensor, outcomes: torch.Tensor) -> torch.Tensor:
        return self.log_prob_from_designs(self.designs(features), outcomes)

    def sample(self, features: torch.Tensor, uniforms: torch.Tensor) -> torch.Tensor:
        return self._implementation.sample(self.parameter_values(features), uniforms)

    def coefficients(self) -> list[torch.Tensor]:
        return [tensor for p in self.predictors for tensor in p.trainable()]

    def to_fitted(self, node: str) -> FittedModel:
        predicted = self.declaration.distribution.predicted()
        return FittedModel(
            node,
            self.declaration,
            {p.name: predictor.to_fit() for p, predictor in zip(predicted, self.predictors, strict=True)},
        )

    @staticmethod
    def from_fitted(fitted: FittedModel, features: Sequence[str]) -> "Model":
        declaration = fitted.model
        distribution = declaration.distribution
        return Model(
            declaration,
            tuple(
                Predictor.from_fit(p, declaration.terms[p.name], fitted.fit[p.name], features)
                for p in distribution.predicted()
            ),
            tuple(GivenColumn.of(declaration.given[name], features) for name in distribution.given()),
        )

    @staticmethod
    def initial(
        declaration: ModelDeclaration, features: Sequence[str], examples: torch.Tensor, outcomes: torch.Tensor
    ) -> "Model":
        distribution = declaration.distribution
        starts = distribution_for(distribution).initial_parameters(outcomes)
        rows = examples
        if distribution.chooses_among_candidates():
            sizes = examples[..., column_of(declaration.given[ChoiceDistribution.SIZE], features)]
            rows = examples[ChoiceShape.of(sizes).candidate]
        return Model(
            declaration,
            tuple(
                Predictor.initial(p, declaration.terms[p.name], features, rows, starts)
                for p in distribution.predicted()
            ),
            tuple(GivenColumn.of(declaration.given[name], features) for name in distribution.given()),
        )
