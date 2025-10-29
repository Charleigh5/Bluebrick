"""LangChain agent orchestration for CAD operations."""

from __future__ import annotations

from typing import Callable, Dict

from langchain.agents import AgentExecutor, Tool
from langchain.chat_models import ChatOpenAI

from cad.pdm.pdm_vault import PdmVault
from cad.solidworks.solidworks_app import SolidWorksApp
from utils.context import DesignContext


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
        solidworks = self._sw_factory()
        pdm = self._pdm_factory()
        context = DesignContext()

        def create_part(depth: float) -> str:
            model = solidworks.new_part()
            solidworks.add_extruded_boss(model, depth=depth)
            context.record_action("create_part", {"depth": depth})
            return f"Created part with extrusion depth {depth:.3f} m"

        def pdm_checkout(path: str) -> str:
            pdm.checkout(path)
            context.record_action("pdm_checkout", {"path": path})
            return f"Checked out {path}"

        def pdm_checkin(path: str, comment: str = "") -> str:
            pdm.checkin(path, comment)
            context.record_action(
                "pdm_checkin", {"path": path, "comment": comment}
            )
            return f"Checked in {path}"

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
