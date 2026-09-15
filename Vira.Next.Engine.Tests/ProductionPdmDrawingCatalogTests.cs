using Microsoft.VisualStudio.TestTools.UnitTesting;
using Vira.Next.Contracts;
using Vira.Next.Engine;

namespace Vira.Next.Engine.Tests;

[TestClass]
public sealed class ProductionPdmDrawingCatalogTests
{
    [TestMethod]
    public async Task C01_FindCandidates_QueriesPartAndDocumentIndependently_MapsExactPair_AndCollapsesDuplicates()
    {
        var gateway = new FakeReadOnlyPdmSearchGateway();
        gateway.EnqueueResponse(new PdmSearchResponse
        {
            Candidates = new[]
            {
                Candidate("backend-1500265", "ASY511002", "80233885", "80233885.SLDDRW", false, role: "STARTER", roleAuthority: DrawingRoleAuthority.ControlledEvidence)
            },
            SupportedFields = new[] { PdmSearchField.PartNumber, PdmSearchField.DocumentNumber },
            Limitations = Array.Empty<string>()
        });
        gateway.EnqueueResponse(new PdmSearchResponse
        {
            Candidates = new[]
            {
                Candidate("backend-1500265", "ASY511002", "80233885", "80233885.SLDDRW", false, role: "STARTER", roleAuthority: DrawingRoleAuthority.ControlledEvidence)
            },
            SupportedFields = new[] { PdmSearchField.PartNumber, PdmSearchField.DocumentNumber },
            Limitations = Array.Empty<string>()
        });

        var catalog = new ProductionPdmDrawingCatalog(gateway);

        var candidates = await catalog.FindCandidatesAsync(new DrawingResolutionRequest
        {
            PartNumber = " asy511002 ",
            DocumentNumber = "80233885",
            Identifiers = new[]
            {
                Identifier(DrawingIdentifierKind.PartNumber, "ASY511002"),
                Identifier(DrawingIdentifierKind.DocumentNumber, "80233885")
            }
        });

        Assert.AreEqual(2, gateway.RecordedQueries.Count);
        Assert.AreEqual(PdmSearchField.PartNumber, gateway.RecordedQueries[0].Field);
        Assert.AreEqual("ASY511002", gateway.RecordedQueries[0].NormalizedValue);
        Assert.AreEqual(PdmSearchField.DocumentNumber, gateway.RecordedQueries[1].Field);
        Assert.AreEqual("80233885", gateway.RecordedQueries[1].NormalizedValue);
        Assert.AreEqual(1, candidates.Count);
        Assert.AreEqual("backend-1500265", candidates[0].RecordId);
        Assert.AreEqual("ASY511002", candidates[0].PartNumber);
        Assert.AreEqual("80233885", candidates[0].DocumentNumber);
        Assert.AreEqual("80233885.SLDDRW", candidates[0].FileName);
        Assert.IsFalse(candidates[0].IsLocallyAvailable);
        Assert.IsTrue(candidates[0].RetrievalRequired);
        Assert.AreEqual("STARTER", candidates[0].Role);
        Assert.AreEqual(DrawingRoleAuthority.ControlledEvidence, candidates[0].RoleAuthority);
        Assert.AreEqual(0, catalog.LastReceipt.Limitations.Count);
        Assert.AreEqual(2, catalog.LastReceipt.AttemptedQueries.Count);
        Assert.AreEqual(2, catalog.LastReceipt.PlannedQueries.Count);
    }

