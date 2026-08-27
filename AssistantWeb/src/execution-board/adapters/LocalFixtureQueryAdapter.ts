import {
  actionPlans,
  capabilityDecisions,
  claims,
  evidenceReferences,
  sourceAssets,
  type CapabilityDecision
} from "../fixtures";
import type { EngineeringQuery } from "../contracts/EngineeringQuery";
import type { ExecutionReceipt, RouteMode } from "../contracts/ExecutionReceipt";
import type { EngineeringQueryRoute } from "../contracts/EngineeringResult";
import type { EngineeringQueryPort } from "../ports/EngineeringQueryPort";

type ReceiptInput = {
  request: string;
  routeMode: RouteMode;
  routingDecision: string;
  matchedIds: string[];
  resultIds: string[];
  capabilities: CapabilityDecision[];
  summary: string;
  errorDetails?: Array<{ code: string; message: string; detail?: string }>;
};

const idPattern = /\bBB[\s_-]*[A-Z]+[\s_-]*\d{4}\b/gi;

export class LocalFixtureQueryAdapter implements EngineeringQueryPort {
  readonly kind = "local-fixture" as const;

  route(query: EngineeringQuery): EngineeringQueryRoute {
    return routeLocalFixtureQuery(query.request);
  }
}

export function routeLocalFixtureQuery(request: string): EngineeringQueryRoute {
  const trimmed = request.trim();
  const exactIds = findExactIds(trimmed);

  if (exactIds.length) {
    const exactSources = sourceAssets.filter((source) => exactIds.includes(source.id));
    const exactSourceIds = exactSources.map((source) => source.id);
    const exactEvidence = evidenceReferences.filter((reference) => exactIds.includes(reference.id) || exactSourceIds.includes(reference.sourceAssetId));
    const exactClaims = claims.filter((claim) => exactIds.includes(claim.id));
    const exactCapabilities = capabilityDecisions.filter((capability) => exactIds.includes(capability.id));
    const exactActions = actionPlans.filter((action) => exactIds.includes(action.id));
    const claimIds = uniqueById([
      ...exactClaims.map((claim) => ({ id: claim.id })),
      ...exactEvidence.flatMap((reference) => reference.claimIds.map((id) => ({ id }))),
      ...exactCapabilities.flatMap((capability) => capability.relatedClaimIds.map((id) => ({ id })))
    ]).map((item) => item.id);
    const expanded = expandByClaimIds(claimIds);
    const sources = uniqueById([...exactSources, ...expanded.sources]);
    const evidence = uniqueById([...exactEvidence, ...expanded.evidence]);
    const routedClaims = uniqueById([...exactClaims, ...expanded.claims]);
    const capabilities = uniqueById([...exactCapabilities, ...expanded.capabilities]);
    const actions = uniqueById([...exactActions, ...expanded.actions]);
    const resultIds = collectResultIds(sources, evidence, routedClaims, capabilities, actions);
    const gaps = sources.length || evidence.length || routedClaims.length || capabilities.length
      ? []
      : [`No local fixture record exists for exact ID ${exactIds.join(", ")}.`];

    return {
      request: trimmed,
      mode: "exact-id",
      sources,
      evidence,
      claims: routedClaims,
      capabilities,
      actions,
      gaps,
      receipt: createReceipt({
        request: trimmed,
        routeMode: "exact-id",
        routingDecision: "Exact normalized identifier route took precedence over fixture keyword matching.",
        matchedIds: exactIds,
        resultIds,
        capabilities,
        summary: "Exact ID route completed against local fixtures only.",
        errorDetails: gaps.length ? [{ code: "UNKNOWN_EXACT_ID", message: "Exact ID was not present in local fixtures." }] : []
      })
    };
  }

  const lower = trimmed.toLowerCase();
  const requestTokens = tokenize(trimmed);
  const sourceMatches = sourceAssets.filter((source) =>
    [source.title, source.summary, source.path, ...source.keywords].some((value) => matchesText(value, lower, requestTokens))
  );
  const claimMatches = claims.filter((claim) => matchesText(`${claim.title} ${claim.statement}`, lower, requestTokens));
  const capabilityMatches = capabilityDecisions.filter((capability) =>
    matchesText(`${capability.label} ${capability.reason} ${capability.state}`, lower, requestTokens)
  );
  const evidenceMatches = evidenceReferences.filter((reference) =>
    matchesText(`${reference.title} ${reference.quote}`, lower, requestTokens)
  );
  const claimIds = uniqueById([
    ...claimMatches.map((claim) => ({ id: claim.id })),
    ...evidenceMatches.flatMap((reference) => reference.claimIds.map((id) => ({ id }))),
    ...capabilityMatches.flatMap((capability) => capability.relatedClaimIds.map((id) => ({ id })))
  ]).map((item) => item.id);
  const expanded = expandByClaimIds(claimIds);
  const sources = uniqueById([...sourceMatches, ...expanded.sources]);
  const evidence = uniqueById([...evidenceMatches, ...expanded.evidence]);
  const routedClaims = uniqueById([...claimMatches, ...expanded.claims]);
  const capabilities = uniqueById([...capabilityMatches, ...expanded.capabilities]);
  const capabilityIds = capabilities.map((capability) => capability.id);
  const actions = uniqueById(actionPlans.filter((action) => capabilityIds.includes(action.capabilityId)));

  if (sources.length || evidence.length || routedClaims.length || capabilities.length) {
    const gaps = capabilities.some((capability) => capability.state === "NOT_CONNECTED")
      ? ["One or more requested systems are not connected in this sandbox."]
      : [];
    const resultIds = collectResultIds(sources, evidence, routedClaims, capabilities, actions);

    return {
      request: trimmed,
      mode: "fixture-search",
      sources,
      evidence,
      claims: routedClaims,
      capabilities,
      actions,
      gaps,
      receipt: createReceipt({
        request: trimmed,
        routeMode: "fixture-search",
        routingDecision: "No exact normalized identifier was found; bounded local fixture search was used.",
        matchedIds: resultIds,
        resultIds,
        capabilities,
        summary: "Keyword route completed against bounded local fixtures only.",
        errorDetails: gaps.length ? [{ code: "ADAPTER_NOT_CONNECTED", message: "Requested system has no approved local adapter." }] : []
      })
    };
  }

  const notConnectedCapabilities = capabilityDecisions.filter((capability) => capability.state === "NOT_CONNECTED");
  const notConnectedActions = actionPlans.filter((action) => ["BB-CAP-4003", "BB-CAP-4004"].includes(action.capabilityId));
  const gaps = [
    "No exact ID or fixture keyword matched the request.",
    "No live SOLIDWORKS, PDM, Epicor, Salesforce, customer system, or production lookup was attempted.",
    "Add a source asset, evidence record, or approved adapter before promoting this request."
  ];

  return {
    request: trimmed,
    mode: "not-connected",
    sources: [],
    evidence: [],
    claims: [],
    capabilities: notConnectedCapabilities,
    actions: notConnectedActions,
    gaps,
    receipt: createReceipt({
      request: trimmed,
      routeMode: "not-connected",
      routingDecision: "No exact normalized identifier or fixture keyword matched; unavailable systems were returned as NOT_CONNECTED.",
      matchedIds: [],
      resultIds: collectResultIds(notConnectedCapabilities, notConnectedActions),
      capabilities: notConnectedCapabilities,
      summary: "No local fixture match. Returned NOT_CONNECTED instead of fabricated data.",
      errorDetails: [{ code: "NO_LOCAL_RESULT", message: "No local fixture result or approved adapter is available." }]
    })
  };
}

