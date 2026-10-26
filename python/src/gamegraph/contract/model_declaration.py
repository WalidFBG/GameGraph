from __future__ import annotations

from collections.abc import Iterable
from dataclasses import dataclass

from gamegraph.contract.distributions import DistributionDeclaration, distribution_from_json
from gamegraph.contract.json_document import Json
from gamegraph.contract.names import require_distinct
from gamegraph.contract.terms import Term, term_from_json


@dataclass(frozen=True)
class ModelDeclaration:
    distribution: DistributionDeclaration
    terms: dict[str, tuple[Term, ...]]
    given: dict[str, str]

    def __post_init__(self) -> None:
        _require_parameters(self.distribution, self.terms.keys(), self.given.keys())
        for terms in self.terms.values():
            require_distinct([t.feature for t in terms])

    def features(self) -> set[str]:
        return {t.feature for terms in self.terms.values() for t in terms} | set(self.given.values())

    def to_json(self) -> Json:
        return {
            "distribution": self.distribution.to_json(),
            "terms": {name: [t.to_json() for t in terms] for name, terms in self.terms.items()},
            "given": dict(self.given),
        }

    @staticmethod
    def from_json(json: Json) -> ModelDeclaration:
        return ModelDeclaration(
            distribution_from_json(json["distribution"]),
            {name: tuple(term_from_json(t) for t in terms) for name, terms in json["terms"].items()},
            dict(json["given"]),
        )


def _require_parameters(distribution: DistributionDeclaration, predicted: Iterable[str], given: Iterable[str]) -> None:
    expected, actual = sorted(p.name for p in distribution.predicted()), sorted(predicted)
    if actual != expected:
        raise ValueError(f"{distribution} predicts {expected}, got {actual}")
    expected, actual = sorted(distribution.given()), sorted(given)
    if actual != expected:
        raise ValueError(f"{distribution} is given {expected}, got {actual}")
