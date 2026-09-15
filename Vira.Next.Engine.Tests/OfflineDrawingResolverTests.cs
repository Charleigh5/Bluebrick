using Microsoft.VisualStudio.TestTools.UnitTesting;
using Vira.Next.Contracts;
using Vira.Next.Engine;

namespace Vira.Next.Engine.Tests;

[TestClass]
public sealed class OfflineDrawingResolverTests
{
    [TestMethod]
    public void Resolve_ExactWalmartPair_PreservesContextRawValuesAndControlledPath()
    {
        var request = Request(
            part: " asy511002 ",
            document: "80233885",
            identifiers: new[]
            {
                Identifier(DrawingIdentifierKind.PartNumber, " asy511002 ", sourceField: "title-block:PART NO"),
                Identifier(DrawingIdentifierKind.DocumentNumber, "80233885", sourceLabel: "DOCUMENT NUMBER", sourceType: PacketDrawingSourceType.SalesforceScreenshot)
            });

        var result = Resolve(request, Record("r-1", "ASY511002", "80233885"));

        Assert.AreEqual(DrawingResolutionStatus.ExactMatch, result.Status);
        Assert.AreEqual("OP-7", result.OpportunityId);
        Assert.AreEqual("TASK-9", result.TaskId);
        Assert.AreEqual("Walmart", result.Customer);
        Assert.AreEqual("Project-42", result.Project);
        Assert.AreEqual(" asy511002 ", result.PartIdentifier!.RawValue);
        Assert.AreEqual("ASY511002", result.PartIdentifier.NormalizedValue);
        Assert.AreEqual("title-block:PART NO", result.PartIdentifier.SourceField);
        Assert.AreEqual("80233885", result.DocumentIdentifier!.RawValue);
        Assert.AreEqual("DOCUMENT NUMBER", result.DocumentIdentifier.SourceLabel);
        Assert.AreEqual(PacketDrawingSourceType.SalesforceScreenshot, result.DocumentIdentifier.SourceType);
        Assert.AreEqual("C:\\controlled\\ASY511002-80233885.PDF", result.ResolvedRecord!.ExactPath);
        Assert.AreEqual("ASY511002-80233885.PDF", result.ResolvedRecord.FileName);
        Assert.IsFalse(result.ResolvedRecord.IsLocallyAvailable);
        Assert.IsTrue(result.ResolvedRecord.RetrievalRequired);
        Assert.AreEqual(DrawingReadOnlyPolicy.ReadOnlyRequired, result.ReadOnlyPolicy);
        Assert.IsFalse(result.ExternalSystemsAccessed);
        Assert.AreEqual(0, result.MutationActions);
    }

    [TestMethod]
    public void Resolve_MissingDocument_ReturnsPartialAndRetainsSourceRole()
    {
        var request = Request(part: "ASY511002", role: "starter", identifiers: new[] { Identifier(DrawingIdentifierKind.PartNumber, "ASY511002") });
        var result = Resolve(request, Record("r-1", "ASY511002", "80233885"));

        Assert.AreEqual(DrawingResolutionStatus.PartialMatch, result.Status);
        Assert.IsNull(result.DocumentIdentifier);
        Assert.AreEqual("starter", result.SourceRole);
        Assert.AreEqual(DrawingRoleAuthority.SourceDerived, result.RoleAuthority);
    }

    [TestMethod]
    public void Resolve_ControlledRoleEvidence_ReturnsControlledRoleAuthorityAndPreservesRole()
    {
        var request = Request(part: "ASY511002", identifiers: new[] { Identifier(DrawingIdentifierKind.PartNumber, "ASY511002") });
        var result = Resolve(request, Record(
            "r-1",
            "ASY511002",
            "80233885",
            role: "adder",
            roleAuthority: DrawingRoleAuthority.ControlledEvidence));

        Assert.AreEqual(DrawingResolutionStatus.PartialMatch, result.Status);
        Assert.AreEqual("adder", result.ResolvedRecord!.Role);
        Assert.AreEqual(DrawingRoleAuthority.ControlledEvidence, result.ResolvedRecord.RoleAuthority);
        Assert.AreEqual("reference", result.SourceRole);
        Assert.AreEqual("adder", result.EffectiveRole);
        Assert.AreEqual(DrawingRoleAuthority.ControlledEvidence, result.RoleAuthority);
    }

