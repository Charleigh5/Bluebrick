using Microsoft.VisualStudio.TestTools.UnitTesting;
using Vira.Next.Contracts;
using Vira.Next.Engine;

namespace Vira.Next.Engine.Tests;

[TestClass]
public sealed class ProductionPdmResolutionPipelineTests
{
    [TestMethod]
    public async Task P06_TruncatedCatalog_CannotResolveFalseExactMatch()
    {
        var mapping = new PacketDrawingEvidenceMapper().Map(SalesforcePair("ASY511002", "80233885", "STARTER", "row-starter"));
        var response = Response(
            Candidate("a", "ASY511002", "80233885", "80233885.SLDDRW", true, "STARTER"),
            Candidate("b", "ASY511002", "80233885", "80233885.SLDDRW", true, "STARTER"));
        var gateway = new FakeReadOnlyPdmSearchGateway();
        gateway.EnqueueResponse(response);
        gateway.EnqueueResponse(response);
        var catalog = new ProductionPdmDrawingCatalog(gateway, new ProductionPdmDrawingCatalogOptions { MaximumCandidateCount = 1 });

        var candidates = await catalog.FindCandidatesAsync(mapping.Request);
        var result = new OfflineDrawingResolver().Resolve(mapping.Request, candidates);

        Assert.AreEqual(DrawingResolutionStatus.Unresolved, result.Status);
        Assert.IsNull(result.ResolvedRecord);
        Assert.AreEqual(2, catalog.LastReceipt.AttemptedQueries.Count);
        Assert.IsTrue(catalog.LastReceipt.Limitations.Any(item => item.Contains("uniqueness", StringComparison.OrdinalIgnoreCase)));
        Assert.IsFalse(result.ExternalSystemsAccessed);
        Assert.AreEqual(0, result.MutationActions);
    }

    [TestMethod]
    public async Task P01_WalmartStarterFixture_MapsThroughProductionCatalogAndResolvesExactMatch()
    {
        var run = await ResolveAsync(
            SalesforcePair("ASY511002", "80233885", "STARTER", "row-starter"),
            Response(Candidate("backend-1500265", "ASY511002", "80233885", "80233885.SLDDRW", false, "STARTER")),
            Response(Candidate("backend-1500265", "ASY511002", "80233885", "80233885.SLDDRW", false, "STARTER")));

        Assert.AreEqual(2, run.Gateway.RecordedQueries.Count);
        Assert.AreEqual(PdmSearchField.PartNumber, run.Gateway.RecordedQueries[0].Field);
        Assert.AreEqual("ASY511002", run.Gateway.RecordedQueries[0].NormalizedValue);
        Assert.AreEqual(PdmSearchField.DocumentNumber, run.Gateway.RecordedQueries[1].Field);
        Assert.AreEqual("80233885", run.Gateway.RecordedQueries[1].NormalizedValue);
        Assert.IsFalse(run.Mapping.ExternalSystemsAccessed);
        Assert.AreEqual(0, run.Mapping.MutationActions);
        Assert.AreEqual(DrawingResolutionStatus.ExactMatch, run.Result.Status);
        Assert.AreEqual("backend-1500265", run.Result.ResolvedRecord!.RecordId);
        Assert.AreEqual("ASY511002", run.Result.ResolvedRecord.PartNumber);
        Assert.AreEqual("80233885", run.Result.ResolvedRecord.DocumentNumber);
        Assert.IsFalse(run.Result.ExternalSystemsAccessed);
        Assert.AreEqual(0, run.Result.MutationActions);
    }

