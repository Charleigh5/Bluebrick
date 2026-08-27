export type ActiveDocumentContextState = "ready" | "no-document" | "loading" | "unavailable" | "error";

export type ActiveDocumentContext = {
  state: ActiveDocumentContextState;
  message: string;
  documentType: string;
  titleHash: string;
  pathHash: string;
  activeConfigurationHash: string;
  runtimeVersion: string;
  isDirty?: boolean;
  isReadOnly?: boolean;
  customPropertyCount: number;
  propertyEvidence: ActiveDocumentPropertyDigest[];
  componentEvidence: ActiveDocumentComponentDigest[];
  assemblyTraversal: ActiveDocumentAssemblyTraversal;
  assemblyPayloadStatus: "ok" | "empty" | "malformed" | "oversized";
  mutationActions: number;
};

export type ActiveDocumentPropertyDigest = {
  evidenceId: string;
  canonicalField: string;
  scope: "document" | "configuration" | "component";
  rawValueHash: string;
  evaluatedValueHash: string;
  normalizedValueHash: string;
  wasResolved: boolean;
  linkedToParent: boolean;
  resultCode: number;
  readStatus: "resolved" | "cached-unresolved" | "missing" | "unsupported";
  ruleId: string;
};

export type ActiveDocumentComponentDigest = {
  snapshotId: string;
  parentSnapshotId: string;
  nativeComponentId: number;
  depth: number;
  nameHash: string;
  nativePathHash: string;
  identifierHash: string;
  referencedConfigurationHash: string;
  kind: "unknown" | "part" | "assembly";
  suppressionState: string;
  resolutionState: "unknown" | "resolved" | "lightweight" | "suppressed" | "unloaded" | "missing-reference";
  childrenState: string;
  isVirtual: boolean;
  isGraphicsOnly: boolean;
  isSpeedPak: boolean;
  propertyEvidence: ActiveDocumentPropertyDigest[];
  limitations: string[];
};

export type ActiveDocumentAssemblyTraversal = {
  maxDepth: number;
  recordLimit: number;
  recordedCount: number;
  unloadedCount: number;
  cycleCount: number;
  truncated: boolean;
  mutationActions: number;
  externalSystemsAccessed: boolean;
  warnings: string[];
};

const emptyTraversal: ActiveDocumentAssemblyTraversal = {
  maxDepth: 32,
  recordLimit: 5000,
  recordedCount: 0,
  unloadedCount: 0,
  cycleCount: 0,
  truncated: false,
  mutationActions: 0,
  externalSystemsAccessed: false,
  warnings: []
};
const maxAssemblyPayloadCharacters = 524288;

type RecordValue = Record<string, unknown>;

function asRecord(value: unknown): RecordValue {
  return value && typeof value === "object" && !Array.isArray(value) ? (value as RecordValue) : {};
}

function value(record: RecordValue, camel: string, pascal: string): unknown {
  return record[camel] ?? record[pascal];
}

function text(record: RecordValue, camel: string, pascal: string, fallback = ""): string {
  const next = value(record, camel, pascal);
  return next === undefined || next === null ? fallback : String(next);
}

function bool(record: RecordValue, camel: string, pascal: string): boolean | undefined {
  const next = value(record, camel, pascal);
  if (typeof next === "boolean") return next;
  if (typeof next !== "string") return undefined;
  if (next.toLowerCase() === "true") return true;
  if (next.toLowerCase() === "false") return false;
  return undefined;
}

function count(record: RecordValue, camel: string, pascal: string): number {
  const next = Number(value(record, camel, pascal));
  return Number.isFinite(next) && next >= 0 ? next : 0;
}

function normalizeDocumentType(raw: string): string {
  switch (raw.trim().toUpperCase()) {
    case "PART":
      return "Part";
    case "ASSEMBLY":
      return "Assembly";
    case "DRAWING":
      return "Drawing";
    default:
      return "Unknown document";
  }
}

function stateFromStatus(status: string): ActiveDocumentContextState {
  switch (status.toLowerCase()) {
    case "ok":
      return "ready";
    case "no_active_document":
      return "no-document";
    case "loading":
      return "loading";
    case "error":
      return "error";
    default:
      return "unavailable";
  }
}

