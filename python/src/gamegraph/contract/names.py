from __future__ import annotations

from collections import Counter


def require_distinct(names: list[str]) -> None:
    repeated = sorted(name for name, count in Counter(names).items() if count > 1)
    if repeated:
        raise ValueError(f"{repeated} appear more than once")
