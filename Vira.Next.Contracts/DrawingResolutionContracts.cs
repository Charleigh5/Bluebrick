using System.Text.Json.Serialization;

namespace Vira.Next.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DrawingResolutionStatus
{
    ExactMatch,
    PartialMatch,
    Unresolved,
    IdentityConflict,
    Ambiguous
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DrawingEvidenceAuthority
{
    SourceDerived,
    ControlledRecord
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DrawingRoleAuthority
{
    SourceDerived,
    ControlledEvidence
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DrawingIdentifierKind
{
    Unclassified,
    PartNumber,
    DocumentNumber
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DrawingReadOnlyPolicy
{
    ReadOnlyRequired
}

public sealed record DrawingIdentifierCandidate
{
    public DrawingIdentifierKind Kind { get; init; }
    public string RawValue { get; init; } = string.Empty;
    public DrawingEvidenceAuthority Authority { get; init; } = DrawingEvidenceAuthority.SourceDerived;
    public string EvidenceId { get; init; } = string.Empty;
    public string SourceField { get; init; } = string.Empty;
    public string SourceLabel { get; init; } = string.Empty;
    public PacketDrawingSourceType? SourceType { get; init; }
    public string SourceArtifactId { get; init; } = string.Empty;
    public string SourceArtifactSha256 { get; init; } = string.Empty;
    public string SourceRecordId { get; init; } = string.Empty;
    public int? SourcePageNumber { get; init; }
    public string SourceRegion { get; init; } = string.Empty;
    public string ExtractionMethod { get; init; } = string.Empty;
    public string EvidenceStatus { get; init; } = string.Empty;
    public double? Confidence { get; init; }
}

public sealed record DrawingResolutionRequest
{
    public string OpportunityId { get; init; } = string.Empty;
    public string TaskId { get; init; } = string.Empty;
    public string Customer { get; init; } = string.Empty;
    public string Project { get; init; } = string.Empty;
    public string SourceRole { get; init; } = string.Empty;
    public DrawingRoleAuthority RoleAuthority { get; init; } = DrawingRoleAuthority.SourceDerived;
    public IReadOnlyList<DrawingIdentifierCandidate> Identifiers { get; init; } = Array.Empty<DrawingIdentifierCandidate>();

    // These are explicit fields and therefore are valid identifier evidence.
    public string PartNumber { get; init; } = string.Empty;
    public string DocumentNumber { get; init; } = string.Empty;
}

public sealed record ControlledDrawingRecord
{
    public string RecordId { get; init; } = string.Empty;
    public string PartNumber { get; init; } = string.Empty;
    public string DocumentNumber { get; init; } = string.Empty;
    public string ExactPath { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public bool IsLocallyAvailable { get; init; }
    public bool RetrievalRequired { get; init; }
    public DrawingReadOnlyPolicy ReadOnlyPolicy { get; init; } = DrawingReadOnlyPolicy.ReadOnlyRequired;
    public string Role { get; init; } = string.Empty;
    public DrawingRoleAuthority RoleAuthority { get; init; } = DrawingRoleAuthority.ControlledEvidence;
}

public sealed record ResolvedDrawingIdentifier
{
    public DrawingIdentifierKind Kind { get; init; }
    public string RawValue { get; init; } = string.Empty;
    public string NormalizedValue { get; init; } = string.Empty;
    public DrawingEvidenceAuthority Authority { get; init; }
    public string EvidenceId { get; init; } = string.Empty;
    public string SourceField { get; init; } = string.Empty;
    public string SourceLabel { get; init; } = string.Empty;
    public PacketDrawingSourceType? SourceType { get; init; }
    public string SourceArtifactId { get; init; } = string.Empty;
    public string SourceArtifactSha256 { get; init; } = string.Empty;
    public string SourceRecordId { get; init; } = string.Empty;
    public int? SourcePageNumber { get; init; }
    public string SourceRegion { get; init; } = string.Empty;
    public string ExtractionMethod { get; init; } = string.Empty;
    public string EvidenceStatus { get; init; } = string.Empty;
    public double? Confidence { get; init; }
}

public sealed record DrawingResolutionResult
{
    public DrawingResolutionStatus Status { get; init; }
    public string OpportunityId { get; init; } = string.Empty;
    public string TaskId { get; init; } = string.Empty;
    public string Customer { get; init; } = string.Empty;
    public string Project { get; init; } = string.Empty;
    public string SourceRole { get; init; } = string.Empty;
    public string EffectiveRole { get; init; } = string.Empty;
    public DrawingRoleAuthority RoleAuthority { get; init; }
    public ResolvedDrawingIdentifier? PartIdentifier { get; init; }
    public ResolvedDrawingIdentifier? DocumentIdentifier { get; init; }
    public ControlledDrawingRecord? ResolvedRecord { get; init; }
    public IReadOnlyList<ControlledDrawingRecord> Candidates { get; init; } = Array.Empty<ControlledDrawingRecord>();
    public DrawingReadOnlyPolicy ReadOnlyPolicy { get; init; } = DrawingReadOnlyPolicy.ReadOnlyRequired;
    public bool ExternalSystemsAccessed { get; init; }
    public int MutationActions { get; init; }
    public IReadOnlyList<string> Limitations { get; init; } = Array.Empty<string>();
}
