using System.Text.Json.Serialization;

namespace Vira.Next.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EvidenceSourceKind
{
    Pdf,
    CadDocument,
    CadConfiguration,
    CadComponent,
    CadDrawing,
    CadTable,
    CadAnnotation
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EvidenceAuthority
{
    Observed,
    Derived,
    Candidate
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EvidenceVerificationStatus
{
    Confirmed,
    NeedsVerification,
    Unknown,
    Unsupported,
    Conflict
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CadSnapshotState
{
    Ready,
    NoActiveDocument,
    Blocked,
    Error,
    Unsupported
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CadSnapshotSource
{
    Unknown,
    LocalFixture,
    ApprovedRedactedFixture,
    ReadOnlyHost
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CadDocumentType
{
    Unknown,
    Part,
    Assembly,
    Drawing
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CadPropertyScope
{
    Document,
    Configuration,
    Component,
    CutList
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CadPropertyReadStatus
{
    Resolved,
    Cached,
    CachedUnresolved,
    Missing,
    Blank,
    Unsupported,
    ReadError
}

public sealed record EvidenceRef
{
    public string EvidenceId { get; init; } = string.Empty;
    public EvidenceSourceKind SourceKind { get; init; }
    public string SourceId { get; init; } = string.Empty;
    public string PageOrSheet { get; init; } = string.Empty;
    public string RegionOrNativePath { get; init; } = string.Empty;
    public string FieldName { get; init; } = string.Empty;
    public string RawValue { get; init; } = string.Empty;
    public string EvaluatedValue { get; init; } = string.Empty;
    public string ExtractionMethod { get; init; } = string.Empty;
    public string RuleId { get; init; } = string.Empty;
    public EvidenceAuthority Authority { get; init; }
    public double Confidence { get; init; }
    public EvidenceVerificationStatus VerificationStatus { get; init; }
    public IReadOnlyList<string> Limitations { get; init; } = Array.Empty<string>();
}

public sealed record CadPropertySnapshot
{
    public string Name { get; init; } = string.Empty;
    public string NameHash { get; init; } = string.Empty;
    public CadPropertyScope Scope { get; init; }
    public string ConfigurationName { get; init; } = string.Empty;
    public string RawValue { get; init; } = string.Empty;
    public string EvaluatedValue { get; init; } = string.Empty;
    public bool WasResolved { get; init; }
    public bool LinkedToParent { get; init; }
    public int ResultCode { get; init; }
    public CadPropertyReadStatus ReadStatus { get; init; }
    public EvidenceRef Evidence { get; init; } = new();
}

public sealed record CadDocumentSnapshot
{
    public string SchemaVersion { get; init; } = "vira.cad-document.v1";
    public string SnapshotId { get; init; } = string.Empty;
    public DateTime CapturedUtc { get; init; }
    public string RuntimeVersion { get; init; } = "unknown";
    public CadSnapshotSource Source { get; init; } = CadSnapshotSource.Unknown;
    public CadSnapshotState State { get; init; } = CadSnapshotState.Unsupported;
    public string Message { get; init; } = string.Empty;
    public CadDocumentType DocumentType { get; init; } = CadDocumentType.Unknown;
    public string TitleHash { get; init; } = "redacted";
    public string PathHash { get; init; } = "redacted";
    public string ActiveConfiguration { get; init; } = string.Empty;
    public bool IsDirty { get; init; }
    public bool IsReadOnly { get; init; }
    public IReadOnlyList<CadPropertySnapshot> Properties { get; init; } = Array.Empty<CadPropertySnapshot>();
    public IReadOnlyList<CadComponentSnapshot> Components { get; init; } = Array.Empty<CadComponentSnapshot>();
    public CadAssemblyTraversalSummary AssemblyTraversal { get; init; } = new();
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Unsupported { get; init; } = Array.Empty<string>();
    public int MutationActions { get; init; }
    public bool ExternalSystemsAccessed { get; init; }
}
