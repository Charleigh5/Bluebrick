import type { EngineeringQueryPort } from "../ports/EngineeringQueryPort";

export type RelayQueryAdapterContract = {
  readonly kind: "relay-contract-only";
  readonly status: "NOT_IMPLEMENTED";
  readonly allowedWhen: "future scoped local Relay approval only";
  readonly forbiddenInCurrentSlice: true;
};

export type RelayQueryAdapter = EngineeringQueryPort & RelayQueryAdapterContract;

export const relayQueryAdapterContract: RelayQueryAdapterContract = {
  kind: "relay-contract-only",
  status: "NOT_IMPLEMENTED",
  allowedWhen: "future scoped local Relay approval only",
  forbiddenInCurrentSlice: true
};
