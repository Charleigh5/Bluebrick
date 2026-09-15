using System.Text.RegularExpressions;
using Vira.Next.Contracts;

namespace Vira.Next.Engine;

public sealed partial class PacketDrawingEvidenceMapper
{
    private static readonly Regex SupportedPartField = PartFieldRegex();
    private static readonly Regex SupportedDocumentField = DocumentFieldRegex();
    private static readonly HashSet<string> LabelOnlyPartFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "PART NO",
        "PART NUMBER",
        "PART_NO",
        "PART_NUMBER",
        "PARTNUMBER"
    };
    private static readonly HashSet<string> LabelOnlyDocumentFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "DOC NO",
        "DOC NUMBER",
        "DOC_NO",
        "DOC_NUMBER",
        "DOCUMENT NO",
        "DOCUMENT NUMBER",
        "DOCUMENT_NO",
        "DOCUMENT_NUMBER",
        "DRAWING NO",
        "DRAWING NUMBER",
        "DRAWING_NO",
        "DRAWING_NUMBER"
    };

    private readonly NormalizationRegistry _normalization;

    public PacketDrawingEvidenceMapper()
        : this(new NormalizationRegistry())
    {
    }

    public PacketDrawingEvidenceMapper(NormalizationRegistry normalization)
    {
        _normalization = normalization ?? throw new ArgumentNullException(nameof(normalization));
    }

    public DrawingResolutionMappingResult Map(PacketDrawingEvidenceSet evidenceSet)
    {
        ArgumentNullException.ThrowIfNull(evidenceSet);

        var warnings = new List<string>();
        var limitations = new List<string>();
        var mapped = new List<PacketDrawingMappedEvidence>();
        var requestCandidates = new List<DrawingIdentifierCandidate>();
        var eligibleIndexes = new List<int>();
        var identifiers = evidenceSet.Identifiers ?? Array.Empty<PacketDrawingIdentifierEvidence>();
        var duplicateEvidenceIds = FindDuplicateEvidenceIds(identifiers, limitations);
        var artifactManifest = BuildArtifactManifest(evidenceSet.Artifacts, limitations);

        if (evidenceSet.Identifiers is null)
        {
            limitations.Add("Identifier evidence collection was null; no identifier evidence was supplied.");
        }
        else if (identifiers.Count == 0)
        {
            limitations.Add("No packet drawing identifier evidence was supplied.");
        }

        for (var index = 0; index < identifiers.Count; index++)
        {
            var evidence = identifiers[index];
            if (evidence is null)
            {
                limitations.Add($"Identifier evidence at index {index} was null and was not used for resolution.");
                mapped.Add(new PacketDrawingMappedEvidence
                {
                    Evidence = new PacketDrawingIdentifierEvidence(),
                    ClassifiedKind = DrawingIdentifierKind.Unclassified,
                    IncludedInResolutionRequest = false
                });
                continue;
            }

            var effectiveEvidence = evidence;
            var artifactValid = ApplyArtifactManifest(
                evidence,
                artifactManifest,
                limitations,
                out effectiveEvidence);
            var kind = Classify(effectiveEvidence);
            var normalized = _normalization.NormalizeIdentifier(evidence.RawValue ?? string.Empty);
            var sourceTypeValid = Enum.IsDefined(typeof(PacketDrawingSourceType), evidence.SourceType);
            var evidenceStatusValid = Enum.IsDefined(typeof(PacketDrawingEvidenceStatus), evidence.Status);
            AddClassificationDisagreementLimitation(evidence, kind, limitations);
            var include = kind != DrawingIdentifierKind.Unclassified &&
                evidence.Status != PacketDrawingEvidenceStatus.Unsupported &&
                evidence.Status != PacketDrawingEvidenceStatus.Rejected &&
                sourceTypeValid &&
                evidenceStatusValid &&
                artifactValid &&
                !string.IsNullOrWhiteSpace(normalized);

            if (string.IsNullOrWhiteSpace(evidence.EvidenceId))
            {
                limitations.Add($"Evidence at index {index} has a blank EvidenceId and was not used for resolution.");
                include = false;
            }
            else if (duplicateEvidenceIds.Contains(evidence.EvidenceId.Trim()))
            {
                include = false;
            }

            if (string.IsNullOrWhiteSpace(evidence.RawValue))
            {
                warnings.Add($"Evidence '{EvidenceLabel(evidence)}' has an empty raw value and was not used for resolution.");
                include = false;
            }
            else if (kind == DrawingIdentifierKind.Unclassified)
            {
                limitations.Add($"Evidence '{EvidenceLabel(evidence)}' remains unclassified.");
            }
            else if (!sourceTypeValid)
            {
                limitations.Add($"Evidence '{EvidenceLabel(evidence)}' has unsupported source type value '{(int)evidence.SourceType}' and was not used for resolution.");
                include = false;
            }
            else if (!evidenceStatusValid)
            {
                limitations.Add($"Evidence '{EvidenceLabel(evidence)}' has unsupported evidence status value '{(int)evidence.Status}' and was not used for resolution.");
                include = false;
            }
            else if (evidence.Status is PacketDrawingEvidenceStatus.Unsupported or PacketDrawingEvidenceStatus.Rejected)
            {
                limitations.Add($"Evidence '{EvidenceLabel(evidence)}' has status {evidence.Status} and was not used for resolution.");
                include = false;
            }

            mapped.Add(new PacketDrawingMappedEvidence
            {
                Evidence = effectiveEvidence,
                ClassifiedKind = kind,
                NormalizedValue = normalized,
                IncludedInResolutionRequest = false
            });

            if (include)
            {
                eligibleIndexes.Add(mapped.Count - 1);
            }
        }

        var equivalentGroups = eligibleIndexes
            .GroupBy(index => CanonicalKey(mapped[index]), StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var group in equivalentGroups)
        {
            var representativeIndex = OrderRepresentative(group, mapped).First();
            mapped[representativeIndex] = mapped[representativeIndex] with
            {
                IncludedInResolutionRequest = true
            };
            var representative = mapped[representativeIndex];
            requestCandidates.Add(ToCandidate(representative.Evidence, representative.ClassifiedKind));

            if (group.Count() > 1)
            {
                warnings.Add($"Equivalent duplicate {representative.ClassifiedKind} evidence was preserved; representative '{EvidenceLabel(representative.Evidence)}' was selected deterministically for the resolution request.");
            }
        }

        var partValue = SelectExplicitValue(mapped, DrawingIdentifierKind.PartNumber, warnings, limitations);
        var documentValue = SelectExplicitValue(mapped, DrawingIdentifierKind.DocumentNumber, warnings, limitations);
        WarnOnCrossRecordPair(mapped, partValue, documentValue, limitations);

        if (evidenceSet.RoleAuthority == DrawingRoleAuthority.ControlledEvidence)
        {
            warnings.Add("Input role authority ControlledEvidence was not propagated; packet mapping forces SourceDerived authority.");
            limitations.Add("Source-derived packet evidence cannot establish controlled authority; controlled authority is reserved for controlled records.");
        }

        return new DrawingResolutionMappingResult
        {
            Request = new DrawingResolutionRequest
            {
                OpportunityId = evidenceSet.OpportunityId,
                TaskId = evidenceSet.TaskId,
                Customer = evidenceSet.Customer,
                Project = evidenceSet.Project,
                SourceRole = evidenceSet.SourceRole,
                RoleAuthority = DrawingRoleAuthority.SourceDerived,
                PartNumber = partValue,
                DocumentNumber = documentValue,
                Identifiers = requestCandidates
            },
            Evidence = SortMappedEvidence(mapped),
            Warnings = warnings.Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            Limitations = limitations.Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            ExternalSystemsAccessed = false,
            MutationActions = 0
        };
    }

    private DrawingIdentifierKind Classify(PacketDrawingIdentifierEvidence evidence)
    {
        var sourceField = evidence.SourceField ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(sourceField))
        {
            return ClassifySourceField(sourceField);
        }

        return ClassifyLabelOnly(evidence.SourceLabel);
    }

    private static DrawingIdentifierKind ClassifySourceField(string sourceField)
    {
        if (SupportedPartField.IsMatch(sourceField))
        {
            return DrawingIdentifierKind.PartNumber;
        }

        if (SupportedDocumentField.IsMatch(sourceField))
        {
            return DrawingIdentifierKind.DocumentNumber;
        }

        return DrawingIdentifierKind.Unclassified;
    }

    private static DrawingIdentifierKind ClassifyLabelOnly(string? sourceLabel)
    {
        var normalizedLabel = NormalizeLabelOnly(sourceLabel);
        if (LabelOnlyPartFields.Contains(normalizedLabel))
        {
            return DrawingIdentifierKind.PartNumber;
        }

        if (LabelOnlyDocumentFields.Contains(normalizedLabel))
        {
            return DrawingIdentifierKind.DocumentNumber;
        }

        return DrawingIdentifierKind.Unclassified;
    }

    private static string NormalizeLabelOnly(string? sourceLabel) =>
        Regex.Replace(sourceLabel?.Trim() ?? string.Empty, @"\s+", " ").ToUpperInvariant();

    private static void AddClassificationDisagreementLimitation(
        PacketDrawingIdentifierEvidence evidence,
        DrawingIdentifierKind classifiedKind,
        ICollection<string> limitations)
    {
        var sourceField = evidence.SourceField ?? string.Empty;
        var sourceFieldKind = string.IsNullOrWhiteSpace(sourceField)
            ? DrawingIdentifierKind.Unclassified
            : ClassifySourceField(sourceField);
        var labelKind = ClassifyLabelOnly(evidence.SourceLabel);
        var hintKind = evidence.KindHint is DrawingIdentifierKind.PartNumber or DrawingIdentifierKind.DocumentNumber
            ? evidence.KindHint
            : DrawingIdentifierKind.Unclassified;

        var knownKinds = new[] { sourceFieldKind, labelKind, hintKind }
            .Where(kind => kind != DrawingIdentifierKind.Unclassified)
            .Distinct()
            .ToArray();
        if (knownKinds.Length < 2 || knownKinds.Length == 1 && knownKinds[0] == classifiedKind)
        {
            return;
        }

        var basis = !string.IsNullOrWhiteSpace(sourceField) && sourceFieldKind != DrawingIdentifierKind.Unclassified
            ? "SourceField"
            : labelKind != DrawingIdentifierKind.Unclassified
                ? "SourceLabel"
                : "no supported field or label";
        limitations.Add($"Evidence '{EvidenceLabel(evidence)}' has a material field/label/hint disagreement; {basis} classification was authoritative.");
    }

    private string SelectExplicitValue(
        IReadOnlyList<PacketDrawingMappedEvidence> mapped,
        DrawingIdentifierKind kind,
        ICollection<string> warnings,
        ICollection<string> limitations)
    {
        var values = mapped
            .Where(item => item.IncludedInResolutionRequest && item.ClassifiedKind == kind)
            .GroupBy(item => item.NormalizedValue, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (values.Length == 0)
        {
            return string.Empty;
        }

        if (values.Length > 1)
        {
            limitations.Add($"Multiple distinct {kind} values were observed; resolver must fail closed on typed candidates.");
            return string.Empty;
        }

        var ordered = values[0]
            .OrderByDescending(item => item.Evidence.Confidence ?? 0)
            .ThenBy(item => item.Evidence.EvidenceId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Length > 1)
        {
            warnings.Add($"Equivalent {kind} evidence was normalized to '{values[0].Key}'.");
        }

        return ordered[0].Evidence.RawValue;
    }

    private static void WarnOnCrossRecordPair(
        IReadOnlyList<PacketDrawingMappedEvidence> mapped,
        string partValue,
        string documentValue,
        ICollection<string> limitations)
    {
        if (string.IsNullOrWhiteSpace(partValue) || string.IsNullOrWhiteSpace(documentValue))
        {
            return;
        }

        var partRecords = SourceRecords(mapped, DrawingIdentifierKind.PartNumber);
        var documentRecords = SourceRecords(mapped, DrawingIdentifierKind.DocumentNumber);
        if (partRecords.Count == 0 || documentRecords.Count == 0)
        {
            return;
        }

        if (!partRecords.Overlaps(documentRecords))
        {
            limitations.Add("Part-number and document-number evidence came from different source records; controlled resolution must prove the relationship.");
        }
    }

    private static HashSet<string> SourceRecords(
        IEnumerable<PacketDrawingMappedEvidence> mapped,
        DrawingIdentifierKind kind) =>
        mapped
            .Where(item => item.IncludedInResolutionRequest && item.ClassifiedKind == kind)
            .Select(item => item.Evidence.SourceRecordId)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToHashSet(StringComparer.Ordinal);

    private static DrawingIdentifierCandidate ToCandidate(
        PacketDrawingIdentifierEvidence evidence,
        DrawingIdentifierKind kind) => new()
    {
        Kind = kind,
        RawValue = evidence.RawValue ?? string.Empty,
        Authority = DrawingEvidenceAuthority.SourceDerived,
        EvidenceId = evidence.EvidenceId ?? string.Empty,
        SourceField = evidence.SourceField ?? string.Empty,
        SourceLabel = evidence.SourceLabel ?? string.Empty,
        SourceType = evidence.SourceType,
        SourceArtifactId = evidence.SourceArtifactId ?? string.Empty,
        SourceArtifactSha256 = evidence.SourceArtifactSha256 ?? string.Empty,
        SourceRecordId = evidence.SourceRecordId ?? string.Empty,
        SourcePageNumber = evidence.PageNumber,
        SourceRegion = evidence.Region ?? string.Empty,
        ExtractionMethod = evidence.ExtractionMethod ?? string.Empty,
        EvidenceStatus = evidence.Status.ToString(),
        Confidence = evidence.Confidence
    };

    private static string EvidenceLabel(PacketDrawingIdentifierEvidence evidence) =>
        string.IsNullOrWhiteSpace(evidence.EvidenceId)
            ? evidence.RawValue ?? string.Empty
            : evidence.EvidenceId;

    private static string CanonicalKey(PacketDrawingMappedEvidence item) =>
        $"{(int)item.ClassifiedKind}|{item.NormalizedValue ?? string.Empty}";

    private static IEnumerable<int> OrderRepresentative(
        IEnumerable<int> indexes,
        IReadOnlyList<PacketDrawingMappedEvidence> mapped) => indexes
        .OrderByDescending(index => mapped[index].Evidence.Confidence ?? double.MinValue)
        .ThenBy(index => Text(mapped[index].Evidence.SourceArtifactId), StringComparer.Ordinal)
        .ThenBy(index => Text(mapped[index].Evidence.SourceArtifactSha256), StringComparer.Ordinal)
        .ThenBy(index => mapped[index].Evidence.PageNumber ?? int.MaxValue)
        .ThenBy(index => Text(mapped[index].Evidence.Region), StringComparer.Ordinal)
        .ThenBy(index => Text(mapped[index].Evidence.SourceField), StringComparer.Ordinal)
        .ThenBy(index => Text(mapped[index].Evidence.SourceLabel), StringComparer.Ordinal)
        .ThenBy(index => Text(mapped[index].Evidence.RawValue), StringComparer.Ordinal)
        .ThenBy(index => Text(mapped[index].Evidence.EvidenceId), StringComparer.Ordinal)
        .ThenBy(index => Text(mapped[index].Evidence.SourceRecordId), StringComparer.Ordinal)
        .ThenBy(index => Text(mapped[index].Evidence.ExtractionMethod), StringComparer.Ordinal)
        .ThenBy(index => (int)mapped[index].Evidence.KindHint)
        .ThenBy(index => Text(mapped[index].Evidence.Status.ToString()), StringComparer.Ordinal)
        .ThenBy(index => (int)mapped[index].Evidence.SourceType);

    private static IReadOnlyList<PacketDrawingMappedEvidence> SortMappedEvidence(
        IEnumerable<PacketDrawingMappedEvidence> mapped) => mapped
        .OrderBy(item => item.ClassifiedKind)
        .ThenBy(item => Text(item.NormalizedValue), StringComparer.Ordinal)
        .ThenByDescending(item => item.IncludedInResolutionRequest)
        .ThenBy(item => (int)item.Evidence.SourceType)
        .ThenBy(item => Text(item.Evidence.SourceArtifactId), StringComparer.Ordinal)
        .ThenBy(item => Text(item.Evidence.SourceArtifactSha256), StringComparer.Ordinal)
        .ThenBy(item => item.Evidence.PageNumber ?? int.MaxValue)
        .ThenBy(item => Text(item.Evidence.Region), StringComparer.Ordinal)
        .ThenBy(item => Text(item.Evidence.SourceField), StringComparer.Ordinal)
        .ThenBy(item => Text(item.Evidence.SourceLabel), StringComparer.Ordinal)
        .ThenBy(item => Text(item.Evidence.RawValue), StringComparer.Ordinal)
        .ThenBy(item => Text(item.Evidence.EvidenceId), StringComparer.Ordinal)
        .ThenBy(item => Text(item.Evidence.SourceRecordId), StringComparer.Ordinal)
        .ThenBy(item => Text(item.Evidence.ExtractionMethod), StringComparer.Ordinal)
        .ThenBy(item => (int)item.Evidence.KindHint)
        .ThenBy(item => Text(item.Evidence.Status.ToString()), StringComparer.Ordinal)
        .ThenBy(item => item.Evidence.Confidence ?? double.MinValue)
        .ToArray();

    private static string Text(string? value) => value ?? string.Empty;

    private static HashSet<string> FindDuplicateEvidenceIds(
        IReadOnlyList<PacketDrawingIdentifierEvidence> identifiers,
        ICollection<string> limitations)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var duplicates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var evidence in identifiers)
        {
            if (evidence is null || string.IsNullOrWhiteSpace(evidence.EvidenceId))
            {
                continue;
            }

            var evidenceId = evidence.EvidenceId.Trim();
            if (!seen.Add(evidenceId))
            {
                duplicates.Add(evidenceId);
            }
        }

        foreach (var evidenceId in duplicates.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
        {
            limitations.Add($"EvidenceId '{evidenceId}' is duplicated; all evidence with that ID was excluded from resolution.");
        }

        return duplicates;
    }

    private static ArtifactManifest BuildArtifactManifest(
        IReadOnlyList<PacketDrawingArtifact>? artifacts,
        ICollection<string> limitations)
    {
        if (artifacts is null || artifacts.Count == 0)
        {
            return ArtifactManifest.NotSupplied;
        }

        var valid = new Dictionary<string, PacketDrawingArtifact>(StringComparer.OrdinalIgnoreCase);
        var duplicateIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < artifacts.Count; index++)
        {
            var artifact = artifacts[index];
            if (artifact is null)
            {
                limitations.Add($"Artifact manifest entry at index {index} was null and cannot be referenced.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(artifact.ArtifactId))
            {
                limitations.Add($"Artifact manifest entry at index {index} has a blank ArtifactId and cannot be referenced.");
                continue;
            }

            var artifactId = artifact.ArtifactId.Trim();
            if (valid.ContainsKey(artifactId))
            {
                duplicateIds.Add(artifactId);
                continue;
            }

            valid[artifactId] = artifact;
        }

        foreach (var artifactId in duplicateIds.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
        {
            valid.Remove(artifactId);
            limitations.Add($"ArtifactId '{artifactId}' is duplicated in the supplied artifact manifest; linked evidence was excluded.");
        }

        return new ArtifactManifest(true, valid, duplicateIds);
    }

    private static bool ApplyArtifactManifest(
        PacketDrawingIdentifierEvidence evidence,
        ArtifactManifest manifest,
        ICollection<string> limitations,
        out PacketDrawingIdentifierEvidence effectiveEvidence)
    {
        effectiveEvidence = evidence;
        if (!manifest.IsSupplied)
        {
            return true;
        }

        var artifactId = evidence.SourceArtifactId?.Trim() ?? string.Empty;
        var directHash = evidence.SourceArtifactSha256?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(artifactId))
        {
            if (string.IsNullOrWhiteSpace(directHash))
            {
                limitations.Add($"Evidence '{EvidenceLabel(evidence)}' has no source artifact reference while an artifact manifest is supplied and was not used for resolution.");
                return false;
            }

            limitations.Add($"Evidence '{EvidenceLabel(evidence)}' has no source artifact reference; direct artifact hash metadata was retained.");
            return true;
        }

        if (manifest.DuplicateIds.Contains(artifactId))
        {
            limitations.Add($"Evidence '{EvidenceLabel(evidence)}' references duplicated ArtifactId '{artifactId}' and was not used for resolution.");
            return false;
        }

        if (!manifest.ValidArtifacts.TryGetValue(artifactId, out var artifact))
        {
            limitations.Add($"Evidence '{EvidenceLabel(evidence)}' source artifact '{artifactId}' was not found in the supplied artifact manifest and was not used for resolution.");
            return false;
        }

        if (evidence.SourceType != artifact.SourceType)
        {
            limitations.Add($"Evidence '{EvidenceLabel(evidence)}' source type does not match artifact manifest source type (evidence='{evidence.SourceType}', manifest='{artifact.SourceType}') and was not used for resolution.");
            return false;
        }

        var manifestHash = artifact.Sha256?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(manifestHash) &&
            !string.IsNullOrWhiteSpace(directHash) &&
            !string.Equals(manifestHash, directHash, StringComparison.OrdinalIgnoreCase))
        {
            limitations.Add($"Evidence '{EvidenceLabel(evidence)}' artifact hash does not match the supplied artifact manifest and was not used for resolution.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(directHash))
        {
            if (string.IsNullOrWhiteSpace(manifestHash))
            {
                limitations.Add($"Evidence '{EvidenceLabel(evidence)}' references an artifact manifest entry without a hash and was not used for resolution.");
                return false;
            }

            effectiveEvidence = evidence with { SourceArtifactSha256 = manifestHash };
        }
        else if (string.IsNullOrWhiteSpace(manifestHash))
        {
            limitations.Add($"Evidence '{EvidenceLabel(evidence)}' retained its direct artifact hash because the referenced manifest entry has no hash.");
        }

        return true;
    }

    private sealed record ArtifactManifest(
        bool IsSupplied,
        IReadOnlyDictionary<string, PacketDrawingArtifact> ValidArtifacts,
        IReadOnlySet<string> DuplicateIds)
    {
        public static ArtifactManifest NotSupplied { get; } = new(
            false,
            new Dictionary<string, PacketDrawingArtifact>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }

    [GeneratedRegex(@"(^|[._\-\s:])(PART\s+(NO|NUMBER)|PARTNUMBER|PART_NO|PART_NUMBER)($|[._\-\s:])", RegexOptions.IgnoreCase)]
    private static partial Regex PartFieldRegex();

    [GeneratedRegex(@"(^|[._\-\s:])((DOC|DOCUMENT|DRAWING)\s+(NO|NUMBER)|DOC_NO|DOC_NUMBER|DOCUMENT_NO|DOCUMENT_NUMBER|DRAWING_NO|DRAWING_NUMBER)($|[._\-\s:])", RegexOptions.IgnoreCase)]
    private static partial Regex DocumentFieldRegex();
}
