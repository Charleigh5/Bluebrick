using Microsoft.VisualStudio.TestTools.UnitTesting;
using Vira.Next.Contracts;
using Vira.Next.Engine;

namespace Vira.Next.Engine.Tests;

[TestClass]
public sealed class ReadOnlyPdmSearchGatewayTests
{
    [TestMethod]
    public async Task G01_SearchAsync_CapturesQuery_AndPreservesUnknownAvailability()
    {
        var gateway = new FakeReadOnlyPdmSearchGateway();
        gateway.EnqueueResponse(new PdmSearchResponse
        {
            Candidates = new[]
            {
                new PdmSearchCandidate
                {
                    BackendId = "42",
                    FileName = "80233885.SLDDRW",
                    ExactPath = "C:\\_PDMVault\\Engineering Data Base\\80233885.SLDDRW",
                    IsLocallyAvailable = null
                }
            },
            SupportedFields = new[]
            {
                PdmSearchField.PartNumber,
                PdmSearchField.DocumentNumber,
                PdmSearchField.FileName
            },
            Limitations = new[] { "offline fake response" }
        });

        var query = new PdmSearchQuery
        {
            Field = PdmSearchField.PartNumber,
            NormalizedValue = "ASY511002",
            Limit = 3
        };

        var response = await gateway.SearchAsync(query);

        Assert.AreEqual(1, gateway.RecordedQueries.Count);
        Assert.AreEqual(query, gateway.RecordedQueries[0]);
        Assert.AreEqual(1, response.Candidates.Count);
        Assert.AreEqual("42", response.Candidates[0].BackendId);
        Assert.AreEqual("80233885.SLDDRW", response.Candidates[0].FileName);
        Assert.AreEqual("C:\\_PDMVault\\Engineering Data Base\\80233885.SLDDRW", response.Candidates[0].ExactPath);
        Assert.IsNull(response.Candidates[0].IsLocallyAvailable);
        CollectionAssert.AreEqual(
            new[] { PdmSearchField.PartNumber, PdmSearchField.DocumentNumber, PdmSearchField.FileName },
            response.SupportedFields.ToArray());
        CollectionAssert.AreEqual(new[] { "offline fake response" }, response.Limitations.ToArray());
        Assert.IsNull(response.Failure);
    }

    [TestMethod]
    public async Task G02_SearchAsync_PreCanceledToken_ThrowsBeforeRecordingOrConsumingResponse()
    {
        var gateway = new FakeReadOnlyPdmSearchGateway();
        gateway.EnqueueResponse(new PdmSearchResponse
        {
            Candidates = new[]
            {
                new PdmSearchCandidate
                {
                    BackendId = "canceled",
                    FileName = "unused.SLDDRW",
                    ExactPath = "C:\\_PDMVault\\unused.SLDDRW",
                    IsLocallyAvailable = true
                }
            }
        });

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() =>
            gateway.SearchAsync(new PdmSearchQuery
            {
                Field = PdmSearchField.FileName,
                NormalizedValue = "80233885.SLDDRW",
                Limit = 1
            }, cancellation.Token));

