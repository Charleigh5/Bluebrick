using Vira.Next.Contracts;

namespace Vira.Next.Engine;

public sealed class EngineeringHealthEvaluator
{
    public EngineeringHealthReport Evaluate(FakeHostDocument document, IEnumerable<RequiredPropertyRule> rules)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(rules);

        var findings = new List<EngineeringHealthFinding>();
        var verifiedCount = 0;

        foreach (var rule in rules)
        {
            if (document.CustomProperties.TryGetValue(rule.PropertyName, out var observed) && !string.IsNullOrWhiteSpace(observed))
            {
                verifiedCount++;
                continue;
            }

            findings.Add(new EngineeringHealthFinding
            {
                PropertyName = rule.PropertyName,
                Severity = EngineeringFindingSeverity.Warning,
                EvidenceState = EngineeringEvidenceState.Unknown,
                Observed = "Missing or blank in the local document snapshot.",
                Expected = rule.Expected,
                Evidence = "Direct local document snapshot inspection.",
                Limitation = "No authoritative source was queried or used to assign a value.",
                RecommendedAction = "Inspect approved local sources and preview candidate evidence.",
                ActionBoundary = EngineeringActionBoundary.PreviewOnly
            });
        }

        return new EngineeringHealthReport
        {
            VerifiedCount = verifiedCount,
            WarningCount = findings.Count,
            UnknownCount = findings.Count(item => item.EvidenceState == EngineeringEvidenceState.Unknown),
            Findings = findings
        };
    }
}
