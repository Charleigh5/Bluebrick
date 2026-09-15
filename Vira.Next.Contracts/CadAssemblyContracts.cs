using System.Text.Json.Serialization;

namespace Vira.Next.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CadComponentKind
{
    Unknown,
    Part,
    Assembly
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CadComponentSuppressionState
{
    Unknown,
    Suppressed,
    Lightweight,
    FullyResolved,
    Resolved,
    InternalIdMismatch
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CadComponentResolutionState
{
    Unknown,
    Resolved,
    Lightweight,
    Suppressed,
    Unloaded,
    MissingReference
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CadComponentChildrenState
{
    Unknown,
    None,
    Enumerated,
    OpaqueSuppressed,
    Truncated
}

public sealed record CadComponentSnapshot
{
    public string SnapshotId { get; init; } = string.Empty;
    public string ParentSnapshotId { get; init; } = string.Empty;
    public int NativeComponentId { get; init; }
    public int Depth { get; init; }
    public string NameHash { get; init; } = "redacted";
    public string NativePathHash { get; init; } = "redacted";
    public string IdentifierCandidate { get; init; } = string.Empty;
    public string IdentifierHash { get; init; } = "redacted";
    public string ReferencedConfiguration { get; init; } = string.Empty;
    public string ReferencedConfigurationHash { get; init; } = "redacted";
    public int InstanceCount { get; init; } = 1;
    public CadComponentKind Kind { get; init; } = CadComponentKind.Unknown;
    public CadComponentSuppressionState SuppressionState { get; init; } = CadComponentSuppressionState.Unknown;
    public CadComponentResolutionState ResolutionState { get; init; } = CadComponentResolutionState.Unknown;
    public CadComponentChildrenState ChildrenState { get; init; } = CadComponentChildrenState.Unknown;
    public bool IsVirtual { get; init; }
    public bool IsGraphicsOnly { get; init; }
    public bool IsSpeedPak { get; init; }
    public IReadOnlyList<CadPropertySnapshot> Properties { get; init; } = Array.Empty<CadPropertySnapshot>();
    public EvidenceRef Evidence { get; init; } = new();
    public IReadOnlyList<string> Limitations { get; init; } = Array.Empty<string>();
}

public sealed record CadAssemblyTraversalSummary
{
    public int MaxDepth { get; init; } = 32;
    public int RecordLimit { get; init; } = 5000;
    public int RecordedCount { get; init; }
    public int UnloadedCount { get; init; }
    public int CycleCount { get; init; }
    public bool Truncated { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public int MutationActions { get; init; }
    public bool ExternalSystemsAccessed { get; init; }
}
