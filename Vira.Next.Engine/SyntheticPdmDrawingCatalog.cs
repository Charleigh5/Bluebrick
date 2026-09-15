using Vira.Next.Contracts;

namespace Vira.Next.Engine;

public sealed class SyntheticPdmDrawingCatalog : IPdmDrawingCatalog
{
    private readonly NormalizationRegistry _normalization;
    private readonly ControlledDrawingRecord[] _records;

    public SyntheticPdmDrawingCatalog(
        IEnumerable<ControlledDrawingRecord> records,
        NormalizationRegistry? normalization = null)
    {
        ArgumentNullException.ThrowIfNull(records);

        _normalization = normalization ?? new NormalizationRegistry();
        var materialized = records.Select((record, index) => (record, index)).ToArray();
        var nullRecord = materialized.FirstOrDefault(item => item.record is null);
        if (nullRecord.record is null && materialized.Any(item => item.record is null))
        {
            throw new ArgumentException($"Controlled catalog record at index {nullRecord.index} was null.", nameof(records));
        }

        var nonNullRecords = materialized.Select(item => item.record!);
        var blankRecord = nonNullRecords.FirstOrDefault(record => string.IsNullOrWhiteSpace(record.RecordId));
        if (blankRecord is not null)
        {
            var index = materialized.First(item => ReferenceEquals(item.record, blankRecord)).index;
            throw new ArgumentException($"Controlled catalog record at index {index} has a blank RecordId.", nameof(records));
        }

        var duplicateRecord = nonNullRecords
            .GroupBy(record => record.RecordId.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (duplicateRecord is not null)
        {
            throw new ArgumentException($"Controlled catalog RecordId '{duplicateRecord.Key}' is duplicated.", nameof(records));
        }

        _records = nonNullRecords
            .Select(Clone)
            .OrderBy(record => record.RecordId, StringComparer.Ordinal)
            .ThenBy(record => record.PartNumber, StringComparer.Ordinal)
            .ThenBy(record => record.DocumentNumber, StringComparer.Ordinal)
            .ThenBy(record => record.ExactPath, StringComparer.Ordinal)
            .ThenBy(record => record.FileName, StringComparer.Ordinal)
            .ThenBy(record => record.IsLocallyAvailable)
            .ThenBy(record => record.RetrievalRequired)
            .ThenBy(record => record.ReadOnlyPolicy)
            .ThenBy(record => record.Role, StringComparer.Ordinal)
            .ThenBy(record => record.RoleAuthority)
            .ToArray();
    }

    public Task<IReadOnlyList<ControlledDrawingRecord>> FindCandidatesAsync(
        DrawingResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var parts = IdentifierValues(request, DrawingIdentifierKind.PartNumber, request.PartNumber);
        var documents = IdentifierValues(request, DrawingIdentifierKind.DocumentNumber, request.DocumentNumber);

        if (parts.Count == 0 && documents.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<ControlledDrawingRecord>>(Array.Empty<ControlledDrawingRecord>());
        }

        var matches = _records
            .Where(record =>
                parts.Contains(_normalization.NormalizeIdentifier(record.PartNumber)) ||
                documents.Contains(_normalization.NormalizeIdentifier(record.DocumentNumber)))
            .Select(Clone)
            .ToArray();

        return Task.FromResult<IReadOnlyList<ControlledDrawingRecord>>(matches);
    }

    private HashSet<string> IdentifierValues(DrawingResolutionRequest request, DrawingIdentifierKind kind, string explicitValue)
    {
        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Add(values, explicitValue);

        foreach (var candidate in (request.Identifiers ?? Array.Empty<DrawingIdentifierCandidate>()).Where(item => item is not null && item.Kind == kind))
        {
            Add(values, candidate.RawValue);
        }

        return values;
    }

    private void Add(ISet<string> values, string value)
    {
        var normalized = _normalization.NormalizeIdentifier(value);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            values.Add(normalized);
        }
    }

    private static ControlledDrawingRecord Clone(ControlledDrawingRecord record) => record with { };
}
