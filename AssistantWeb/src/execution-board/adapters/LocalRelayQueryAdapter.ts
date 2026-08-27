import {
  actionPlans,
  capabilityDecisions,
  claims,
  evidenceReferences,
  sourceAssets,
  type ActionPlan,
  type CapabilityDecision,
  type CapabilityState
} from "../fixtures";
import type { EngineeringQuery } from "../contracts/EngineeringQuery";
import type { ExecutionReceipt, RouteMode } from "../contracts/ExecutionReceipt";
import type { EngineeringQueryRoute } from "../contracts/EngineeringResult";

type RelayExecutionBoardStatus =
  | "LOCAL_FIXTURE_RESULT"
  | "NOT_CONNECTED"
  | "APPROVAL_REQUIRED"
  | "UNKNOWN_ID"
  | "VALIDATION_ERROR"
  | "POLICY_DENIED";

type RelayCapabilityState = {
  id?: string;
  Id?: string;
  label?: string;
  Label?: string;
  state?: string;
  State?: string;
};

type RelayActionPreview = {
  id?: string;
  Id?: string;
  title?: string;
  Title?: string;
  executionState?: string;
  ExecutionState?: string;
  blockedLiveSystems?: string[];
  BlockedLiveSystems?: string[];
};

type RelayReceipt = {
  id?: string;
  Id?: string;
  query?: string;
  Query?: string;
  routedAtUtc?: string;
  RoutedAtUtc?: string;
  routingDecision?: string;
  RoutingDecision?: string;
  resultIds?: string[];
  ResultIds?: string[];
  noExternalAccessAssertion?: boolean;
  NoExternalAccessAssertion?: boolean;
  externalSystemsAccessed?: boolean;
  ExternalSystemsAccessed?: boolean;
  cadAccessed?: boolean;
  CadAccessed?: boolean;
  pdmAccessed?: boolean;
  PdmAccessed?: boolean;
  secretsAccessed?: boolean;
  SecretsAccessed?: boolean;
  productionDataAccessed?: boolean;
  ProductionDataAccessed?: boolean;
};

type RelayQueryResponse = {
  status?: RelayExecutionBoardStatus;
  Status?: RelayExecutionBoardStatus;
  routeMode?: RouteMode;
  RouteMode?: RouteMode;
  message?: string;
  Message?: string;
  matchedIds?: string[];
  MatchedIds?: string[];
  resultIds?: string[];
  ResultIds?: string[];
  capabilityStates?: RelayCapabilityState[];
  CapabilityStates?: RelayCapabilityState[];
  actionPreviews?: RelayActionPreview[];
  ActionPreviews?: RelayActionPreview[];
  dataGaps?: string[];
  DataGaps?: string[];
  receipt?: RelayReceipt;
  Receipt?: RelayReceipt;
  persistedReceipt?: boolean;
  PersistedReceipt?: boolean;
  redactedErrorDetail?: string;
  RedactedErrorDetail?: string;
};

const relayStatuses: RelayExecutionBoardStatus[] = [
  "LOCAL_FIXTURE_RESULT",
  "NOT_CONNECTED",
  "APPROVAL_REQUIRED",
  "UNKNOWN_ID",
  "VALIDATION_ERROR",
  "POLICY_DENIED"
];

const capabilityStates: CapabilityState[] = ["MOCK", "LOCAL", "NOT_CONNECTED", "READ_ONLY", "APPROVAL_REQUIRED"];

export class LocalRelayQueryAdapter {
  readonly kind = "localhost-relay" as const;
  readonly endpoint: string;

  constructor(baseUrl: string) {
    const origin = requireLocalhostHttpOrigin(baseUrl);
    this.endpoint = `${origin}/execution-board/query`;
  }

  async route(query: EngineeringQuery): Promise<EngineeringQueryRoute> {
    const response = await fetch(this.endpoint, {
      method: "POST",
      cache: "no-store",
      credentials: "omit",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({
        query: query.request,
        sessionId: "assistant-web-execution-board",
        source: query.source
      })
    });
    const payload = (await response.json()) as RelayQueryResponse;
    return mapRelayResponse(query.request, payload, this.endpoint);
  }
}

