using Vira.Next.Contracts;

namespace Vira.Next.Engine;

public sealed class PropertyComparisonEngine
{
    private readonly PropertyComparisonOptions _options;
    private readonly NormalizationRegistry _normalization;
    private readonly ComparisonSeverityPolicy _severity;

    public PropertyComparisonEngine()
        : this(new PropertyComparisonOptions())
    {
    }

    public PropertyComparisonEngine(PropertyComparisonOptions options)
        : this(options, new NormalizationRegistry(), new ComparisonSeverityPolicy())
    {
    }

    public PropertyComparisonEngine(
        PropertyComparisonOptions options,
        NormalizationRegistry normalization,
        ComparisonSeverityPolicy severity)
    {
        _options = options;
        _normalization = normalization;
        _severity = severity;
    }

    public IReadOnlyList<PacketCadComparison> Compare(
        PacketEvidenceSnapshot packet,
        CadDocumentSnapshot cad,
        PacketCadIdentityResolution identity)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(cad);
        ArgumentNullException.ThrowIfNull(identity);

        var packetGroups = packet.Properties
            .GroupBy(item => _normalization.CanonicalPropertyName(item.Name), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var cadGroups = cad.Properties
            .GroupBy(item => _normalization.CanonicalPropertyName(item.Name), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var names = packetGroups.Keys.Union(cadGroups.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(item => item, StringComparer.OrdinalIgnoreCase);
        var results = new List<PacketCadComparison>();

        foreach (var name in names)
        {
            packetGroups.TryGetValue(name, out var packetProperties);
            cadGroups.TryGetValue(name, out var cadProperties);
            packetProperties ??= Array.Empty<PacketPropertyEvidence>();
            cadProperties ??= Array.Empty<CadPropertySnapshot>();

            if (packetProperties.Length == 0)
            {
                foreach (var cadProperty in cadProperties)
                {
                    results.Add(Missing(name, null, cadProperty, PacketCadComparisonStatus.MissingInPdf, identity));
                }
                continue;
            }

            if (packetProperties.Select(item => NormalizePacketValue(name, PacketValue(item)))
                .Distinct(StringComparer.Ordinal).Skip(1).Any())
            {
                foreach (var cadProperty in cadProperties.DefaultIfEmpty())
                {
                    results.Add(PacketConflict(name, packetProperties, cadProperty, identity));
                }
                continue;
            }

            if (cadProperties.Length == 0)
            {
                results.Add(Missing(name, packetProperties, null, PacketCadComparisonStatus.MissingInCad, identity));
                continue;
            }

            foreach (var cadProperty in cadProperties)
            {
                results.Add(CompareProperty(name, packetProperties, cadProperty, identity));
            }
        }

        return results;
    }

    private PacketCadComparison CompareProperty(
        string canonicalName,
        IReadOnlyList<PacketPropertyEvidence> packetProperties,
        CadPropertySnapshot cadProperty,
        PacketCadIdentityResolution identity)
    {
        var packetValues = packetProperties.Select(PacketValue).OrderBy(item => item, StringComparer.Ordinal).ToArray();
        var packetValue = packetValues[0];
        var cadValue = cadProperty.WasResolved && !string.IsNullOrWhiteSpace(cadProperty.EvaluatedValue)
            ? cadProperty.EvaluatedValue
            : cadProperty.RawValue;
        var allExact = packetValues.All(item => string.Equals(item.Trim(), cadValue.Trim(), StringComparison.Ordinal));
        var limitations = new List<string>();
        PacketCadComparisonStatus status;
        string ruleId;
        double confidence;

        if (cadProperty.ReadStatus == CadPropertyReadStatus.Unsupported)
        {
            status = PacketCadComparisonStatus.Unsupported;
            ruleId = "VIRA-PROPERTY-UNSUPPORTED-001";
            confidence = 1.0;
            limitations.Add("The CAD property reader does not support this evidence.");
        }
        else if (cadProperty.ReadStatus == CadPropertyReadStatus.ReadError)
        {
            status = PacketCadComparisonStatus.InsufficientEvidence;
            ruleId = "VIRA-PROPERTY-READ-ERROR-001";
            confidence = 1.0;
            limitations.Add("The CAD property read failed; no comparison claim is permitted.");
        }
        else if (canonicalName == "Thickness")
        {
            var packetThickness = _normalization.NormalizeThickness(packetValue, _options);
            var cadThickness = _normalization.NormalizeThickness(cadValue, _options);
            if (!packetThickness.IsSupported || !cadThickness.IsSupported)
            {
                status = PacketCadComparisonStatus.Unsupported;
                ruleId = "VIRA-THICKNESS-UNSUPPORTED-001";
                confidence = 1.0;
                limitations.Add(packetThickness.IsSupported ? cadThickness.Limitation : packetThickness.Limitation);
            }
            else if (Math.Abs(packetThickness.Inches - cadThickness.Inches) <= _options.ThicknessToleranceInches)
            {
                status = allExact
                    ? PacketCadComparisonStatus.ExactMatch
                    : PacketCadComparisonStatus.NormalizedMatch;
                ruleId = allExact
                    ? "VIRA-TEXT-EXACT-001"
                    : packetThickness.RuleId;
                confidence = allExact ? 1.0 : 0.99;
            }
            else
            {
                status = PacketCadComparisonStatus.Conflict;
                ruleId = "VIRA-THICKNESS-INCH-TOLERANCE-001";
                confidence = 0.99;
            }
        }
        else if (allExact)
        {
            status = PacketCadComparisonStatus.ExactMatch;
            ruleId = "VIRA-TEXT-EXACT-001";
            confidence = 1.0;
        }
        else if (_normalization.NormalizeValue(canonicalName, packetValue, _options) ==
                 _normalization.NormalizeValue(canonicalName, cadValue, _options))
        {
            status = PacketCadComparisonStatus.NormalizedMatch;
            ruleId = "VIRA-TEXT-NORMALIZE-001";
            confidence = 0.98;
        }
        else
        {
            status = PacketCadComparisonStatus.Conflict;
            ruleId = "VIRA-TEXT-NORMALIZE-001";
            confidence = 0.98;
        }

        if (cadProperty.ReadStatus == CadPropertyReadStatus.CachedUnresolved)
        {
            if (status is PacketCadComparisonStatus.ExactMatch or PacketCadComparisonStatus.NormalizedMatch)
            {
                status = PacketCadComparisonStatus.ProbableMatch;
            }
            limitations.Add("CAD evidence is cached and unresolved; evaluated equivalence is not proven.");
        }

        var authoritative = identity.IsAuthoritative &&
            status is not PacketCadComparisonStatus.ProbableMatch and
            not PacketCadComparisonStatus.InsufficientEvidence and
            not PacketCadComparisonStatus.Unsupported;
        AddIdentityLimitation(identity, limitations);

        return new PacketCadComparison
        {
            ComparisonId = Id(canonicalName, cadProperty.Scope),
            Category = $"property:{cadProperty.Scope.ToString().ToLowerInvariant()}:{canonicalName}",
            PacketEvidence = OrderedPacketEvidence(packetProperties),
            CadEvidence = new[] { cadProperty.Evidence },
            Status = status,
            IdentityAuthority = identity.Authority,
            IsAuthoritative = authoritative,
            NormalizationRuleId = ruleId,
            Confidence = confidence,
            Severity = _severity.Classify(canonicalName, status, authoritative),
            Limitations = limitations.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            RecommendedVerificationAction = Recommended(canonicalName, status),
            MutationBoundary = EngineeringMutationBoundary.ReadOnly
        };
    }

    private static string PacketValue(PacketPropertyEvidence property) =>
        string.IsNullOrWhiteSpace(property.EvaluatedValue) ? property.RawValue : property.EvaluatedValue;

    private string NormalizePacketValue(string canonicalName, string value)
    {
        if (canonicalName == "Thickness")
        {
            var thickness = _normalization.NormalizeThickness(value, _options);
            if (thickness.IsSupported)
            {
                return thickness.Inches.ToString("G29", System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        return _normalization.NormalizeValue(canonicalName, value, _options);
    }

    private static EvidenceRef[] OrderedPacketEvidence(IEnumerable<PacketPropertyEvidence> properties) =>
        properties.Select(item => item.Evidence)
            .OrderBy(item => item.EvidenceId, StringComparer.Ordinal)
            .ThenBy(item => item.RawValue, StringComparer.Ordinal)
            .ThenBy(item => item.EvaluatedValue, StringComparer.Ordinal)
            .ToArray();

    private PacketCadComparison PacketConflict(
        string canonicalName,
        IReadOnlyList<PacketPropertyEvidence> packetProperties,
        CadPropertySnapshot? cadProperty,
        PacketCadIdentityResolution identity)
    {
        var scope = cadProperty?.Scope ?? CadPropertyScope.Document;
        var limitations = new List<string> { "Multiple distinct normalized packet values exist; resolve the packet evidence conflict before comparing with CAD." };
        AddIdentityLimitation(identity, limitations);
        return new PacketCadComparison
        {
            ComparisonId = Id(canonicalName, scope),
            Category = $"property:{scope.ToString().ToLowerInvariant()}:{canonicalName}",
            PacketEvidence = OrderedPacketEvidence(packetProperties),
            CadEvidence = cadProperty == null ? Array.Empty<EvidenceRef>() : new[] { cadProperty.Evidence },
            Status = PacketCadComparisonStatus.Conflict,
            IdentityAuthority = identity.Authority,
            IsAuthoritative = false,
            NormalizationRuleId = "VIRA-PROPERTY-PACKET-CONFLICT-001",
            Confidence = 1.0,
            Severity = _severity.Classify(canonicalName, PacketCadComparisonStatus.Conflict, false),
            Limitations = limitations,
            RecommendedVerificationAction = $"Resolve the conflicting packet {canonicalName} evidence against approved engineering authority.",
            MutationBoundary = EngineeringMutationBoundary.ReadOnly
        };
    }

    private PacketCadComparison Missing(
        string canonicalName,
        IReadOnlyList<PacketPropertyEvidence>? packetProperties,
        CadPropertySnapshot? cadProperty,
        PacketCadComparisonStatus status,
        PacketCadIdentityResolution identity)
    {
        var authoritative = identity.IsAuthoritative;
        var limitations = new List<string>();
        AddIdentityLimitation(identity, limitations);
        return new PacketCadComparison
        {
            ComparisonId = Id(canonicalName, cadProperty?.Scope ?? CadPropertyScope.Document),
            Category = $"property:{(cadProperty?.Scope ?? CadPropertyScope.Document).ToString().ToLowerInvariant()}:{canonicalName}",
            PacketEvidence = packetProperties?.Select(item => item.Evidence).ToArray() ?? Array.Empty<EvidenceRef>(),
            CadEvidence = cadProperty == null ? Array.Empty<EvidenceRef>() : new[] { cadProperty.Evidence },
            Status = status,
            IdentityAuthority = identity.Authority,
            IsAuthoritative = authoritative,
            NormalizationRuleId = "VIRA-PROPERTY-PRESENCE-001",
            Confidence = 1.0,
            Severity = _severity.Classify(canonicalName, status, authoritative),
            Limitations = limitations,
            RecommendedVerificationAction = status == PacketCadComparisonStatus.MissingInCad
                ? $"Inspect the active CAD document for the missing {canonicalName} evidence."
                : $"Inspect the packet for the missing {canonicalName} evidence.",
            MutationBoundary = EngineeringMutationBoundary.ReadOnly
        };
    }

    private static void AddIdentityLimitation(PacketCadIdentityResolution identity, ICollection<string> limitations)
    {
        if (!identity.IsAuthoritative)
        {
            limitations.Add("Packet-to-CAD identity is not confirmed; this comparison is non-authoritative.");
        }
    }

    private static string Id(string canonicalName, CadPropertyScope scope) =>
        $"CMP-PROPERTY-{scope.ToString().ToUpperInvariant()}-{canonicalName.ToUpperInvariant()}";

    private static string Recommended(string canonicalName, PacketCadComparisonStatus status) => status switch
    {
        PacketCadComparisonStatus.Conflict => $"Verify the {canonicalName} discrepancy against approved engineering authority.",
        PacketCadComparisonStatus.ProbableMatch => $"Resolve and verify the CAD {canonicalName} value without changing document state.",
        PacketCadComparisonStatus.Unsupported => $"Provide an approved rule or supported {canonicalName} evidence before comparison.",
        PacketCadComparisonStatus.InsufficientEvidence => $"Repeat the read-only {canonicalName} inspection after the evidence source is available.",
        _ => $"Review the linked {canonicalName} evidence before an engineering decision."
    };
}
