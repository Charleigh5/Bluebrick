using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Vira.Next.Contracts;
using Vira.Next.Engine;

namespace Vira.Next.Engine.Tests;

[TestClass]
public sealed class PacketCadReceiptServiceTests
{
    [DataTestMethod]
    [DataRow(CadSnapshotState.Ready, ReceiptResult.Success, false)]
    [DataRow(CadSnapshotState.Blocked, ReceiptResult.Denied, true)]
    [DataRow(CadSnapshotState.Error, ReceiptResult.Denied, false)]
    [DataRow(CadSnapshotState.Unsupported, ReceiptResult.Denied, false)]
    [DataRow(CadSnapshotState.NoActiveDocument, ReceiptResult.Denied, true)]
    public void Create_EverySnapshotState_PreservesMessageAndTruthfulResult(CadSnapshotState state, ReceiptResult expected, bool safeToRetry)
    {
        var message = $"Snapshot state: {state}.";
        var receipt = new PacketCadReceiptService().Create(new PacketEvidenceSnapshot(),
            new CadDocumentSnapshot { State = state, Message = message },
            Array.Empty<PacketCadComparison>(), new PacketCadReceiptContext());

        Assert.AreEqual(expected, receipt.Result);
        Assert.AreEqual(safeToRetry, receipt.SafeToRetry);
        CollectionAssert.Contains(receipt.Limitations.ToArray(), message);
    }

    [DataTestMethod]
    [DataRow(CadSnapshotState.Ready, 1, false)]
    [DataRow(CadSnapshotState.Ready, 0, true)]
    [DataRow(CadSnapshotState.Blocked, 1, false)]
    [DataRow(CadSnapshotState.NoActiveDocument, 0, true)]
    public void Create_SafetyViolation_FailsAndCannotAdvertiseSafeRetry(CadSnapshotState state, int mutations, bool externalAccess)
    {
        var receipt = new PacketCadReceiptService().Create(new PacketEvidenceSnapshot(),
            new CadDocumentSnapshot { State = state, MutationActions = mutations, ExternalSystemsAccessed = externalAccess },
            Array.Empty<PacketCadComparison>(), new PacketCadReceiptContext());

        Assert.AreEqual(ReceiptResult.Failed, receipt.Result);
        Assert.IsFalse(receipt.SafeToRetry);
    }

    [TestMethod]
    public void Create_RecordsDeterministicCountsAndAccessFlags_WithoutRawEngineeringValues()
    {
        var packet = new PacketEvidenceSnapshot
        {
            SnapshotId = "packet-snapshot-1",
            PacketId = "packet-redacted-1",
            Identifiers = new[]
            {
                new PacketIdentifierEvidence
                {
                    Identifier = "ASY511185-80238229",
                    Evidence = new EvidenceRef { EvidenceId = "packet-id-1", RawValue = "ASY511185-80238229" }
                }
            }
        };
        var cad = new CadDocumentSnapshot
        {
            SnapshotId = "cad-snapshot-1",
            Source = CadSnapshotSource.LocalFixture,
            State = CadSnapshotState.Ready,
            Properties = new[]
            {
                new CadPropertySnapshot
                {
                    Name = "Revision",
                    RawValue = "SECRET-RAW-REVISION",
                    EvaluatedValue = "B"
                }
            },
            MutationActions = 0,
            ExternalSystemsAccessed = false
        };
        var comparisons = new[]
        {
            new PacketCadComparison
            {
                ComparisonId = "CMP-REVISION",
                Category = "property:document:Revision",
                Status = PacketCadComparisonStatus.Conflict,
                NormalizationRuleId = "VIRA-TEXT-NORMALIZE-001",
                MutationBoundary = EngineeringMutationBoundary.ReadOnly
            },
            new PacketCadComparison
            {
                ComparisonId = "CMP-DESCRIPTION",
                Category = "property:document:Description",
                Status = PacketCadComparisonStatus.NormalizedMatch,
                NormalizationRuleId = "VIRA-TEXT-NORMALIZE-001",
                MutationBoundary = EngineeringMutationBoundary.ReadOnly
            }
        };

        var receipt = new PacketCadReceiptService().Create(
            packet,
            cad,
            comparisons,
            new PacketCadReceiptContext
            {
                ReceiptId = "PC-A-RCPT-001",
                SessionId = "phase-a-static",
                CorrelationId = "correlation-001",
                TimestampUtc = new DateTime(2026, 7, 14, 16, 0, 0, DateTimeKind.Utc),
                PromotionState = PacketCadPromotionState.PartialVerified
            });

        var json = JsonSerializer.Serialize(receipt);

        Assert.AreEqual("vira.packet-cad.receipt.v1", receipt.SchemaVersion);
        Assert.AreEqual(1, receipt.ComparisonCounts[nameof(PacketCadComparisonStatus.Conflict)]);
        Assert.AreEqual(1, receipt.ComparisonCounts[nameof(PacketCadComparisonStatus.NormalizedMatch)]);
        CollectionAssert.AreEqual(new[] { "VIRA-TEXT-NORMALIZE-001" }, receipt.RuleVersions.ToArray());
        Assert.IsFalse(receipt.CadAccessed, "Local fixtures are not live CAD access.");
        Assert.IsFalse(receipt.ExternalSystemsAccessed);
        Assert.AreEqual(0, receipt.MutationActions);
        Assert.AreEqual(ReceiptResult.Success, receipt.Result);
        Assert.IsFalse(json.Contains("ASY511185-80238229", StringComparison.Ordinal));
        Assert.IsFalse(json.Contains("SECRET-RAW-REVISION", StringComparison.Ordinal));
        Assert.IsTrue(json.Contains("packet-snapshot-1", StringComparison.Ordinal));
        Assert.IsTrue(json.Contains("cad-snapshot-1", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Create_ReadOnlyHostSnapshot_RecordsCadAccessWithoutMutationOrExternalAccess()
    {
        var receipt = new PacketCadReceiptService().Create(
            new PacketEvidenceSnapshot { SnapshotId = "packet-1" },
            new CadDocumentSnapshot
            {
                SnapshotId = "cad-1",
                Source = CadSnapshotSource.ReadOnlyHost,
                State = CadSnapshotState.Ready,
                MutationActions = 0,
                ExternalSystemsAccessed = false
            },
            Array.Empty<PacketCadComparison>(),
            new PacketCadReceiptContext
            {
                ReceiptId = "PC-A-RCPT-002",
                CorrelationId = "correlation-002",
                TimestampUtc = new DateTime(2026, 7, 14, 16, 1, 0, DateTimeKind.Utc)
            });

        Assert.IsTrue(receipt.CadAccessed);
        Assert.AreEqual(0, receipt.MutationActions);
        Assert.IsFalse(receipt.ExternalSystemsAccessed);
        Assert.AreEqual(EngineeringMutationBoundary.ReadOnly, receipt.MutationBoundary);
    }
}
