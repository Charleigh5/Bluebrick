export type ViraPacketWorkflowState =
  | "idle"
  | "packet-loading"
  | "ready-to-compare"
  | "evaluating"
  | "partial"
  | "complete"
  | "failed"
  | "cancelled";

export type ViraPacketWorkflowEvent =
  | { type: "select" }
  | { type: "packet-ready" }
  | { type: "evaluate" }
  | { type: "complete" }
  | { type: "fail"; retainsEvidence: boolean }
  | { type: "cancel" }
  | { type: "reset" };

export type PacketReviewLifecycleEvent = {
  state: ViraPacketWorkflowState;
  fileName: string;
  message: string;
  retainsEvidence: boolean;
  pageCount: number;
  findingCount: number;
  phaseAStatus: string;
  phaseBComparisonCount: number;
};

export function nextPacketWorkflowState(
  current: ViraPacketWorkflowState,
  event: ViraPacketWorkflowEvent
): ViraPacketWorkflowState {
  switch (event.type) {
    case "select":
      return "packet-loading";
    case "packet-ready":
      return "ready-to-compare";
    case "evaluate":
      return current === "ready-to-compare" || current === "partial" || current === "complete"
        ? "evaluating"
        : current;
    case "complete":
      return current === "evaluating" ? "complete" : current;
    case "fail":
      return event.retainsEvidence ? "partial" : "failed";
    case "cancel":
      return "cancelled";
    case "reset":
      return "idle";
    default:
      return current;
  }
}

export function sanitizePacketFileName(raw: string): string {
  const baseName = raw.split(/[\\/]/).at(-1)?.trim() || "packet.pdf";
  const sanitized = baseName.replace(/[\u0000-\u001f\u007f]/g, "").slice(-120);
  return sanitized || "packet.pdf";
}