function parsePropertyEvidence(raw: string): ActiveDocumentPropertyDigest[] {
  if (!raw) return [];
  try {
    const parsed = JSON.parse(raw);
    if (!Array.isArray(parsed)) return [];
    return parsed.flatMap((item) => {
      const record = asRecord(item);
      const canonicalField = text(record, "canonicalField", "CanonicalField").trim().toLowerCase();
      const scope = text(record, "scope", "Scope").trim().toLowerCase();
      const readStatus = text(record, "readStatus", "ReadStatus").trim().toLowerCase();
      if (!canonicalField || !["document", "configuration", "component"].includes(scope)) return [];
      if (!["resolved", "cached-unresolved", "missing", "unsupported"].includes(readStatus)) return [];
      return [{
        evidenceId: text(record, "evidenceId", "EvidenceId"),
        canonicalField,
        scope: scope as ActiveDocumentPropertyDigest["scope"],
        rawValueHash: text(record, "rawValueHash", "RawValueHash", "redacted"),
        evaluatedValueHash: text(record, "evaluatedValueHash", "EvaluatedValueHash", "redacted"),
        normalizedValueHash: text(record, "normalizedValueHash", "NormalizedValueHash", "redacted"),
        wasResolved: bool(record, "wasResolved", "WasResolved") === true,
        linkedToParent: bool(record, "linkedToParent", "LinkedToParent") === true,
        resultCode: Number(value(record, "resultCode", "ResultCode")) || 0,
        readStatus: readStatus as ActiveDocumentPropertyDigest["readStatus"],
        ruleId: text(record, "ruleId", "RuleId")
      }];
    });
  } catch {
    return [];
  }
}

function stringList(value: unknown): string[] {
  return Array.isArray(value) ? value.filter((item): item is string => typeof item === "string").slice(0, 100) : [];
}

function parseComponentEvidence(raw: string): { items: ActiveDocumentComponentDigest[]; status: ActiveDocumentContext["assemblyPayloadStatus"] } {
  if (!raw) return { items: [], status: "empty" };
  if (raw.length > maxAssemblyPayloadCharacters) return { items: [], status: "oversized" };
  try {
    const parsed = JSON.parse(raw);
    if (!Array.isArray(parsed)) return { items: [], status: "malformed" };
    const items = parsed.slice(0, 5000).flatMap((item) => {
      const record = asRecord(item);
      const snapshotId = text(record, "snapshotId", "SnapshotId");
      const identifierHash = text(record, "identifierHash", "IdentifierHash", "redacted");
      if (!snapshotId || !identifierHash) return [];
      const propertyRaw = value(record, "propertyEvidence", "PropertyEvidence");
      const propertyEvidence = parsePropertyEvidence(JSON.stringify(Array.isArray(propertyRaw) ? propertyRaw : []));
      const kind = text(record, "kind", "Kind", "unknown").toLowerCase();
      const resolutionState = text(record, "resolutionState", "ResolutionState", "unknown").toLowerCase();
      return [{
        snapshotId,
        parentSnapshotId: text(record, "parentSnapshotId", "ParentSnapshotId"),
        nativeComponentId: Number(value(record, "nativeComponentId", "NativeComponentId")) || 0,
        depth: Math.max(0, Number(value(record, "depth", "Depth")) || 0),
        nameHash: text(record, "nameHash", "NameHash", "redacted"),
        nativePathHash: text(record, "nativePathHash", "NativePathHash", "redacted"),
        identifierHash,
        referencedConfigurationHash: text(record, "referencedConfigurationHash", "ReferencedConfigurationHash", "redacted"),
        kind: (["part", "assembly"].includes(kind) ? kind : "unknown") as ActiveDocumentComponentDigest["kind"],
        suppressionState: text(record, "suppressionState", "SuppressionState", "unknown").toLowerCase(),
        resolutionState: (["resolved", "lightweight", "suppressed", "unloaded", "missing-reference"].includes(resolutionState) ? resolutionState : "unknown") as ActiveDocumentComponentDigest["resolutionState"],
        childrenState: text(record, "childrenState", "ChildrenState", "unknown").toLowerCase(),
        isVirtual: bool(record, "isVirtual", "IsVirtual") === true,
        isGraphicsOnly: bool(record, "isGraphicsOnly", "IsGraphicsOnly") === true,
        isSpeedPak: bool(record, "isSpeedPak", "IsSpeedPak") === true,
        propertyEvidence,
        limitations: stringList(value(record, "limitations", "Limitations"))
      }];
    });
    return { items, status: "ok" };
  } catch {
    return { items: [], status: "malformed" };
  }
}

