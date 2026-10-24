from datetime import UTC, datetime, timedelta

import numpy as np
import torch

from gamegraph.models.contract import (
    Distribution,
    LinearTerm,
    Link,
    ModelDeclaration,
    PredictedParameter,
    TrainingDataManifest,
)
from gamegraph.models.training_data import Examples, Game, TrainingData

FEATURES = ("Margin", "Three")
INTERCEPT, MARGIN, THREE = -0.4, 0.25, -0.8
START = datetime(2024, 1, 1, tzinfo=UTC)
DECLARATION = ModelDeclaration(
    Distribution.BERNOULLI,
    {"probability": PredictedParameter(Link.LOGIT, (LinearTerm("Margin"), LinearTerm("Three")))},
)


def bernoulli(node: str, games: int, rows_per_game: int, seed: int) -> TrainingData:
    generator = np.random.default_rng(seed)
    rows = games * rows_per_game
    margin = generator.normal(3, 2, rows)
    three = generator.binomial(1, 0.4, rows).astype(np.float64)
    probability = 1 / (1 + np.exp(-(INTERCEPT + MARGIN * margin + THREE * three)))
    outcomes = generator.binomial(1, probability)
    return TrainingData(
        TrainingDataManifest(node, "synthetic", START, FEATURES, rows, DECLARATION),
        tuple(Game(f"g{i}", START + timedelta(days=i), rows_per_game) for i in range(games)),
        Examples(
            torch.tensor(np.column_stack([margin, three]), dtype=torch.float64),
            torch.tensor(outcomes, dtype=torch.int64),
        ),
    )
