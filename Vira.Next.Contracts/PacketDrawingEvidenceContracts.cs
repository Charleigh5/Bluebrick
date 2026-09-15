using System.Text.Json.Serialization;

namespace Vira.Next.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PacketDrawingSourceType
{
    PdfPacket,
    SalesforceScreenshot
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PacketDrawingEvidenceStatus
{
    Candidate,
    Observed,
    Unsupported,
    Rejected
}

public sealed record PacketDrawingArtifact
{
    public string ArtifactId { get; init; } = string.Empty;
    public PacketDrawingSourceType SourceType { get; init; }
    public string Sha256 { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
}

public sealed record PacketDrawingIdentifierEvidence
{
    public string EvidenceId { get; init; } = string.Empty;
    public PacketDrawingSourceType SourceType { get; init; }
    public string SourceArtifactId { get; init; } = string.Empty;
    public string SourceArtifactSha256 { get; init; } = string.Empty;
    public string SourceRecordId { get; init; } = string.Empty;
    public int? PageNumber { get; init; }
    public string Region { get; init; } = string.Empty;
    public string ExtractionMethod { get; init; } = string.Empty;
    public string SourceField { get; init; } = string.Empty;
    public string SourceLabel { get; init; } = string.Empty;
    public string RawValue { get; init; } = string.Empty;
    public DrawingIdentifierKind KindHint { get; init; } = DrawingIdentifierKind.Unclassified;
    public PacketDrawingEvidenceStatus Status { get; init; } = PacketDrawingEvidenceStatus.Candidate;
    public double? Confidence { get; init; }
}

public sealed record PacketDrawingEvidenceSet
{
    public string OpportunityId { get; init; } = string.Empty;
    public string TaskId { get; init; } = string.Empty;
    public string Customer { get; init; } = string.Empty;
    public string Project { get; init; } = string.Empty;
    public string SourceRole { get; init; } = string.Empty;
    public DrawingRoleAuthority RoleAuthority { get; init; } = DrawingRoleAuthority.SourceDerived;
    public IReadOnlyList<PacketDrawingArtifact> Artifacts { get; init; } = Array.Empty<PacketDrawingArtifact>();
    public IReadOnlyList<PacketDrawingIdentifierEvidence> Identifiers { get; init; } = Array.Empty<PacketDrawingIdentifierEvidence>();
}

public sealed record PacketDrawingMappedEvidence
{
    public PacketDrawingIdentifierEvidence Evidence { get; init; } = new();
    public DrawingIdentifierKind ClassifiedKind { get; init; }
    public string NormalizedValue { get; init; } = string.Empty;
    public bool IncludedInResolutionRequest { get; init; }
}

public sealed record DrawingResolutionMappingResult
{
    public DrawingResolutionRequest Request { get; init; } = new();
    public IReadOnlyList<PacketDrawingMappedEvidence> Evidence { get; init; } = Array.Empty<PacketDrawingMappedEvidence>();
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Limitations { get; init; } = Array.Empty<string>();
    public bool ExternalSystemsAccessed { get; init; }
    public int MutationActions { get; init; }
}
