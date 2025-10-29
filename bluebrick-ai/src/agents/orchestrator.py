"""LangChain agent orchestration for CAD operations routed through STA workers."""

from __future__ import annotations

import atexit
from typing import Callable, Dict

from langchain.agents import AgentExecutor, Tool
from langchain.chat_models import ChatOpenAI

from cad.pdm.pdm_vault import PdmVault
from cad.solidworks.geometry import Rectangle
from cad.solidworks.solidworks_app import SolidWorksApp
from utils.context import DesignContext
from workers.sta_worker import StaWorker, progress_logger


class AgentFactory:
    """Factory responsible for wiring together the CAD agent stack."""

    def __init__(
        self,
        sw_factory: Callable[[], SolidWorksApp],
        pdm_factory: Callable[[], PdmVault],
    ) -> None:
        self._sw_factory = sw_factory
        self._pdm_factory = pdm_factory

    def build(self) -> AgentExecutor:
        context = DesignContext()

        def _setup_resources() -> Dict[str, object]:
            sw = self._sw_factory()
            pdm = self._pdm_factory()
            return {"solidworks": sw, "pdm": pdm}

        def _teardown_resources(resources: Dict[str, object]) -> None:
            sw = resources.get("solidworks")
            if sw:
                sw.close()
            pdm = resources.get("pdm")
            if pdm:
                pdm.close()

        worker = StaWorker(setup=_setup_resources, teardown=_teardown_resources)
        atexit.register(worker.shutdown)

        def _create_part_task(resources, progress, depth: float):
            solidworks: SolidWorksApp = resources["solidworks"]
            if progress:
                progress("start_part", {"depth": depth})
            model = solidworks.new_part()
            profile = [Rectangle(center=(0.0, 0.0, 0.0), width=0.1, height=0.1)]
            solidworks.add_extruded_boss(model, profile=profile, depth=depth)
            if progress:
                progress("extrude_complete", {"depth": depth})
            return {"message": f"Created part with extrusion depth {depth:.3f} m"}

        def _pdm_checkout_task(resources, progress, path: str):
            pdm: PdmVault = resources["pdm"]
            if progress:
                progress("checkout", {"path": path})
            pdm.checkout(path)
            return {"message": f"Checked out {path}"}

        def _pdm_checkin_task(resources, progress, path: str, comment: str = ""):
            pdm: PdmVault = resources["pdm"]
            if progress:
                progress("checkin", {"path": path, "comment": comment})
            pdm.checkin(path, comment)
            return {"message": f"Checked in {path}"}

        def create_part(depth: float) -> str:
            future = worker.submit(_create_part_task, depth, progress_callback=progress_logger)
            result = future.result()
            context.record_action("create_part", {"depth": depth})
            return result["message"]

        def pdm_checkout(path: str) -> str:
            future = worker.submit(_pdm_checkout_task, path, progress_callback=progress_logger)
            result = future.result()
            context.record_action("pdm_checkout", {"path": path})
            return result["message"]

        def pdm_checkin(path: str, comment: str = "") -> str:
            future = worker.submit(_pdm_checkin_task, path, comment, progress_callback=progress_logger)
            result = future.result()
            context.record_action("pdm_checkin", {"path": path, "comment": comment})
            return result["message"]

        tools = [
            Tool(
                name="create_part",
                func=create_part,
                description="Create a new extruded part with a specified depth.",
            ),
            Tool(
                name="pdm_checkout",
                func=pdm_checkout,
                description="Check out a file from the PDM vault by path.",
            ),
            Tool(
                name="pdm_checkin",
                func=pdm_checkin,
                description="Check in a file to the PDM vault with an optional comment.",
            ),
        ]

        llm = ChatOpenAI(temperature=0.0)
        return AgentExecutor.from_agent_and_tools(llm, tools)


def build_default_agent() -> AgentExecutor:
    """Convenience helper for the default SolidWorks/PDM agent."""

    factory = AgentFactory(SolidWorksApp, lambda: PdmVault("EngineeringVault"))
    return factory.build()
