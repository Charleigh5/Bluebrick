using Microsoft.VisualStudio.TestTools.UnitTesting;
using Vira.Next.Contracts;
using Vira.Next.Engine;

namespace Vira.Next.Engine.Tests;

[TestClass]
public sealed class SyntheticPdmDrawingCatalogTests
{
    [TestMethod]
    public async Task C01_FindCandidates_ReturnsDeterministicUnionWithoutExternalAccess()
    {
        var catalog = new SyntheticPdmDrawingCatalog(new[]
        {
            Record("b", "ASY511003", "80233886"),
            Record("a", "ASY511002", "80233885"),
            Record("c", "ASY511009", "80233885")
        });

        var candidates = await catalog.FindCandidatesAsync(new DrawingResolutionRequest
        {
            PartNumber = "ASY511002",
            DocumentNumber = "80233885",
            Identifiers = new[]
            {
                Candidate(DrawingIdentifierKind.PartNumber, "ASY511002"),
                Candidate(DrawingIdentifierKind.DocumentNumber, "80233885")
            }
        });

        CollectionAssert.AreEqual(new[] { "a", "c" }, candidates.Select(item => item.RecordId).ToArray());
        Assert.IsTrue(candidates.All(item => item.ReadOnlyPolicy == DrawingReadOnlyPolicy.ReadOnlyRequired));
    }

    [TestMethod]
    public async Task C02_CatalogIsImmutableAndReturnsClonedCandidateRecords()
    {
        var original = new List<ControlledDrawingRecord>
        {
            Record("b", "ASY511003", "80233886")
        };
        var catalog = new SyntheticPdmDrawingCatalog(original);
        original.Add(Record("a", "ASY511002", "80233885"));

        var candidates = await catalog.FindCandidatesAsync(new DrawingResolutionRequest
        {
            PartNumber = "ASY511002",
            Identifiers = new[] { Candidate(DrawingIdentifierKind.PartNumber, "ASY511002") }
        });
        Assert.AreEqual(0, candidates.Count);

        var found = await catalog.FindCandidatesAsync(new DrawingResolutionRequest
        {
            PartNumber = "ASY511003",
            Identifiers = new[] { Candidate(DrawingIdentifierKind.PartNumber, "ASY511003") }
        });
        Assert.AreEqual(1, found.Count);
        Assert.AreNotSame(original[0], found[0]);
    }

    [TestMethod]
    public async Task C03_BlankRequest_ReturnsNoCandidates()
    {
        var catalog = new SyntheticPdmDrawingCatalog(new[] { Record("a", "ASY511002", "80233885") });

        var candidates = await catalog.FindCandidatesAsync(new DrawingResolutionRequest());

        Assert.AreEqual(0, candidates.Count);
    }

    [TestMethod]
    public void C04_BlankControlledRecordId_IsRejectedDeterministically()
    {
        var exception = Assert.ThrowsException<ArgumentException>(() =>
            new SyntheticPdmDrawingCatalog(new[] { Record(" ", "ASY511002", "80233885") }));

        StringAssert.Contains(exception.Message, "blank RecordId");
    }

    [TestMethod]
    public void C05_DuplicateControlledRecordId_IsRejectedDeterministically()
    {
        var exception = Assert.ThrowsException<ArgumentException>(() =>
            new SyntheticPdmDrawingCatalog(new[]
            {
                Record("duplicate", "ASY511002", "80233885"),
                Record("DUPLICATE", "ASY511003", "80233886")
            }));

        StringAssert.Contains(exception.Message, "RecordId 'duplicate' is duplicated");
    }

    [TestMethod]
    public async Task C06_NullRequestIdentifierCollection_IsTreatedAsEmpty()
    {
        var catalog = new SyntheticPdmDrawingCatalog(new[] { Record("a", "ASY511002", "80233885") });

        var candidates = await catalog.FindCandidatesAsync(new DrawingResolutionRequest
        {
            PartNumber = "ASY511002",
            Identifiers = null!
        });

        Assert.AreEqual(1, candidates.Count);
        Assert.AreEqual("a", candidates[0].RecordId);
    }

    [TestMethod]
    public async Task C07_CanceledRequest_ThrowsOperationCanceledException()
    {
        var catalog = new SyntheticPdmDrawingCatalog(new[] { Record("a", "ASY511002", "80233885") });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() =>
            catalog.FindCandidatesAsync(new DrawingResolutionRequest
            {
                PartNumber = "ASY511002"
            }, cancellation.Token));
    }

    private static DrawingIdentifierCandidate Candidate(DrawingIdentifierKind kind, string value) => new()
    {
        Kind = kind,
        RawValue = value,
        Authority = DrawingEvidenceAuthority.SourceDerived,
        EvidenceId = $"{kind}-{value}",
        SourceField = kind.ToString()
    };

    private static ControlledDrawingRecord Record(string id, string part, string document) => new()
    {
        RecordId = id,
        PartNumber = part,
        DocumentNumber = document,
        ExactPath = $"C:\\synthetic\\{document}.SLDDRW",
        FileName = $"{document}.SLDDRW",
        IsLocallyAvailable = false,
        RetrievalRequired = true,
        ReadOnlyPolicy = DrawingReadOnlyPolicy.ReadOnlyRequired,
        Role = "STARTER",
        RoleAuthority = DrawingRoleAuthority.ControlledEvidence
    };
}
