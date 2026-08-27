import type { ActiveDocumentContext } from "../activeDocumentContext";
import type { PacketReviewLifecycleEvent, ViraPacketWorkflowState } from "./packetWorkflow";
import type { ViraDiagnostic, ViraRuntimeKind } from "./viraLabRuntime";

export type ViraLabReceiptInput = {
  sessionId: string;
  runtimeKind: ViraRuntimeKind;
  workbenchState: string;
  packetState: ViraPacketWorkflowState;
  packetSummary: PacketReviewLifecycleEvent | null;
  context: ActiveDocumentContext;
  diagnostics: ViraDiagnostic[];
  durationMs: number;
};

function safeToken(raw: string, fallback: string): string {
  const sanitized = raw.replace(/[^a-z0-9_.:-]+/gi, "-").slice(0, 120);
  return sanitized || fallback;
}

function diagnosticSummary(code: string): string {
  if (code === "FIXTURE_CONTEXT_READY") return "Fixture active-document context loaded.";
  if (code.startsWith("FIXTURE_CONTEXT_")) return "Fixture context returned a controlled non-ready state.";
  if (code === "PACKET_PACKET_LOADING") return "Local packet loading started.";
  if (code === "PACKET_READY_TO_COMPARE") return "Local packet evidence is ready for comparison.";
  if (code === "PACKET_EVALUATING") return "Packet evidence comparison started.";
  if (code === "PACKET_COMPLETE") return "Packet review and comparison completed.";
  if (code === "PACKET_PARTIAL") return "Packet review completed with explicit incomplete evidence.";
  if (code === "PACKET_FAILED") return "Packet review failed without retained evidence.";
  if (code === "PACKET_CANCELLED") return "Local packet state was cancelled or cleared.";
  return "Controlled VIRA Lab diagnostic event.";
}

function boundedDiagnostics(diagnostics: ViraDiagnostic[]) {
  return diagnostics.slice(-20).map((item, index) => {
    const code = safeToken(item.code.toUpperCase(), "VIRA_DIAGNOSTIC");
    return {
      sequence: index + 1,
      level: item.level,
      code,
      summary: diagnosticSummary(code)
    };
  });
}

export function buildViraLabReceipt(input: ViraLabReceiptInput, createdUtc = new Date().toISOString()) {
  const packet = input.packetSummary;
  const diagnostics = boundedDiagnostics(input.diagnostics);
  return {
    schemaVersion: "vira.lab.receipt.v1",
    createdUtc,
    session: {
      id: safeToken(input.sessionId, "VIRA-LAB-SESSION"),
      runtimeKind: input.runtimeKind,
      workbenchState: safeToken(input.workbenchState, "unknown"),
      durationMs: Math.max(0, Math.round(input.durationMs))
    },
    context: {
      state: input.context.state,
      documentType: safeToken(input.context.documentType, "unknown"),
      readOnly: input.context.isReadOnly === true,
      propertyEvidenceCount: input.context.propertyEvidence.length,
      componentEvidenceCount: input.context.componentEvidence.length,
      assemblyPayloadStatus: input.context.assemblyPayloadStatus,
      traversalTruncated: input.context.assemblyTraversal.truncated,
      mutationActions: input.context.mutationActions
    },
    packet: {
      state: input.packetState,
      fileName: packet?.fileName || "packet.pdf",
      pageCount: packet?.pageCount ?? 0,
      findingCount: packet?.findingCount ?? 0,
      retainedEvidence: packet?.retainsEvidence === true,
      phaseAAuthority: safeToken(packet?.phaseAStatus ?? "", "unavailable"),
      phaseBComparisonCount: packet?.phaseBComparisonCount ?? 0
    },
    provenance: {
      localPacketOnly: true,
      retainedPageEvidence: packet?.retainsEvidence === true,
      digestOnlyCadContext: true,
      rawPacketTextPersisted: false,
      rawCadValuesPersisted: false
    },
    diagnostics: {
      count: diagnostics.length,
      warningCodes: diagnostics.filter((item) => item.level !== "info").map((item) => item.code)
    },
    actions: {
      engineeringMutations: input.context.mutationActions,
      connectorCalls: 0,
      externalRequests: 0,
      fileUploads: 0
    }
  };
}

export function buildViraDiagnosticsExport(
  sessionId: string,
  diagnostics: ViraDiagnostic[],
  createdUtc = new Date().toISOString()
) {
  return {
    schemaVersion: "vira.lab.diagnostics.v1",
    createdUtc,
    sessionId: safeToken(sessionId, "VIRA-LAB-SESSION"),
    boundedTo: 20,
    entries: boundedDiagnostics(diagnostics),
    redaction: {
      includesRawPacketText: false,
      includesCadPaths: false,
      includesSecrets: false,
      includesDiagnosticIds: false
    }
  };
}
