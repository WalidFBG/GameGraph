from pathlib import Path

import pytest
from synthetic import bernoulli, categorical

from gamegraph.models.contract_files import read_fitted_model, read_training_report
from gamegraph.models.train_nodes import train_nodes
from gamegraph.models.training_data import MANIFEST, write_training_data


def two_nodes(directory: Path) -> None:
    write_training_data(directory / "Second", categorical("Second", games=20, rows_per_game=50, seed=3))
    write_training_data(directory / "First", bernoulli("First", games=20, rows_per_game=50, seed=2))


def test_every_node_s_training_data_is_trained_and_replaces_the_fitted_models(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    two_nodes(tmp_path / "data")
    (tmp_path / "fitted").mkdir()
    (tmp_path / "fitted" / "Old.json").write_text("{}", encoding="utf-8")
    train_nodes(tmp_path / "data", tmp_path / "fitted")
    assert sorted(p.name for p in (tmp_path / "fitted").iterdir()) == [
        "First.json",
        "First.training.json",
        "Second.json",
        "Second.training.json",
    ]
    for node in ("First", "Second"):
        assert read_fitted_model(tmp_path / "fitted" / f"{node}.json").node == node
        assert read_training_report(tmp_path / "fitted" / f"{node}.training.json").node == node
    lines = capsys.readouterr().out.splitlines()
    assert [line.split(":")[0] for line in lines] == ["First", "Second"]


def test_a_node_without_a_manifest_fails_and_leaves_the_fitted_models_as_they_were(tmp_path: Path) -> None:
    two_nodes(tmp_path / "data")
    (tmp_path / "data" / "Second" / MANIFEST).unlink()
    (tmp_path / "fitted").mkdir()
    (tmp_path / "fitted" / "Old.json").write_text("{}", encoding="utf-8")
    with pytest.raises(ValueError, match=r"\['Second'\] under .* have no manifest.json"):
        train_nodes(tmp_path / "data", tmp_path / "fitted")
    assert [p.name for p in (tmp_path / "fitted").iterdir()] == ["Old.json"]


def test_a_node_that_fails_to_train_leaves_the_fitted_models_as_they_were(tmp_path: Path) -> None:
    two_nodes(tmp_path / "data")
    (tmp_path / "data" / "Second" / "examples.csv").write_text("broken\n", encoding="utf-8")
    (tmp_path / "fitted").mkdir()
    (tmp_path / "fitted" / "Old.json").write_text("{}", encoding="utf-8")
    with pytest.raises(ValueError):
        train_nodes(tmp_path / "data", tmp_path / "fitted")
    assert [p.name for p in (tmp_path / "fitted").iterdir()] == ["Old.json"]


def test_a_folder_without_training_data_fails(tmp_path: Path) -> None:
    with pytest.raises(ValueError, match="No node's training data"):
        train_nodes(tmp_path, tmp_path / "fitted")
