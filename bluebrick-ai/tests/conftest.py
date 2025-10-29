"""Shared pytest configuration for bluebrick-ai."""

from __future__ import annotations

import sys
import types
from pathlib import Path
from unittest.mock import MagicMock

PROJECT_ROOT = Path(__file__).resolve().parents[1]
SRC_PATH = PROJECT_ROOT / "src"

if str(SRC_PATH) not in sys.path:
    sys.path.insert(0, str(SRC_PATH))


def _ensure_mock_module(name: str, module: types.ModuleType) -> None:
    if name not in sys.modules:
        sys.modules[name] = module


pythoncom_stub = types.ModuleType("pythoncom")
pythoncom_stub.CoInitialize = MagicMock()
pythoncom_stub.CoUninitialize = MagicMock()

win32com_stub = types.ModuleType("win32com")
win32com_client_stub = types.ModuleType("win32com.client")
win32com_client_stub.Dispatch = MagicMock()
win32com_stub.client = win32com_client_stub

comtypes_stub = types.ModuleType("comtypes")
comtypes_client_stub = types.ModuleType("comtypes.client")
comtypes_client_stub.CreateObject = MagicMock()
comtypes_stub.client = comtypes_client_stub

_ensure_mock_module("pythoncom", pythoncom_stub)
_ensure_mock_module("win32com", win32com_stub)
_ensure_mock_module("win32com.client", win32com_client_stub)
_ensure_mock_module("comtypes", comtypes_stub)
_ensure_mock_module("comtypes.client", comtypes_client_stub)
