from datetime import UTC, datetime, timedelta

import numpy as np
import torch

from gamegraph.contract import (
    BernoulliDistribution,
    CategoricalDistribution,
    ChoiceDistribution,
    DiscreteJohnsonSuDistribution,
    LinearSplineTerm,
    LinearTerm,
    ModelDeclaration,
    TrainingDataManifest,
)
from gamegraph.models.distributions import DiscreteJohnsonSu
from gamegraph.training.training_data import Examples, Game, TrainingData

START = datetime(2024, 1, 1, tzinfo=UTC)

BERNOULLI_FEATURES = ("Gaussian", "Indicator", "Uniform")
BERNOULLI_DECLARATION = ModelDeclaration(
    BernoulliDistribution(),
    {"probability": (LinearTerm("Gaussian"), LinearTerm("Indicator"), LinearSplineTerm("Uniform", 5))},
    {},
)

CATEGORIES = ("first", "second", "third")
CATEGORICAL_FEATURES = ("Gaussian", "Uniform")
CATEGORICAL_DECLARATION = ModelDeclaration(
    CategoricalDistribution(CATEGORIES),
    {category: (LinearTerm("Gaussian"), LinearSplineTerm("Uniform", 5)) for category in CATEGORIES[1:]},
    {},
)

CANDIDATES = 5
CHOICE_FEATURES = ("Share", "Indicator")
CHOICE_DECLARATION = ModelDeclaration(
    ChoiceDistribution(), {"utility": (LinearSplineTerm("Share", 4), LinearTerm("Indicator"))}, {}
)

INTERCEPT_ONLY_DECLARATION = ModelDeclaration(BernoulliDistribution(), {"probability": ()}, {})

JOHNSON_SU_FEATURES = ("Gaussian", "Bound")
JOHNSON_SU_DECLARATION = ModelDeclaration(
    DiscreteJohnsonSuDistribution(),
    {"location": (LinearTerm("Gaussian"),), "scale": (LinearTerm("Gaussian"),), "skew": (), "tail": ()},
    {"maximum": "Bound"},
)


def johnson_su_parameters(gaussian: torch.Tensor, bound: torch.Tensor) -> tuple[torch.Tensor, ...]:
    return (
        9 + 2 * gaussian,
        torch.exp(1.5 + 0.2 * gaussian),
        torch.full_like(gaussian, 0.6),
        torch.full_like(gaussian, float(np.exp(0.4))),
        bound,
    )


def bernoulli_logit(gaussian: np.ndarray, indicator: np.ndarray, uniform: np.ndarray) -> np.ndarray:
    kink = 0.08 * np.minimum(uniform, 12) - 0.1 * np.maximum(uniform - 12, 0)
    return -0.4 + 0.25 * gaussian - 0.8 * indicator + kink


def categorical_log_odds(gaussian: np.ndarray, uniform: np.ndarray) -> np.ndarray:
    second = -0.3 + 0.15 * gaussian + 0.04 * np.minimum(uniform, 12) - 0.06 * np.maximum(uniform - 12, 0)
    third = -1.0 - 0.1 * gaussian + 0.05 * np.maximum(uniform - 12, 0)
    return np.column_stack([second, third])


def choice_utility(share: np.ndarray, indicator: np.ndarray) -> np.ndarray:
    return 1.2 * np.minimum(share, -1.5) + 0.3 * np.maximum(share + 1.5, 0) + 0.6 * indicator


def bernoulli(node: str, games: int, rows_per_game: int, seed: int) -> TrainingData:
    generator = np.random.default_rng(seed)
    rows = games * rows_per_game
    gaussian = generator.normal(3, 2, rows)
    indicator = generator.binomial(1, 0.4, rows).astype(np.float64)
    uniform = generator.integers(0, 25, rows).astype(np.float64)
    outcomes = generator.binomial(1, 1 / (1 + np.exp(-bernoulli_logit(gaussian, indicator, uniform))))
    manifest = TrainingDataManifest(node, "synthetic", START, BERNOULLI_FEATURES, rows, BERNOULLI_DECLARATION)
    return _training_data(manifest, rows_per_game, np.column_stack([gaussian, indicator, uniform]), outcomes)


def categorical(node: str, games: int, rows_per_game: int, seed: int) -> TrainingData:
    generator = np.random.default_rng(seed)
    rows = games * rows_per_game
    gaussian = generator.normal(3, 2, rows)
    uniform = generator.integers(0, 25, rows).astype(np.float64)
    scores = np.column_stack([np.zeros(rows), categorical_log_odds(gaussian, uniform)])
    probabilities = np.exp(scores) / np.exp(scores).sum(axis=1, keepdims=True)
    outcomes = (probabilities.cumsum(axis=1) <= generator.random(rows)[:, None]).sum(axis=1)
    manifest = TrainingDataManifest(node, "synthetic", START, CATEGORICAL_FEATURES, rows, CATEGORICAL_DECLARATION)
    return _training_data(manifest, rows_per_game, np.column_stack([gaussian, uniform]), outcomes)


def choice(node: str, games: int, decisions_per_game: int, seed: int) -> TrainingData:
    generator = np.random.default_rng(seed)
    decisions = games * decisions_per_game
    share = np.log(generator.dirichlet(np.full(CANDIDATES, 2.0), decisions))
    indicator = generator.binomial(1, 0.3, (decisions, CANDIDATES)).astype(np.float64)
    utility = choice_utility(share, indicator)
    probabilities = np.exp(utility) / np.exp(utility).sum(axis=1, keepdims=True)
    outcomes = (probabilities.cumsum(axis=1) <= generator.random(decisions)[:, None]).sum(axis=1)
    manifest = TrainingDataManifest(node, "synthetic", START, CHOICE_FEATURES, decisions, CHOICE_DECLARATION)
    return _training_data(manifest, decisions_per_game, np.stack([share, indicator], axis=2), outcomes)


def intercept_only(node: str, games: int, rows_per_game: int, seed: int, probability: float) -> TrainingData:
    rows = games * rows_per_game
    outcomes = np.random.default_rng(seed).binomial(1, probability, rows)
    manifest = TrainingDataManifest(node, "synthetic", START, (), rows, INTERCEPT_ONLY_DECLARATION)
    return _training_data(manifest, rows_per_game, np.zeros((rows, 0)), outcomes)


def johnson_su(node: str, games: int, rows_per_game: int, seed: int) -> TrainingData:
    generator = np.random.default_rng(seed)
    rows = games * rows_per_game
    gaussian = generator.normal(0, 1, rows)
    draw = generator.random(rows)
    bound = np.where(draw < 0.7, 24.0, np.where(draw < 0.9, 14.0, generator.integers(1, 14, rows).astype(np.float64)))
    parameters = johnson_su_parameters(torch.tensor(gaussian), torch.tensor(bound))
    outcomes = DiscreteJohnsonSu().sample(parameters, torch.tensor(generator.random(rows))).numpy()
    manifest = TrainingDataManifest(node, "synthetic", START, JOHNSON_SU_FEATURES, rows, JOHNSON_SU_DECLARATION)
    return _training_data(manifest, rows_per_game, np.column_stack([gaussian, bound]), outcomes)


def _training_data(
    manifest: TrainingDataManifest, rows_per_game: int, features: np.ndarray, outcomes: np.ndarray
) -> TrainingData:
    games = manifest.examples // rows_per_game
    return TrainingData(
        manifest,
        tuple(Game(f"g{i}", START + timedelta(days=i), rows_per_game) for i in range(games)),
        Examples(torch.tensor(features, dtype=torch.float64), torch.tensor(outcomes, dtype=torch.int64)),
    )
