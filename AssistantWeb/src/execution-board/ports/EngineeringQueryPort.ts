import type { EngineeringQuery } from "../contracts/EngineeringQuery";
import type { EngineeringQueryRoute } from "../contracts/EngineeringResult";

export type EngineeringQueryPortKind =
  | "local-fixture"
  | "unavailable-system"
  | "relay-contract-only"
  | "in-process-host-contract-only";

export type EngineeringQueryPort = {
  readonly kind: EngineeringQueryPortKind;
  route(query: EngineeringQuery): EngineeringQueryRoute;
};
