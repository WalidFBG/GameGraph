from collections.abc import Sequence

import torch

from gamegraph.models.contract import DistributionDeclaration, FittedModel, ModelDeclaration
from gamegraph.models.distributions import distribution_for
from gamegraph.models.parameters import ParameterValue, initial_value, value_from_fitted


class Model:
    def __init__(self, distribution: DistributionDeclaration, parameters: tuple[ParameterValue, ...]) -> None:
        names = distribution.parameter_names()
        if len(parameters) != len(names):
            raise ValueError(f"{distribution} needs {len(names)} parameters, got {len(parameters)}")
        self.distribution = distribution
        self._implementation = distribution_for(distribution)
        self.parameters = parameters

    def designs(self, features: torch.Tensor) -> tuple[torch.Tensor, ...]:
        if features.dtype != torch.float64:
            raise ValueError(f"Features must be float64, got {features.dtype}")
        return tuple(p.design(features) for p in self.parameters)

    def parameter_values_of(self, designs: tuple[torch.Tensor, ...]) -> tuple[torch.Tensor, ...]:
        return tuple(p.value_of(d) for p, d in zip(self.parameters, designs, strict=True))

    def log_prob_of(self, designs: tuple[torch.Tensor, ...], outcomes: torch.Tensor) -> torch.Tensor:
        return self._implementation.log_prob(self.parameter_values_of(designs), outcomes)

    def parameter_values(self, features: torch.Tensor) -> tuple[torch.Tensor, ...]:
        return self.parameter_values_of(self.designs(features))

    def log_prob(self, features: torch.Tensor, outcomes: torch.Tensor) -> torch.Tensor:
        return self.log_prob_of(self.designs(features), outcomes)

    def sample(self, features: torch.Tensor, uniforms: torch.Tensor) -> torch.Tensor:
        return self._implementation.sample(self.parameter_values(features), uniforms)

    def coefficients(self) -> list[torch.Tensor]:
        return [tensor for p in self.parameters for tensor in p.trainable()]

    def to_fitted(self, node: str) -> FittedModel:
        names = self.distribution.parameter_names()
        return FittedModel(
            node, self.distribution, {name: p.to_fitted() for name, p in zip(names, self.parameters, strict=True)}
        )

    @staticmethod
    def from_fitted(fitted: FittedModel, features: Sequence[str]) -> "Model":
        names = fitted.distribution.parameter_names()
        return Model(fitted.distribution, tuple(value_from_fitted(fitted.parameters[n], features) for n in names))

    @staticmethod
    def initial(
        declaration: ModelDeclaration, features: Sequence[str], examples: torch.Tensor, outcomes: torch.Tensor
    ) -> "Model":
        starts = distribution_for(declaration.distribution).initial_parameters(outcomes)
        return Model(
            declaration.distribution,
            tuple(
                initial_value(name, declaration.parameters[name], features, examples, starts)
                for name in declaration.distribution.parameter_names()
            ),
        )
