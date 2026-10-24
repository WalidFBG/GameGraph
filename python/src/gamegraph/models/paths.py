from pathlib import Path

REPOSITORY = Path(__file__).resolve().parents[4]
SCHEMAS = REPOSITORY / "schemas"
GOLDEN = REPOSITORY / "golden" / "models"