    [TestMethod]
    public async Task C02_FindCandidates_OrdersSameQueryCandidatesDeterministically()
    {
        var gateway = new FakeReadOnlyPdmSearchGateway();
        gateway.EnqueueResponse(new PdmSearchResponse
        {
            Candidates = new[]
            {
                Candidate("c", "ASY511002", "80233887", "80233887.SLDDRW", true),
                Candidate("a", "ASY511002", "80233885", "80233885.SLDDRW", true),
                Candidate("b", "ASY511002", "80233886", "80233886.SLDDRW", true)
            },
            SupportedFields = new[] { PdmSearchField.PartNumber, PdmSearchField.DocumentNumber },
            Limitations = Array.Empty<string>()
        });

        var catalog = new ProductionPdmDrawingCatalog(gateway);

        var candidates = await catalog.FindCandidatesAsync(new DrawingResolutionRequest
        {
            PartNumber = "ASY511002"
        });

        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, candidates.Select(item => item.RecordId).ToArray());
    }

    [TestMethod]
    public async Task C03_BlankRequest_ReturnsNoCandidatesAndDoesNotQueryGateway()
    {
        var gateway = new FakeReadOnlyPdmSearchGateway();
        var catalog = new ProductionPdmDrawingCatalog(gateway);

        var candidates = await catalog.FindCandidatesAsync(new DrawingResolutionRequest());

        Assert.AreEqual(0, candidates.Count);
        Assert.AreEqual(0, gateway.RecordedQueries.Count);
        Assert.AreEqual(0, catalog.LastReceipt.AttemptedQueries.Count);
        Assert.AreEqual(0, catalog.LastReceipt.PlannedQueries.Count);
    }

    [TestMethod]
    public async Task C04_UnsupportedRequestedField_DoesNotFallBackToFileName()
    {
        var gateway = new FakeReadOnlyPdmSearchGateway();
        gateway.EnqueueResponse(new PdmSearchResponse
        {
            Candidates = new[]
            {
                Candidate("filename-only", "", "", "ASY511002.SLDDRW", true)
            },
            SupportedFields = new[] { PdmSearchField.FileName },
            Limitations = Array.Empty<string>()
        });

        var catalog = new ProductionPdmDrawingCatalog(gateway);

        var candidates = await catalog.FindCandidatesAsync(new DrawingResolutionRequest
        {
            PartNumber = "ASY511002"
        });

        Assert.AreEqual(0, candidates.Count);
        Assert.AreEqual(1, gateway.RecordedQueries.Count);
        Assert.IsTrue(catalog.LastReceipt.Limitations.Any(item => item.Contains("does not support PartNumber", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task C05_UnknownAvailabilityAndMissingRequestedIdentity_AreExcludedFailClosed()
    {
        var gateway = new FakeReadOnlyPdmSearchGateway();
        gateway.EnqueueResponse(new PdmSearchResponse
        {
            Candidates = new[]
            {
                Candidate("unknown-availability", "ASY511002", "80233885", "80233885.SLDDRW", null),
                Candidate("missing-part", "", "80233885", "80233885.SLDDRW", true)
            },
            SupportedFields = new[] { PdmSearchField.PartNumber, PdmSearchField.DocumentNumber },
            Limitations = Array.Empty<string>()
        });

        var catalog = new ProductionPdmDrawingCatalog(gateway);

        var candidates = await catalog.FindCandidatesAsync(new DrawingResolutionRequest
        {
            PartNumber = "ASY511002"
        });

        Assert.AreEqual(0, candidates.Count);
        Assert.IsTrue(catalog.LastReceipt.Limitations.Any(item => item.Contains("unknown local availability", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(catalog.LastReceipt.Limitations.Any(item => item.Contains("missing observed PartNumber", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task C06_MismatchedReturnedPartNumber_IsExcludedFailClosed()
    {
        var gateway = new FakeReadOnlyPdmSearchGateway();
        gateway.EnqueueResponse(new PdmSearchResponse
        {
            Candidates = new[]
            {
                Candidate("mismatch-part", "ASY511003", "80233885", "80233885.SLDDRW", true)
            },
            SupportedFields = new[] { PdmSearchField.PartNumber, PdmSearchField.DocumentNumber },
            Limitations = Array.Empty<string>()
        });

        var catalog = new ProductionPdmDrawingCatalog(gateway);

        var candidates = await catalog.FindCandidatesAsync(new DrawingResolutionRequest
        {
            PartNumber = "ASY511002"
        });

        Assert.AreEqual(0, candidates.Count);
        Assert.IsTrue(catalog.LastReceipt.Limitations.Any(item => item.Contains("does not match requested PartNumber", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task C07_MismatchedReturnedDocumentNumber_IsExcludedFailClosed()
    {
        var gateway = new FakeReadOnlyPdmSearchGateway();
        gateway.EnqueueResponse(new PdmSearchResponse
        {
            Candidates = new[]
            {
                Candidate("mismatch-document", "ASY511002", "80233886", "80233886.SLDDRW", true)
            },
            SupportedFields = new[] { PdmSearchField.PartNumber, PdmSearchField.DocumentNumber },
            Limitations = Array.Empty<string>()
        });

        var catalog = new ProductionPdmDrawingCatalog(gateway);

        var candidates = await catalog.FindCandidatesAsync(new DrawingResolutionRequest
        {
            DocumentNumber = "80233885"
        });

        Assert.AreEqual(0, candidates.Count);
        Assert.IsTrue(catalog.LastReceipt.Limitations.Any(item => item.Contains("does not match requested DocumentNumber", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task C08_ContradictoryFilenameAndPathExtensions_AreExcludedFailClosed()
    {
        var gateway = new FakeReadOnlyPdmSearchGateway();
        gateway.EnqueueResponse(new PdmSearchResponse
        {
            Candidates = new[]
            {
                Candidate("pdf-name", "ASY511002", "80233885", "80233885.PDF", true, exactPath: "C:\\_PDMVault\\Engineering Data Base\\80233885.SLDDRW"),
                Candidate("pdf-path", "ASY511002", "80233886", "80233886.SLDDRW", true, exactPath: "C:\\_PDMVault\\Engineering Data Base\\80233886.PDF"),
                Candidate("draw", "ASY511002", "80233887", "80233887.slddrw", true)
            },
            SupportedFields = new[] { PdmSearchField.PartNumber, PdmSearchField.DocumentNumber },
            Limitations = Array.Empty<string>()
        });

        var catalog = new ProductionPdmDrawingCatalog(gateway);

        var candidates = await catalog.FindCandidatesAsync(new DrawingResolutionRequest
        {
            PartNumber = "ASY511002"
        });

        Assert.AreEqual(1, candidates.Count);
        Assert.AreEqual("draw", candidates[0].RecordId);
        Assert.IsTrue(catalog.LastReceipt.Limitations.Any(item => item.Contains("filename extension", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(catalog.LastReceipt.Limitations.Any(item => item.Contains("path extension", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task C09_MaximumCandidateCount_FailsClosedInsteadOfReturningIncompleteSubset()
    {
        var gateway = new FakeReadOnlyPdmSearchGateway();
        gateway.EnqueueResponse(new PdmSearchResponse
        {
            Candidates = new[]
            {
                Candidate("c", "ASY511002", "80233887", "80233887.SLDDRW", true),
                Candidate("a", "ASY511002", "80233885", "80233885.SLDDRW", true),
                Candidate("b", "ASY511002", "80233886", "80233886.SLDDRW", true)
            },
            SupportedFields = new[] { PdmSearchField.PartNumber, PdmSearchField.DocumentNumber },
            Limitations = Array.Empty<string>()
        });

        var catalog = new ProductionPdmDrawingCatalog(
            gateway,
            new ProductionPdmDrawingCatalogOptions
            {
                MaximumCandidateCount = 2,
                QueryLimit = 5
            });

        var candidates = await catalog.FindCandidatesAsync(new DrawingResolutionRequest
        {
            PartNumber = "ASY511002"
        });

        Assert.AreEqual(0, candidates.Count);
        Assert.AreEqual(0, catalog.LastReceipt.ReturnedCandidateCount);
        Assert.IsTrue(catalog.LastReceipt.Limitations.Any(item => item.Contains("Maximum candidate count", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task C14_MaximumCandidateCountOne_CannotExposeOneOfTwoIdentities()
    {
        var gateway = new FakeReadOnlyPdmSearchGateway();
        gateway.EnqueueResponse(new PdmSearchResponse
        {
            Candidates = new[]
            {
                Candidate("a", "ASY511002", "80233885", "80233885.SLDDRW", true),
                Candidate("b", "ASY511002", "80233885", "80233885.SLDDRW", true)
            }
        });
        var catalog = new ProductionPdmDrawingCatalog(gateway, new ProductionPdmDrawingCatalogOptions { MaximumCandidateCount = 1 });

        var candidates = await catalog.FindCandidatesAsync(new DrawingResolutionRequest { PartNumber = "ASY511002" });

        Assert.AreEqual(0, candidates.Count);
        Assert.AreEqual(0, catalog.LastReceipt.ReturnedCandidateCount);
        Assert.IsTrue(catalog.LastReceipt.Limitations.Any(item => item.Contains("uniqueness", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task C10_PreCanceledToken_ThrowsAndDoesNotQueryGateway()
    {
        var gateway = new FakeReadOnlyPdmSearchGateway();
        var catalog = new ProductionPdmDrawingCatalog(gateway);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() =>
            catalog.FindCandidatesAsync(new DrawingResolutionRequest
            {
                PartNumber = "ASY511002"
            }, cancellation.Token));

        Assert.AreEqual(0, gateway.RecordedQueries.Count);
    }

    [TestMethod]
    public async Task C11_BackendFailure_RecordsOnlyAttemptedQuery_WhenSecondPlannedQueryWasNotRun()
    {
        var gateway = new FakeReadOnlyPdmSearchGateway();
        gateway.EnqueueFailure(new PdmSearchFailure
        {
            Code = "PDM_UNAVAILABLE",
            Message = "PDM backend unavailable.",
            SafeToRetry = true
        }, "legacy backend is offline");

        var catalog = new ProductionPdmDrawingCatalog(gateway);

        var candidates = await catalog.FindCandidatesAsync(new DrawingResolutionRequest
        {
            PartNumber = "ASY511002",
            DocumentNumber = "80233885"
        });

        Assert.AreEqual(0, candidates.Count);
        Assert.AreEqual(2, catalog.LastReceipt.PlannedQueries.Count);
        Assert.AreEqual(1, catalog.LastReceipt.AttemptedQueries.Count);
        Assert.AreEqual(PdmSearchField.PartNumber, catalog.LastReceipt.AttemptedQueries[0].Field);
        Assert.IsTrue(catalog.LastReceipt.Limitations.Any(item => item.Contains("PDM_UNAVAILABLE", StringComparison.Ordinal)));
        Assert.IsTrue(catalog.LastReceipt.Limitations.Any(item => item.Contains("legacy backend is offline", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task C12_IncompleteRoleAuthority_ClearsRoleClaimAndRecordsLimitation()
    {
        var gateway = new FakeReadOnlyPdmSearchGateway();
        gateway.EnqueueResponse(new PdmSearchResponse
        {
            Candidates = new[]
            {
                Candidate("unknown-role-authority", "ASY511002", "80233885", "80233885.SLDDRW", true, role: "STARTER", roleAuthority: null)
            },
            SupportedFields = new[] { PdmSearchField.PartNumber, PdmSearchField.DocumentNumber },
            Limitations = Array.Empty<string>()
        });

        var catalog = new ProductionPdmDrawingCatalog(gateway);

        var candidates = await catalog.FindCandidatesAsync(new DrawingResolutionRequest
        {
            PartNumber = "ASY511002"
        });

        Assert.AreEqual(1, candidates.Count);
        Assert.AreEqual(string.Empty, candidates[0].Role);
        Assert.AreEqual(DrawingRoleAuthority.ControlledEvidence, candidates[0].RoleAuthority);
        Assert.IsTrue(catalog.LastReceipt.Limitations.Any(item => item.Contains("role authority was not supplied", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task C13_GatewayThrownCancellation_IsPropagated()
    {
        var gateway = new FakeReadOnlyPdmSearchGateway();
        gateway.EnqueueException(new OperationCanceledException("gateway canceled"));

        var catalog = new ProductionPdmDrawingCatalog(gateway);

        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() =>
            catalog.FindCandidatesAsync(new DrawingResolutionRequest
            {
                PartNumber = "ASY511002"
            }));

        Assert.AreEqual(1, gateway.RecordedQueries.Count);
    }

    private static DrawingIdentifierCandidate Identifier(DrawingIdentifierKind kind, string value) => new()
    {
        Kind = kind,
        RawValue = value,
        Authority = DrawingEvidenceAuthority.SourceDerived,
        EvidenceId = $"{kind}-{value}",
        SourceField = kind.ToString()
    };

    private static PdmSearchCandidate Candidate(
        string backendId,
        string partNumber,
        string documentNumber,
        string fileName,
        bool? isLocallyAvailable,
        string role = "",
        DrawingRoleAuthority? roleAuthority = null,
        string? exactPath = null) => new()
    {
        BackendId = backendId,
        PartNumber = partNumber,
        DocumentNumber = documentNumber,
        FileName = fileName,
        ExactPath = exactPath ?? $"C:\\_PDMVault\\Engineering Data Base\\{fileName}",
        IsLocallyAvailable = isLocallyAvailable,
        Role = role,
        RoleAuthority = roleAuthority
    };
}
