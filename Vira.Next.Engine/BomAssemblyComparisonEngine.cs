using Vira.Next.Contracts;

namespace Vira.Next.Engine;

public sealed class BomAssemblyComparisonEngine
{
    private readonly ComparisonSeverityPolicy _severity = new();

    public BomAssemblyComparisonReport Compare(
        PacketEvidenceSnapshot packet,
        CadDocumentSnapshot cad,
        PacketCadIdentityResolution identity)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(cad);
        ArgumentNullException.ThrowIfNull(identity);

        var results = new List<PacketCadComparison>();
        var packetRows = packet.BomRows
            .Where(row => !string.IsNullOrWhiteSpace(row.Identifier))
            .ToArray();
        var controlledRows = packetRows
            .Where(row => row.Responsibility is PacketBomResponsibility.Controlled or PacketBomResponsibility.Unknown)
            .ToArray();
        var responsibilityRows = packetRows.Except(controlledRows).ToArray();
        var packetGroups = controlledRows
            .GroupBy(row => Normalize(row.Identifier), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var cadGroups = cad.Components
            .Where(component => !string.IsNullOrWhiteSpace(component.IdentifierCandidate))
            .Where(component => component.Kind != CadComponentKind.Assembly || packetGroups.ContainsKey(Normalize(component.IdentifierCandidate)))
            .GroupBy(component => Normalize(component.IdentifierCandidate), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var componentById = cad.Components
            .Where(item => !string.IsNullOrWhiteSpace(item.SnapshotId))
            .GroupBy(item => item.SnapshotId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        foreach (var row in responsibilityRows.OrderBy(item => item.ItemNumber, StringComparer.Ordinal))
        {
            results.Add(Result(
                $"CMP-BOM-RESPONSIBILITY-{Normalize(row.Identifier)}",
                "bom:responsibility",
                new[] { row.Evidence },
                Array.Empty<EvidenceRef>(),
                PacketCadComparisonStatus.NotApplicable,
                identity,
                "VIRA-BOM-RESPONSIBILITY-001",
                new[] { $"{row.Responsibility} is a semantic responsibility label, not proof that CAD evidence is missing." }));
        }

        foreach (var identifier in packetGroups.Keys.Union(cadGroups.Keys, StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal))
        {
            packetGroups.TryGetValue(identifier, out var rows);
            cadGroups.TryGetValue(identifier, out var components);
            rows ??= Array.Empty<PacketBomRowEvidence>();
            components ??= Array.Empty<CadComponentSnapshot>();
            var packetEvidence = rows.Select(item => item.Evidence).ToArray();
            var cadEvidence = components.Select(item => item.Evidence).ToArray();

            if (rows.Length == 0)
            {
                results.Add(Result($"CMP-BOM-MEMBERSHIP-{identifier}", "bom:membership", packetEvidence, cadEvidence,
                    PacketCadComparisonStatus.MissingInPdf, identity, "VIRA-BOM-MEMBERSHIP-001"));
                continue;
            }

            if (components.Length == 0)
            {
                results.Add(Result($"CMP-BOM-MEMBERSHIP-{identifier}", "bom:membership", packetEvidence, cadEvidence,
                    PacketCadComparisonStatus.MissingInCad, identity, "VIRA-BOM-MEMBERSHIP-001"));
                continue;
            }

            var unresolved = components.Where(item => item.ResolutionState != CadComponentResolutionState.Resolved).ToArray();
            if (unresolved.Length > 0)
            {
                results.Add(Result(
                    $"CMP-BOM-UNRESOLVED-{identifier}",
                    "bom:resolution",
                    packetEvidence,
                    unresolved.Select(item => item.Evidence).ToArray(),
                    PacketCadComparisonStatus.UnresolvedEvidence,
                    identity with { IsAuthoritative = false },
                    "VIRA-BOM-RESOLUTION-001",
                    unresolved.SelectMany(item => item.Limitations)
                        .Append("The component remains visible but unresolved evidence cannot support an authoritative match.")
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray()));
                continue;
            }

            results.Add(Result($"CMP-BOM-MEMBERSHIP-{identifier}", "bom:membership", packetEvidence, cadEvidence,
                PacketCadComparisonStatus.ExactMatch, identity, "VIRA-BOM-MEMBERSHIP-001"));

            if (rows.Length > 1)
            {
                results.Add(Result($"CMP-BOM-DUPLICATE-{identifier}", "bom:duplicate", packetEvidence, cadEvidence,
                    PacketCadComparisonStatus.Duplicate, identity, "VIRA-BOM-DUPLICATE-001"));
            }

            var expectedQuantity = rows.Sum(item => item.Quantity ?? 0m);
            var observedQuantity = components.Sum(item => Math.Max(1, item.InstanceCount));
            results.Add(Result($"CMP-BOM-QUANTITY-{identifier}", "bom:quantity", packetEvidence, cadEvidence,
                expectedQuantity == observedQuantity ? PacketCadComparisonStatus.ExactMatch : PacketCadComparisonStatus.QuantityConflict,
                identity, "VIRA-BOM-QUANTITY-001"));

            CompareHierarchy(identifier, rows, components, componentById, identity, results);
            CompareConfiguration(identifier, rows, components, identity, results);
            CompareTextProperty(identifier, "description", rows.Select(item => item.Description), "Description", packetEvidence, components, identity, results);
            CompareTextProperty(identifier, "revision", rows.Select(item => item.Revision), "Revision", packetEvidence, components, identity, results);
        }

        var packetIds = packetGroups.Keys.ToHashSet(StringComparer.Ordinal);
        var cadControlledIds = cadGroups
            .Where(pair => pair.Value.Any(item => item.Kind != CadComponentKind.Assembly) || packetIds.Contains(pair.Key))
            .Select(pair => pair.Key)
            .ToHashSet(StringComparer.Ordinal);
        var matched = packetIds.Intersect(cadControlledIds, StringComparer.Ordinal).Count();

        return new BomAssemblyComparisonReport
        {
            Comparisons = results,
            Scorecard = new BomAssemblyMatchScorecard
            {
                PacketControlledIdentifiers = packetIds.Count,
                CadControlledIdentifiers = cadControlledIds.Count,
                MatchedIdentifiers = matched,
                Precision = cadControlledIds.Count == 0 ? (packetIds.Count == 0 ? 1.0 : 0.0) : (double)matched / cadControlledIds.Count,
                Recall = packetIds.Count == 0 ? 1.0 : (double)matched / packetIds.Count
            },
            Limitations = new AssemblyHierarchyValidator().Validate(cad).Limitations,
            MutationActions = 0,
            ExternalSystemsAccessed = false
        };
    }

    private void CompareHierarchy(
        string identifier,
        IReadOnlyList<PacketBomRowEvidence> rows,
        IReadOnlyList<CadComponentSnapshot> components,
        IReadOnlyDictionary<string, CadComponentSnapshot> componentById,
        PacketCadIdentityResolution identity,
        ICollection<PacketCadComparison> results)
    {
        var expected = rows.Select(item => Normalize(item.ParentIdentifierCandidate)).FirstOrDefault(item => item.Length > 0);
        if (string.IsNullOrEmpty(expected))
        {
            return;
        }

        var observed = components.Select(item => componentById.TryGetValue(item.ParentSnapshotId, out var parent)
                ? Normalize(parent.IdentifierCandidate)
                : string.Empty)
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var status = observed.Length > 0 && observed.All(item => item == expected)
            ? PacketCadComparisonStatus.ExactMatch
            : PacketCadComparisonStatus.HierarchyConflict;
        results.Add(Result($"CMP-BOM-HIERARCHY-{identifier}", "bom:hierarchy", rows.Select(item => item.Evidence).ToArray(),
            components.Select(item => item.Evidence).ToArray(), status, identity, "VIRA-BOM-HIERARCHY-001"));
    }

    private void CompareConfiguration(
        string identifier,
        IReadOnlyList<PacketBomRowEvidence> rows,
        IReadOnlyList<CadComponentSnapshot> components,
        PacketCadIdentityResolution identity,
        ICollection<PacketCadComparison> results)
    {
        var expected = rows.Select(item => item.ReferencedConfiguration.Trim()).FirstOrDefault(item => item.Length > 0);
        if (string.IsNullOrEmpty(expected))
        {
            return;
        }

        var exact = components.All(item => string.Equals(item.ReferencedConfiguration.Trim(), expected, StringComparison.OrdinalIgnoreCase));
        results.Add(Result($"CMP-BOM-CONFIGURATION-{identifier}", "bom:configuration", rows.Select(item => item.Evidence).ToArray(),
            components.Select(item => item.Evidence).ToArray(), exact ? PacketCadComparisonStatus.ExactMatch : PacketCadComparisonStatus.ConfigurationGap,
            identity, "VIRA-BOM-CONFIGURATION-001"));
    }

    private void CompareTextProperty(
        string identifier,
        string category,
        IEnumerable<string> packetValues,
        string propertyName,
        IReadOnlyList<EvidenceRef> packetEvidence,
        IReadOnlyList<CadComponentSnapshot> components,
        PacketCadIdentityResolution identity,
        ICollection<PacketCadComparison> results)
    {
        var expected = packetValues.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item))?.Trim();
        if (string.IsNullOrWhiteSpace(expected))
        {
            return;
        }

        var properties = components.SelectMany(item => item.Properties)
            .Where(item => string.Equals(item.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var missingProperty = components.Any(component => !component.Properties.Any(item =>
            string.Equals(item.Name, propertyName, StringComparison.OrdinalIgnoreCase)));
        if (missingProperty || properties.Any(item => item.ReadStatus != CadPropertyReadStatus.Resolved || !item.WasResolved))
        {
            results.Add(Result($"CMP-BOM-{category.ToUpperInvariant()}-{identifier}", $"bom:{category}", packetEvidence,
                components.Select(item => item.Evidence).Concat(properties.Select(item => item.Evidence)).ToArray(),
                PacketCadComparisonStatus.InsufficientEvidence, identity,
                $"VIRA-BOM-{category.ToUpperInvariant()}-001",
                new[] { $"Required component {propertyName} evidence is missing or not successfully resolved; all components require a successful resolved property read before comparison." }));
            return;
        }
        var values = properties.Select(item => string.IsNullOrWhiteSpace(item.EvaluatedValue) ? item.RawValue : item.EvaluatedValue).ToArray();
        var exact = values.Length > 0 && values.All(item => string.Equals(item.Trim(), expected, StringComparison.OrdinalIgnoreCase));
        results.Add(Result($"CMP-BOM-{category.ToUpperInvariant()}-{identifier}", $"bom:{category}", packetEvidence,
            properties.Select(item => item.Evidence).ToArray(), exact ? PacketCadComparisonStatus.ExactMatch : PacketCadComparisonStatus.Conflict,
            identity, $"VIRA-BOM-{category.ToUpperInvariant()}-001"));
    }

    private PacketCadComparison Result(
        string id,
        string category,
        IReadOnlyList<EvidenceRef> packetEvidence,
        IReadOnlyList<EvidenceRef> cadEvidence,
        PacketCadComparisonStatus status,
        PacketCadIdentityResolution identity,
        string ruleId,
        IReadOnlyList<string>? limitations = null)
    {
        var authoritative = identity.IsAuthoritative &&
            status is not PacketCadComparisonStatus.UnresolvedEvidence and not PacketCadComparisonStatus.InsufficientEvidence;
        var allLimitations = (limitations ?? Array.Empty<string>()).ToList();
        if (!identity.IsAuthoritative)
        {
            allLimitations.Add("Packet-to-CAD identity is not confirmed; this comparison is non-authoritative.");
        }

        return new PacketCadComparison
        {
            ComparisonId = id,
            Category = category,
            PacketEvidence = packetEvidence,
            CadEvidence = cadEvidence,
            Status = status,
            IdentityAuthority = identity.Authority,
            IsAuthoritative = authoritative,
            NormalizationRuleId = ruleId,
            Confidence = 1.0,
            Severity = _severity.Classify(category, status, authoritative),
            Limitations = allLimitations.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            RecommendedVerificationAction = status is PacketCadComparisonStatus.ExactMatch or PacketCadComparisonStatus.NotApplicable
                ? "Review the linked packet and CAD evidence before an engineering decision."
                : "Inspect the linked packet and CAD sources without changing document state.",
            MutationBoundary = EngineeringMutationBoundary.ReadOnly
        };
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
