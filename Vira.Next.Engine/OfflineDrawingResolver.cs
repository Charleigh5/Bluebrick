using Vira.Next.Contracts;

namespace Vira.Next.Engine;

public sealed class OfflineDrawingResolver
{
    private readonly NormalizationRegistry _normalization;

    public OfflineDrawingResolver()
        : this(new NormalizationRegistry())
    {
    }

    public OfflineDrawingResolver(NormalizationRegistry normalization)
    {
        _normalization = normalization ?? throw new ArgumentNullException(nameof(normalization));
    }

    public DrawingResolutionResult Resolve(
        DrawingResolutionRequest request,
        IReadOnlyList<ControlledDrawingRecord> catalog)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(catalog);

        var identifiers = request.Identifiers ?? Array.Empty<DrawingIdentifierCandidate>();
        var partResolution = ResolveIdentifier(identifiers, DrawingIdentifierKind.PartNumber, request.PartNumber);
        var documentResolution = ResolveIdentifier(identifiers, DrawingIdentifierKind.DocumentNumber, request.DocumentNumber);
        var part = partResolution.Identifier;
        var document = documentResolution.Identifier;
        var records = catalog.Where(record => record != null).ToArray();
        var partMatches = part == null
            ? Array.Empty<ControlledDrawingRecord>()
            : records.Where(record => Same(record.PartNumber, part.NormalizedValue)).ToArray();
        var documentMatches = document == null
            ? Array.Empty<ControlledDrawingRecord>()
            : records.Where(record => Same(record.DocumentNumber, document.NormalizedValue)).ToArray();

        var candidates = part != null && document != null
            ? records.Where(record =>
                Same(record.PartNumber, part.NormalizedValue) &&
                Same(record.DocumentNumber, document.NormalizedValue)).ToArray()
            : (partMatches.Length > 0 ? partMatches : documentMatches)
                .ToArray();

        DrawingResolutionStatus status;
        ControlledDrawingRecord? resolved = null;
        var limitations = new List<string>();

        if (partResolution.Limitation != null || documentResolution.Limitation != null)
        {
            status = DrawingResolutionStatus.IdentityConflict;
            if (partResolution.Limitation != null)
            {
                limitations.Add(partResolution.Limitation);
            }

            if (documentResolution.Limitation != null)
            {
                limitations.Add(documentResolution.Limitation);
            }
        }
        else if (part != null && document != null && partMatches.Length > 0 && documentMatches.Length > 0 && candidates.Length == 0)
        {
            status = DrawingResolutionStatus.IdentityConflict;
            limitations.Add("Part and document identifiers resolve to different controlled records.");
        }
        else if (candidates.Length > 1)
        {
            status = DrawingResolutionStatus.Ambiguous;
            limitations.Add("More than one controlled record matches the requested identity.");
        }
        else if (candidates.Length == 1 && part != null && document != null)
        {
            status = DrawingResolutionStatus.ExactMatch;
            resolved = candidates[0];
        }
        else if (candidates.Length == 1 && (part != null || document != null))
        {
            status = DrawingResolutionStatus.PartialMatch;
            resolved = candidates[0];
            limitations.Add("Only one identifier was supplied; the controlled pair is not fully proven.");
        }
        else
        {
            status = DrawingResolutionStatus.Unresolved;
            limitations.Add(part == null && document == null
                ? "No explicit part-number or document-number field was supplied."
                : "The catalog cannot prove the requested identity.");
        }

        return new DrawingResolutionResult
        {
            Status = status,
            OpportunityId = request.OpportunityId,
            TaskId = request.TaskId,
            Customer = request.Customer,
            Project = request.Project,
            SourceRole = request.SourceRole,
            EffectiveRole = string.IsNullOrWhiteSpace(resolved?.Role) ? request.SourceRole : resolved.Role,
            RoleAuthority = string.IsNullOrWhiteSpace(resolved?.Role) ? request.RoleAuthority : resolved.RoleAuthority,
            PartIdentifier = part,
            DocumentIdentifier = document,
            ResolvedRecord = resolved,
            Candidates = candidates,
            ReadOnlyPolicy = resolved?.ReadOnlyPolicy ?? DrawingReadOnlyPolicy.ReadOnlyRequired,
            ExternalSystemsAccessed = false,
            MutationActions = 0,
            Limitations = limitations
        };
    }

    private bool Same(string left, string right) =>
        !string.IsNullOrWhiteSpace(left) && _normalization.NormalizeIdentifier(left) == right;

    private IdentifierResolution ResolveIdentifier(
        IReadOnlyList<DrawingIdentifierCandidate> identifiers,
        DrawingIdentifierKind kind,
        string explicitValue)
    {
        var typedCandidates = identifiers
            .Where(item => item is not null && item.Kind == kind && !string.IsNullOrWhiteSpace(item.RawValue))
            .Select(item => new ResolvedDrawingIdentifier
            {
                Kind = item.Kind,
                RawValue = item.RawValue,
                NormalizedValue = _normalization.NormalizeIdentifier(item.RawValue),
                Authority = item.Authority,
                EvidenceId = item.EvidenceId,
                SourceField = item.SourceField ?? string.Empty,
                SourceLabel = item.SourceLabel ?? string.Empty,
                SourceType = item.SourceType,
                SourceArtifactId = item.SourceArtifactId ?? string.Empty,
                SourceArtifactSha256 = item.SourceArtifactSha256 ?? string.Empty,
                SourceRecordId = item.SourceRecordId ?? string.Empty,
                SourcePageNumber = item.SourcePageNumber,
                SourceRegion = item.SourceRegion ?? string.Empty,
                ExtractionMethod = item.ExtractionMethod ?? string.Empty,
                EvidenceStatus = item.EvidenceStatus ?? string.Empty,
                Confidence = item.Confidence
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.NormalizedValue))
            .DistinctBy(item => item.NormalizedValue, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var explicitIdentifier = string.IsNullOrWhiteSpace(explicitValue)
            ? null
            : new ResolvedDrawingIdentifier
            {
                Kind = kind,
                RawValue = explicitValue,
                NormalizedValue = _normalization.NormalizeIdentifier(explicitValue),
                Authority = DrawingEvidenceAuthority.SourceDerived,
                SourceField = kind.ToString()
            };

        if (typedCandidates.Length > 1)
        {
            return new IdentifierResolution(null, $"Multiple distinct {kind} candidates disagree after normalization.");
        }

        if (explicitIdentifier != null && typedCandidates.Length == 1 &&
            !string.Equals(explicitIdentifier.NormalizedValue, typedCandidates[0].NormalizedValue, StringComparison.OrdinalIgnoreCase))
        {
            return new IdentifierResolution(null, $"Explicit {kind} field disagrees with the typed identifier candidate.");
        }

        return new IdentifierResolution(typedCandidates.FirstOrDefault() ?? explicitIdentifier, null);
    }

    private sealed record IdentifierResolution(ResolvedDrawingIdentifier? Identifier, string? Limitation);
}
