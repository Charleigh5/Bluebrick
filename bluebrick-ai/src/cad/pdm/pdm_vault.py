"""Enterprise PDM (EPDM) vault interaction helpers."""

from __future__ import annotations

from dataclasses import dataclass

import comtypes.client


@dataclass
class PdmVault:
    """Thin wrapper around the EPDM vault COM interface."""

    vault_name: str

    def __post_init__(self) -> None:
        self.vault = comtypes.client.CreateObject("ConisioLib.EdmVault")
        # LoginAuto will reuse cached user credentials configured locally.
        self.vault.LoginAuto(self.vault_name, 0)

    def checkout(self, path: str) -> None:
        """Check out the specified file for editing."""
        file, folder = self.vault.GetFileFromPath(path)
        file.LockFile(folder, 0)

    def checkin(self, path: str, comment: str = "") -> None:
        """Check in the file with an optional comment."""
        file, _ = self.vault.GetFileFromPath(path)
        file.UnlockFile(0, comment)
