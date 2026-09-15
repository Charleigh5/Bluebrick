namespace Vira.Next.Contracts;

public enum EngineeringFindingSeverity
{
    Info,
    Warning,
    High,
    Critical
}

public enum EngineeringEvidenceState
{
    Verified,
    Unknown,
    Conflict
}

public enum EngineeringActionBoundary
{
    PreviewOnly,
    ApprovalRequired,
    Blocked
}

public sealed record RequiredPropertyRule(string PropertyName, string Expected);

public sealed class EngineeringHealthFinding
{
    public string PropertyName { get; init; } = string.Empty;
    public EngineeringFindingSeverity Severity { get; init; }
    public EngineeringEvidenceState EvidenceState { get; init; }
    public string Observed { get; init; } = string.Empty;
    public string Expected { get; init; } = string.Empty;
    public string Evidence { get; init; } = string.Empty;
    public string Limitation { get; init; } = string.Empty;
    public string RecommendedAction { get; init; } = string.Empty;
    public EngineeringActionBoundary ActionBoundary { get; init; }
}

public sealed class EngineeringHealthReport
{
    public int VerifiedCount { get; init; }
    public int WarningCount { get; init; }
    public int UnknownCount { get; init; }
    public IReadOnlyList<EngineeringHealthFinding> Findings { get; init; } = Array.Empty<EngineeringHealthFinding>();
}