function uniqueById<T extends { id: string }>(items: T[]): T[] {
  const seen = new Set<string>();
  return items.filter((item) => {
    if (seen.has(item.id)) return false;
    seen.add(item.id);
    return true;
  });
}

function collectResultIds(...groups: Array<Array<{ id: string }>>): string[] {
  return uniqueById(groups.flat()).map((item) => item.id);
}

function createReceipt(input: ReceiptInput): ExecutionReceipt {
  const routedAtUtc = new Date().toISOString();
  return {
    id: `BB-RCPT-${routedAtUtc.replace(/[-:.TZ]/g, "").slice(0, 14)}`,
    query: input.request,
    request: input.request,
    routedAtUtc,
    routeMode: input.routeMode,
    routingDecision: input.routingDecision,
    matchedIds: input.matchedIds,
    resultIds: input.resultIds,
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
    redactedErrorDetails: (input.errorDetails ?? []).map((detail) => ({
      code: detail.code,
      message: detail.message,
      detail: "[REDACTED]"
    })),
    summary: input.summary
  };
}

function expandByClaimIds(claimIds: string[]) {
  const nextClaims = claims.filter((claim) => claimIds.includes(claim.id));
  const evidenceIds = uniqueById(nextClaims.flatMap((claim) => claim.evidenceIds.map((id) => ({ id })))).map((item) => item.id);
  const nextEvidence = evidenceReferences.filter((reference) => evidenceIds.includes(reference.id));
  const sourceIds = uniqueById(nextEvidence.map((reference) => ({ id: reference.sourceAssetId }))).map((item) => item.id);
  const nextSources = sourceAssets.filter((source) => sourceIds.includes(source.id));
  const nextCapabilities = capabilityDecisions.filter((capability) =>
    capability.relatedClaimIds.some((claimId) => claimIds.includes(claimId))
  );
  const capabilityIds = nextCapabilities.map((capability) => capability.id);
  const nextActions = actionPlans.filter((action) => capabilityIds.includes(action.capabilityId));

  return {
    sources: nextSources,
    evidence: nextEvidence,
    claims: nextClaims,
    capabilities: nextCapabilities,
    actions: nextActions
  };
}

function normalizeIdentifier(raw: string): string | null {
  const compact = raw.toUpperCase().replace(/[^A-Z0-9]/g, "");
  const match = compact.match(/^BB([A-Z]+)(\d{4})$/);
  return match ? `BB-${match[1]}-${match[2]}` : null;
}

function findExactIds(request: string) {
  return uniqueById(
    Array.from(request.matchAll(idPattern))
      .map((match) => normalizeIdentifier(match[0]))
      .filter((id): id is string => Boolean(id))
      .map((id) => ({ id }))
  ).map((item) => item.id);
}

function tokenize(value: string): string[] {
  return value
    .toLowerCase()
    .split(/[^a-z0-9]+/)
    .filter((token) => token.length > 2);
}

function matchesText(text: string, request: string, requestTokens: string[]) {
  const lowerText = text.toLowerCase();
  return lowerText.includes(request) || requestTokens.some((token) => lowerText.includes(token));
}