    [TestMethod]
    public void Resolve_MissingPart_ReturnsPartial()
    {
        var result = Resolve(Request(document: "80233885", identifiers: new[] { Identifier(DrawingIdentifierKind.DocumentNumber, "80233885") }), Record("r-1", "ASY511002", "80233885"));

        Assert.AreEqual(DrawingResolutionStatus.PartialMatch, result.Status);
        Assert.IsNull(result.PartIdentifier);
        Assert.AreEqual("80233885", result.DocumentIdentifier!.RawValue);
        Assert.AreEqual("C:\\controlled\\ASY511002-80233885.PDF", result.ResolvedRecord!.ExactPath);
        Assert.AreEqual("ASY511002-80233885.PDF", result.ResolvedRecord.FileName);
        Assert.IsFalse(result.ResolvedRecord.IsLocallyAvailable);
        Assert.IsTrue(result.ResolvedRecord.RetrievalRequired);
        Assert.AreEqual(DrawingReadOnlyPolicy.ReadOnlyRequired, result.ResolvedRecord.ReadOnlyPolicy);
        Assert.AreEqual(DrawingReadOnlyPolicy.ReadOnlyRequired, result.ReadOnlyPolicy);
    }

    [TestMethod]
    public void Resolve_ConflictingPartAndDocument_ReturnsIdentityConflict()
    {
        var result = Resolve(Request(
            part: "ASY511002",
            document: "80233885",
            identifiers: new[] { Identifier(DrawingIdentifierKind.PartNumber, "ASY511002"), Identifier(DrawingIdentifierKind.DocumentNumber, "80233885") }),
            Record("part-record", "ASY511002", "80230000"),
            Record("document-record", "ASY511003", "80233885"));

        Assert.AreEqual(DrawingResolutionStatus.IdentityConflict, result.Status);
        Assert.IsNull(result.ResolvedRecord);
    }

    [TestMethod]
    public void Resolve_BlankRecordIdsOnContradictoryRows_FailsClosed()
    {
        var result = Resolve(Request(part: "ASY511002", document: "80233885"),
            Record("", "ASY511002", "80230000"),
            Record("", "ASY511003", "80233885"));

        Assert.AreEqual(DrawingResolutionStatus.IdentityConflict, result.Status);
        Assert.IsNull(result.ResolvedRecord);
    }

    [TestMethod]
    public void Resolve_DuplicateRecordIdsOnContradictoryRows_FailsClosed()
    {
        var result = Resolve(Request(part: "ASY511002", document: "80233885"),
            Record("duplicate", "ASY511002", "80230000"),
            Record("duplicate", "ASY511003", "80233885"));

        Assert.AreEqual(DrawingResolutionStatus.IdentityConflict, result.Status);
        Assert.IsNull(result.ResolvedRecord);
    }

