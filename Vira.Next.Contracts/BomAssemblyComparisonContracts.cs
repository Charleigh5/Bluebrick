using System.Text.Json.Serialization;

namespace Vira.Next.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PacketBomResponsibility
{
    Unknown,
    Controlled,
    PurchasedByOthers,
    Reference,
    Existing,
    StoreSupplied
}

public sealed record PacketBomRowEvidence
{
    public string ItemNumber { get; init; } = string.Empty;
    public string RawQuantity { get; init; } = string.Empty;
    public decimal? Quantity { get; init; }
    public string Identifier { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Revision { get; init; } = string.Empty;
    public string ReferencedConfiguration { get; init; } = string.Empty;
    public string ParentIdentifierCandidate { get; init; } = string.Empty;
    public PacketBomResponsibility Responsibility { get; init; } = PacketBomResponsibility.Unknown;
    public EvidenceRef Evidence { get; init; } = new();
    public IReadOnlyList<string> Limitations { get; init; } = Array.Empty<string>();
}

public sealed record BomAssemblyMatchScorecard
{
    public int PacketControlledIdentifiers { get; init; }
    public int CadControlledIdentifiers { get; init; }
    public int MatchedIdentifiers { get; init; }
    public double Precision { get; init; }
    public double Recall { get; init; }
}

public sealed record BomAssemblyComparisonReport
{
    public string SchemaVersion { get; init; } = "vira.packet-cad.bom-assembly.v1";
    public IReadOnlyList<PacketCadComparison> Comparisons { get; init; } = Array.Empty<PacketCadComparison>();
    public BomAssemblyMatchScorecard Scorecard { get; init; } = new();
    public IReadOnlyList<string> Limitations { get; init; } = Array.Empty<string>();
    public int MutationActions { get; init; }
    public bool ExternalSystemsAccessed { get; init; }
}

public sealed record AssemblyHierarchyValidation
{
    public bool IsValid { get; init; }
    public int RecordedCount { get; init; }
    public int CycleCount { get; init; }
    public IReadOnlyList<string> Limitations { get; init; } = Array.Empty<string>();
}
