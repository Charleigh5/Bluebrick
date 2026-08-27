import type { EngineeringQuery } from "../contracts/EngineeringQuery";
import type { CapabilityState } from "../contracts/CapabilityState";
import type { ExecutionReceipt } from "../contracts/ExecutionReceipt";
import type { ActionPlan, CapabilityDecision, EngineeringQueryRoute } from "../contracts/EngineeringResult";
import type { EngineeringQueryPort } from "../ports/EngineeringQueryPort";

export type UnavailableSystem = "SOLIDWORKS" | "PDM" | "Epicor" | "Salesforce" | "Relay" | "InProcessHostBridge";

export type UnavailableSystemAdapterOptions = {
  system: UnavailableSystem;
  capabilityState: Extract<CapabilityState, "NOT_CONNECTED" | "APPROVAL_REQUIRED">;
  reason: string;
};

export class UnavailableSystemAdapter implements EngineeringQueryPort {
  readonly kind = "unavailable-system" as const;

  constructor(private readonly options: UnavailableSystemAdapterOptions) {}

  route(query: EngineeringQuery): EngineeringQueryRoute {
    const capability: CapabilityDecision = {
      id: `BB-CAP-UNAVAILABLE-${this.options.system.toUpperCase()}`,
      label: `${this.options.system} unavailable`,
      state: this.options.capabilityState,
      reason: this.options.reason,
      approvalBoundary: "No live adapter is connected in the execution-board sandbox.",
      relatedClaimIds: []
    };
    const action: ActionPlan = {
      id: `BB-ACT-UNAVAILABLE-${this.options.system.toUpperCase()}`,
      capabilityId: capability.id,
      title: `Preview ${this.options.system} unavailable route`,
      executionState: "NON_EXECUTABLE_PREVIEW",
      previewType: "approval_packet",
      steps: [
        "Return explicit unavailable state.",
        "Do not open sockets, processes, COM objects, credentials, CAD files, or external endpoints.",
        "Require a scoped approval packet before any live adapter exists."
      ],
      blockedLiveSystems: [this.options.system]
    };

    return {
      request: query.request.trim(),
      mode: "not-connected",
      sources: [],
      evidence: [],
      claims: [],
      capabilities: [capability],
      actions: [action],
      gaps: [`${this.options.system} is unavailable in this local sandbox.`],
      receipt: createUnavailableReceipt(query.request.trim(), [capability], [action.id])
    };
  }
}

function createUnavailableReceipt(request: string, capabilities: CapabilityDecision[], resultIds: string[]): ExecutionReceipt {
  const routedAtUtc = new Date().toISOString();
  return {
    id: `BB-RCPT-${routedAtUtc.replace(/[-:.TZ]/g, "").slice(0, 14)}`,
    query: request,
    request,
    routedAtUtc,
    routeMode: "not-connected",
    routingDecision: "Unavailable-system adapter returned an explicit non-live state.",
    matchedIds: [],
    resultIds,
    capabilityStates: capabilities.map((capability) => ({
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
        code: "SYSTEM_UNAVAILABLE",
        message: "No live adapter is connected for this system.",
        detail: "[REDACTED]"
      }
    ],
    summary: "Returned unavailable system state without external access."
  };
}
