import type { ActiveDocumentContext } from "../activeDocumentContext";
import type { PhaseAComparison } from "../cad-compare/phaseAComparison";
import type { PhaseBComparisonReport } from "../cad-compare/phaseBComparison";
import type { PacketReview } from "../packet-review/packetReview";

export type ViraRuntimeKind = "fixture" | "localhost-relay" | "embedded-host" | "unavailable";

export type ViraDiagnostic = {
  id: string;
  level: "info" | "warning" | "error";
  code: string;
  message: string;
};

export type ViraCapabilityResult<T> =
  | { status: "ok"; value: T }
  | { status: "unavailable" | "error"; code: string; message: string; value?: undefined };

export type ViraComparisonResult = {
  phaseA: PhaseAComparison | null;
  phaseB: PhaseBComparisonReport | null;
};

export type ViraSession = {
  id: string;
  runtimeKind: ViraRuntimeKind;
  context: ActiveDocumentContext | null;
  packet: PacketReview | null;
  comparison: ViraComparisonResult | null;
};

export type DiagnosticListener = (diagnostic: ViraDiagnostic) => void;

export type ViraLabRuntime = {
  kind: ViraRuntimeKind;
  sessionId: string;
  getActiveDocumentContext(): Promise<ViraCapabilityResult<ActiveDocumentContext>>;
  reviewPacket(file: File): Promise<ViraCapabilityResult<PacketReview>>;
  comparePacket(
    packet: PacketReview,
    document: ActiveDocumentContext
  ): Promise<ViraCapabilityResult<ViraComparisonResult>>;
  exportReceipt(session: ViraSession): Promise<ViraCapabilityResult<Blob>>;
  subscribeDiagnostics(listener: DiagnosticListener): () => void;
};

const fixtureContext: ActiveDocumentContext = {
  state: "ready",
  message: "Deterministic local fixture aligned to the VIRA Lab packet smoke sample. No SOLIDWORKS session or engineering system was accessed.",
  documentType: "Assembly",
  titleHash: "sha256:869eca596e991863",
  pathHash: "redacted",
  activeConfigurationHash: "fixture:default",
  runtimeVersion: "vira-lab-fixture-v1",
  isDirty: false,
  isReadOnly: true,
  customPropertyCount: 3,
  propertyEvidence: [
    {
      evidenceId: "fixture-property-part-number",
      canonicalField: "part_number",
      scope: "document",
      rawValueHash: "value_sha256:869eca596e991863",
      evaluatedValueHash: "value_sha256:869eca596e991863",
      normalizedValueHash: "value_sha256:869eca596e991863",
      wasResolved: true,
      linkedToParent: false,
      resultCode: 1,
      readStatus: "resolved",
      ruleId: "VIRA-FIXTURE-CONTROLLED-PART-NUMBER-001"
    },
    {
      evidenceId: "fixture-property-revision",
      canonicalField: "revision",
      scope: "document",
      rawValueHash: "value_sha256:df7e70e5021544f4",
      evaluatedValueHash: "value_sha256:df7e70e5021544f4",
      normalizedValueHash: "value_sha256:df7e70e5021544f4",
      wasResolved: true,
      linkedToParent: false,
      resultCode: 1,
      readStatus: "resolved",
      ruleId: "VIRA-FIXTURE-REVISION-001"
    },
    {
      evidenceId: "fixture-property-description",
      canonicalField: "description",
      scope: "document",
      rawValueHash: "value_sha256:0ceb3d5feec75781",
      evaluatedValueHash: "value_sha256:0ceb3d5feec75781",
      normalizedValueHash: "value_sha256:0ceb3d5feec75781",
      wasResolved: true,
      linkedToParent: false,
      resultCode: 1,
      readStatus: "resolved",
      ruleId: "VIRA-FIXTURE-DESCRIPTION-001"
    }
  ],
  componentEvidence: [],
  assemblyTraversal: {
    maxDepth: 32,
    recordLimit: 5000,
    recordedCount: 0,
    unloadedCount: 0,
    cycleCount: 0,
    truncated: false,
    mutationActions: 0,
    externalSystemsAccessed: false,
    warnings: ["Fixture context only. Component evidence is intentionally absent so incomplete-evidence handling remains testable."]
  },
  assemblyPayloadStatus: "empty",
  mutationActions: 0
};

