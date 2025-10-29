"""Enterprise PDM (EPDM) vault interaction helpers."""

from __future__ import annotations

import logging
import time
from contextlib import AbstractContextManager
from dataclasses import dataclass, field
from typing import Any

import comtypes.client

LOGGER = logging.getLogger("bluebrick_ai.cad.pdm")


@dataclass
class PdmVault(AbstractContextManager):
    """Thin wrapper around the EPDM vault COM interface."""

    vault_name: str
    max_attempts: int = 3
    retry_delay: float = 0.5
    _vault: Any = field(init=False, repr=False)
    _connected: bool = field(default=False, init=False, repr=False)

    def __post_init__(self) -> None:
        self._logger = LOGGER
        self._vault = self._call_com("CreateObject(ConisioLib.EdmVault)", comtypes.client.CreateObject, "ConisioLib.EdmVault")
        self._call_com("LoginAuto", self._vault.LoginAuto, self.vault_name, 0)
        self._connected = True
        self._logger.debug("Connected to PDM vault '%s'", self.vault_name)

    def __enter__(self) -> "PdmVault":
        return self

    def __exit__(self, exc_type, exc, exc_tb) -> None:
        self.close()
        return None

    def checkout(self, path: str) -> None:
        """Check out the specified file for editing."""

        def _operation():
            file, folder = self._vault.GetFileFromPath(path)
            if file is None:
                raise RuntimeError(f"File '{path}' not found in vault")
            file.LockFile(folder, 0)

        self._call_com("LockFile", _operation)

    def checkin(self, path: str, comment: str = "") -> None:
        """Check in the file with an optional comment."""

        def _operation():
            file, _ = self._vault.GetFileFromPath(path)
            if file is None:
                raise RuntimeError(f"File '{path}' not found in vault")
            file.UnlockFile(0, comment)

        self._call_com("UnlockFile", _operation)

    def change_state(self, path: str, state_name: str) -> None:
        """Move the file to a new workflow state."""

        def _operation():
            file, folder = self._vault.GetFileFromPath(path)
            if file is None:
                raise RuntimeError(f"File '{path}' not found in vault")
            state = folder.FileGetState(file.ID)
            if state and getattr(state, "Name", None) == state_name:
                return
            transitions = file.GetAllowedTransitions()
            for transition in transitions or []:
                if getattr(transition, "Name", None) == state_name:
                    file.ChangeState(transition.ID, comment="Automated state change")
                    return
            raise RuntimeError(f"No transition available to state '{state_name}' for '{path}'")

        self._call_com("ChangeState", _operation)

    def close(self) -> None:
        if not self._connected:
            return
        self._connected = False
        self._logger.debug("Closing PDM vault connection")
        try:
            self._call_com("Logout", self._vault.Logout)
        except Exception:  # pragma: no cover - defensive cleanup
            self._logger.exception("Failed to logout from PDM vault")

    # ------------------------------------------------------------------
    def _call_com(self, description: str, func, *args, **kwargs):
        for attempt in range(1, self.max_attempts + 1):
            try:
                return func(*args, **kwargs)
            except Exception as exc:  # pragma: no cover - depends on COM behaviour
                self._logger.exception(
                    "PDM COM call '%s' failed (attempt %s/%s)", description, attempt, self.max_attempts
                )
                if attempt >= self.max_attempts:
                    raise
                time.sleep(self.retry_delay)
        return None
