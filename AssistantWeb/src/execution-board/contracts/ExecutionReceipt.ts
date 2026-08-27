import type { CapabilityState } from "./CapabilityState";

export type RouteMode = "exact-id" | "fixture-search" | "not-connected";

export type ExecutionReceipt = {
  id: string;
  query: string;
  request: string;
  routedAtUtc: string;
  routeMode: RouteMode;
  routingDecision: string;
  matchedIds: string[];
  resultIds: string[];
  capabilityStates: Array<{
    id: string;
    label: string;
    state: CapabilityState;
  }>;
  noExternalAccessAssertion: true;
  externalSystemsAccessed: false;
  cadAccessed: false;
  pdmAccessed: false;
  secretsAccessed: false;
  productionDataAccessed: false;
  redactedErrorDetails: Array<{
    code: string;
    message: string;
    detail: "[REDACTED]";
  }>;
  relayStatus?: string;
  persistedReceipt?: boolean;
  transport?: {
    kind: "localhost-relay";
    url: string;
  };
  summary: string;
};