function unavailable<T>(code: string, message: string): ViraCapabilityResult<T> {
  return { status: "unavailable", code, message };
}

function createUnavailableRuntime(kind: Exclude<ViraRuntimeKind, "fixture">): ViraLabRuntime {
  const message =
    kind === "localhost-relay"
      ? "The localhost relay does not expose the complete VIRA Lab runtime contract."
      : kind === "embedded-host"
        ? "The embedded SOLIDWORKS host adapter is not enabled in the browser-first slice."
        : "No VIRA Lab runtime is available.";
  const result = { status: "unavailable" as const, code: "VIRA_RUNTIME_UNAVAILABLE", message };

  return {
    kind,
    sessionId: `VIRA-LAB-${kind.toUpperCase()}-UNAVAILABLE`,
    getActiveDocumentContext: async () => result,
    reviewPacket: async () => result,
    comparePacket: async () => result,
    exportReceipt: async () => result,
    subscribeDiagnostics(listener) {
      listener({ id: "diagnostic-001", level: "warning", code: result.code, message: result.message });
      return () => undefined;
    }
  };
}

function fixtureContextFromSearch(params: URLSearchParams): ViraCapabilityResult<ActiveDocumentContext> {
  const state = params.get("fixtureState");
  if (state === "unavailable") {
    return unavailable("FIXTURE_CONTEXT_UNAVAILABLE", "The fixture context was intentionally marked unavailable.");
  }
  if (state === "error") {
    return { status: "error", code: "FIXTURE_CONTEXT_ERROR", message: "The fixture context returned a controlled test error." };
  }
  if (state === "loading") {
    return {
      status: "ok",
      value: { ...fixtureContext, state: "loading", message: "The fixture context is held in a controlled loading state." }
    };
  }
  if (state === "no-document") {
    return {
      status: "ok",
      value: {
        ...fixtureContext,
        state: "no-document",
        message: "The fixture runtime has no active document.",
        documentType: "No active document",
        titleHash: "redacted",
        customPropertyCount: 0
      }
    };
  }
  return { status: "ok", value: fixtureContext };
}

function createFixtureRuntime(params: URLSearchParams): ViraLabRuntime {
  const listeners = new Set<DiagnosticListener>();
  const emit = (diagnostic: ViraDiagnostic) => {
    for (const listener of listeners) listener(diagnostic);
  };

  return {
    kind: "fixture",
    sessionId: "VIRA-LAB-FIXTURE-001",
    async getActiveDocumentContext() {
      const result = fixtureContextFromSearch(params);
      emit({
        id: "diagnostic-context-001",
        level: result.status === "ok" ? "info" : result.status === "error" ? "error" : "warning",
        code: result.status === "ok" ? "FIXTURE_CONTEXT_READY" : result.code,
        message: result.status === "ok" ? "Fixture active-document context loaded." : result.message
      });
      return result;
    },
    async reviewPacket() {
      return unavailable("PACKET_REVIEW_NOT_ENABLED", "Packet review is added in the next VIRA Lab slice.");
    },
    async comparePacket() {
      return unavailable("PACKET_COMPARE_NOT_ENABLED", "Packet comparison is added after packet review is integrated.");
    },
    async exportReceipt() {
      return unavailable("RECEIPT_EXPORT_NOT_ENABLED", "Receipt export is added after the first complete fixture workflow.");
    },
    subscribeDiagnostics(listener) {
      listeners.add(listener);
      return () => listeners.delete(listener);
    }
  };
}

export function createViraLabRuntimeFromSearch(search: string): ViraLabRuntime {
  const params = new URLSearchParams(search);
  const requested = params.get("runtime");
  if (requested === "localhost-relay" || requested === "embedded-host" || requested === "unavailable") {
    return createUnavailableRuntime(requested);
  }
  return createFixtureRuntime(params);
}
