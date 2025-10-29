"""SolidWorks COM automation wrapper.

This module provides a minimal SolidWorks application facade that can be used
by higher-level orchestration layers. It is intentionally light-weight so it
can run inside a dedicated single-threaded apartment (STA) worker responsible
for all COM interactions.
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Iterable, Protocol

import pythoncom
import win32com.client


class SketchSegment(Protocol):
    """Protocol describing the operations required for sketch segments."""

    def draw(self, sketch_manager) -> None:
        """Draw the segment using the provided sketch manager."""


@dataclass
class SolidWorksApp:
    """Entry point into the SolidWorks COM API."""

    visible: bool = False

    def __post_init__(self) -> None:
        # COM requires explicit initialization for the current thread.
        pythoncom.CoInitialize()
        self.sw_app = win32com.client.Dispatch("SldWorks.Application")
        self.sw_app.Visible = self.visible

    def new_part(self):
        """Create and return a new blank part document."""
        # Arguments map to template path, document type, width, height.
        # Using default template and scale for now.
        return self.sw_app.NewDocument("", 0, 0.0, 0.0)

    def add_extruded_boss(
        self,
        model,
        sketch_plane: str = "Front",
        profile: Iterable[SketchSegment] | None = None,
        depth: float = 0.1,
    ) -> None:
        """Add a simple boss-extrude feature to the provided model."""

        feat_mgr = model.FeatureManager
        sketch_mgr = model.SketchManager
        # Select the sketch plane and begin the sketch.
        model.Extension.SelectByID2(
            sketch_plane,
            "PLANE",
            0,
            0,
            0,
            False,
            0,
            None,
            0,
        )
        sketch_mgr.InsertSketch(True)

        for segment in profile or []:
            segment.draw(sketch_mgr)

        sketch_mgr.InsertSketch(True)
        feat_mgr.FeatureExtrusion2(
            True,
            False,
            False,
            0,
            0,
            depth,
            0,
            False,
            False,
            False,
            False,
            0,
            0,
            False,
            False,
            False,
            False,
            True,
            True,
            True,
            0,
            0,
            False,
        )

    def close(self) -> None:
        """Release the SolidWorks session."""
        try:
            self.sw_app.ExitApp()
        finally:
            pythoncom.CoUninitialize()
