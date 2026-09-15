using Microsoft.VisualStudio.TestTools.UnitTesting;
using Vira.Next.Contracts;
using Vira.Next.Engine;

namespace Vira.Next.Engine.Tests;

[TestClass]
public sealed class PacketDrawingResolutionPipelineTests
{
    [TestMethod]
    public async Task P01_SalesforceExactStarterPair_ResolvesExactMatch()
    {
        var result = await ResolveAsync(SalesforcePair("ASY511002", "80233885", "STARTER", "row-starter"), WalmartCatalog());

        Assert.AreEqual(DrawingResolutionStatus.ExactMatch, result.Status);
        Assert.AreEqual("ASY511002", result.ResolvedRecord!.PartNumber);
        Assert.AreEqual("80233885", result.ResolvedRecord.DocumentNumber);
        Assert.IsFalse(result.ExternalSystemsAccessed);
        Assert.AreEqual(0, result.MutationActions);
    }

    [TestMethod]
    public async Task P02_SalesforceExactAdderPair_ResolvesExactMatch()
    {
        var result = await ResolveAsync(SalesforcePair("ASY511003", "80233886", "ADDER", "row-adder"), WalmartCatalog());

        Assert.AreEqual(DrawingResolutionStatus.ExactMatch, result.Status);
        Assert.AreEqual("ASY511003", result.ResolvedRecord!.PartNumber);
        Assert.AreEqual("80233886", result.ResolvedRecord.DocumentNumber);
    }

