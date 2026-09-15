using Vira.Next.Contracts;

namespace Vira.Next.Engine.Tests;

internal static class PacketCadAssemblyFixtures
{
    internal static readonly PacketCadIdentityResolution ConfirmedIdentity = new()
    {
        Authority = PacketCadIdentityAuthority.Confirmed,
        IsAuthoritative = true,
        MatchSources = new[] { "CAD_CONTROLLED_PROPERTY", "DOCUMENT_TITLE_HASH" },
        MutationBoundary = EngineeringMutationBoundary.ReadOnly
    };

    internal static PacketEvidenceSnapshot Packet(params PacketBomRowEvidence[] rows) => new()
    {
        SnapshotId = "packet-b-1",
        SchemaVersion = "vira.packet-evidence.v2",
        PacketId = "ASY511185-80238229",
        BomRows = rows
    };

    internal static PacketBomRowEvidence Bom(
        string item,
        decimal quantity,
        string identifier,
        string description = "",
        string revision = "",
        string configuration = "",
        string parent = "",
        PacketBomResponsibility responsibility = PacketBomResponsibility.Controlled)
    {
        return new PacketBomRowEvidence
        {
            ItemNumber = item,
            RawQuantity = quantity.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Quantity = quantity,
            Identifier = identifier,
            Description = description,
            Revision = revision,
            ReferencedConfiguration = configuration,
            ParentIdentifierCandidate = parent,
            Responsibility = responsibility,
            Evidence = Evidence($"packet-bom-{item}", EvidenceSourceKind.Pdf, "bom-row", identifier)
        };
    }

    internal static CadDocumentSnapshot Cad(params CadComponentSnapshot[] components) => new()
    {
        SnapshotId = "cad-b-1",
        SchemaVersion = "vira.cad-document.v1",
        Source = CadSnapshotSource.LocalFixture,
        State = CadSnapshotState.Ready,
        DocumentType = CadDocumentType.Assembly,
        ActiveConfiguration = "Default",
        IsReadOnly = true,
        Components = components,
        AssemblyTraversal = new CadAssemblyTraversalSummary
        {
            MaxDepth = 32,
            RecordLimit = 5000,
            RecordedCount = components.Length,
            MutationActions = 0,
            ExternalSystemsAccessed = false
        },
        MutationActions = 0,
        ExternalSystemsAccessed = false
    };

    internal static CadComponentSnapshot Component(
        string id,
        string identifier,
        string parent = "",
        string configuration = "Default",
        CadComponentResolutionState resolution = CadComponentResolutionState.Resolved,
        CadComponentSuppressionState suppression = CadComponentSuppressionState.FullyResolved,
        CadComponentKind kind = CadComponentKind.Part,
        params CadPropertySnapshot[] properties)
    {
        return new CadComponentSnapshot
        {
            SnapshotId = id,
            ParentSnapshotId = parent,
            NativeComponentId = id.GetHashCode(StringComparison.Ordinal) & 0x7fffffff,
            Depth = string.IsNullOrEmpty(parent) ? 0 : 1,
            NameHash = $"name_sha256:{id}",
            NativePathHash = $"native_sha256:{id}",
            IdentifierCandidate = identifier,
            IdentifierHash = $"value_sha256:{id}",
            ReferencedConfiguration = configuration,
            ReferencedConfigurationHash = $"value_sha256:{configuration.ToLowerInvariant()}",
            InstanceCount = 1,
            Kind = kind,
            SuppressionState = suppression,
            ResolutionState = resolution,
            ChildrenState = kind == CadComponentKind.Assembly
                ? CadComponentChildrenState.Enumerated
                : CadComponentChildrenState.None,
            Properties = properties,
            Evidence = Evidence($"cad-component-{id}", EvidenceSourceKind.CadComponent, "component", identifier),
            Limitations = resolution == CadComponentResolutionState.Resolved
                ? Array.Empty<string>()
                : new[] { $"Component state is {resolution}." }
        };
    }

    internal static CadPropertySnapshot Property(string name, string value) => new()
    {
        Name = name,
        Scope = CadPropertyScope.Component,
        RawValue = value,
        EvaluatedValue = value,
        WasResolved = true,
        ReadStatus = CadPropertyReadStatus.Resolved,
        Evidence = Evidence($"cad-property-{name}", EvidenceSourceKind.CadComponent, name, value)
    };

    internal static EvidenceRef Evidence(string id, EvidenceSourceKind kind, string field, string value) => new()
    {
        EvidenceId = id,
        SourceKind = kind,
        SourceId = kind == EvidenceSourceKind.Pdf ? "packet-b-1" : "cad-b-1",
        RegionOrNativePath = kind == EvidenceSourceKind.Pdf ? "bom:fixture" : "component:fixture",
        FieldName = field,
        RawValue = value,
        EvaluatedValue = value,
        ExtractionMethod = "deterministic-fixture",
        RuleId = "VIRA-PHASE-B-FIXTURE-001",
        Authority = EvidenceAuthority.Observed,
        Confidence = 1.0,
        VerificationStatus = EvidenceVerificationStatus.Confirmed
    };
}