    [TestMethod]
    public async Task P02_CrossRecordSplit_ResolvesIdentityConflict()
    {
        var run = await ResolveAsync(
            SalesforcePair("ASY511002", "80233885", "STARTER", "row-starter"),
            Response(Candidate("part-record", "ASY511002", "80230000", "80230000.SLDDRW", false, "STARTER")),
            Response(Candidate("document-record", "ASY511003", "80233885", "80233885.SLDDRW", false, "ADDER")));

        Assert.AreEqual(DrawingResolutionStatus.IdentityConflict, run.Result.Status);
        Assert.IsNull(run.Result.ResolvedRecord);
        Assert.IsTrue(run.Result.Limitations.Any(item => item.Contains("different controlled records", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task P03_DuplicateControlledIdentityRowsWithUniqueBackendIds_RemainAmbiguous()
    {
        var duplicateRows = Response(
            Candidate("controlled-1", "ASY511002", "80233885", "80233885.SLDDRW", false, "STARTER"),
            Candidate("controlled-2", "ASY511002", "80233885", "80233885.SLDDRW", false, "STARTER"));
        var run = await ResolveAsync(
            SalesforcePair("ASY511002", "80233885", "STARTER", "row-starter"),
            duplicateRows,
            duplicateRows);

        Assert.AreEqual(2, run.Gateway.RecordedQueries.Count);
        Assert.AreEqual(PdmSearchField.PartNumber, run.Gateway.RecordedQueries[0].Field);
        Assert.AreEqual("ASY511002", run.Gateway.RecordedQueries[0].NormalizedValue);
        Assert.AreEqual(PdmSearchField.DocumentNumber, run.Gateway.RecordedQueries[1].Field);
        Assert.AreEqual("80233885", run.Gateway.RecordedQueries[1].NormalizedValue);
        Assert.AreEqual(2, run.Catalog.LastReceipt.PlannedQueries.Count);
        Assert.AreEqual(2, run.Catalog.LastReceipt.AttemptedQueries.Count);
        Assert.AreEqual(DrawingResolutionStatus.Ambiguous, run.Result.Status);
        Assert.IsNull(run.Result.ResolvedRecord);
        Assert.AreEqual(2, run.Result.Candidates.Count);
        Assert.IsFalse(run.Result.ExternalSystemsAccessed);
        Assert.AreEqual(0, run.Result.MutationActions);
    }

    [TestMethod]
    public async Task P04_OneSupportedIdentifier_RemainsPartialMatch()
    {
        var run = await ResolveAsync(
            EvidenceSet(Salesforce("part", "SALESFORCE.PART_NUMBER", "ASY511001", "row-single")),
            Response(Candidate("backend-1500252", "ASY511001", "80233881", "80233881.SLDDRW", false, "STARTER")));

        Assert.AreEqual(1, run.Gateway.RecordedQueries.Count);
        Assert.AreEqual(PdmSearchField.PartNumber, run.Gateway.RecordedQueries[0].Field);
        Assert.AreEqual(DrawingResolutionStatus.PartialMatch, run.Result.Status);
        Assert.AreEqual("ASY511001", run.Result.ResolvedRecord!.PartNumber);
        Assert.IsTrue(run.Result.Limitations.Any(item => item.Contains("Only one identifier", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task P05_PdfProvenance_SurvivesMapperCatalogAndResolver_Unchanged()
    {
        var run = await ResolveAsync(
            EvidenceSet(new[]
            {
                Pdf("pdf-part", "TITLE_BLOCK.PART_NO", "ASY511003", 3, "x=10,y=20,w=30,h=40", "pdf-row-3"),
                Pdf("pdf-doc", "TITLE_BLOCK.DOC_NO", "80233886", 3, "x=50,y=20,w=30,h=40", "pdf-row-3")
            }, "STARTER"),
            Response(Candidate("pdf-record", "ASY511003", "80233886", "80233886.SLDDRW", false, "STARTER")),
            Response(Candidate("pdf-record", "ASY511003", "80233886", "80233886.SLDDRW", false, "STARTER")));

        Assert.AreEqual(2, run.Gateway.RecordedQueries.Count);
        Assert.AreEqual(PdmSearchField.PartNumber, run.Gateway.RecordedQueries[0].Field);
        Assert.AreEqual("ASY511003", run.Gateway.RecordedQueries[0].NormalizedValue);
        Assert.AreEqual(PdmSearchField.DocumentNumber, run.Gateway.RecordedQueries[1].Field);
        Assert.AreEqual("80233886", run.Gateway.RecordedQueries[1].NormalizedValue);
        Assert.AreEqual(2, run.Catalog.LastReceipt.PlannedQueries.Count);
        Assert.AreEqual(2, run.Catalog.LastReceipt.AttemptedQueries.Count);
        Assert.AreEqual(DrawingResolutionStatus.ExactMatch, run.Result.Status);
        Assert.AreEqual(PacketDrawingSourceType.PdfPacket, run.Result.PartIdentifier!.SourceType);
        Assert.AreEqual("pdf-packet-1", run.Result.PartIdentifier.SourceArtifactId);
        Assert.AreEqual("sha-pdf", run.Result.PartIdentifier.SourceArtifactSha256);
        Assert.AreEqual("pdf-row-3", run.Result.PartIdentifier.SourceRecordId);
        Assert.AreEqual(3, run.Result.PartIdentifier.SourcePageNumber);
        Assert.AreEqual("x=10,y=20,w=30,h=40", run.Result.PartIdentifier.SourceRegion);
        Assert.AreEqual("NATIVE_TEXT", run.Result.PartIdentifier.ExtractionMethod);
        Assert.AreEqual("Observed", run.Result.PartIdentifier.EvidenceStatus);
        Assert.AreEqual(0.98, run.Result.PartIdentifier.Confidence);
        Assert.AreEqual(PacketDrawingSourceType.PdfPacket, run.Result.DocumentIdentifier!.SourceType);
        Assert.AreEqual("pdf-packet-1", run.Result.DocumentIdentifier.SourceArtifactId);
        Assert.AreEqual("sha-pdf", run.Result.DocumentIdentifier.SourceArtifactSha256);
        Assert.AreEqual("pdf-row-3", run.Result.DocumentIdentifier.SourceRecordId);
        Assert.AreEqual(3, run.Result.DocumentIdentifier.SourcePageNumber);
        Assert.AreEqual("x=50,y=20,w=30,h=40", run.Result.DocumentIdentifier.SourceRegion);
        Assert.AreEqual("NATIVE_TEXT", run.Result.DocumentIdentifier.ExtractionMethod);
        Assert.AreEqual("Observed", run.Result.DocumentIdentifier.EvidenceStatus);
        Assert.AreEqual(0.98, run.Result.DocumentIdentifier.Confidence);
        Assert.IsFalse(run.Result.ExternalSystemsAccessed);
        Assert.AreEqual(0, run.Result.MutationActions);
    }

    private static async Task<ProductionPipelineRun> ResolveAsync(
        PacketDrawingEvidenceSet evidenceSet,
        params PdmSearchResponse[] responses)
    {
        var mapping = new PacketDrawingEvidenceMapper().Map(evidenceSet);
        var gateway = new FakeReadOnlyPdmSearchGateway();
        foreach (var response in responses)
        {
            gateway.EnqueueResponse(response);
        }

        var catalog = new ProductionPdmDrawingCatalog(gateway);
        var candidates = await catalog.FindCandidatesAsync(mapping.Request);
        var result = new OfflineDrawingResolver().Resolve(mapping.Request, candidates);
        return new ProductionPipelineRun(mapping, gateway, catalog, candidates, result);
    }

    private static PdmSearchResponse Response(params PdmSearchCandidate[] candidates) => new()
    {
        Candidates = candidates,
        SupportedFields = new[] { PdmSearchField.PartNumber, PdmSearchField.DocumentNumber },
        Limitations = Array.Empty<string>()
    };

    private static PacketDrawingEvidenceSet SalesforcePair(string part, string document, string role, string recordId) =>
        EvidenceSet(new[]
        {
            Salesforce("part", "SALESFORCE.PART_NUMBER", part, recordId),
            Salesforce("doc", "SALESFORCE.DOCUMENT_NUMBER", document, recordId)
        }, role);

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

    private static PdmSearchCandidate Candidate(
        string backendId,
        string partNumber,
        string documentNumber,
        string fileName,
        bool isLocallyAvailable,
        string role) => new()
    {
        BackendId = backendId,
        PartNumber = partNumber,
        DocumentNumber = documentNumber,
        FileName = fileName,
        ExactPath = $"C:\\_PDMVault\\Engineering Data Base\\{fileName}",
        IsLocallyAvailable = isLocallyAvailable,
        Role = role,
        RoleAuthority = DrawingRoleAuthority.ControlledEvidence
    };

    private sealed record ProductionPipelineRun(
        DrawingResolutionMappingResult Mapping,
        FakeReadOnlyPdmSearchGateway Gateway,
        ProductionPdmDrawingCatalog Catalog,
        IReadOnlyList<ControlledDrawingRecord> Candidates,
        DrawingResolutionResult Result);
}
