"""SolidWorks COM automation wrapper with resilience features."""

from __future__ import annotations

import logging
import time
from contextlib import AbstractContextManager
from dataclasses import dataclass, field
from typing import Any, Iterable, Optional

import pythoncom
import win32com.client

from .geometry import SketchSegment

LOGGER = logging.getLogger("bluebrick_ai.cad.solidworks")


@dataclass
class SolidWorksApp(AbstractContextManager):
    """Entry point into the SolidWorks COM API."""

    visible: bool = False
    max_attempts: int = 3
    retry_delay: float = 0.5
    _sw_app: Any = field(init=False, repr=False)
    _co_initialized: bool = field(default=False, init=False, repr=False)
    _closed: bool = field(default=False, init=False, repr=False)

    def __post_init__(self) -> None:
        self._logger = LOGGER
        self._initialize_com()
        self._sw_app = self._dispatch("SldWorks.Application")
        self._sw_app.Visible = self.visible
        self._logger.debug("SolidWorks application initialized (visible=%s)", self.visible)

    # ------------------------------------------------------------------
    # Context manager plumbing
    def __enter__(self) -> "SolidWorksApp":
        return self

    def __exit__(self, exc_type, exc, exc_tb) -> Optional[bool]:
        self.close()
        return None

    # ------------------------------------------------------------------
    # Public API wrappers
    def new_part(self):
        """Create and return a new blank part document."""
        return self._call_com("NewDocument(part)", self._sw_app.NewDocument, "", 0, 0.0, 0.0)

    def new_assembly(self):
        """Create a new assembly document."""
        return self._call_com("NewDocument(assembly)", self._sw_app.NewDocument, "", 2, 0.0, 0.0)

    def create_assembly(self):
        """Alias for :meth:`new_assembly` for semantic clarity."""

        return self.new_assembly()

    def new_drawing(self, template: str):
        """Create a new drawing using the provided template path."""
        return self._call_com("NewDocument(drawing)", self._sw_app.NewDocument, template, 3, 0.0, 0.0)

    def add_extruded_boss(
        self,
        model,
        sketch_plane: str = "Front",
        profile: Iterable[SketchSegment] | None = None,
        depth: float = 0.1,
    ) -> None:
        """Add a simple boss-extrude feature to the provided model."""

        def _operation():
            feat_mgr = model.FeatureManager
            sketch_mgr = model.SketchManager
            self._logger.debug("Creating sketch on plane '%s'", sketch_plane)
            selection = model.Extension.SelectByID2(
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
            if not selection:
                raise RuntimeError(f"Failed to select sketch plane '{sketch_plane}'")
            sketch_mgr.InsertSketch(True)

            for segment in profile or []:
                segment.draw(sketch_mgr)

            sketch_mgr.InsertSketch(True)
            feature = feat_mgr.FeatureExtrusion2(
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
            if feature is None:
                raise RuntimeError("FeatureExtrusion2 returned None; extrusion failed")

        self._call_com("FeatureExtrusion2", _operation)

    def apply_material(self, model, material_database: str, material_name: str) -> None:
        """Assign a material to the active document."""

        def _operation():
            success = model.SetMaterialPropertyName2("", material_database, material_name)
            if not success:
                raise RuntimeError(
                    f"Failed to apply material '{material_name}' using database '{material_database}'"
                )

        self._call_com("SetMaterialPropertyName2", _operation)

    def create_drawing(self, part_path: str, template: str):
        """Create a drawing document and add the model as the base view."""

        drawing = self.new_drawing(template)

        def _operation():
            view = drawing.CreateDrawViewFromModelView3(
                part_path,
                "*Front",
                0.1,
                0.1,
                0,
            )
            if view is None:
                raise RuntimeError("Failed to create drawing view from model")

        self._call_com("CreateDrawViewFromModelView3", _operation)
        return drawing

    def add_drawing_view(self, drawing, model_path: str, orientation: str, x: float, y: float, scale: float = 1.0):
        """Add an additional projected view to a drawing."""

        def _operation():
            view = drawing.CreateDrawViewFromModelView3(model_path, orientation, x, y, scale)
            if view is None:
                raise RuntimeError("Failed to insert drawing view")

        self._call_com("CreateDrawViewFromModelView3", _operation)

    def insert_bill_of_materials(self, drawing, anchor_point: tuple[float, float], configuration: str = "Default"):
        """Insert a Bill of Materials table into the drawing."""

        def _operation():
            table = drawing.InsertBomTable4(
                anchor_point[0],
                anchor_point[1],
                0,
                configuration,
                0,
                False,
                None,
            )
            if table is None:
                raise RuntimeError("Failed to insert BOM table")

        self._call_com("InsertBomTable4", _operation)

    def add_component_to_assembly(self, assembly, component_path: str, transform_data: Optional[list[float]] = None):
        """Add a component into the active assembly document."""

        def _operation():
            component = assembly.AddComponent5(
                component_path,
                0,
                "",
                False,
                "",
                0,
                transform_data or [],
            )
            if component is None:
                raise RuntimeError(f"Failed to add component '{component_path}' to assembly")

        self._call_com("AddComponent5", _operation)

    def close(self) -> None:
        """Release the SolidWorks session."""
        if self._closed:
            return
        self._closed = True
        try:
            if hasattr(self, "_sw_app"):
                self._logger.debug("Closing SolidWorks application")
                try:
                    self._call_com("ExitApp", self._sw_app.ExitApp)
                except Exception:  # pragma: no cover - defensive cleanup
                    self._logger.exception("Error while closing SolidWorks")
        finally:
            if self._co_initialized:
                pythoncom.CoUninitialize()
                self._co_initialized = False
                self._logger.debug("pythoncom.CoUninitialize completed")

    # ------------------------------------------------------------------
    # Internal helpers
    def _initialize_com(self) -> None:
        if not self._co_initialized:
            pythoncom.CoInitialize()
            self._co_initialized = True
            self._logger.debug("pythoncom.CoInitialize completed")

    def _dispatch(self, prog_id: str):
        return self._call_com(f"Dispatch({prog_id})", win32com.client.Dispatch, prog_id)

    def _call_com(self, description: str, func, *args, **kwargs):
        for attempt in range(1, self.max_attempts + 1):
            try:
                result = func(*args, **kwargs)
                last_error = self._get_last_error()
                if last_error:
                    code, message = last_error
                    if code not in (0, None):
                        self._logger.warning("SolidWorks reported error %s: %s", code, message)
                return result
            except Exception as exc:  # pragma: no cover - relies on COM failures
                self._logger.exception(
                    "COM call '%s' failed (attempt %s/%s)", description, attempt, self.max_attempts
                )
                if attempt >= self.max_attempts:
                    raise
                time.sleep(self.retry_delay)
        return None

    def _get_last_error(self) -> Optional[tuple[int, str]]:
        if not hasattr(self, "_sw_app"):
            return None
        try:
            err_code, err_msg = self._sw_app.GetLastError()
            return err_code, err_msg
        except Exception:  # pragma: no cover - depends on COM implementation
            return None
