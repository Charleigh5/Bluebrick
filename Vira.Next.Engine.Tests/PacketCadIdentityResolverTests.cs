using Microsoft.VisualStudio.TestTools.UnitTesting;
using Vira.Next.Contracts;
using Vira.Next.Engine;

namespace Vira.Next.Engine.Tests;

[TestClass]
public sealed class PacketCadIdentityResolverTests
{
    [DataTestMethod]
    [DataRow(CadPropertyReadStatus.ReadError, true)]
    [DataRow(CadPropertyReadStatus.Unsupported, true)]
    [DataRow(CadPropertyReadStatus.CachedUnresolved, true)]
    [DataRow(CadPropertyReadStatus.Missing, true)]
    [DataRow(CadPropertyReadStatus.Blank, true)]
    [DataRow(CadPropertyReadStatus.Cached, true)]
    [DataRow(CadPropertyReadStatus.Resolved, false)]
    public void Resolve_IneligibleIdentityWithMatchingTitleHash_CannotConfirm(CadPropertyReadStatus status, bool wasResolved)
    {
        var property = Property("Part Number", "ASY511185-80238229") with { ReadStatus = status, WasResolved = wasResolved };
        var result = new PacketCadIdentityResolver().Resolve(
            Packet("ASY511185-80238229", "sha256:match"), Cad("sha256:match", property));

        Assert.AreEqual(PacketCadIdentityAuthority.StrongCandidate, result.Authority);
        Assert.IsFalse(result.IsAuthoritative);
        CollectionAssert.AreEqual(new[] { "DOCUMENT_TITLE_HASH" }, result.MatchSources.ToArray());
        Assert.IsTrue(result.CadEvidence.Any(item => item.EvidenceId == property.Evidence.EvidenceId));
        Assert.IsTrue(result.Limitations.Any(item => item.Contains("resolved", StringComparison.OrdinalIgnoreCase)));
    }

    [DataTestMethod]
    [DataRow(CadPropertyReadStatus.ReadError, true)]
    [DataRow(CadPropertyReadStatus.Unsupported, true)]
    [DataRow(CadPropertyReadStatus.CachedUnresolved, true)]
    [DataRow(CadPropertyReadStatus.Missing, true)]
    [DataRow(CadPropertyReadStatus.Blank, true)]
    [DataRow(CadPropertyReadStatus.Cached, true)]
    [DataRow(CadPropertyReadStatus.Resolved, false)]
    public void Resolve_IneligibleDisagreeingIdentity_CannotConflict(CadPropertyReadStatus status, bool wasResolved)
    {
        var property = Property("Part Number", "OTHER") with { ReadStatus = status, WasResolved = wasResolved };
        var result = new PacketCadIdentityResolver().Resolve(
            Packet("ASY511185-80238229", "sha256:packet"), Cad("sha256:cad", property));

        Assert.AreEqual(PacketCadIdentityAuthority.Unresolved, result.Authority);
        Assert.IsFalse(result.IsAuthoritative);
        Assert.AreEqual(0, result.MatchSources.Count);
        Assert.IsTrue(result.CadEvidence.Any(item => item.EvidenceId == property.Evidence.EvidenceId));
        Assert.IsTrue(result.Limitations.Any(item => item.Contains("resolved", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void Resolve_ControlledPropertyAndTitleHashMatch_ConfirmsIdentity()
    {
        var packet = Packet(
            "ASY511185-80238229",
            "sha256:title-match");
        var cad = Cad(
            "sha256:title-match",
            Property("Part Number", "ASY511185-80238229"));

        var result = new PacketCadIdentityResolver().Resolve(packet, cad);

        Assert.AreEqual(PacketCadIdentityAuthority.Confirmed, result.Authority);
        Assert.IsTrue(result.IsAuthoritative);
        CollectionAssert.AreEquivalent(
            new[] { "CAD_CONTROLLED_PROPERTY", "DOCUMENT_TITLE_HASH" },
            result.MatchSources.ToArray());
        Assert.AreEqual(EngineeringMutationBoundary.ReadOnly, result.MutationBoundary);
    }

    [TestMethod]
    public void Resolve_OneControlledPropertyMatch_RemainsStrongCandidate()
    {
        var packet = Packet(
            "ASY511185-80238229",
            "sha256:packet-title");
        var cad = Cad(
            "sha256:different-title",
            Property("Document Number", "asy511185-80238229.sldasm"));

        var result = new PacketCadIdentityResolver().Resolve(packet, cad);

        Assert.AreEqual(PacketCadIdentityAuthority.StrongCandidate, result.Authority);
        Assert.IsFalse(result.IsAuthoritative);
        CollectionAssert.AreEqual(new[] { "CAD_CONTROLLED_PROPERTY" }, result.MatchSources.ToArray());
    }

    [TestMethod]
    public void Resolve_ComparableControlledPropertyDisagrees_ReturnsConflict()
    {
        var packet = Packet(
            "ASY511185-80238229",
            "sha256:packet-title");
        var cad = Cad(
            "sha256:different-title",
            Property("Part Number", "ASY511185-99999999"));

        var result = new PacketCadIdentityResolver().Resolve(packet, cad);

        Assert.AreEqual(PacketCadIdentityAuthority.Conflict, result.Authority);
        Assert.IsFalse(result.IsAuthoritative);
        Assert.IsTrue(result.Limitations.Any(item => item.Contains("disagrees", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void Resolve_NoActiveDocument_ReturnsUnresolvedWithoutInventingEvidence()
    {
        var packet = Packet("ASY511185-80238229", "sha256:packet-title");
        var cad = new CadDocumentSnapshot
        {
            SnapshotId = "cad-none",
            State = CadSnapshotState.NoActiveDocument,
            Message = "No active document.",
            MutationActions = 0,
            ExternalSystemsAccessed = false
        };

        var result = new PacketCadIdentityResolver().Resolve(packet, cad);

        Assert.AreEqual(PacketCadIdentityAuthority.Unresolved, result.Authority);
        Assert.IsFalse(result.IsAuthoritative);
        Assert.AreEqual(0, result.CadEvidence.Count);
        Assert.AreEqual(0, cad.MutationActions);
        Assert.IsFalse(cad.ExternalSystemsAccessed);
    }

    private static PacketEvidenceSnapshot Packet(string identifier, string titleHash)
    {
        return new PacketEvidenceSnapshot
        {
            SnapshotId = "packet-1",
            SchemaVersion = "vira.packet-evidence.v2",
            Identifiers = new[]
            {
                new PacketIdentifierEvidence
                {
                    Identifier = identifier,
                    CandidateDocumentTitleHashes = new[] { titleHash },
                    Evidence = Evidence("packet-id", EvidenceSourceKind.Pdf, "document-number", identifier)
                }
            }
        };
    }

    private static CadDocumentSnapshot Cad(string titleHash, params CadPropertySnapshot[] properties)
    {
        return new CadDocumentSnapshot
        {
            SnapshotId = "cad-1",
            SchemaVersion = "vira.cad-document.v1",
            State = CadSnapshotState.Ready,
            DocumentType = CadDocumentType.Assembly,
            TitleHash = titleHash,
            IsReadOnly = true,
            Properties = properties,
            MutationActions = 0,
            ExternalSystemsAccessed = false
        };
    }

    private static CadPropertySnapshot Property(string name, string value)
    {
        return new CadPropertySnapshot
        {
            Name = name,
            Scope = CadPropertyScope.Document,
            RawValue = value,
            EvaluatedValue = value,
            ReadStatus = CadPropertyReadStatus.Resolved,
            WasResolved = true,
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
