from __future__ import annotations

from unittest.mock import MagicMock

from utils.context import DesignContext
from workflows.simple_plate_workflow import SimplePlateWorkflow


def test_simple_plate_workflow_records_actions():
    sw = MagicMock()
    pdm = MagicMock()
    context = DesignContext()

    model = MagicMock()
    sw.new_part.return_value = model

    workflow = SimplePlateWorkflow(sw, pdm, context)
    workflow.run(
        width=0.2,
        height=0.1,
        thickness=0.01,
        vault_path="/vault/plate.SLDPRT",
    )

    pdm.checkout.assert_called_once_with("/vault/plate.SLDPRT")
    sw.add_extruded_boss.assert_called_once()
    model.SaveAs.assert_called_once_with("/vault/plate.SLDPRT")
    pdm.checkin.assert_called_once()

    assert context.history[0]["action"] == "checkout"
    assert any(entry["action"] == "extrude" for entry in context.history)
    assert context.active_document == "/vault/plate.SLDPRT"
