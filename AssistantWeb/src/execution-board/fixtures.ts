import type { CapabilityState } from "./contracts/CapabilityState";
import type {
  ActionPlan,
  CapabilityDecision,
  Claim,
  EvidenceReference,
  SourceAsset
} from "./contracts/EngineeringResult";

export type { CapabilityState } from "./contracts/CapabilityState";
export type { ExecutionReceipt, RouteMode } from "./contracts/ExecutionReceipt";
export type {
  ActionPlan,
  CapabilityDecision,
  Claim,
  EngineeringQueryRoute,
  EvidenceReference,
  SourceAsset
} from "./contracts/EngineeringResult";

export const sourceAssets: SourceAsset[] = [
  {
    id: "BB-SRC-1001",
    title: "Execution Board Handoff",
    authority: "handoff-installed",
    sourceType: "handoff",
    path: "docs/handoffs/vira-bluebrick-execution-board/CODEX_HANDOFF__EXECUTION_AUTHORITY_AND_PROTOTYPE_LOOP.md",
    summary: "Prototype-first authority ladder for governed Level 0 through Level 5 work.",
    state: "LOCAL",
    keywords: ["execution", "handoff", "prototype", "receipt", "authority", "level"]
  },
  {
    id: "BB-SRC-1002",
    title: "Assistant Safe Verification Runbook",
    authority: "repo-local",
    sourceType: "runbook",
    path: "docs/BLUEBRICK_ASSISTANT_SAFE_VERIFICATION_RUNBOOK.md",
    summary: "Non-live validation path for AssistantWeb, bridge contracts, and browser-local smoke.",
    state: "READ_ONLY",
    keywords: ["safe", "verification", "smoke", "assistantweb", "browser", "local"]
  },
  {
    id: "BB-SRC-1003",
    title: "Route Manifest",
    authority: "repo-local",
    sourceType: "repo",
    path: "docs/BLUEBRICK_ASSISTANT_ROUTE_MANIFEST.md",
    summary: "Bridge route risk map that blocks CAD, native PDM, and destructive routes from assistant use.",
    state: "READ_ONLY",
    keywords: ["route", "manifest", "sw", "pdm", "policy", "blocked"]
  },
  {
    id: "BB-SRC-1004",
    title: "Engineering Query Sandbox Fixtures",
    authority: "local-fixture",
    sourceType: "fixture",
    path: "AssistantWeb/src/execution-board/fixtures.ts",
    summary: "Local-only fixture set for demonstrating deterministic routing without live adapters.",
    state: "MOCK",
    keywords: ["fixture", "sandbox", "query", "mock", "local", "evidence"]
  }
];

export const evidenceReferences: EvidenceReference[] = [
  {
    id: "BB-EVID-2001",
    sourceAssetId: "BB-SRC-1001",
    title: "Prototype-first loop",
    authority: "source-quote",
    quote: "Intent -> source/context recovery -> capability routing -> smallest working implementation -> build/run -> visible output.",
    claimIds: ["BB-CLAIM-3001"]
  },
  {
    id: "BB-EVID-2002",
    sourceAssetId: "BB-SRC-1002",
    title: "Safe runner boundary",
    authority: "source-quote",
    quote: "The runner does not launch SOLIDWORKS, call PDM, call Epicor, call Salesforce, install packages, or read secrets.",
    claimIds: ["BB-CLAIM-3002"]
  },
  {
    id: "BB-EVID-2003",
    sourceAssetId: "BB-SRC-1003",
    title: "Critical route policy",
    authority: "source-quote",
    quote: "Critical routes must not be callable from assistant text without policy decision, human approval, and durable receipt.",
    claimIds: ["BB-CLAIM-3003"]
  },
  {
    id: "BB-EVID-2004",
    sourceAssetId: "BB-SRC-1004",
    title: "Local fixture authority",
    authority: "fixture-assertion",
    quote: "Fixture records are examples only; unavailable live systems return NOT_CONNECTED rather than invented data.",
    claimIds: ["BB-CLAIM-3004"]
  }
];

export const claims: Claim[] = [
  {
    id: "BB-CLAIM-3001",
    title: "Smallest working slice",
    statement: "The first execution-board behavior should route an engineering request into evidence, capability state, preview action, and receipt.",
    evidenceIds: ["BB-EVID-2001"],
    authority: "handoff",
    confidence: "handoff-intent"
  },
  {
    id: "BB-CLAIM-3002",
    title: "Level 1/2 safety boundary",
    statement: "The sandbox can run as a local browser/Vite surface without touching CAD, PDM, Epicor, Salesforce, secrets, or production data.",
    evidenceIds: ["BB-EVID-2002"],
    authority: "repo",
    confidence: "repo-observed"
  },
  {
    id: "BB-CLAIM-3003",
    title: "Live action approval boundary",
    statement: "SOLIDWORKS, PDM, Epicor, and Salesforce actions must remain previews until explicit Level 3+ approval exists.",
    evidenceIds: ["BB-EVID-2003"],
    authority: "repo",
    confidence: "repo-observed"
  },
  {
    id: "BB-CLAIM-3004",
    title: "No fabrication behavior",
    statement: "When a requested adapter is unavailable, the UI must show NOT_CONNECTED and a data gap rather than inventing live results.",
    evidenceIds: ["BB-EVID-2004"],
    authority: "fixture",
    confidence: "fixture"
  }
];

