using Microsoft.VisualStudio.TestTools.UnitTesting;
using Vira.Next.Contracts;
using Vira.Next.Engine;

namespace Vira.Next.Engine.Tests;

[TestClass]
public sealed class PropertyComparisonEngineTests
{
    private static readonly PacketCadIdentityResolution ConfirmedIdentity = new()
    {
        Authority = PacketCadIdentityAuthority.Confirmed,
        IsAuthoritative = true,
        MutationBoundary = EngineeringMutationBoundary.ReadOnly
    };

    [DataTestMethod]
    [DataRow("A", "B")]
    [DataRow("B", "A")]
    public void Compare_DistinctPacketValues_ConflictsRegardlessOfOrder(string first, string second)
    {
        var result = new PropertyComparisonEngine().Compare(
            Packet(new[] { PacketProperty("Revision", first), PacketProperty("REV", second) }),
            Cad(new[] { CadProperty("Revision", "A") }), ConfirmedIdentity).Single();

        Assert.AreEqual(PacketCadComparisonStatus.Conflict, result.Status);
        Assert.IsFalse(result.IsAuthoritative);
        Assert.AreEqual("VIRA-PROPERTY-PACKET-CONFLICT-001", result.NormalizationRuleId);
        CollectionAssert.AreEqual(new[] { "packet-REV", "packet-Revision" }, result.PacketEvidence.Select(item => item.EvidenceId).ToArray());
        Assert.AreEqual(1, result.CadEvidence.Count);
        Assert.IsTrue(result.Limitations.Any(item => item.Contains("distinct normalized packet values", StringComparison.Ordinal)));
    }

    [DataTestMethod]
    [DataRow("A", " a ")]
    [DataRow(" a ", "A")]
    public void Compare_EquivalentPacketValues_CoalescesWithoutOrderDependence(string first, string second)
    {
        var result = new PropertyComparisonEngine().Compare(
            Packet(new[] { PacketProperty("Revision", first), PacketProperty("REV", second) }),
            Cad(new[] { CadProperty("Revision", "A") }), ConfirmedIdentity).Single();

        Assert.AreEqual(PacketCadComparisonStatus.NormalizedMatch, result.Status);
        Assert.IsTrue(result.IsAuthoritative);
        Assert.AreEqual(2, result.PacketEvidence.Count);
        Assert.AreEqual(0, result.Limitations.Count);
    }

    [TestMethod]
    public void Compare_DistinctPacketValuesWithoutCad_RemainsPacketConflict()
    {
        var result = new PropertyComparisonEngine().Compare(
            Packet(new[] { PacketProperty("Revision", "A"), PacketProperty("REV", "B") }),
            Cad(Array.Empty<CadPropertySnapshot>()), ConfirmedIdentity).Single();

        Assert.AreEqual(PacketCadComparisonStatus.Conflict, result.Status);
        Assert.IsFalse(result.IsAuthoritative);
        Assert.AreEqual(2, result.PacketEvidence.Count);
    }


    [TestMethod]
    public void Compare_ExactRevisionMatch_IsAuthoritativeExactMatch()
    {
        var result = Compare(
            PacketProperty("Revision", "B"),
            CadProperty("REV", "B"),
            ConfirmedIdentity).Single();

        Assert.AreEqual(PacketCadComparisonStatus.ExactMatch, result.Status);
        Assert.IsTrue(result.IsAuthoritative);
        Assert.AreEqual("VIRA-TEXT-EXACT-001", result.NormalizationRuleId);
        Assert.AreEqual(1, result.PacketEvidence.Count);
        Assert.AreEqual(1, result.CadEvidence.Count);
    }

    [TestMethod]
    public void Compare_CaseAndWhitespaceDifference_IsNormalizedMatch()
    {
        var result = Compare(
            PacketProperty("Description", "Optical  Value Sign Holder"),
            CadProperty("DESC", " optical value sign holder "),
            ConfirmedIdentity).Single();

        Assert.AreEqual(PacketCadComparisonStatus.NormalizedMatch, result.Status);
        Assert.AreEqual("VIRA-TEXT-NORMALIZE-001", result.NormalizationRuleId);
    }

    [TestMethod]
    public void Compare_ThicknessWithinConfiguredTolerance_IsNormalizedMatch()
    {
        var engine = new PropertyComparisonEngine(new PropertyComparisonOptions
        {
            ThicknessToleranceInches = 0.0005m
        });

        var result = engine.Compare(
            Packet(new[] { PacketProperty("Thickness", "0.0747 in") }),
            Cad(new[] { CadProperty("Thickness", "0.075 in") }),
            ConfirmedIdentity).Single();

        Assert.AreEqual(PacketCadComparisonStatus.NormalizedMatch, result.Status);
        Assert.AreEqual("VIRA-THICKNESS-INCH-TOLERANCE-001", result.NormalizationRuleId);
        Assert.AreEqual(0.99, result.Confidence, 0.0001);
    }

