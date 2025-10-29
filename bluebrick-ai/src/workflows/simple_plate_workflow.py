"""Workflow for creating a plate part and checking it into PDM."""

from __future__ import annotations

import logging
from dataclasses import dataclass

from cad.pdm.pdm_vault import PdmVault
from cad.solidworks.geometry import Rectangle
from cad.solidworks.solidworks_app import SolidWorksApp
from utils.context import DesignContext

LOGGER = logging.getLogger("bluebrick_ai.workflows.simple_plate")


@dataclass
class SimplePlateWorkflow:
    """Create a simple plate, save it, and check it into PDM."""

    sw: SolidWorksApp
    pdm: PdmVault
    context: DesignContext

    def run(
        self,
        width: float,
        height: float,
        thickness: float,
        vault_path: str,
        comment: str = "Automated plate design",
    ) -> None:
        LOGGER.info("Starting plate workflow for %s", vault_path)
        self.pdm.checkout(vault_path)
        self.context.record_action("checkout", {"path": vault_path})

        model = self.sw.new_part()
        self.context.set_document(vault_path)

        profile = [Rectangle(center=(0.0, 0.0, 0.0), width=width, height=height)]
        self.sw.add_extruded_boss(model, profile=profile, depth=thickness)
        self.context.record_action(
            "extrude",
            {"width": width, "height": height, "thickness": thickness},
        )

        model.SaveAs(vault_path)
        self.context.record_action("save", {"path": vault_path})

        self.pdm.checkin(vault_path, comment)
        self.context.record_action("checkin", {"path": vault_path, "comment": comment})
        LOGGER.info("Completed plate workflow for %s", vault_path)