export async function routeEngineeringQueryViaLocalRelay(request: string, baseUrl: string): Promise<EngineeringQueryRoute> {
  try {
    const adapter = new LocalRelayQueryAdapter(baseUrl);
    return await adapter.route({ request, source: "browser-ui", noExternalEgress: true });
  } catch {
    return createLocalTransportFailureRoute(request, baseUrl);
  }
}

export function isAllowedLocalRelayUrl(baseUrl: string): boolean {
  try {
    requireLocalhostHttpOrigin(baseUrl);
    return true;
  } catch {
    return false;
  }
}

function requireLocalhostHttpOrigin(baseUrl: string): string {
  const parsed = new URL(baseUrl);
  const allowedHosts = new Set(["127.0.0.1", "localhost", "::1", "[::1]"]);
  if (parsed.protocol !== "http:" || !allowedHosts.has(parsed.hostname)) {
    throw new Error("Execution-board Relay adapter only accepts localhost HTTP origins.");
  }

  return parsed.origin;
}

function mapRelayResponse(request: string, response: RelayQueryResponse, endpoint: string): EngineeringQueryRoute {
  const status = normalizeRelayStatus(pick(response, "status", "Status"));
  const routeMode = normalizeRouteMode(pick(response, "routeMode", "RouteMode"));
  const message = pick(response, "message", "Message") || "Relay returned a typed local execution-board response.";
  const matchedIds = pick(response, "matchedIds", "MatchedIds") ?? [];
  const resultIds = pick(response, "resultIds", "ResultIds") ?? [];
  const relayCapabilities = pick(response, "capabilityStates", "CapabilityStates") ?? [];
  const relayActions = pick(response, "actionPreviews", "ActionPreviews") ?? [];
  const receipt = pick(response, "receipt", "Receipt") ?? {};
  const capabilities = mapCapabilities(relayCapabilities, message);
  const actions = mapActions(relayActions);
  const sources = sourceAssets.filter((source) => resultIds.includes(source.id) || matchedIds.includes(source.id));
  const evidence = evidenceReferences.filter((item) => resultIds.includes(item.id) || sources.some((source) => source.id === item.sourceAssetId));
  const routedClaims = claims.filter((claim) => resultIds.includes(claim.id) || evidence.some((item) => item.claimIds.includes(claim.id)));
  const gaps = pick(response, "dataGaps", "DataGaps") ?? [];

  return {
    request,
    mode: routeMode,
    sources,
    evidence,
    claims: routedClaims,
    capabilities,
    actions,
    gaps,
    receipt: mapReceipt({
      request,
      endpoint,
      status,
      routeMode,
      message,
      matchedIds,
      resultIds,
      capabilities,
      relayReceipt: receipt,
      persistedReceipt: pick(response, "persistedReceipt", "PersistedReceipt") ?? false,
      redactedErrorDetail: pick(response, "redactedErrorDetail", "RedactedErrorDetail") || "[REDACTED]"
    })
  };
}

function mapCapabilities(items: RelayCapabilityState[], message: string): CapabilityDecision[] {
  return items.map((item) => {
    const id = pick(item, "id", "Id") || "BB-CAP-RELAY";
    const state = normalizeCapabilityState(pick(item, "state", "State"));
    const known = capabilityDecisions.find((capability) => capability.id === id);
    return known
      ? { ...known, state }
      : {
          id,
          label: pick(item, "label", "Label") || "Relay local capability",
          state,
          reason: message,
          approvalBoundary: state === "LOCAL" ? "Local Relay fixture only." : "Live system is unavailable or approval gated.",
          relatedClaimIds: []
        };
  });
}

function mapActions(items: RelayActionPreview[]): ActionPlan[] {
  return items.map((item) => {
    const id = pick(item, "id", "Id") || "BB-ACT-RELAY";
    const known = actionPlans.find((action) => action.id === id);
    return known
      ? known
      : {
          id,
          capabilityId: "BB-CAP-RELAY",
          title: pick(item, "title", "Title") || "Relay action preview",
          executionState: "NON_EXECUTABLE_PREVIEW",
          previewType: "mock_plan",
          steps: ["Relay returned a non-executable local preview.", "No live system call is available from this browser route."],
          blockedLiveSystems: pick(item, "blockedLiveSystems", "BlockedLiveSystems") ?? []
        };
  });
}

