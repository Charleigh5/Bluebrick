using Vira.Next.Contracts;

namespace Vira.Next.Engine;

public sealed class PacketCadIdentityResolver
{
    private readonly NormalizationRegistry _normalization;

    public PacketCadIdentityResolver()
        : this(new NormalizationRegistry())
    {
    }

    public PacketCadIdentityResolver(NormalizationRegistry normalization)
    {
        _normalization = normalization;
    }

    public PacketCadIdentityResolution Resolve(PacketEvidenceSnapshot packet, CadDocumentSnapshot cad)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(cad);

        if (cad.State != CadSnapshotState.Ready)
        {
            return new PacketCadIdentityResolution
            {
                Authority = PacketCadIdentityAuthority.Unresolved,
                IsAuthoritative = false,
                PacketEvidence = packet.Identifiers.Select(item => item.Evidence).ToArray(),
                CadEvidence = Array.Empty<EvidenceRef>(),
                Limitations = new[] { string.IsNullOrWhiteSpace(cad.Message) ? "Active CAD evidence is unavailable." : cad.Message },
                MutationBoundary = EngineeringMutationBoundary.ReadOnly
            };
        }

        var packetIdentifiers = packet.Identifiers
            .Select(item => new { Item = item, Value = _normalization.NormalizeIdentifier(item.Identifier) })
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .ToArray();
        var identityProperties = cad.Properties
            .Where(item => _normalization.IsIdentityProperty(item.Name))
            .ToArray();
        var ineligibleProperties = identityProperties
            .Where(item => item.ReadStatus != CadPropertyReadStatus.Resolved || !item.WasResolved)
            .ToArray();
        var evidenceLimitations = ineligibleProperties.Select(item =>
            $"CAD identity property {item.Name} is not successfully resolved ({item.ReadStatus}, WasResolved={item.WasResolved}); it cannot corroborate or conflict with controlled identity.").ToArray();
        var cadIdentifiers = identityProperties
            .Where(item => item.ReadStatus == CadPropertyReadStatus.Resolved && item.WasResolved)
            .Select(item => new
            {
                Item = item,
                Value = _normalization.NormalizeIdentifier(string.IsNullOrWhiteSpace(item.EvaluatedValue) ? item.RawValue : item.EvaluatedValue)
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .ToArray();

        var propertyMatches = cadIdentifiers
            .Where(cadItem => packetIdentifiers.Any(packetItem => packetItem.Value == cadItem.Value))
            .ToArray();
        var titleMatch = packet.Identifiers.FirstOrDefault(item =>
            item.CandidateDocumentTitleHashes.Any(hash =>
                _normalization.NormalizeHash(hash) == _normalization.NormalizeHash(cad.TitleHash)));

        var matchSources = new List<string>();
        var packetEvidence = new List<EvidenceRef>();
        var cadEvidence = ineligibleProperties.Select(item => item.Evidence).ToList();

        if (propertyMatches.Length > 0)
        {
            matchSources.Add("CAD_CONTROLLED_PROPERTY");
            cadEvidence.AddRange(propertyMatches.Select(item => item.Item.Evidence));
            var matchedValues = propertyMatches.Select(item => item.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
            packetEvidence.AddRange(packetIdentifiers.Where(item => matchedValues.Contains(item.Value)).Select(item => item.Item.Evidence));
        }

        if (titleMatch != null)
        {
            matchSources.Add("DOCUMENT_TITLE_HASH");
            packetEvidence.Add(titleMatch.Evidence);
            cadEvidence.Add(new EvidenceRef
            {
                EvidenceId = $"{cad.SnapshotId}:title-hash",
                SourceKind = EvidenceSourceKind.CadDocument,
                SourceId = cad.SnapshotId,
                FieldName = "DocumentTitleHash",
                RawValue = cad.TitleHash,
                EvaluatedValue = cad.TitleHash,
                ExtractionMethod = "read-only-host-snapshot",
                RuleId = "VIRA-IDENTITY-TITLE-HASH-001",
                Authority = EvidenceAuthority.Observed,
                Confidence = 1.0,
                VerificationStatus = EvidenceVerificationStatus.Confirmed
            });
        }

        var distinctSources = matchSources.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (distinctSources.Length >= 2)
        {
            return Resolution(PacketCadIdentityAuthority.Confirmed, true, distinctSources, packetEvidence, cadEvidence, evidenceLimitations);
        }

        if (distinctSources.Length == 1)
        {
            return Resolution(
                PacketCadIdentityAuthority.StrongCandidate,
                false,
                distinctSources,
                packetEvidence,
                cadEvidence,
                evidenceLimitations.Append("Only one independent identity source matched; corroboration is required."));
        }

        if (packetIdentifiers.Length > 0 && cadIdentifiers.Length > 0)
        {
            return Resolution(
                PacketCadIdentityAuthority.Conflict,
                false,
                Array.Empty<string>(),
                packetIdentifiers.Select(item => item.Item.Evidence),
                cadIdentifiers.Select(item => item.Item.Evidence).Concat(cadEvidence),
                evidenceLimitations.Append("Comparable controlled packet evidence disagrees with the CAD identifier."));
        }

        return Resolution(
            PacketCadIdentityAuthority.Unresolved,
            false,
            Array.Empty<string>(),
            packetIdentifiers.Select(item => item.Item.Evidence),
            cadIdentifiers.Select(item => item.Item.Evidence).Concat(cadEvidence),
            evidenceLimitations.Append("Insufficient controlled identity evidence is available."));
    }

    private static PacketCadIdentityResolution Resolution(
        PacketCadIdentityAuthority authority,
        bool authoritative,
        IEnumerable<string> matchSources,
        IEnumerable<EvidenceRef> packetEvidence,
        IEnumerable<EvidenceRef> cadEvidence,
        IEnumerable<string> limitations)
    {
        return new PacketCadIdentityResolution
        {
            Authority = authority,
            IsAuthoritative = authoritative,
            MatchSources = matchSources.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            PacketEvidence = packetEvidence.DistinctBy(item => item.EvidenceId).ToArray(),
            CadEvidence = cadEvidence.DistinctBy(item => item.EvidenceId).ToArray(),
            Limitations = limitations.ToArray(),
            MutationBoundary = EngineeringMutationBoundary.ReadOnly
        };
    }
}
