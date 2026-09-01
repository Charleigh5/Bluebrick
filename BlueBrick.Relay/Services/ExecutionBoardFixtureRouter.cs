using System.Text.RegularExpressions;
using BlueBrick.Relay.Models;

namespace BlueBrick.Relay.Services;

public sealed class ExecutionBoardFixtureRouter
{
    private static readonly string[] KnownIds = { "BB-SRC-1001", "BB-SRC-1002", "BB-SRC-1003", "BB-SRC-1004" };

    public static ExecutionBoardFixtureResponse Route(string query, string sessionId)
    {
        var request = (query ?? string.Empty).Trim();
        var id = ResolveId(request);
        if (id != null)
        {
            var known = KnownIds.Contains(id, StringComparer.OrdinalIgnoreCase);
            return Build(
                known ? "LOCAL_FIXTURE_RESULT" : "UNKNOWN_ID",
                "exact-id",
                known ? new[] { id } : Array.Empty<string>(),
                known ? new[] { id } : Array.Empty<string>(),
                Array.Empty<ExecutionBoardCapabilityState>(),
                Array.Empty<ExecutionBoardActionPreview>(),
                known ? "Exact local fixture identifier matched." : "Exact identifier was not present in the local fixture catalog.",
                request,
                sessionId);
        }

        if (request.Contains("pdm", StringComparison.OrdinalIgnoreCase))
        {
            return Build(
                "NOT_CONNECTED",
                "fixture-search",
                Array.Empty<string>(),
                new[] { "BB-CAP-4003", "BB-ACT-5002" },
                new[] { new ExecutionBoardCapabilityState("BB-CAP-4003", "PDM search", "NOT_CONNECTED") },
                new[] { new ExecutionBoardActionPreview("BB-ACT-5002", "Preview PDM search") },
                "PDM is not connected in the local relay fixture.",
                request,
                sessionId);
        }

        if (request.Contains("solidworks", StringComparison.OrdinalIgnoreCase) || request.Contains("cad", StringComparison.OrdinalIgnoreCase))
        {
            return Build(
                "APPROVAL_REQUIRED",
                "fixture-search",
                Array.Empty<string>(),
                new[] { "BB-CAP-4002", "BB-ACT-5001" },
                new[] { new ExecutionBoardCapabilityState("BB-CAP-4002", "SOLIDWORKS context read", "APPROVAL_REQUIRED") },
                new[] { new ExecutionBoardActionPreview("BB-ACT-5001", "Preview SOLIDWORKS metadata read") },
                "SOLIDWORKS context read requires an explicit approval packet.",
                request,
                sessionId);
        }

        return Build(
            "LOCAL_FIXTURE_RESULT",
            "fixture-search",
            new[] { "BB-SRC-1004" },
            new[] { "BB-SRC-1004", "BB-CAP-4001", "BB-ACT-5004" },
            new[] { new ExecutionBoardCapabilityState("BB-CAP-4001", "Local evidence lookup", "LOCAL") },
            new[] { new ExecutionBoardActionPreview("BB-ACT-5004", "Local evidence-card lookup") },
            "Bounded local fixture search completed.",
            request,
            sessionId);
    }

    private static string? ResolveId(string request)
    {
        var compact = Regex.Match(request.Replace(" ", string.Empty), "BB-[A-Z]+-\\d{4}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (compact.Success) return compact.Value.ToUpperInvariant();

        var spaced = Regex.Match(request, "\\bBB\\s+([A-Z]+)\\s+(\\d{4})\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return spaced.Success
            ? ("BB-" + spaced.Groups[1].Value + "-" + spaced.Groups[2].Value).ToUpperInvariant()
            : null;
    }

    private static ExecutionBoardFixtureResponse Build(
        string status,
        string routeMode,
        IReadOnlyList<string> matchedIds,
        IReadOnlyList<string> resultIds,
        IReadOnlyList<ExecutionBoardCapabilityState> capabilities,
        IReadOnlyList<ExecutionBoardActionPreview> actions,
        string message,
        string query,
        string sessionId)
    {
        return new ExecutionBoardFixtureResponse
        {
            Status = status,
            RouteMode = routeMode,
            MatchedIds = matchedIds,
            ResultIds = resultIds,
            CapabilityStates = capabilities,
            ActionPreviews = actions,
            Message = message,
            DataGaps = status == "LOCAL_FIXTURE_RESULT" ? Array.Empty<string>() : new[] { message },
            Receipt = new ExecutionBoardFixtureReceipt
            {
                Id = "BB-RELAY-FIXTURE-" + Guid.NewGuid().ToString("N"),
                Query = query,
                SessionId = sessionId,
                RouteMode = routeMode,
                ResultIds = resultIds,
                RoutingDecision = message,
                RoutedAtUtc = DateTime.UtcNow
            }
        };
    }
}

public sealed class ExecutionBoardFixtureRequest
{
    public string Query { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
}

public sealed class ExecutionBoardFixtureResponse
{
    public string Status { get; set; } = string.Empty;
    public string RouteMode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<string> MatchedIds { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> ResultIds { get; set; } = Array.Empty<string>();
    public IReadOnlyList<ExecutionBoardCapabilityState> CapabilityStates { get; set; } = Array.Empty<ExecutionBoardCapabilityState>();
    public IReadOnlyList<ExecutionBoardActionPreview> ActionPreviews { get; set; } = Array.Empty<ExecutionBoardActionPreview>();
    public IReadOnlyList<string> DataGaps { get; set; } = Array.Empty<string>();
    public ExecutionBoardFixtureReceipt Receipt { get; set; } = new();
    public bool PersistedReceipt { get; set; }
}

public sealed record ExecutionBoardCapabilityState(string Id, string Label, string State);

public sealed record ExecutionBoardActionPreview(string Id, string Title);

public sealed class ExecutionBoardFixtureReceipt
{
    public string Id { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string RouteMode { get; set; } = string.Empty;
    public string RoutingDecision { get; set; } = string.Empty;
    public IReadOnlyList<string> ResultIds { get; set; } = Array.Empty<string>();
    public DateTime RoutedAtUtc { get; set; }
}
