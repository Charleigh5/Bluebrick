"""Validates local prerequisites for running bluebrick-ai."""

from __future__ import annotations

import sys
from typing import List

REQUIRED_PROG_IDS = [
    "SldWorks.Application",
    "ConisioLib.EdmVault",
]


def check_prog_id(prog_id: str) -> bool:
    try:
        import win32com.client  # type: ignore

        win32com.client.Dispatch(prog_id)
        return True
    except Exception:
        return False


def main() -> None:
    if not sys.platform.startswith("win"):
        print("Skipping validation: SolidWorks is only available on Windows.")
        return

    missing: List[str] = []
    for prog_id in REQUIRED_PROG_IDS:
        if not check_prog_id(prog_id):
            missing.append(prog_id)

    if missing:
        print("The following COM registrations are missing:")
        for item in missing:
            print(f"  - {item}")
        raise SystemExit(1)

    print("All SolidWorks/EPDM prerequisites satisfied.")


if __name__ == "__main__":
    main()
