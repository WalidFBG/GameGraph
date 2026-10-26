from __future__ import annotations

from datetime import UTC, datetime, timedelta


def utc_to_text(moment: datetime) -> str:
    if moment.tzinfo != UTC:
        raise ValueError(f"{moment} is not in UTC")
    return moment.isoformat().replace("+00:00", "Z")


def utc_from_text(text: str) -> datetime:
    moment = datetime.fromisoformat(text)
    if moment.utcoffset() != timedelta(0):
        raise ValueError(f"{text} is not in UTC")
    return moment.astimezone(UTC)