    [TestMethod]
    public async Task P03_SalesforcePartOnlyUniqueCatalog_ReturnsPartialMatch()
    {
        var result = await ResolveAsync(EvidenceSet(Salesforce("part", "SALESFORCE.PART_NUMBER", "ASY511001", "row-single")), WalmartCatalog());

        Assert.AreEqual(DrawingResolutionStatus.PartialMatch, result.Status);
        Assert.AreEqual("ASY511001", result.ResolvedRecord!.PartNumber);
        Assert.IsTrue(result.Limitations.Any(item => item.Contains("Only one identifier", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task P04_BareNumericOnly_DoesNotBecomeDocumentNumber()
    {
        var result = await ResolveAsync(EvidenceSet(Salesforce("bare", "", "80233885", "row-bare")), WalmartCatalog());

        Assert.AreEqual(DrawingResolutionStatus.Unresolved, result.Status);
        Assert.IsNull(result.DocumentIdentifier);
        Assert.IsNull(result.ResolvedRecord);
    }

    [TestMethod]
    public async Task P05_DistinctTypedPartCandidates_FailClosedAsIdentityConflict()
    {
        var result = await ResolveAsync(EvidenceSet(
            Salesforce("part-a", "SALESFORCE.PART_NUMBER", "ASY511002", "row-conflict"),
            Salesforce("part-b", "TYPED.PART_NUMBER", "ASY511003", "row-conflict"),
            Salesforce("doc", "SALESFORCE.DOCUMENT_NUMBER", "80233885", "row-conflict")), WalmartCatalog());

        Assert.AreEqual(DrawingResolutionStatus.IdentityConflict, result.Status);
        Assert.IsNull(result.ResolvedRecord);
    }

    [TestMethod]
    public async Task P06_CrossRecordPartAndDocumentSplit_FailsClosedThroughResolver()
    {
        var result = await ResolveAsync(EvidenceSet(
            Salesforce("part", "SALESFORCE.PART_NUMBER", "ASY511002", "row-part"),
            Salesforce("doc", "SALESFORCE.DOCUMENT_NUMBER", "80233886", "row-doc")), WalmartCatalog());

        Assert.AreEqual(DrawingResolutionStatus.IdentityConflict, result.Status);
        Assert.IsNull(result.ResolvedRecord);
        Assert.IsTrue(result.Limitations.Any(item => item.Contains("different controlled records", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task P07_SourceRoleCanDifferFromControlledRole()
    {
        var result = await ResolveAsync(SalesforcePair("ASY511003", "80233886", "STARTER", "row-adder"), WalmartCatalog());

        Assert.AreEqual(DrawingResolutionStatus.ExactMatch, result.Status);
        Assert.AreEqual("STARTER", result.SourceRole);
        Assert.AreEqual("ADDER", result.EffectiveRole);
        Assert.AreEqual(DrawingRoleAuthority.ControlledEvidence, result.RoleAuthority);
    }

    [TestMethod]
    public async Task P08_ExplicitOverrideDisagreeingWithMappedTypedCandidate_FailsClosed()
    {
        var mapping = new PacketDrawingEvidenceMapper().Map(
            SalesforcePair("ASY511002", "80233885", "STARTER", "row-starter"));
        var request = mapping.Request with
        {
            PartNumber = "ASY511003"
        };
        var catalog = new SyntheticPdmDrawingCatalog(WalmartCatalog());
        var candidates = await catalog.FindCandidatesAsync(request);

        var result = new OfflineDrawingResolver().Resolve(request, candidates);

        Assert.AreEqual(DrawingResolutionStatus.IdentityConflict, result.Status);
        Assert.IsNull(result.ResolvedRecord);
        Assert.IsTrue(result.Limitations.Any(item => item.Contains("Explicit PartNumber", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task P09_PdfTitleBlockPair_ResolvesExactMatchWithPdfProvenance()
    {
        var mapping = new PacketDrawingEvidenceMapper().Map(EvidenceSet(new[]
        {
            Pdf("pdf-part", "TITLE_BLOCK.PART_NO", "ASY511003", 3, "x=10,y=20,w=30,h=40", "pdf-row-3"),
            Pdf("pdf-doc", "TITLE_BLOCK.DOC_NO", "80233886", 3, "x=50,y=20,w=30,h=40", "pdf-row-3")
        }, "STARTER"));
        var catalog = new SyntheticPdmDrawingCatalog(new[]
        {
            Record("pdf-record", "ASY511003", "80233886", "STARTER")
        });
        var candidates = await catalog.FindCandidatesAsync(mapping.Request);
        var result = new OfflineDrawingResolver().Resolve(mapping.Request, candidates);

        Assert.AreEqual(DrawingResolutionStatus.ExactMatch, result.Status);
        Assert.AreEqual("pdf-record", result.ResolvedRecord!.RecordId);
        Assert.AreEqual("STARTER", result.SourceRole);
        Assert.AreEqual("STARTER", result.EffectiveRole);
        Assert.AreEqual(DrawingRoleAuthority.SourceDerived, mapping.Request.RoleAuthority);
        Assert.AreEqual(DrawingRoleAuthority.ControlledEvidence, result.RoleAuthority);

        var part = mapping.Request.Identifiers.Single(item => item.Kind == DrawingIdentifierKind.PartNumber);
        Assert.AreEqual(PacketDrawingSourceType.PdfPacket, part.SourceType);
        Assert.AreEqual(3, part.SourcePageNumber);
        Assert.AreEqual("x=10,y=20,w=30,h=40", part.SourceRegion);
        Assert.AreEqual("NATIVE_TEXT", part.ExtractionMethod);
        Assert.AreEqual("pdf-packet-1", part.SourceArtifactId);
        Assert.AreEqual("sha-pdf", part.SourceArtifactSha256);
        Assert.AreEqual("pdf-row-3", part.SourceRecordId);
        Assert.AreEqual("Observed", part.EvidenceStatus);
        Assert.AreEqual(0.98, part.Confidence);
        Assert.AreEqual(PacketDrawingSourceType.PdfPacket, result.PartIdentifier!.SourceType);
        Assert.AreEqual("pdf-packet-1", result.PartIdentifier.SourceArtifactId);
        Assert.AreEqual("sha-pdf", result.PartIdentifier.SourceArtifactSha256);
        Assert.AreEqual("pdf-row-3", result.PartIdentifier.SourceRecordId);
        Assert.AreEqual(3, result.PartIdentifier.SourcePageNumber);
        Assert.AreEqual("x=10,y=20,w=30,h=40", result.PartIdentifier.SourceRegion);
        Assert.AreEqual("NATIVE_TEXT", result.PartIdentifier.ExtractionMethod);
        Assert.AreEqual("Observed", result.PartIdentifier.EvidenceStatus);
        Assert.AreEqual(0.98, result.PartIdentifier.Confidence);
        Assert.IsFalse(result.ExternalSystemsAccessed);
        Assert.AreEqual(0, result.MutationActions);
    }

    [TestMethod]
    public async Task P10_PdfPartAndDocumentMismatch_FailsClosed()
    {
        var mapping = new PacketDrawingEvidenceMapper().Map(EvidenceSet(new[]
        {
            Pdf("pdf-part", "TITLE_BLOCK.PART_NO", "ASY511003", 3, "part-region", "pdf-row-3"),
            Pdf("pdf-doc", "TITLE_BLOCK.DOC_NO", "80233885", 3, "doc-region", "pdf-row-3")
        }, "STARTER"));
        var catalog = new SyntheticPdmDrawingCatalog(new[]
        {
            Record("pdf-adder", "ASY511003", "80233886", "ADDER"),
            Record("pdf-starter", "ASY511002", "80233885", "STARTER")
        });
        var candidates = await catalog.FindCandidatesAsync(mapping.Request);
        var result = new OfflineDrawingResolver().Resolve(mapping.Request, candidates);

        Assert.AreEqual(DrawingResolutionStatus.IdentityConflict, result.Status);
        Assert.IsNull(result.ResolvedRecord);
        Assert.IsTrue(result.Limitations.Any(item => item.Contains("different controlled records", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task P11_ExplicitDocumentOverrideDisagreeingWithMappedTypedCandidate_FailsClosed()
    {
        var mapping = new PacketDrawingEvidenceMapper().Map(
            SalesforcePair("ASY511002", "80233885", "STARTER", "row-starter"));
        var request = mapping.Request with
        {
            DocumentNumber = "80233886"
        };
        var catalog = new SyntheticPdmDrawingCatalog(WalmartCatalog());
        var candidates = await catalog.FindCandidatesAsync(request);

        var result = new OfflineDrawingResolver().Resolve(request, candidates);

        Assert.AreEqual(DrawingResolutionStatus.IdentityConflict, result.Status);
        Assert.IsNull(result.ResolvedRecord);
        Assert.IsTrue(result.Limitations.Any(item => item.Contains("Explicit DocumentNumber", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task P12_DuplicateControlledIdentityRowsWithUniqueRecordIds_ReturnAmbiguous()
    {
        var result = await ResolveAsync(
            SalesforcePair("ASY511002", "80233885", "STARTER", "row-starter"),
            new[]
            {
                Record("controlled-1", "ASY511002", "80233885", "STARTER"),
                Record("controlled-2", "ASY511002", "80233885", "STARTER")
            });

        Assert.AreEqual(DrawingResolutionStatus.Ambiguous, result.Status);
        Assert.IsNull(result.ResolvedRecord);
        Assert.AreEqual(2, result.Candidates.Count);
        Assert.IsFalse(result.ExternalSystemsAccessed);
        Assert.AreEqual(0, result.MutationActions);
    }

    private static async Task<DrawingResolutionResult> ResolveAsync(
        PacketDrawingEvidenceSet evidenceSet,
        IReadOnlyList<ControlledDrawingRecord> controlledRecords)
    {
        var mapping = new PacketDrawingEvidenceMapper().Map(evidenceSet);
        var catalog = new SyntheticPdmDrawingCatalog(controlledRecords);
        var candidates = await catalog.FindCandidatesAsync(mapping.Request);
        return new OfflineDrawingResolver().Resolve(mapping.Request, candidates);
    }

    private static PacketDrawingEvidenceSet SalesforcePair(string part, string document, string role, string recordId) =>
        EvidenceSet(new[]
        {
            Salesforce("part", "SALESFORCE.PART_NUMBER", part, recordId),
            Salesforce("doc", "SALESFORCE.DOCUMENT_NUMBER", document, recordId)
        },
            role);

    private static PacketDrawingEvidenceSet EvidenceSet(params PacketDrawingIdentifierEvidence[] evidence) =>
        EvidenceSet(evidence, "STARTER");

    private static PacketDrawingEvidenceSet EvidenceSet(
        IReadOnlyList<PacketDrawingIdentifierEvidence> evidence,
        string role) => new()
    {
        OpportunityId = "42576",
        TaskId = "TE-3057",
        Customer = "Walmart",
        Project = "DELI BAKERY RACK",
        SourceRole = role,
        RoleAuthority = DrawingRoleAuthority.SourceDerived,
        Identifiers = evidence
    };

    private static PacketDrawingIdentifierEvidence Salesforce(string id, string field, string value, string recordId) => new()
    {
        EvidenceId = id,
        SourceType = PacketDrawingSourceType.SalesforceScreenshot,
        SourceArtifactId = "sf-shot-1",
        SourceArtifactSha256 = "sha-salesforce",
        SourceRecordId = recordId,
        ExtractionMethod = "SCREENSHOT_OCR",
        SourceField = field,
        RawValue = value,
        Status = PacketDrawingEvidenceStatus.Candidate,
        Confidence = 0.9
    };

    private static PacketDrawingIdentifierEvidence Pdf(
        string id,
        string field,
        string value,
        int page,
        string region,
        string recordId) => new()
    {
        EvidenceId = id,
        SourceType = PacketDrawingSourceType.PdfPacket,
        SourceArtifactId = "pdf-packet-1",
        SourceArtifactSha256 = "sha-pdf",
        SourceRecordId = recordId,
        PageNumber = page,
        Region = region,
        ExtractionMethod = "NATIVE_TEXT",
        SourceField = field,
        RawValue = value,
        Status = PacketDrawingEvidenceStatus.Observed,
        Confidence = 0.98
    };

    private static IReadOnlyList<ControlledDrawingRecord> WalmartCatalog() => new[]
    {
        Record("1500265", "ASY511002", "80233885", "STARTER"),
        Record("1500268", "ASY511003", "80233886", "ADDER"),
        Record("1500252", "ASY511001", "80233881", "STARTER")
    };

    private static ControlledDrawingRecord Record(string id, string part, string document, string role) => new()
    {
        RecordId = id,
        PartNumber = part,
        DocumentNumber = document,
        ExactPath = $"C:\\_PDMVault\\Engineering Data Base\\{document}.SLDDRW",
        FileName = $"{document}.SLDDRW",
        IsLocallyAvailable = false,
        RetrievalRequired = true,
        ReadOnlyPolicy = DrawingReadOnlyPolicy.ReadOnlyRequired,
        Role = role,
        RoleAuthority = DrawingRoleAuthority.ControlledEvidence
    };
}