function mapReceipt(input: {
  request: string;
  endpoint: string;
  status: RelayExecutionBoardStatus;
  routeMode: RouteMode;
  message: string;
  matchedIds: string[];
  resultIds: string[];
  capabilities: CapabilityDecision[];
  relayReceipt: RelayReceipt;
  persistedReceipt: boolean;
  redactedErrorDetail: string;
}): ExecutionReceipt {
  return {
    id: pick(input.relayReceipt, "id", "Id") || `BB-RELAY-LOCAL-${Date.now()}`,
    query: pick(input.relayReceipt, "query", "Query") || input.request,
    request: input.request,
    routedAtUtc: pick(input.relayReceipt, "routedAtUtc", "RoutedAtUtc") || new Date().toISOString(),
    routeMode: input.routeMode,
    routingDecision: pick(input.relayReceipt, "routingDecision", "RoutingDecision") || input.message,
    matchedIds: input.matchedIds,
    resultIds: input.resultIds.length ? input.resultIds : pick(input.relayReceipt, "resultIds", "ResultIds") ?? [],
    capabilityStates: input.capabilities.map((capability) => ({
      id: capability.id,
      label: capability.label,
      state: capability.state
    })),
    noExternalAccessAssertion: true,
    externalSystemsAccessed: false,
    cadAccessed: false,
    pdmAccessed: false,
    secretsAccessed: false,
    productionDataAccessed: false,
    redactedErrorDetails: [
      {
        code: input.status,
        message: input.redactedErrorDetail,
        detail: "[REDACTED]"
      }
    ],
    relayStatus: input.status,
    persistedReceipt: input.persistedReceipt,
    transport: {
      kind: "localhost-relay",
      url: input.endpoint
    },
    summary: "Browser route completed through localhost Relay typed mock contract."
  };
}

function createLocalTransportFailureRoute(request: string, baseUrl: string): EngineeringQueryRoute {
  const now = new Date().toISOString();
  const capabilities = [
    {
      id: "BB-CAP-RELAY",
      label: "Local Relay transport",
      state: "NOT_CONNECTED" as CapabilityState,
      reason: "Relay URL was unavailable, invalid, or not localhost-only.",
      approvalBoundary: "Only localhost HTTP Relay proof URLs are allowed."
    }
  ];

  return {
    request,
    mode: "not-connected",
    sources: [],
    evidence: [],
    claims: [],
    capabilities: capabilities.map((capability) => ({ ...capability, relatedClaimIds: [] })),
    actions: [],
    gaps: ["Local Relay proof route did not complete. No external fallback was attempted."],
    receipt: {
      id: `BB-RELAY-TRANSPORT-${now.replace(/[-:.TZ]/g, "").slice(0, 14)}`,
      query: request,
      request,
      routedAtUtc: now,
      routeMode: "not-connected",
      routingDecision: "Local Relay transport did not complete; returned NOT_CONNECTED instead of fallback egress.",
      matchedIds: [],
      resultIds: [],
      capabilityStates: capabilities,
      noExternalAccessAssertion: true,
      externalSystemsAccessed: false,
      cadAccessed: false,
      pdmAccessed: false,
      secretsAccessed: false,
      productionDataAccessed: false,
      redactedErrorDetails: [{ code: "NOT_CONNECTED", message: "[REDACTED]", detail: "[REDACTED]" }],
      relayStatus: "NOT_CONNECTED",
      persistedReceipt: false,
      transport: { kind: "localhost-relay", url: baseUrl },
      summary: "Local Relay adapter blocked or failed without external fallback."
    }
  };
}

function normalizeRelayStatus(value: string | undefined): RelayExecutionBoardStatus {
  return relayStatuses.includes(value as RelayExecutionBoardStatus) ? (value as RelayExecutionBoardStatus) : "VALIDATION_ERROR";
}

function normalizeRouteMode(value: string | undefined): RouteMode {
  return value === "exact-id" || value === "fixture-search" || value === "not-connected" ? value : "not-connected";
}

function normalizeCapabilityState(value: string | undefined): CapabilityState {
  return capabilityStates.includes(value as CapabilityState) ? (value as CapabilityState) : "NOT_CONNECTED";
}

function pick<T, K1 extends keyof T, K2 extends keyof T>(item: T, lower: K1, upper: K2): T[K1] | T[K2] | undefined {
  return item[lower] ?? item[upper];
}
