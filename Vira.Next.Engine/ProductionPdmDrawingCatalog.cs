using System.IO;
using Vira.Next.Contracts;

namespace Vira.Next.Engine;

public sealed class ProductionPdmDrawingCatalog : IPdmDrawingCatalog
{
    private readonly IReadOnlyPdmSearchGateway _gateway;
    private readonly NormalizationRegistry _normalization;
    private readonly ProductionPdmDrawingCatalogOptions _options;

    public ProductionPdmDrawingCatalog(
        IReadOnlyPdmSearchGateway gateway,
        ProductionPdmDrawingCatalogOptions? options = null,
        NormalizationRegistry? normalization = null)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _options = options ?? new ProductionPdmDrawingCatalogOptions();
        _normalization = normalization ?? new NormalizationRegistry();
    }

    public ProductionPdmDrawingCatalogReceipt LastReceipt { get; private set; } = ProductionPdmDrawingCatalogReceipt.Empty;

    public async Task<IReadOnlyList<ControlledDrawingRecord>> FindCandidatesAsync(
        DrawingResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        LastReceipt = ProductionPdmDrawingCatalogReceipt.Empty;
        cancellationToken.ThrowIfCancellationRequested();

        var plannedQueries = BuildQueries(request);
        var attemptedQueries = new List<PdmSearchQuery>();
        if (plannedQueries.Count == 0)
        {
            LastReceipt = new ProductionPdmDrawingCatalogReceipt
            {
                PlannedQueries = Array.Empty<PdmSearchQuery>(),
                AttemptedQueries = Array.Empty<PdmSearchQuery>(),
                Limitations = Array.Empty<string>(),
                ReturnedCandidateCount = 0
            };

            return Array.Empty<ControlledDrawingRecord>();
        }

        var limitations = new List<string>();
        var mapped = new List<ControlledDrawingRecord>();

        try
        {
            foreach (var query in plannedQueries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                attemptedQueries.Add(query with { });
                var response = await _gateway.SearchAsync(query, cancellationToken).ConfigureAwait(false);
                AddLimitations(limitations, response.Limitations);

                if (response.Failure is not null)
                {
                    limitations.Add(
                        $"PDM search failure {response.Failure.Code}: {response.Failure.Message}");
                    return Complete(plannedQueries, attemptedQueries, limitations, Array.Empty<ControlledDrawingRecord>());
                }

                var supportedFields = response.SupportedFields
                    .Distinct()
                    .ToHashSet();
                if (!supportedFields.Contains(query.Field))
                {
                    limitations.Add(
                        $"Search response for {query.Field} '{query.NormalizedValue}' does not support {query.Field}; no filename fallback was used.");
                    continue;
                }

                foreach (var candidate in response.Candidates)
                {
                    var record = TryMapCandidate(query, supportedFields, candidate, limitations);
                    if (record is not null)
                    {
                        mapped.Add(record);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            Complete(plannedQueries, attemptedQueries, limitations, Array.Empty<ControlledDrawingRecord>());
            throw;
        }
        catch (Exception exception)
        {
            limitations.Add($"PDM search failed closed: {exception.Message}");
            return Complete(plannedQueries, attemptedQueries, limitations, Array.Empty<ControlledDrawingRecord>());
        }

        var deduplicated = mapped
            .GroupBy(CreateDeduplicationKey, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(record => Normalize(record.PartNumber), StringComparer.Ordinal)
            .ThenBy(record => Normalize(record.DocumentNumber), StringComparer.Ordinal)
            .ThenBy(record => record.RecordId, StringComparer.Ordinal)
            .ThenBy(record => record.ExactPath, StringComparer.Ordinal)
            .ThenBy(record => record.FileName, StringComparer.Ordinal)
            .ThenBy(record => record.IsLocallyAvailable)
            .ThenBy(record => record.RetrievalRequired)
            .ThenBy(record => record.Role, StringComparer.Ordinal)
            .ThenBy(record => record.RoleAuthority)
            .ToArray();

        if (deduplicated.Length > _options.MaximumCandidateCount)
        {
            limitations.Add(
                $"Maximum candidate count {_options.MaximumCandidateCount} exceeded by {deduplicated.Length} distinct records; truncation would compromise completeness and identity uniqueness, so no candidates were returned.");
            return Complete(plannedQueries, attemptedQueries, limitations, Array.Empty<ControlledDrawingRecord>());
        }

        return Complete(plannedQueries, attemptedQueries, limitations, deduplicated);
    }

    private IReadOnlyList<PdmSearchQuery> BuildQueries(DrawingResolutionRequest request)
    {
        var queries = new List<PdmSearchQuery>();
        foreach (var value in IdentifierValues(request, DrawingIdentifierKind.PartNumber, request.PartNumber))
        {
            queries.Add(new PdmSearchQuery
            {
                Field = PdmSearchField.PartNumber,
                NormalizedValue = value,
                Limit = _options.QueryLimit
            });
        }

        foreach (var value in IdentifierValues(request, DrawingIdentifierKind.DocumentNumber, request.DocumentNumber))
        {
            queries.Add(new PdmSearchQuery
            {
                Field = PdmSearchField.DocumentNumber,
                NormalizedValue = value,
                Limit = _options.QueryLimit
            });
        }

        return queries;
    }

    private IReadOnlyList<string> IdentifierValues(
        DrawingResolutionRequest request,
        DrawingIdentifierKind kind,
        string explicitValue)
    {
        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddValue(values, explicitValue);

        foreach (var candidate in (request.Identifiers ?? Array.Empty<DrawingIdentifierCandidate>())
                     .Where(item => item is not null && item.Kind == kind))
        {
            AddValue(values, candidate.RawValue);
        }

        return values
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private void AddValue(ISet<string> values, string value)
    {
        var normalized = Normalize(value);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            values.Add(normalized);
        }
    }

    private ControlledDrawingRecord? TryMapCandidate(
        PdmSearchQuery query,
        ISet<PdmSearchField> supportedFields,
        PdmSearchCandidate candidate,
        ICollection<string> limitations)
    {
        if (!HasAllowedExtension(candidate.FileName))
        {
            limitations.Add(
                $"Candidate '{candidate.BackendId}' was excluded because filename extension for '{candidate.FileName}' is not '{_options.AllowedDrawingExtension}'.");
            return null;
        }

        if (!PathExtensionMatchesFileName(candidate.FileName, candidate.ExactPath))
        {
            limitations.Add(
                $"Candidate '{candidate.BackendId}' was excluded because path extension for '{candidate.ExactPath}' does not agree with filename extension for '{candidate.FileName}'.");
            return null;
        }

        if (candidate.IsLocallyAvailable is null)
        {
            limitations.Add(
                $"Candidate '{candidate.BackendId}' was excluded because it has unknown local availability.");
            return null;
        }

        var observedPart = supportedFields.Contains(PdmSearchField.PartNumber)
            ? SafeText(candidate.PartNumber)
            : string.Empty;
        var observedDocument = supportedFields.Contains(PdmSearchField.DocumentNumber)
            ? SafeText(candidate.DocumentNumber)
            : string.Empty;

        if (query.Field == PdmSearchField.PartNumber && string.IsNullOrWhiteSpace(observedPart))
        {
            limitations.Add(
                $"Candidate '{candidate.BackendId}' was excluded because it is missing observed PartNumber metadata.");
            return null;
        }

        if (query.Field == PdmSearchField.PartNumber &&
            !string.Equals(Normalize(observedPart), query.NormalizedValue, StringComparison.OrdinalIgnoreCase))
        {
            limitations.Add(
                $"Candidate '{candidate.BackendId}' was excluded because observed PartNumber '{observedPart}' does not match requested PartNumber '{query.NormalizedValue}'.");
            return null;
        }

        if (query.Field == PdmSearchField.DocumentNumber && string.IsNullOrWhiteSpace(observedDocument))
        {
            limitations.Add(
                $"Candidate '{candidate.BackendId}' was excluded because it is missing observed DocumentNumber metadata.");
            return null;
        }

        if (query.Field == PdmSearchField.DocumentNumber &&
            !string.Equals(Normalize(observedDocument), query.NormalizedValue, StringComparison.OrdinalIgnoreCase))
        {
            limitations.Add(
                $"Candidate '{candidate.BackendId}' was excluded because observed DocumentNumber '{observedDocument}' does not match requested DocumentNumber '{query.NormalizedValue}'.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(observedPart) && string.IsNullOrWhiteSpace(observedDocument))
        {
            limitations.Add(
                $"Candidate '{candidate.BackendId}' was excluded because it provided no observed controlled identity fields.");
            return null;
        }

        var role = SafeText(candidate.Role);
        var hasRole = !string.IsNullOrWhiteSpace(role) && candidate.RoleAuthority.HasValue;
        if (!string.IsNullOrWhiteSpace(role) && !candidate.RoleAuthority.HasValue)
        {
            limitations.Add(
                $"Candidate '{candidate.BackendId}' role authority was not supplied; role metadata was cleared.");
        }

        return new ControlledDrawingRecord
        {
            RecordId = candidate.BackendId,
            PartNumber = observedPart,
            DocumentNumber = observedDocument,
            ExactPath = candidate.ExactPath,
            FileName = candidate.FileName,
            IsLocallyAvailable = candidate.IsLocallyAvailable.Value,
            RetrievalRequired = !candidate.IsLocallyAvailable.Value,
            ReadOnlyPolicy = DrawingReadOnlyPolicy.ReadOnlyRequired,
            Role = hasRole ? role : string.Empty,
            RoleAuthority = hasRole
                ? candidate.RoleAuthority!.Value
                : DrawingRoleAuthority.ControlledEvidence
        };
    }

    private IReadOnlyList<ControlledDrawingRecord> Complete(
        IReadOnlyList<PdmSearchQuery> plannedQueries,
        IReadOnlyList<PdmSearchQuery> attemptedQueries,
        IEnumerable<string> limitations,
        IReadOnlyList<ControlledDrawingRecord> results)
    {
        LastReceipt = new ProductionPdmDrawingCatalogReceipt
        {
            PlannedQueries = plannedQueries.Select(query => query with { }).ToArray(),
            AttemptedQueries = attemptedQueries.Select(query => query with { }).ToArray(),
            Limitations = limitations
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray(),
            ReturnedCandidateCount = results.Count
        };

        return results;
    }

    private bool HasAllowedExtension(string value) =>
        string.Equals(Path.GetExtension(value ?? string.Empty), _options.AllowedDrawingExtension, StringComparison.OrdinalIgnoreCase);

    private static bool PathExtensionMatchesFileName(string fileName, string exactPath) =>
        string.Equals(
            Path.GetExtension(fileName ?? string.Empty),
            Path.GetExtension(exactPath ?? string.Empty),
            StringComparison.OrdinalIgnoreCase);

    private string Normalize(string value) => _normalization.NormalizeIdentifier(value);

    private static string SafeText(string? value) => value?.Trim() ?? string.Empty;

    private string CreateDeduplicationKey(ControlledDrawingRecord record) => string.Join(
        "\u001F",
        Normalize(record.PartNumber),
        Normalize(record.DocumentNumber),
        record.RecordId.Trim(),
        record.ExactPath.Trim(),
        record.FileName.Trim(),
        record.IsLocallyAvailable ? "1" : "0",
        record.RetrievalRequired ? "1" : "0",
        record.Role.Trim(),
        ((int)record.RoleAuthority).ToString());

    private static void AddLimitations(ICollection<string> destination, IEnumerable<string>? source)
    {
        foreach (var item in source ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(item))
            {
                destination.Add(item);
            }
        }
    }
}

public sealed record ProductionPdmDrawingCatalogOptions
{
    private int _queryLimit = 25;
    private int _maximumCandidateCount = 25;
    private string _allowedDrawingExtension = ".SLDDRW";

    public int QueryLimit
    {
        get => _queryLimit;
        init => _queryLimit = value > 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(QueryLimit), "QueryLimit must be positive.");
    }

    public int MaximumCandidateCount
    {
        get => _maximumCandidateCount;
        init => _maximumCandidateCount = value > 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(MaximumCandidateCount), "MaximumCandidateCount must be positive.");
    }

    public string AllowedDrawingExtension
    {
        get => _allowedDrawingExtension;
        init => _allowedDrawingExtension = NormalizeExtension(value);
    }

    private static string NormalizeExtension(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("AllowedDrawingExtension is required.", nameof(AllowedDrawingExtension));
        }

        var trimmed = value.Trim();
        return trimmed.StartsWith(".", StringComparison.Ordinal) ? trimmed : $".{trimmed}";
    }
}

public sealed record ProductionPdmDrawingCatalogReceipt
{
    public static readonly ProductionPdmDrawingCatalogReceipt Empty = new();

    public IReadOnlyList<PdmSearchQuery> PlannedQueries { get; init; } = Array.Empty<PdmSearchQuery>();

    public IReadOnlyList<PdmSearchQuery> AttemptedQueries { get; init; } = Array.Empty<PdmSearchQuery>();

    public IReadOnlyList<PdmSearchQuery> Queries => AttemptedQueries;

    public IReadOnlyList<string> Limitations { get; init; } = Array.Empty<string>();

    public int ReturnedCandidateCount { get; init; }
}
