using System.Text.Json.Serialization;

namespace Vira.Next.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PacketCadPromotionState
{
    Proposed,
    Planned,
    ImplementedUnverified,
    PartialVerified,
    Verified2024Sp5,
    Verified2024And2026,
    Blocked
}

public sealed record PacketCadReceiptContext
{
    public string ReceiptId { get; init; } = string.Empty;
    public string SessionId { get; init; } = "packet-cad-local";
    public string CorrelationId { get; init; } = string.Empty;
    public DateTime TimestampUtc { get; init; }
    public string ApprovalId { get; init; } = string.Empty;
    public PacketCadPromotionState PromotionState { get; init; } = PacketCadPromotionState.ImplementedUnverified;
}

public sealed record PacketCadExecutionReceipt
{
    public string SchemaVersion { get; init; } = "vira.packet-cad.receipt.v1";
    public string ReceiptId { get; init; } = string.Empty;
    public DateTime TimestampUtc { get; init; }
    public string SessionId { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
    public ViraExecutionMode Mode { get; init; } = ViraExecutionMode.ReadOnlyAnalyst;
    public string ToolName { get; init; } = "compare_packet_to_cad";
    public ToolCategory ToolCategory { get; init; } = ToolCategory.Read;
    public string TargetSummary { get; init; } = "[REDACTED PACKET/CAD SNAPSHOTS]";
    public string PacketSnapshotId { get; init; } = string.Empty;
    public string CadSnapshotId { get; init; } = string.Empty;
    public IReadOnlyList<string> RuleVersions { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, int> ComparisonCounts { get; init; } = new Dictionary<string, int>();
    public ApprovalSnapshot Approval { get; init; } = new();
    public ReceiptResult Result { get; init; }
    public bool SafeToRetry { get; init; }
    public bool CadAccessed { get; init; }
    public bool PdmAccessed { get; init; }
    public bool ExternalSystemsAccessed { get; init; }
    public bool SecretsAccessed { get; init; }
    public bool ProductionDataAccessed { get; init; }
    public int MutationActions { get; init; }
    public EngineeringMutationBoundary MutationBoundary { get; init; } = EngineeringMutationBoundary.ReadOnly;
    public PacketCadPromotionState PromotionState { get; init; }
    public IReadOnlyList<ReceiptError> Errors { get; init; } = Array.Empty<ReceiptError>();
    public IReadOnlyList<string> Limitations { get; init; } = Array.Empty<string>();
}
