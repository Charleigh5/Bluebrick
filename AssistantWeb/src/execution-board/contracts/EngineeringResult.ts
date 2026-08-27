import type { CapabilityState } from "./CapabilityState";
import type { ExecutionReceipt, RouteMode } from "./ExecutionReceipt";

export type SourceAsset = {
  id: string;
  title: string;
  authority: "handoff-installed" | "repo-local" | "local-fixture";
  sourceType: "handoff" | "fixture" | "repo" | "runbook";
  path: string;
  summary: string;
  state: CapabilityState;
  keywords: string[];
};

export type EvidenceReference = {
  id: string;
  sourceAssetId: string;
  title: string;
  authority: "source-quote" | "fixture-assertion";
  quote: string;
  claimIds: string[];
};

export type Claim = {
  id: string;
  title: string;
  statement: string;
  evidenceIds: string[];
  authority: "handoff" | "repo" | "fixture";
  confidence: "fixture" | "repo-observed" | "handoff-intent";
};

export type CapabilityDecision = {
  id: string;
  label: string;
  state: CapabilityState;
  reason: string;
  approvalBoundary: string;
  relatedClaimIds: string[];
};

export type ActionPlan = {
  id: string;
  capabilityId: string;
  title: string;
  executionState: "NON_EXECUTABLE_PREVIEW";
  previewType: "mock_plan" | "read_only_plan" | "approval_packet";
  steps: string[];
  blockedLiveSystems: string[];
};

export type EngineeringQueryRoute = {
  request: string;
  mode: RouteMode;
  sources: SourceAsset[];
  evidence: EvidenceReference[];
  claims: Claim[];
  capabilities: CapabilityDecision[];
  actions: ActionPlan[];
  gaps: string[];
  receipt: ExecutionReceipt;
};
