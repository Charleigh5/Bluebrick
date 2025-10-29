"""Sample workflow orchestration tying CAD and PDM steps together."""

from __future__ import annotations

from cad.pdm.pdm_vault import PdmVault
from cad.solidworks.solidworks_app import SolidWorksApp
from utils.context import DesignContext


class SimplePlateWorkflow:
    """Create a simple plate, save it, and check it into PDM."""

    def __init__(self, sw: SolidWorksApp, pdm: PdmVault, context: DesignContext):
        self.sw = sw
        self.pdm = pdm
        self.context = context

    def run(
        self,
        width: float,
        height: float,
        thickness: float,
        vault_path: str,
        comment: str = "Automated plate design",
    ) -> None:
        self.pdm.checkout(vault_path)
        self.context.record_action("checkout", {"path": vault_path})

        model = self.sw.new_part()
        self.context.set_document(vault_path)

        # Example uses anonymous rectangle helper drawn externally.
        self.sw.add_extruded_boss(model, depth=thickness)
        self.context.record_action(
            "extrude",
            {"width": width, "height": height, "thickness": thickness},
        )

        model.SaveAs(vault_path)
        self.context.record_action("save", {"path": vault_path})

        self.pdm.checkin(vault_path, comment)
        self.context.record_action("checkin", {"path": vault_path, "comment": comment})
