using Vira.Next.Contracts;

namespace Vira.Next.Engine;

public sealed class PacketCadReceiptService
{
    public PacketCadExecutionReceipt Create(
        PacketEvidenceSnapshot packet,
        CadDocumentSnapshot cad,
        IEnumerable<PacketCadComparison> comparisons,
        PacketCadReceiptContext context)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(cad);
        ArgumentNullException.ThrowIfNull(comparisons);
        ArgumentNullException.ThrowIfNull(context);

        var comparisonArray = comparisons.ToArray();
        var counts = Enum.GetValues<PacketCadComparisonStatus>()
            .Select(status => new
            {
                Name = status.ToString(),
                Count = comparisonArray.Count(item => item.Status == status)
            })
            .Where(item => item.Count > 0)
            .ToDictionary(item => item.Name, item => item.Count, StringComparer.Ordinal);
        var ruleVersions = comparisonArray
            .Select(item => item.NormalizationRuleId)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var limitations = cad.Warnings
            .Concat(cad.Unsupported)
            .Concat(string.IsNullOrWhiteSpace(cad.Message) ? Array.Empty<string>() : new[] { cad.Message })
            .Concat(comparisonArray.SelectMany(item => item.Limitations))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var safetyViolation = cad.MutationActions != 0 || cad.ExternalSystemsAccessed;
        var result = safetyViolation
            ? ReceiptResult.Failed
            : cad.State switch
            {
                CadSnapshotState.Ready => ReceiptResult.Success,
                CadSnapshotState.NoActiveDocument => ReceiptResult.Denied,
                CadSnapshotState.Blocked => ReceiptResult.Denied,
                CadSnapshotState.Error => ReceiptResult.Denied,
                CadSnapshotState.Unsupported => ReceiptResult.Denied,
                _ => ReceiptResult.Failed
            };

        return new PacketCadExecutionReceipt
        {
            ReceiptId = context.ReceiptId,
            TimestampUtc = context.TimestampUtc,
            SessionId = context.SessionId,
            CorrelationId = context.CorrelationId,
            PacketSnapshotId = packet.SnapshotId,
            CadSnapshotId = cad.SnapshotId,
            RuleVersions = ruleVersions,
            ComparisonCounts = counts,
            Approval = new ApprovalSnapshot
            {
                Required = cad.Source == CadSnapshotSource.ReadOnlyHost,
                Granted = !string.IsNullOrWhiteSpace(context.ApprovalId),
                ApprovalId = string.IsNullOrWhiteSpace(context.ApprovalId) ? null : context.ApprovalId
            },
            Result = result,
            SafeToRetry = !safetyViolation && (cad.State is CadSnapshotState.NoActiveDocument or CadSnapshotState.Blocked),
            CadAccessed = cad.Source == CadSnapshotSource.ReadOnlyHost,
            PdmAccessed = false,
            ExternalSystemsAccessed = cad.ExternalSystemsAccessed,
            SecretsAccessed = false,
            ProductionDataAccessed = false,
            MutationActions = cad.MutationActions,
            MutationBoundary = EngineeringMutationBoundary.ReadOnly,
            PromotionState = context.PromotionState,
            Errors = Array.Empty<ReceiptError>(),
            Limitations = limitations
        };
    }
}
