using System.Text.Json.Serialization;

namespace Vira.Next.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CapabilityState
{
    Mock,
    Local,
    NotConnected,
    ReadOnly,
    ApprovalRequired
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CapabilityRouteStatus
{
    LocalFixtureResult,
    NotConnected,
    ApprovalRequired,
    UnknownId,
    ValidationError,
    PolicyDenied
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ToolCategory
{
    Read,
    Preview,
    Mutation,
    External,
    Forbidden
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ViraExecutionMode
{
    Mock,
    ReadOnlyAnalyst,
    PreviewOnly,
    HumanApprovedMutation
}

public sealed class CapabilityDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string SourceBlueBrickClass { get; init; } = string.Empty;
    public CapabilityState State { get; init; }
    public ToolCategory Category { get; init; }
    public bool RequiresApproval { get; init; }
    public string LiveSystem { get; init; } = string.Empty;
    public string[] ResultIds { get; init; } = Array.Empty<string>();
}

public sealed class CapabilityRequest
{
    public string CapabilityId { get; init; } = string.Empty;
    public string Intent { get; init; } = string.Empty;
    public string TargetSummary { get; init; } = string.Empty;
    public ViraExecutionMode Mode { get; init; } = ViraExecutionMode.Mock;
    public bool ApprovalGranted { get; init; }
}

public sealed class CapabilityDecision
{
    public CapabilityRouteStatus Status { get; init; }
    public string CapabilityId { get; init; } = string.Empty;
    public CapabilityState State { get; init; }
    public ToolCategory Category { get; init; }
    public string Reason { get; init; } = string.Empty;
    public bool RequiresApproval { get; init; }
    public bool ApprovalGranted { get; init; }
    public string[] ResultIds { get; init; } = Array.Empty<string>();
    public string[] DataGaps { get; init; } = Array.Empty<string>();
    public bool ExternalSystemsAccessed { get; init; }
}