function parseAssemblyTraversal(raw: string): ActiveDocumentAssemblyTraversal {
  if (!raw || raw.length > 16384) return { ...emptyTraversal };
  try {
    const record = asRecord(JSON.parse(raw));
    return {
      maxDepth: count(record, "maxDepth", "MaxDepth") || 32,
      recordLimit: count(record, "recordLimit", "RecordLimit") || 5000,
      recordedCount: count(record, "recordedCount", "RecordedCount"),
      unloadedCount: count(record, "unloadedCount", "UnloadedCount"),
      cycleCount: count(record, "cycleCount", "CycleCount"),
      truncated: bool(record, "truncated", "Truncated") === true,
      mutationActions: count(record, "mutationActions", "MutationActions"),
      externalSystemsAccessed: bool(record, "externalSystemsAccessed", "ExternalSystemsAccessed") === true,
      warnings: stringList(value(record, "warnings", "Warnings"))
    };
  } catch {
    return { ...emptyTraversal };
  }
}

export function normalizeActiveDocumentContext(raw: unknown): ActiveDocumentContext {
  const result = asRecord(raw);
  const state = stateFromStatus(text(result, "status", "Status", "unavailable"));
  const items = value(result, "items", "Items");
  const firstItem = Array.isArray(items) ? items[0] : undefined;
  const metadata = asRecord(value(asRecord(firstItem), "metadata", "Metadata"));
  const message = text(
    result,
    "message",
    "Message",
    "Active document context is unavailable until the in-process read-only adapter supplies a result."
  );
  const componentPayload = parseComponentEvidence(text(metadata, "component_evidence_json", "ComponentEvidenceJson"));
  const hostAssemblyPayloadStatus = text(metadata, "assembly_payload_status", "AssemblyPayloadStatus").toLowerCase();
  if (hostAssemblyPayloadStatus.includes("oversized")) componentPayload.status = "oversized";
  if (hostAssemblyPayloadStatus.includes("malformed")) componentPayload.status = "malformed";
  const assemblyTraversal = parseAssemblyTraversal(text(metadata, "assembly_traversal_json", "AssemblyTraversalJson"));

  if (state === "no-document") {
    return {
      state,
      message,
      documentType: "No active document",
      titleHash: "redacted",
      pathHash: "redacted",
      activeConfigurationHash: "redacted",
      runtimeVersion: "unknown",
      customPropertyCount: 0,
      propertyEvidence: [],
      componentEvidence: [],
      assemblyTraversal: { ...emptyTraversal },
      assemblyPayloadStatus: "empty",
      mutationActions: 0
    };
  }

  return {
    state,
    message,
    documentType: normalizeDocumentType(text(metadata, "document_type", "DocumentType")),
    titleHash: text(metadata, "document_title_hash", "DocumentTitleHash", "redacted"),
    pathHash: text(metadata, "document_path_hash", "DocumentPathHash", "redacted"),
    activeConfigurationHash: text(metadata, "active_configuration_hash", "ActiveConfigurationHash", "redacted"),
    runtimeVersion: text(metadata, "runtime_version", "RuntimeVersion", "unknown"),
    isDirty: bool(metadata, "is_dirty", "IsDirty"),
    isReadOnly: bool(metadata, "is_read_only", "IsReadOnly"),
    customPropertyCount: count(metadata, "custom_property_count", "CustomPropertyCount"),
    propertyEvidence: parsePropertyEvidence(text(metadata, "property_evidence_json", "PropertyEvidenceJson")),
    componentEvidence: componentPayload.items,
    assemblyTraversal,
    assemblyPayloadStatus: componentPayload.status,
    mutationActions: count(metadata, "mutation_actions", "MutationActions")
  };
}

export function activeDocumentContextFromToolResult(raw: unknown): ActiveDocumentContext | null {
  const result = asRecord(raw);
  const receipt = asRecord(value(result, "receipt", "Receipt"));
  const toolName = text(result, "toolName", "ToolName", text(receipt, "toolName", "ToolName"));
  const label = text(result, "label", "Label");
  const isActiveDocumentResult =
    toolName.toLowerCase() === "read_active_document_context" ||
    label.toLowerCase() === "read active document context" ||
    label.toLowerCase() === "read_active_document_context";

  return isActiveDocumentResult ? normalizeActiveDocumentContext(result) : null;
}
