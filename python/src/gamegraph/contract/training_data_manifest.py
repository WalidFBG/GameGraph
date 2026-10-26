from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime

from gamegraph.contract.json_document import Json
from gamegraph.contract.model_declaration import ModelDeclaration
from gamegraph.contract.names import require_distinct
from gamegraph.contract.utc import utc_from_text, utc_to_text


@dataclass(frozen=True)
class TrainingDataManifest:
    node: str
    source: str
    created_utc: datetime
    features: tuple[str, ...]
    examples: int
    model: ModelDeclaration

    def __post_init__(self) -> None:
        require_distinct(list(self.features))
        missing = self.model.features() - set(self.features)
        if missing:
            raise ValueError(f"The model of {self.node} uses {sorted(missing)}, which are not among its features")
        unused = set(self.features) - self.model.features()
        if unused:
            raise ValueError(f"{self.node} has features {sorted(unused)} that its model does not use")

    def to_json(self) -> Json:
        return {
            "node": self.node,
            "source": self.source,
            "createdUtc": utc_to_text(self.created_utc),
            "features": list(self.features),
            "examples": self.examples,
            "model": self.model.to_json(),
        }

    @staticmethod
    def from_json(json: Json) -> TrainingDataManifest:
        return TrainingDataManifest(
            json["node"],
            json["source"],
            utc_from_text(json["createdUtc"]),
            tuple(json["features"]),
            json["examples"],
            ModelDeclaration.from_json(json["model"]),
        )
