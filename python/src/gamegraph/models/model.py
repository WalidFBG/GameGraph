from collections.abc import Iterable, Sequence

import torch

from gamegraph.models.contract import Distribution, FittedModel, ModelDeclaration
from gamegraph.models.distributions import Bernoulli
from gamegraph.models.predictor import Predictor


class Model:
    def __init__(self, distribution: Distribution, predictors: tuple[Predictor, ...]) -> None:
        self.distribution = distribution
        self._implementation = _implementation(distribution)
        if len(predictors) != len(self._implementation.PARAMETERS):
            raise ValueError(
                f"{distribution} needs {len(self._implementation.PARAMETERS)} parameters, got {len(predictors)}"
            )
        self.predictors = predictors

    def designs(self, features: torch.Tensor) -> tuple[torch.Tensor, ...]:
        if features.dtype != torch.float64:
            raise ValueError(f"Features must be float64, got {features.dtype}")
        return tuple(p.design(features) for p in self.predictors)

    def parameter_values_of(self, designs: tuple[torch.Tensor, ...]) -> tuple[torch.Tensor, ...]:
        return tuple(p.value_of(d) for p, d in zip(self.predictors, designs, strict=True))

    def log_prob_of(self, designs: tuple[torch.Tensor, ...], outcomes: torch.Tensor) -> torch.Tensor:
        return self._implementation.log_prob(self.parameter_values_of(designs), outcomes)

    def parameter_values(self, features: torch.Tensor) -> tuple[torch.Tensor, ...]:
        return self.parameter_values_of(self.designs(features))

    def log_prob(self, features: torch.Tensor, outcomes: torch.Tensor) -> torch.Tensor:
        return self.log_prob_of(self.designs(features), outcomes)

    def sample(self, features: torch.Tensor, uniforms: torch.Tensor) -> torch.Tensor:
        return self._implementation.sample(self.parameter_values(features), uniforms)

    def coefficients(self) -> list[torch.Tensor]:
        return [tensor for p in self.predictors for tensor in (p.intercept, p.coefficients)]

    def to_fitted(self, node: str) -> FittedModel:
        names = self._implementation.PARAMETERS
        return FittedModel(
            node, self.distribution, {name: p.to_fitted() for name, p in zip(names, self.predictors, strict=True)}
        )

    @staticmethod
    def from_fitted(fitted: FittedModel, features: Sequence[str]) -> "Model":
        names = _parameter_names(fitted.distribution, fitted.parameters.keys())
        return Model(fitted.distribution, tuple(Predictor.from_fitted(fitted.parameters[n], features) for n in names))

    @staticmethod
    def initial(
        declaration: ModelDeclaration, features: Sequence[str], examples: torch.Tensor, outcomes: torch.Tensor
    ) -> "Model":
        names = _parameter_names(declaration.distribution, declaration.parameters.keys())
        starts = _implementation(declaration.distribution).initial_parameters(outcomes)
        return Model(
            declaration.distribution,
            tuple(
                Predictor.initial(declaration.parameters[n], features, examples, start)
                for n, start in zip(names, starts, strict=True)
            ),
        )


def _implementation(distribution: Distribution) -> type[Bernoulli]:
    match distribution:
        case Distribution.BERNOULLI:
            return Bernoulli


def _parameter_names(distribution: Distribution, given: Iterable[str]) -> tuple[str, ...]:
    names = _implementation(distribution).PARAMETERS
    given_names = list(given)
    if set(given_names) != set(names):
        raise ValueError(f"{distribution} has parameters {list(names)}, got {given_names}")
    return names
