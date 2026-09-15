using Microsoft.VisualStudio.TestTools.UnitTesting;
using Vira.Next.Contracts;
using Vira.Next.Engine;

namespace Vira.Next.Engine.Tests;

[TestClass]
public sealed class BomAssemblyComparisonEngineTests
{
    [DataTestMethod]
    [DataRow(CadPropertyReadStatus.ReadError, true)]
    [DataRow(CadPropertyReadStatus.Unsupported, true)]
    [DataRow(CadPropertyReadStatus.CachedUnresolved, true)]
    [DataRow(CadPropertyReadStatus.Missing, true)]
    [DataRow(CadPropertyReadStatus.Blank, true)]
    [DataRow(CadPropertyReadStatus.Cached, true)]
    [DataRow(CadPropertyReadStatus.Resolved, false)]
    public void Compare_IneligibleComponentRevision_CannotMatch(CadPropertyReadStatus status, bool wasResolved)
    {
        var property = PacketCadAssemblyFixtures.Property("Revision", "B") with { ReadStatus = status, WasResolved = wasResolved };
        var component = PacketCadAssemblyFixtures.Component("part-1", "PART-1", properties: new[] { property });
        var packet = PacketCadAssemblyFixtures.Packet(PacketCadAssemblyFixtures.Bom("1", 1, "PART-1", revision: "B"));

        var result = new BomAssemblyComparisonEngine().Compare(packet, PacketCadAssemblyFixtures.Cad(component),
            PacketCadAssemblyFixtures.ConfirmedIdentity).Comparisons.Single(item => item.Category == "bom:revision");

        Assert.AreEqual(PacketCadComparisonStatus.InsufficientEvidence, result.Status);
        Assert.IsFalse(result.IsAuthoritative);
        Assert.IsTrue(result.Limitations.Any(item => item.Contains("resolved", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(result.CadEvidence.Any(item => item.EvidenceId == property.Evidence.EvidenceId));
        Assert.IsTrue(result.CadEvidence.Any(item => item.EvidenceId == component.Evidence.EvidenceId));
        Assert.AreEqual(1, result.PacketEvidence.Count);
    }

    [TestMethod]
    public void Compare_OneComponentMissingRequiredRevision_CannotUseOtherComponentsMatch()
    {
        var property = PacketCadAssemblyFixtures.Property("Revision", "B");
        var complete = PacketCadAssemblyFixtures.Component("part-1", "PART-1", properties: new[] { property });
        var missing = PacketCadAssemblyFixtures.Component("part-2", "PART-1");
        var packet = PacketCadAssemblyFixtures.Packet(PacketCadAssemblyFixtures.Bom("1", 2, "PART-1", revision: "B"));

        var result = new BomAssemblyComparisonEngine().Compare(packet, PacketCadAssemblyFixtures.Cad(complete, missing),
            PacketCadAssemblyFixtures.ConfirmedIdentity).Comparisons.Single(item => item.Category == "bom:revision");

        Assert.AreEqual(PacketCadComparisonStatus.InsufficientEvidence, result.Status);
        Assert.IsFalse(result.IsAuthoritative);
        Assert.AreEqual(3, result.CadEvidence.Count);
        Assert.IsTrue(result.Limitations.Any(item => item.Contains("missing", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void Compare_ResolvedComponentRevision_RemainsAuthoritative()
    {
        var property = PacketCadAssemblyFixtures.Property("Revision", "B");
        var component = PacketCadAssemblyFixtures.Component("part-1", "PART-1", properties: new[] { property });
        var packet = PacketCadAssemblyFixtures.Packet(PacketCadAssemblyFixtures.Bom("1", 1, "PART-1", revision: "B"));

        var result = new BomAssemblyComparisonEngine().Compare(packet, PacketCadAssemblyFixtures.Cad(component),
            PacketCadAssemblyFixtures.ConfirmedIdentity).Comparisons.Single(item => item.Category == "bom:revision");

        Assert.AreEqual(PacketCadComparisonStatus.ExactMatch, result.Status);
        Assert.IsTrue(result.IsAuthoritative);
    }

    [TestMethod]
    public void Compare_MatchingNestedAssembly_ReportsMembershipQuantityHierarchyAndMetadataMatches()
    {
        var packet = PacketCadAssemblyFixtures.Packet(
            PacketCadAssemblyFixtures.Bom(
                "1", 2, "MPM511284-80241102", "Mounting Bracket", "B", "Default", "SUB511200-80240000"));
        var parent = PacketCadAssemblyFixtures.Component(
            "sub-1", "SUB511200-80240000", kind: CadComponentKind.Assembly);
        var first = PacketCadAssemblyFixtures.Component(
            "part-1", "MPM511284-80241102", "sub-1", "Default",
            properties: new[]
            {
                PacketCadAssemblyFixtures.Property("Description", "Mounting Bracket"),
                PacketCadAssemblyFixtures.Property("Revision", "B")
            });
        var second = first with { SnapshotId = "part-2", Evidence = PacketCadAssemblyFixtures.Evidence("cad-component-part-2", EvidenceSourceKind.CadComponent, "component", "MPM511284-80241102") };

        var report = new BomAssemblyComparisonEngine().Compare(
            packet,
            PacketCadAssemblyFixtures.Cad(parent, first, second),
            PacketCadAssemblyFixtures.ConfirmedIdentity);

        Assert.AreEqual("vira.packet-cad.bom-assembly.v1", report.SchemaVersion);
        AssertStatus(report, "bom:membership", PacketCadComparisonStatus.ExactMatch);
        AssertStatus(report, "bom:quantity", PacketCadComparisonStatus.ExactMatch);
        AssertStatus(report, "bom:hierarchy", PacketCadComparisonStatus.ExactMatch);
        AssertStatus(report, "bom:configuration", PacketCadComparisonStatus.ExactMatch);
        AssertStatus(report, "bom:description", PacketCadComparisonStatus.ExactMatch);
        AssertStatus(report, "bom:revision", PacketCadComparisonStatus.ExactMatch);
        Assert.AreEqual(1.0, report.Scorecard.Precision, 0.0001);
        Assert.AreEqual(1.0, report.Scorecard.Recall, 0.0001);
        Assert.IsTrue(report.Comparisons.All(item => item.PacketEvidence.Count > 0 && item.CadEvidence.Count > 0));
        Assert.AreEqual(0, report.MutationActions);
        Assert.IsFalse(report.ExternalSystemsAccessed);
    }

    [TestMethod]
    public void Compare_MissingAndExtraComponents_NameTheMissingEvidenceSide()
    {
        var packet = PacketCadAssemblyFixtures.Packet(
            PacketCadAssemblyFixtures.Bom("1", 1, "MPM511284-80241102"));
        var extra = PacketCadAssemblyFixtures.Component("extra", "HWD511999-80249999");

        var report = new BomAssemblyComparisonEngine().Compare(
            packet,
            PacketCadAssemblyFixtures.Cad(extra),
            PacketCadAssemblyFixtures.ConfirmedIdentity);

        var missingCad = report.Comparisons.Single(item => item.Status == PacketCadComparisonStatus.MissingInCad);
        var missingPdf = report.Comparisons.Single(item => item.Status == PacketCadComparisonStatus.MissingInPdf);
        Assert.AreEqual(1, missingCad.PacketEvidence.Count);
        Assert.AreEqual(0, missingCad.CadEvidence.Count);
        Assert.AreEqual(0, missingPdf.PacketEvidence.Count);
        Assert.AreEqual(1, missingPdf.CadEvidence.Count);
    }

    [TestMethod]
    public void Compare_DuplicatePacketRowsAndQuantityMismatch_AreDistinctFindings()
    {
        var packet = PacketCadAssemblyFixtures.Packet(
            PacketCadAssemblyFixtures.Bom("1", 1, "MPM511284-80241102"),
            PacketCadAssemblyFixtures.Bom("7", 1, "MPM511284-80241102"));
        var only = PacketCadAssemblyFixtures.Component("part-1", "MPM511284-80241102");

        var report = new BomAssemblyComparisonEngine().Compare(
            packet,
            PacketCadAssemblyFixtures.Cad(only),
            PacketCadAssemblyFixtures.ConfirmedIdentity);

        AssertStatus(report, "bom:duplicate", PacketCadComparisonStatus.Duplicate);
        AssertStatus(report, "bom:quantity", PacketCadComparisonStatus.QuantityConflict);
    }

    [TestMethod]
    public void Compare_HierarchyConfigurationAndRevisionDisagree_ReturnsSpecificStatuses()
    {
        var packet = PacketCadAssemblyFixtures.Packet(
            PacketCadAssemblyFixtures.Bom(
                "1", 1, "MPM511284-80241102", revision: "B", configuration: "Default", parent: "SUB511200-80240000"));
        var wrongParent = PacketCadAssemblyFixtures.Component(
            "sub-wrong", "SUB511201-80240001", kind: CadComponentKind.Assembly);
        var part = PacketCadAssemblyFixtures.Component(
            "part-1", "MPM511284-80241102", "sub-wrong", "Alternate",
            properties: new[] { PacketCadAssemblyFixtures.Property("Revision", "A") });

        var report = new BomAssemblyComparisonEngine().Compare(
            packet,
            PacketCadAssemblyFixtures.Cad(wrongParent, part),
            PacketCadAssemblyFixtures.ConfirmedIdentity);

        AssertStatus(report, "bom:hierarchy", PacketCadComparisonStatus.HierarchyConflict);
        AssertStatus(report, "bom:configuration", PacketCadComparisonStatus.ConfigurationGap);
        AssertStatus(report, "bom:revision", PacketCadComparisonStatus.Conflict);
    }

    [TestMethod]
    public void Compare_UnresolvedSuppressedLightweightAndMissingComponents_RemainVisibleAndNonAuthoritative()
    {
        var identifiers = new[]
        {
            "MPM511281-80241101",
            "MPM511282-80241102",
            "MPM511283-80241103",
            "MPM511284-80241104"
        };
        var packet = PacketCadAssemblyFixtures.Packet(identifiers
            .Select((identifier, index) => PacketCadAssemblyFixtures.Bom((index + 1).ToString(), 1, identifier))
            .ToArray());
        var cad = PacketCadAssemblyFixtures.Cad(
            PacketCadAssemblyFixtures.Component("unloaded", identifiers[0], resolution: CadComponentResolutionState.Unloaded),
            PacketCadAssemblyFixtures.Component("suppressed", identifiers[1], resolution: CadComponentResolutionState.Suppressed, suppression: CadComponentSuppressionState.Suppressed),
            PacketCadAssemblyFixtures.Component("lightweight", identifiers[2], resolution: CadComponentResolutionState.Lightweight, suppression: CadComponentSuppressionState.Lightweight),
            PacketCadAssemblyFixtures.Component("missing", identifiers[3], resolution: CadComponentResolutionState.MissingReference));

        var report = new BomAssemblyComparisonEngine().Compare(
            packet,
            cad,
            PacketCadAssemblyFixtures.ConfirmedIdentity);

        var unresolved = report.Comparisons.Where(item => item.Status == PacketCadComparisonStatus.UnresolvedEvidence).ToArray();
        Assert.AreEqual(4, unresolved.Length);
        Assert.IsTrue(unresolved.All(item => item.CadEvidence.Count == 1));
        Assert.IsTrue(unresolved.All(item => !item.IsAuthoritative));
        Assert.IsTrue(unresolved.All(item => item.Limitations.Count > 0));
    }

    [TestMethod]
    public void Compare_ResponsibilityOnlyRows_DoNotBecomeMissingCadFindings()
    {
        var packet = PacketCadAssemblyFixtures.Packet(
            PacketCadAssemblyFixtures.Bom("1", 1, "PBO511001-80240001", responsibility: PacketBomResponsibility.PurchasedByOthers),
            PacketCadAssemblyFixtures.Bom("2", 1, "REF511002-80240002", responsibility: PacketBomResponsibility.Reference),
            PacketCadAssemblyFixtures.Bom("3", 1, "EXIST511003-80240003", responsibility: PacketBomResponsibility.Existing));

        var report = new BomAssemblyComparisonEngine().Compare(
            packet,
            PacketCadAssemblyFixtures.Cad(),
            PacketCadAssemblyFixtures.ConfirmedIdentity);

        Assert.AreEqual(3, report.Comparisons.Count(item => item.Status == PacketCadComparisonStatus.NotApplicable));
        Assert.AreEqual(0, report.Comparisons.Count(item => item.Status == PacketCadComparisonStatus.MissingInCad));
    }

    private static void AssertStatus(BomAssemblyComparisonReport report, string category, PacketCadComparisonStatus status)
    {
        Assert.IsTrue(
            report.Comparisons.Any(item => item.Category == category && item.Status == status),
            $"Expected {category} / {status}. Actual: {string.Join(", ", report.Comparisons.Select(item => $"{item.Category}/{item.Status}"))}");
    }
}
