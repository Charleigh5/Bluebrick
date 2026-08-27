import type { EngineeringQueryPort } from "../ports/EngineeringQueryPort";

export type InProcessHostBridgeAdapterContract = {
  readonly kind: "in-process-host-contract-only";
  readonly status: "NOT_IMPLEMENTED";
  readonly allowedWhen: "future scoped WebView host binding approval only";
  readonly forbiddenInCurrentSlice: true;
};

export type InProcessHostBridgeAdapter = EngineeringQueryPort & InProcessHostBridgeAdapterContract;

export const inProcessHostBridgeAdapterContract: InProcessHostBridgeAdapterContract = {
  kind: "in-process-host-contract-only",
  status: "NOT_IMPLEMENTED",
  allowedWhen: "future scoped WebView host binding approval only",
  forbiddenInCurrentSlice: true
};
