from dataclasses import dataclass

import numpy as np

from gamegraph.contract import (
    BernoulliDistribution,
    CategoricalDistribution,
    ChoiceDistribution,
    DiscreteJohnsonSuDistribution,
    DistributionDeclaration,
)

MOST_VALUES_FOR_CATEGORIES = 30
QUANTILE_BUCKETS = 20


def cuts(values: np.ndarray) -> np.ndarray:
    distinct = np.unique(values)
    if len(distinct) <= MOST_VALUES_FOR_CATEGORIES:
        return (distinct[:-1] + distinct[1:]) / 2
    return np.unique(np.quantile(values, np.linspace(0, 1, QUANTILE_BUCKETS + 1)[1:-1], method="lower"))


def assign(values: np.ndarray, cuts: np.ndarray) -> np.ndarray:
    return np.searchsorted(cuts, values, side="right")


@dataclass(frozen=True)
class Shares:
    prefixes: dict[str, int]


@dataclass(frozen=True)
class Means:
    pass


type Comparison = Shares | Means


def comparison(distribution: DistributionDeclaration) -> Comparison:
    match distribution:
        case BernoulliDistribution() | ChoiceDistribution():
            return Shares({"": 1})
        case CategoricalDistribution(categories=categories):
            return Shares({f"{c} ": k for k, c in enumerate(categories)})
        case DiscreteJohnsonSuDistribution():
            return Means()
