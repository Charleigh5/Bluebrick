"""End-to-end sample demonstrating SolidWorks + PDM automation."""

from cad.pdm.pdm_vault import PdmVault
from cad.solidworks.solidworks_app import SolidWorksApp
from utils.context import DesignContext
from workflows.simple_plate_workflow import SimplePlateWorkflow


def main() -> None:
    context = DesignContext()
    with SolidWorksApp(visible=False) as sw, PdmVault("EngineeringVault") as pdm:
        workflow = SimplePlateWorkflow(sw, pdm, context)
        workflow.run(
            width=0.1,
            height=0.05,
            thickness=0.005,
            vault_path=r"\\Designs\\Plates\\plate.SLDPRT",
        )

    for record in context.history:
        print(record)


if __name__ == "__main__":
    main()
