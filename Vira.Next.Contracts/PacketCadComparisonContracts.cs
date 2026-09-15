using System.Text.Json.Serialization;

namespace Vira.Next.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PacketCadIdentityAuthority
{
    Confirmed,
    StrongCandidate,
    WeakCandidate,
    Conflict,
    Unresolved
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PacketCadComparisonStatus
{
    ExactMatch,
    NormalizedMatch,
    ProbableMatch,
    Conflict,
    MissingInPdf,
    MissingInCad,
    InsufficientEvidence,
    NotApplicable,
    Unsupported,
    Duplicate,
    QuantityConflict,
    HierarchyConflict,
    ConfigurationGap,
    UnresolvedEvidence
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PacketCadSeverity
{
    Info,
    Warning,
    High
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EngineeringMutationBoundary
{
    ReadOnly
}

public sealed record PacketIdentifierEvidence
{
    public string Identifier { get; init; } = string.Empty;
    public IReadOnlyList<string> CandidateDocumentTitleHashes { get; init; } = Array.Empty<string>();
    public EvidenceRef Evidence { get; init; } = new();
}

public sealed record PacketPropertyEvidence
{
    public string Name { get; init; } = string.Empty;
    public string RawValue { get; init; } = string.Empty;
    public string EvaluatedValue { get; init; } = string.Empty;
    public EvidenceRef Evidence { get; init; } = new();
}

public sealed record PacketEvidenceSnapshot
{
    public string SchemaVersion { get; init; } = "vira.packet-evidence.v2";
    public string SnapshotId { get; init; } = string.Empty;
    public string PacketId { get; init; } = string.Empty;
    public string FileNameLabel { get; init; } = string.Empty;
    public int PageCount { get; init; }
    public IReadOnlyList<PacketIdentifierEvidence> Identifiers { get; init; } = Array.Empty<PacketIdentifierEvidence>();
    public IReadOnlyList<PacketPropertyEvidence> Properties { get; init; } = Array.Empty<PacketPropertyEvidence>();
    public IReadOnlyList<PacketBomRowEvidence> BomRows { get; init; } = Array.Empty<PacketBomRowEvidence>();
    public IReadOnlyList<string> Limitations { get; init; } = Array.Empty<string>();
}

public sealed record PacketCadIdentityResolution
{
    public PacketCadIdentityAuthority Authority { get; init; } = PacketCadIdentityAuthority.Unresolved;
    public bool IsAuthoritative { get; init; }
    public IReadOnlyList<string> MatchSources { get; init; } = Array.Empty<string>();
    public IReadOnlyList<EvidenceRef> PacketEvidence { get; init; } = Array.Empty<EvidenceRef>();
    public IReadOnlyList<EvidenceRef> CadEvidence { get; init; } = Array.Empty<EvidenceRef>();
    public IReadOnlyList<string> Limitations { get; init; } = Array.Empty<string>();
    public EngineeringMutationBoundary MutationBoundary { get; init; } = EngineeringMutationBoundary.ReadOnly;
}

public sealed record PacketCadComparison
{
    public string ComparisonId { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public IReadOnlyList<EvidenceRef> PacketEvidence { get; init; } = Array.Empty<EvidenceRef>();
    public IReadOnlyList<EvidenceRef> CadEvidence { get; init; } = Array.Empty<EvidenceRef>();
    public PacketCadComparisonStatus Status { get; init; }
    public PacketCadIdentityAuthority IdentityAuthority { get; init; } = PacketCadIdentityAuthority.Unresolved;
    public bool IsAuthoritative { get; init; }
    public string NormalizationRuleId { get; init; } = string.Empty;
    public double Confidence { get; init; }
    public PacketCadSeverity Severity { get; init; }
    public IReadOnlyList<string> Limitations { get; init; } = Array.Empty<string>();
    public string RecommendedVerificationAction { get; init; } = string.Empty;
    public EngineeringMutationBoundary MutationBoundary { get; init; } = EngineeringMutationBoundary.ReadOnly;
}

public sealed record PropertyComparisonOptions
{
    public decimal ThicknessToleranceInches { get; init; } = 0.0005m;
    public IReadOnlyDictionary<string, decimal> ApprovedGaugeThicknessInches { get; init; } =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ApprovedValueAliases { get; init; } =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
}
