import { LocalFixtureQueryAdapter } from "../adapters/LocalFixtureQueryAdapter";
import { engineeringQueryFromText } from "../contracts/EngineeringQuery";
import type { ExecutionReceipt } from "../contracts/ExecutionReceipt";
import type { EngineeringQueryRoute } from "../contracts/EngineeringResult";
import type { EngineeringQueryPort } from "../ports/EngineeringQueryPort";

const defaultPort = new LocalFixtureQueryAdapter();

export function routeEngineeringQuery(request: string, port: EngineeringQueryPort = defaultPort): EngineeringQueryRoute {
  return port.route(engineeringQueryFromText(request));
}

export function serializeExecutionReceipt(receipt: ExecutionReceipt): string {
  const redactedErrorDetails = Array.isArray(receipt.redactedErrorDetails) ? receipt.redactedErrorDetails : [];

  return JSON.stringify(
    {
      ...receipt,
      redactedErrorDetails: redactedErrorDetails.map((detail) => ({
        code: detail.code,
        message: detail.message,
        detail: "[REDACTED]"
      }))
    },
    null,
    2
  );
}
