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


def outcome_prefixes(distribution: DistributionDeclaration) -> dict[str, int] | None:
    match distribution:
        case BernoulliDistribution() | ChoiceDistribution():
            return {"": 1}
        case CategoricalDistribution(categories=categories):
            return {f"{c} ": k for k, c in enumerate(categories)}
        case DiscreteJohnsonSuDistribution():
            return None
