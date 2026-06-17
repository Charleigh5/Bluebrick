from __future__ import annotations

from unittest.mock import MagicMock

import pytest

from cad.pdm import pdm_vault


@pytest.fixture
def pdm(monkeypatch):
    vault = MagicMock()
    vault.GetFileFromPath.return_value = (MagicMock(), MagicMock())
    create_object = MagicMock(return_value=vault)
    monkeypatch.setattr(pdm_vault.comtypes.client, "CreateObject", create_object)

    instance = pdm_vault.PdmVault("EngineeringVault")
    return instance, vault


def test_checkout_locks_file(pdm):
    instance, vault = pdm
    file_mock, folder_mock = vault.GetFileFromPath.return_value

    instance.checkout("/path/part.sldprt")

    file_mock.LockFile.assert_called_once_with(folder_mock, 0)


def test_checkin_unlocks_file(pdm):
    instance, vault = pdm
    file_mock, _ = vault.GetFileFromPath.return_value

    instance.checkin("/path/part.sldprt", comment="done")

    file_mock.UnlockFile.assert_called_once_with(0, "done")


def test_change_state_selects_transition(pdm):
    instance, vault = pdm
    file_mock, folder_mock = vault.GetFileFromPath.return_value
    transition = MagicMock()
    transition.Name = "Approved"
    transition.ID = 5
    file_mock.GetAllowedTransitions.return_value = [transition]
    folder_mock.FileGetState.return_value = MagicMock(Name="In Work")

    instance.change_state("/path/part.sldprt", "Approved")

    file_mock.ChangeState.assert_called_once_with(transition.ID, comment="Automated state change")


def test_change_state_raises_for_missing_transition(pdm):
    instance, vault = pdm
    file_mock, folder_mock = vault.GetFileFromPath.return_value
    file_mock.GetAllowedTransitions.return_value = []
    folder_mock.FileGetState.return_value = MagicMock(Name="In Work")

    with pytest.raises(RuntimeError):
        instance.change_state("/path/part.sldprt", "Released")


def test_close_logs_out(pdm, monkeypatch):
    instance, vault = pdm
    logout = MagicMock()
    vault.Logout = logout

    instance.close()

    logout.assert_called_once()
