using Vira.Next.Contracts;

namespace Vira.Next.Engine;

public sealed class CapabilityPolicyEvaluator
{
    public CapabilityDecision Evaluate(CapabilityRequest request, CapabilityDefinition? capability)
    {
        if (string.IsNullOrWhiteSpace(request.CapabilityId))
        {
            return Deny(request, CapabilityRouteStatus.ValidationError, "CapabilityId is required.");
        }

        if (capability == null)
        {
            return Deny(request, CapabilityRouteStatus.UnknownId, "Capability is not present in the VIRA Next starter manifest.");
        }

        if (capability.State == CapabilityState.NotConnected)
        {
            return new CapabilityDecision
            {
                Status = CapabilityRouteStatus.NotConnected,
                CapabilityId = capability.Id,
                State = capability.State,
                Category = capability.Category,
                Reason = $"{capability.LiveSystem} is not connected in this local starter.",
                ResultIds = Array.Empty<string>(),
                DataGaps = new[] { $"{capability.LiveSystem} adapter is unavailable; no live data was queried." }
            };
        }

        if (capability.Category == ToolCategory.Mutation && request.ApprovalGranted)
        {
            return Deny(request, CapabilityRouteStatus.PolicyDenied, "Mutation execution is outside this starter scaffold even when approval metadata is present.", capability);
        }

        if (capability.RequiresApproval && !request.ApprovalGranted)
        {
            return new CapabilityDecision
            {
                Status = CapabilityRouteStatus.ApprovalRequired,
                CapabilityId = capability.Id,
                State = capability.State,
                Category = capability.Category,
                Reason = $"{capability.Label} requires explicit scoped approval before live access.",
                RequiresApproval = true,
                ResultIds = capability.ResultIds,
                DataGaps = new[] { "Approval and live-system adapter are not present in this starter scaffold." }
            };
        }

        return new CapabilityDecision
        {
            Status = CapabilityRouteStatus.LocalFixtureResult,
            CapabilityId = capability.Id,
            State = capability.State,
            Category = capability.Category,
            Reason = "Capability resolved against local VIRA Next starter manifest only.",
            RequiresApproval = capability.RequiresApproval,
            ApprovalGranted = request.ApprovalGranted,
            ResultIds = capability.ResultIds,
            DataGaps = Array.Empty<string>(),
            ExternalSystemsAccessed = false
        };
    }

    private static CapabilityDecision Deny(CapabilityRequest request, CapabilityRouteStatus status, string reason, CapabilityDefinition? capability = null)
    {
        return new CapabilityDecision
        {
            Status = status,
            CapabilityId = request.CapabilityId,
            State = capability?.State ?? CapabilityState.NotConnected,
            Category = capability?.Category ?? ToolCategory.Forbidden,
            Reason = reason,
            RequiresApproval = capability?.RequiresApproval ?? false,
            ApprovalGranted = request.ApprovalGranted,
            ResultIds = Array.Empty<string>(),
            DataGaps = new[] { reason },
            ExternalSystemsAccessed = false
        };
    }
}