    [TestMethod]
    public void Compare_GaugeWithoutApprovedMap_IsUnsupported()
    {
        var result = Compare(
            PacketProperty("Thickness", "14 GA"),
            CadProperty("Thickness", "0.0747 in"),
            ConfirmedIdentity).Single();

        Assert.AreEqual(PacketCadComparisonStatus.Unsupported, result.Status);
        Assert.IsFalse(result.IsAuthoritative);
        Assert.IsTrue(result.Limitations.Any(item => item.Contains("gauge", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void Compare_MissingCadProperty_IdentifiesMissingSide()
    {
        var result = new PropertyComparisonEngine().Compare(
            Packet(new[] { PacketProperty("Material", "A1008") }),
            Cad(Array.Empty<CadPropertySnapshot>()),
            ConfirmedIdentity).Single();

        Assert.AreEqual(PacketCadComparisonStatus.MissingInCad, result.Status);
        Assert.AreEqual(1, result.PacketEvidence.Count);
        Assert.AreEqual(0, result.CadEvidence.Count);
        Assert.AreEqual("Inspect the active CAD document for the missing Material evidence.", result.RecommendedVerificationAction);
    }

    [TestMethod]
    public void Compare_UnresolvedCadValue_DoesNotBecomeAuthoritativeMatch()
    {
        var unresolved = CadProperty("Material", "A1008");
        unresolved = unresolved with
        {
            EvaluatedValue = string.Empty,
            WasResolved = false,
            ReadStatus = CadPropertyReadStatus.CachedUnresolved
        };

        var result = Compare(
            PacketProperty("Material", "A1008"),
            unresolved,
            ConfirmedIdentity).Single();

        Assert.AreEqual(PacketCadComparisonStatus.ProbableMatch, result.Status);
        Assert.IsFalse(result.IsAuthoritative);
        Assert.IsTrue(result.Limitations.Any(item => item.Contains("cached", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void Compare_ConflictUnderUnconfirmedIdentity_RemainsNonAuthoritative()
    {
        var result = Compare(
            PacketProperty("Revision", "B"),
            CadProperty("Revision", "A"),
            new PacketCadIdentityResolution
            {
                Authority = PacketCadIdentityAuthority.StrongCandidate,
                IsAuthoritative = false,
                MutationBoundary = EngineeringMutationBoundary.ReadOnly
            }).Single();

        Assert.AreEqual(PacketCadComparisonStatus.Conflict, result.Status);
        Assert.IsFalse(result.IsAuthoritative);
        Assert.AreEqual(PacketCadIdentityAuthority.StrongCandidate, result.IdentityAuthority);
        Assert.IsTrue(result.Limitations.Any(item => item.Contains("identity", StringComparison.OrdinalIgnoreCase)));
    }

    private static IReadOnlyList<PacketCadComparison> Compare(
        PacketPropertyEvidence packetProperty,
        CadPropertySnapshot cadProperty,
        PacketCadIdentityResolution identity)
    {
        return new PropertyComparisonEngine().Compare(
            Packet(new[] { packetProperty }),
            Cad(new[] { cadProperty }),
            identity);
    }

    private static PacketEvidenceSnapshot Packet(IReadOnlyList<PacketPropertyEvidence> properties)
    {
        return new PacketEvidenceSnapshot
        {
            SnapshotId = "packet-1",
            SchemaVersion = "vira.packet-evidence.v2",
            Properties = properties
        };
    }

    private static CadDocumentSnapshot Cad(IReadOnlyList<CadPropertySnapshot> properties)
    {
        return new CadDocumentSnapshot
        {
            SnapshotId = "cad-1",
            SchemaVersion = "vira.cad-document.v1",
            State = CadSnapshotState.Ready,
            DocumentType = CadDocumentType.Part,
            IsReadOnly = true,
            Properties = properties,
            MutationActions = 0,
            ExternalSystemsAccessed = false
        };
    }

    private static PacketPropertyEvidence PacketProperty(string name, string value)
    {
        return new PacketPropertyEvidence
        {
            Name = name,
            RawValue = value,
            EvaluatedValue = value,
            Evidence = Evidence($"packet-{name}", EvidenceSourceKind.Pdf, name, value)
        };
    }

    private static CadPropertySnapshot CadProperty(string name, string value)
    {
        return new CadPropertySnapshot
        {
            Name = name,
            Scope = CadPropertyScope.Document,
            RawValue = value,
            EvaluatedValue = value,
            WasResolved = true,
            ReadStatus = CadPropertyReadStatus.Resolved,
            Evidence = Evidence($"cad-{name}", EvidenceSourceKind.CadDocument, name, value)
        };
    }

    private static EvidenceRef Evidence(string id, EvidenceSourceKind kind, string field, string value)
    {
        return new EvidenceRef
        {
            EvidenceId = id,
            SourceKind = kind,
            SourceId = kind == EvidenceSourceKind.Pdf ? "packet-1" : "cad-1",
            FieldName = field,
            RawValue = value,
            EvaluatedValue = value,
            ExtractionMethod = "fixture",
            RuleId = "FIXTURE-001",
            Authority = EvidenceAuthority.Observed,
            Confidence = 1.0,
            VerificationStatus = EvidenceVerificationStatus.Confirmed
        };
    }
}
