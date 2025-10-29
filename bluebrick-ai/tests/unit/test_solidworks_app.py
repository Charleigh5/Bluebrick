from __future__ import annotations

from unittest.mock import MagicMock

import pytest

from cad.solidworks import solidworks_app
from cad.solidworks.geometry import Rectangle


@pytest.fixture
def sw_app(monkeypatch):
    dispatch = MagicMock()
    sw_instance = MagicMock()
    sw_instance.GetLastError.return_value = (0, "")
    dispatch.return_value = sw_instance

    monkeypatch.setattr(solidworks_app.pythoncom, "CoInitialize", MagicMock())
    monkeypatch.setattr(solidworks_app.pythoncom, "CoUninitialize", MagicMock())
    monkeypatch.setattr(solidworks_app.win32com.client, "Dispatch", dispatch)

    app = solidworks_app.SolidWorksApp()
    return app, sw_instance


def test_new_part_invokes_solidworks(sw_app):
    app, sw_instance = sw_app
    sw_instance.NewDocument.return_value = "doc"

    result = app.new_part()

    assert result == "doc"
    sw_instance.NewDocument.assert_called_with("", 0, 0.0, 0.0)


def test_add_extruded_boss_draws_profile(sw_app):
    app, _ = sw_app
    model = MagicMock()
    model.Extension.SelectByID2.return_value = True
    model.FeatureManager.FeatureExtrusion2.return_value = object()

    profile = [Rectangle(center=(0.0, 0.0, 0.0), width=0.1, height=0.1)]
    app.add_extruded_boss(model, profile=profile, depth=0.25)

    model.Extension.SelectByID2.assert_called_once()
    model.FeatureManager.FeatureExtrusion2.assert_called_once()


def test_add_extruded_boss_raises_when_plane_missing(sw_app):
    app, _ = sw_app
    model = MagicMock()
    model.Extension.SelectByID2.return_value = False

    with pytest.raises(RuntimeError):
        app.add_extruded_boss(model, depth=0.25)


def test_close_cleans_up(sw_app, monkeypatch):
    app, sw_instance = sw_app
    co_uninitialize = MagicMock()
    monkeypatch.setattr(solidworks_app.pythoncom, "CoUninitialize", co_uninitialize)

    app.close()

    sw_instance.ExitApp.assert_called_once()
    assert co_uninitialize.called