        Assert.AreEqual(0, gateway.RecordedQueries.Count);
        Assert.AreEqual(1, gateway.PendingResponseCount);
    }

    [TestMethod]
    public async Task G03_SearchAsync_ReturnsTypedFailureResponse()
    {
        var gateway = new FakeReadOnlyPdmSearchGateway();
        gateway.EnqueueFailure(new PdmSearchFailure
        {
            Code = "PDM_UNAVAILABLE",
            Message = "PDM backend unavailable.",
            SafeToRetry = true
        }, "legacy backend is offline");

        var response = await gateway.SearchAsync(new PdmSearchQuery
        {
            Field = PdmSearchField.DocumentNumber,
            NormalizedValue = "80233885",
            Limit = 2
        });

        Assert.AreEqual(1, gateway.RecordedQueries.Count);
        Assert.AreEqual(0, response.Candidates.Count);
        Assert.IsNotNull(response.Failure);
        Assert.AreEqual("PDM_UNAVAILABLE", response.Failure.Code);
        Assert.AreEqual("PDM backend unavailable.", response.Failure.Message);
        Assert.IsTrue(response.Failure.SafeToRetry);
        CollectionAssert.AreEqual(new[] { "legacy backend is offline" }, response.Limitations.ToArray());
    }

    [TestMethod]
    public async Task G04_SearchAsync_WithoutQueuedResponse_ThrowsInvalidOperationException()
    {
        var gateway = new FakeReadOnlyPdmSearchGateway();

        var exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            gateway.SearchAsync(new PdmSearchQuery
            {
                Field = PdmSearchField.PartNumber,
                NormalizedValue = "ASY511002",
                Limit = 1
            }));

        StringAssert.Contains(exception.Message, "No canned PDM search response");
        Assert.AreEqual(1, gateway.RecordedQueries.Count);
    }

    [TestMethod]
    public void G05_Contracts_RejectRuntimeNullAndBlankState()
    {
        Assert.ThrowsException<ArgumentNullException>(() => new PdmSearchCandidate
        {
            BackendId = null!,
            FileName = "80233885.SLDDRW",
            ExactPath = "C:\\_PDMVault\\Engineering Data Base\\80233885.SLDDRW"
        });

        Assert.ThrowsException<ArgumentException>(() => new PdmSearchCandidate
        {
            BackendId = "42",
            FileName = " ",
            ExactPath = "C:\\_PDMVault\\Engineering Data Base\\80233885.SLDDRW"
        });

        Assert.ThrowsException<ArgumentNullException>(() => new PdmSearchFailure
        {
            Code = "PDM_UNAVAILABLE",
            Message = null!
        });

        Assert.ThrowsException<ArgumentNullException>(() => new PdmSearchResponse
        {
            Candidates = null!
        });

        Assert.ThrowsException<ArgumentException>(() => new PdmSearchResponse
        {
            Candidates = new PdmSearchCandidate[] { null! }
        });

        Assert.ThrowsException<ArgumentException>(() => new PdmSearchResponse
        {
            Limitations = new string[] { null! }
        });
    }

    [TestMethod]
    public async Task G06_EnqueueResponse_SnapshotsPayloadAndReturnsCloneIsolatedCollections()
    {
        var gateway = new FakeReadOnlyPdmSearchGateway();
        var candidates = new[]
        {
            new PdmSearchCandidate
            {
                BackendId = "42",
                FileName = "80233885.SLDDRW",
                ExactPath = "C:\\_PDMVault\\Engineering Data Base\\80233885.SLDDRW",
                IsLocallyAvailable = true
            }
        };
        var supportedFields = new[]
        {
            PdmSearchField.PartNumber,
            PdmSearchField.FileName
        };
        var limitations = new[] { "original limitation" };

        gateway.EnqueueResponse(new PdmSearchResponse
        {
            Candidates = candidates,
            SupportedFields = supportedFields,
            Limitations = limitations
        });

        candidates[0] = candidates[0] with { FileName = "mutated-after-enqueue.SLDDRW" };
        supportedFields[0] = PdmSearchField.DocumentNumber;
        limitations[0] = "mutated limitation";

        var response = await gateway.SearchAsync(new PdmSearchQuery
        {
            Field = PdmSearchField.PartNumber,
            NormalizedValue = "ASY511002",
            Limit = 1
        });

        Assert.AreEqual("80233885.SLDDRW", response.Candidates[0].FileName);
        CollectionAssert.AreEqual(
            new[] { PdmSearchField.PartNumber, PdmSearchField.FileName },
            response.SupportedFields.ToArray());
        CollectionAssert.AreEqual(new[] { "original limitation" }, response.Limitations.ToArray());
    }
}
