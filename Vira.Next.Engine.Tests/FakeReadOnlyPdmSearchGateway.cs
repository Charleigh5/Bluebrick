using Vira.Next.Contracts;
using Vira.Next.Engine;

namespace Vira.Next.Engine.Tests;

internal sealed class FakeReadOnlyPdmSearchGateway : IReadOnlyPdmSearchGateway
{
    private readonly Queue<Func<PdmSearchResponse>> _pendingResponses = new();

    public IReadOnlyList<PdmSearchQuery> RecordedQueries => _recordedQueries;

    public int PendingResponseCount => _pendingResponses.Count;

    private readonly List<PdmSearchQuery> _recordedQueries = new();

    public void EnqueueResponse(PdmSearchResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        var snapshot = Clone(response);
        _pendingResponses.Enqueue(() => Clone(snapshot));
    }

    public void EnqueueFailure(PdmSearchFailure failure, params string[] limitations)
    {
        ArgumentNullException.ThrowIfNull(failure);

        EnqueueResponse(new PdmSearchResponse
        {
            Candidates = Array.Empty<PdmSearchCandidate>(),
            SupportedFields = new[]
            {
                PdmSearchField.PartNumber,
                PdmSearchField.DocumentNumber,
                PdmSearchField.FileName
            },
            Limitations = limitations ?? Array.Empty<string>(),
            Failure = failure with { }
        });
    }

    public void EnqueueException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        _pendingResponses.Enqueue(() => throw exception);
    }

    public Task<PdmSearchResponse> SearchAsync(
        PdmSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        _recordedQueries.Add(query with { });

        if (_pendingResponses.Count == 0)
        {
            throw new InvalidOperationException(
                "No canned PDM search response was queued for FakeReadOnlyPdmSearchGateway.");
        }

        return Task.FromResult(_pendingResponses.Dequeue().Invoke());
    }

    private static PdmSearchResponse Clone(PdmSearchResponse response) => new()
    {
        Candidates = response.Candidates.Select(candidate => candidate with { }).ToArray(),
        SupportedFields = response.SupportedFields.ToArray(),
        Limitations = response.Limitations.ToArray(),
        Failure = response.Failure is null ? null : response.Failure with { }
    };
}
