using System.Text.Json.Serialization;

namespace Vira.Next.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PdmSearchField
{
    PartNumber,
    DocumentNumber,
    FileName
}

public sealed record PdmSearchQuery
{
    private string _normalizedValue = string.Empty;
    private int _limit = 1;

    public PdmSearchField Field { get; init; }

    public string NormalizedValue
    {
        get => _normalizedValue;
        init => _normalizedValue = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("NormalizedValue is required.", nameof(NormalizedValue))
            : value.Trim();
    }

    public int Limit
    {
        get => _limit;
        init => _limit = value > 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(Limit), "Limit must be positive.");
    }
}

public sealed record PdmSearchCandidate
{
    private string _backendId = string.Empty;
    private string _fileName = string.Empty;
    private string _exactPath = string.Empty;

    public string BackendId
    {
        get => _backendId;
        init => _backendId = PdmSearchContractGuards.RequiredText(value, nameof(BackendId));
    }

    public string FileName
    {
        get => _fileName;
        init => _fileName = PdmSearchContractGuards.RequiredText(value, nameof(FileName));
    }

    public string ExactPath
    {
        get => _exactPath;
        init => _exactPath = PdmSearchContractGuards.RequiredText(value, nameof(ExactPath));
    }

    public string PartNumber { get; init; } = string.Empty;

    public string DocumentNumber { get; init; } = string.Empty;

    public bool? IsLocallyAvailable { get; init; }

    public string Role { get; init; } = string.Empty;

    public DrawingRoleAuthority? RoleAuthority { get; init; }
}

public sealed record PdmSearchFailure
{
    private string _code = string.Empty;
    private string _message = string.Empty;

    public string Code
    {
        get => _code;
        init => _code = PdmSearchContractGuards.RequiredText(value, nameof(Code));
    }

    public string Message
    {
        get => _message;
        init => _message = PdmSearchContractGuards.RequiredText(value, nameof(Message));
    }

    public bool SafeToRetry { get; init; }
}

public sealed record PdmSearchResponse
{
    private IReadOnlyList<PdmSearchCandidate> _candidates = Array.Empty<PdmSearchCandidate>();
    private IReadOnlyList<PdmSearchField> _supportedFields = PdmSearchContractDefaults.AllSupportedFields.ToArray();
    private IReadOnlyList<string> _limitations = Array.Empty<string>();

    public IReadOnlyList<PdmSearchCandidate> Candidates
    {
        get => _candidates;
        init => _candidates = PdmSearchContractGuards.RequiredCandidates(value, nameof(Candidates));
    }

    public IReadOnlyList<PdmSearchField> SupportedFields
    {
        get => _supportedFields;
        init => _supportedFields = PdmSearchContractGuards.RequiredFields(value, nameof(SupportedFields));
    }

    public IReadOnlyList<string> Limitations
    {
        get => _limitations;
        init => _limitations = PdmSearchContractGuards.RequiredTexts(value, nameof(Limitations));
    }

    public PdmSearchFailure? Failure { get; init; }
}

internal static class PdmSearchContractDefaults
{
    public static readonly IReadOnlyList<PdmSearchField> AllSupportedFields = new[]
    {
        PdmSearchField.PartNumber,
        PdmSearchField.DocumentNumber,
        PdmSearchField.FileName
    };
}

internal static class PdmSearchContractGuards
{
    public static string RequiredText(string? value, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(value, propertyName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{propertyName} is required.", propertyName);
        }

        return value;
    }

    public static IReadOnlyList<PdmSearchCandidate> RequiredCandidates(
        IReadOnlyList<PdmSearchCandidate>? value,
        string propertyName)
    {
        ArgumentNullException.ThrowIfNull(value, propertyName);

        var items = value.ToArray();
        for (var index = 0; index < items.Length; index++)
        {
            if (items[index] is null)
            {
                throw new ArgumentException($"{propertyName}[{index}] cannot be null.", propertyName);
            }
        }

        return items;
    }

    public static IReadOnlyList<PdmSearchField> RequiredFields(
        IReadOnlyList<PdmSearchField>? value,
        string propertyName)
    {
        ArgumentNullException.ThrowIfNull(value, propertyName);
        return value.ToArray();
    }

    public static IReadOnlyList<string> RequiredTexts(
        IReadOnlyList<string>? value,
        string propertyName)
    {
        ArgumentNullException.ThrowIfNull(value, propertyName);

        var items = value.ToArray();
        for (var index = 0; index < items.Length; index++)
        {
            if (items[index] is null)
            {
                throw new ArgumentException($"{propertyName}[{index}] cannot be null.", propertyName);
            }
        }

        return items;
    }
}
