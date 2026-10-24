from pathlib import Path

import pytest
from synthetic import bernoulli, categorical

from gamegraph.models.contract_files import read_fitted_model, read_training_report
from gamegraph.models.train_nodes import train_nodes
from gamegraph.models.training_data import write_training_data


def test_every_node_s_training_data_is_trained_and_written(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    write_training_data(tmp_path / "data" / "Second", categorical("Second", games=20, rows_per_game=50, seed=3))
    write_training_data(tmp_path / "data" / "First", bernoulli("First", games=20, rows_per_game=50, seed=2))
    (tmp_path / "data" / "notes").mkdir()
    train_nodes(tmp_path / "data", tmp_path / "fitted")
    for node in ("First", "Second"):
        assert read_fitted_model(tmp_path / "fitted" / f"{node}.json").node == node
        assert read_training_report(tmp_path / "fitted" / f"{node}.training.json").node == node
    lines = capsys.readouterr().out.splitlines()
    assert [line.split(":")[0] for line in lines] == ["First", "Second"]


def test_a_folder_without_training_data_fails(tmp_path: Path) -> None:
    with pytest.raises(ValueError, match="No node's training data"):
        train_nodes(tmp_path, tmp_path / "fitted")
