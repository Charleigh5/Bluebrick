using System.Text.Json.Serialization;

namespace Vira.Next.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReceiptResult
{
    Success,
    Partial,
    Failed,
    Denied
}

public sealed class ApprovalSnapshot
{
    public bool Required { get; init; }
    public bool Granted { get; init; }
    public string? ApprovalId { get; init; }
}

public sealed class ReceiptError
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Detail { get; init; } = "[REDACTED]";
}

public sealed class ExecutionReceipt
{
    public string ReceiptId { get; init; } = string.Empty;
    public DateTime TimestampUtc { get; init; }
    public string SessionId { get; init; } = "vira-next-local";
    public string CorrelationId { get; init; } = string.Empty;
    public ViraExecutionMode Mode { get; init; }
    public string ToolName { get; init; } = string.Empty;
    public ToolCategory ToolCategory { get; init; }
    public string TargetSummary { get; init; } = string.Empty;
    public ApprovalSnapshot Approval { get; init; } = new();
    public ReceiptResult Result { get; init; }
    public bool SafeToRetry { get; init; }
    public bool ExternalSystemsAccessed { get; init; }
    public bool CadAccessed { get; init; }
    public bool PdmAccessed { get; init; }
    public bool SecretsAccessed { get; init; }
    public bool ProductionDataAccessed { get; init; }
    public ReceiptError[] Errors { get; init; } = Array.Empty<ReceiptError>();
}
