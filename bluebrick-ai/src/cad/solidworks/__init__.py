"""SolidWorks automation helpers."""

from .geometry import Circle, Line, Rectangle, SketchSegment
from .solidworks_app import SolidWorksApp

__all__ = [
    "SolidWorksApp",
    "Circle",
    "Line",
    "Rectangle",
    "SketchSegment",
]
