"""Geometry primitives compatible with SolidWorks sketch operations."""

from __future__ import annotations

from dataclasses import dataclass
from typing import Protocol, Tuple


class SketchSegment(Protocol):
    """Protocol describing the operations required for sketch segments."""

    def draw(self, sketch_manager) -> None:
        """Draw the segment using the provided sketch manager."""


@dataclass(slots=True)
class Line:
    """Straight line sketch element."""

    start: Tuple[float, float, float]
    end: Tuple[float, float, float]

    def draw(self, sketch_manager) -> None:  # pragma: no cover - call into COM
        sketch_manager.CreateLine(*self.start, *self.end)


@dataclass(slots=True)
class Rectangle:
    """Axis-aligned rectangle defined by its center point."""

    center: Tuple[float, float, float]
    width: float
    height: float

    def draw(self, sketch_manager) -> None:  # pragma: no cover - call into COM
        half_w = self.width / 2
        half_h = self.height / 2
        sketch_manager.CreateCenterRectangle(
            self.center[0],
            self.center[1],
            self.center[2],
            self.center[0] + half_w,
            self.center[1] + half_h,
            self.center[2],
        )


@dataclass(slots=True)
class Circle:
    """Circle defined by center point and radius."""

    center: Tuple[float, float, float]
    radius: float

    def draw(self, sketch_manager) -> None:  # pragma: no cover - call into COM
        sketch_manager.CreateCircleByRadius(
            self.center[0],
            self.center[1],
            self.center[2],
            self.radius,
        )
