"""Context manager for maintaining CAD design state."""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any, Dict, List, Optional


@dataclass
class DesignContext:
    """Holds context about the ongoing CAD session."""

    history: List[Dict[str, Any]] = field(default_factory=list)
    metadata: Dict[str, Any] = field(default_factory=dict)
    active_document: Optional[str] = None

    def record_action(self, action: str, payload: Dict[str, Any]) -> None:
        self.history.append({"action": action, "payload": payload})

    def set_document(self, doc_id: str) -> None:
        self.active_document = doc_id

    def to_dict(self) -> Dict[str, Any]:
        return {
            "history": list(self.history),
            "metadata": dict(self.metadata),
            "active_document": self.active_document,
        }