    [TestMethod]
    public void Resolve_ExplicitAndTypedPartDisagree_ReturnsIdentityConflict()
    {
        var result = Resolve(Request(
            part: "ASY511002",
            document: "80233885",
            identifiers: new[]
            {
                Identifier(DrawingIdentifierKind.PartNumber, "ASY511003", "typed-part"),
                Identifier(DrawingIdentifierKind.DocumentNumber, "80233885", "typed-doc")
            }), Record("r-1", "ASY511003", "80233885"));

        Assert.AreEqual(DrawingResolutionStatus.IdentityConflict, result.Status);
        Assert.IsTrue(result.Limitations.Any(item => item.Contains("Explicit PartNumber", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Resolve_MultipleDistinctSameKindCandidates_ReturnsIdentityConflict()
    {
        var result = Resolve(Request(identifiers: new[]
        {
            Identifier(DrawingIdentifierKind.PartNumber, "ASY511002"),
            Identifier(DrawingIdentifierKind.PartNumber, "ASY511003")
        }), Record("r-1", "ASY511002", "80233885"));

        Assert.AreEqual(DrawingResolutionStatus.IdentityConflict, result.Status);
        Assert.IsTrue(result.Limitations.Any(item => item.Contains("Multiple distinct PartNumber", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Resolve_DuplicateControlledRecords_ReturnsAmbiguous()
    {
        var result = Resolve(Request(part: "ASY511002", document: "80233885"),
            Record("r-1", "ASY511002", "80233885"),
            Record("r-2", "ASY511002", "80233885"));

        Assert.AreEqual(DrawingResolutionStatus.Ambiguous, result.Status);
        Assert.AreEqual(2, result.Candidates.Count);
    }

    [TestMethod]
    public void Resolve_DuplicateInputIdentifiers_SuppressesDuplicates()
    {
        var result = Resolve(Request(
            identifiers: new[]
            {
                Identifier(DrawingIdentifierKind.PartNumber, "ASY511002", "first"),
                Identifier(DrawingIdentifierKind.PartNumber, " asy511002 ", "duplicate"),
                Identifier(DrawingIdentifierKind.DocumentNumber, "80233885"),
                Identifier(DrawingIdentifierKind.DocumentNumber, "80233885", "duplicate-doc")
            }), Record("r-1", "ASY511002", "80233885"));

        Assert.AreEqual(DrawingResolutionStatus.ExactMatch, result.Status);
        Assert.AreEqual("first", result.PartIdentifier!.EvidenceId);
    }

    [TestMethod]
    public void Resolve_UnrelatedEightDigitToken_IsNotDocumentNumber()
    {
        var request = Request(identifiers: new[] { Identifier(DrawingIdentifierKind.Unclassified, "12345678") });
        var result = Resolve(request, Record("r-1", "ASY511002", "12345678"));

        Assert.AreEqual(DrawingResolutionStatus.Unresolved, result.Status);
        Assert.IsNull(result.DocumentIdentifier);
        Assert.IsNull(result.ResolvedRecord);
        Assert.IsFalse(result.ExternalSystemsAccessed);
    }

    private static DrawingResolutionResult Resolve(DrawingResolutionRequest request, params ControlledDrawingRecord[] records) =>
        new OfflineDrawingResolver().Resolve(request, records);

    private static DrawingResolutionRequest Request(
        string part = "",
        string document = "",
        string role = "reference",
        IReadOnlyList<DrawingIdentifierCandidate>? identifiers = null) => new()
    {
        OpportunityId = "OP-7",
        TaskId = "TASK-9",
        Customer = "Walmart",
        Project = "Project-42",
        SourceRole = role,
        PartNumber = part,
        DocumentNumber = document,
        Identifiers = identifiers ?? Array.Empty<DrawingIdentifierCandidate>()
    };

    private static DrawingIdentifierCandidate Identifier(
        DrawingIdentifierKind kind,
        string value,
        string evidence = "",
        string sourceField = "",
        string sourceLabel = "",
        PacketDrawingSourceType? sourceType = null) => new()
    {
        Kind = kind,
        RawValue = value,
        EvidenceId = evidence,
        Authority = DrawingEvidenceAuthority.SourceDerived,
        SourceField = string.IsNullOrWhiteSpace(sourceField) ? kind.ToString() : sourceField,
        SourceLabel = sourceLabel,
        SourceType = sourceType
    };

    private static ControlledDrawingRecord Record(
        string id,
        string part,
        string document,
        string role = "",
        DrawingRoleAuthority roleAuthority = DrawingRoleAuthority.ControlledEvidence) => new()
    {
        RecordId = id,
        PartNumber = part,
        DocumentNumber = document,
        ExactPath = $"C:\\controlled\\{part}-{document}.PDF",
        FileName = $"{part}-{document}.PDF",
        IsLocallyAvailable = false,
        RetrievalRequired = true,
        Role = role,
        RoleAuthority = roleAuthority
    };
}