export const capabilityDecisions: CapabilityDecision[] = [
  {
    id: "BB-CAP-4001",
    label: "Local evidence lookup",
    state: "LOCAL",
    reason: "Uses structured fixtures bundled in AssistantWeb.",
    approvalBoundary: "No approval required for local fixture search.",
    relatedClaimIds: ["BB-CLAIM-3001", "BB-CLAIM-3002"]
  },
  {
    id: "BB-CAP-4002",
    label: "SOLIDWORKS context read",
    state: "APPROVAL_REQUIRED",
    reason: "Would require a named non-production test file and Level 3 receipt.",
    approvalBoundary: "Blocked until LAB_SMOKE_APPROVED=true with test file path.",
    relatedClaimIds: ["BB-CLAIM-3003"]
  },
  {
    id: "BB-CAP-4003",
    label: "PDM search",
    state: "NOT_CONNECTED",
    reason: "No live PDM credentials or approved wrapper are active in this sandbox.",
    approvalBoundary: "Read-only PDM validation requires scoped configuration and approval.",
    relatedClaimIds: ["BB-CLAIM-3003", "BB-CLAIM-3004"]
  },
  {
    id: "BB-CAP-4004",
    label: "Epicor lookup",
    state: "NOT_CONNECTED",
    reason: "No approved Epicor read adapter is connected to this local prototype.",
    approvalBoundary: "Epicor access requires explicit read-only configuration and data policy.",
    relatedClaimIds: ["BB-CLAIM-3004"]
  },
  {
    id: "BB-CAP-4005",
    label: "Mock action preview",
    state: "MOCK",
    reason: "Builds typed local action previews without executing live routes.",
    approvalBoundary: "No live execution is performed from this sandbox.",
    relatedClaimIds: ["BB-CLAIM-3001", "BB-CLAIM-3003"]
  },
  {
    id: "BB-CAP-4006",
    label: "Read-only route policy review",
    state: "READ_ONLY",
    reason: "Displays route policy facts from local source references.",
    approvalBoundary: "Review-only; no bridge calls are made.",
    relatedClaimIds: ["BB-CLAIM-3002", "BB-CLAIM-3003"]
  }
];

export const actionPlans: ActionPlan[] = [
  {
    id: "BB-ACT-5001",
    capabilityId: "BB-CAP-4002",
    title: "Preview SOLIDWORKS metadata read",
    executionState: "NON_EXECUTABLE_PREVIEW",
    previewType: "approval_packet",
    steps: [
      "Require a named non-production SOLIDWORKS test file.",
      "Checkpoint modified time and hash before opening.",
      "Run read-only metadata capture.",
      "Close without save and write runtime receipt."
    ],
    blockedLiveSystems: ["SOLIDWORKS", "CAD files", "COM adapter"]
  },
  {
    id: "BB-ACT-5002",
    capabilityId: "BB-CAP-4003",
    title: "Preview PDM search",
    executionState: "NON_EXECUTABLE_PREVIEW",
    previewType: "approval_packet",
    steps: [
      "Require configured read-only PDM wrapper.",
      "Validate search scope and redaction policy.",
      "Return only metadata allowed by policy.",
      "Attach receipt before promotion."
    ],
    blockedLiveSystems: ["PDM", "Vault credentials", "Customer metadata"]
  },
  {
    id: "BB-ACT-5003",
    capabilityId: "BB-CAP-4004",
    title: "Preview Epicor part lookup",
    executionState: "NON_EXECUTABLE_PREVIEW",
    previewType: "mock_plan",
    steps: [
      "Keep result in MOCK state.",
      "Show missing adapter state.",
      "Record data gap in receipt.",
      "Do not call Epicor endpoints."
    ],
    blockedLiveSystems: ["Epicor", "ERP data"]
  },
  {
    id: "BB-ACT-5004",
    capabilityId: "BB-CAP-4001",
    title: "Local evidence-card lookup",
    executionState: "NON_EXECUTABLE_PREVIEW",
    previewType: "read_only_plan",
    steps: [
      "Parse request for exact IDs first.",
      "Search bounded local fixture keywords.",
      "Return source, evidence, claim, and capability cards.",
      "Generate local execution receipt."
    ],
    blockedLiveSystems: []
  }
];
