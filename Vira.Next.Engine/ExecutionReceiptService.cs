using Vira.Next.Contracts;

namespace Vira.Next.Engine;

public sealed class ExecutionReceiptService
{
    public ExecutionReceipt Create(CapabilityRequest request, CapabilityDecision decision, IEnumerable<ReceiptError>? errors = null)
    {
        var redactedErrors = (errors ?? Array.Empty<ReceiptError>())
            .Select(error => new ReceiptError
            {
                Code = error.Code,
                Message = error.Message,
                Detail = "[REDACTED]"
            })
            .ToArray();

        return new ExecutionReceipt
        {
            ReceiptId = $"VIRA-NEXT-RCPT-{DateTime.UtcNow:yyyyMMddHHmmss}",
            TimestampUtc = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid().ToString("N"),
            Mode = request.Mode,
            ToolName = request.CapabilityId,
            ToolCategory = decision.Category,
            TargetSummary = string.IsNullOrWhiteSpace(request.TargetSummary) ? "[none]" : request.TargetSummary,
            Approval = new ApprovalSnapshot
            {
                Required = decision.RequiresApproval,
                Granted = request.ApprovalGranted,
                ApprovalId = request.ApprovalGranted ? "local-approval-metadata-only" : null
            },
            Result = decision.Status is CapabilityRouteStatus.LocalFixtureResult ? ReceiptResult.Success : ReceiptResult.Denied,
            SafeToRetry = decision.Status is CapabilityRouteStatus.NotConnected or CapabilityRouteStatus.UnknownId,
            ExternalSystemsAccessed = false,
            CadAccessed = false,
            PdmAccessed = false,
            SecretsAccessed = false,
            ProductionDataAccessed = false,
            Errors = redactedErrors
        };
    }
}
