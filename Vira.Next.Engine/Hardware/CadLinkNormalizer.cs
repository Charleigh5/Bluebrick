using Vira.Next.Contracts;

namespace Vira.Next.Engine.Hardware;

public sealed class CadLinkNormalizer
{
    public const string PreferredKey = "3-D STEP";

    public CadLinkNormalizationResult Normalize(IReadOnlyList<McMasterLink> links, string baseUrl, string partNumber)
    {
        if (links == null || links.Count == 0)
            return CadLinkNormalizationResult.Failure("NO_CAD_LINKS", "Product response contains no Links collection.", Array.Empty<string>(), partNumber);
        var stepLinks = links.Where(link => string.Equals(link.Key?.Trim(), PreferredKey, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (stepLinks.Length == 0)
            return CadLinkNormalizationResult.Failure("NO_STEP_LINK", $"No Links entry with Key=\"{PreferredKey}\" was found.", links.Select(l => l.Key).ToArray(), partNumber);
        var candidate = stepLinks.FirstOrDefault(link => link.Value != null && link.Value.EndsWith(".STEP", StringComparison.OrdinalIgnoreCase))
            ?? stepLinks[0];
        var raw = candidate.Value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
            return CadLinkNormalizationResult.Failure("EMPTY_STEP_VALUE", "3-D STEP Value is empty.", links.Select(l => l.Key).ToArray(), partNumber);
        var normalized = NormalizeUrl(raw, baseUrl);
        if (normalized == null)
            return CadLinkNormalizationResult.Failure("INVALID_CAD_URL", "CAD link must resolve to the configured HTTPS origin without userinfo.", links.Select(l => l.Key).ToArray(), partNumber);
        return CadLinkNormalizationResult.Success(candidate.Key, raw, normalized);
    }

    public static string? NormalizeUrl(string rawValue, string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(rawValue) || !CadOriginBoundary.TryHttps(baseUrl, out var baseUri)) return null;
        rawValue = rawValue.Trim();
        if (rawValue.StartsWith("//", StringComparison.Ordinal) || rawValue.Contains('\\')) return null;
        // URI parsing treats rooted paths as file URIs on Windows; only explicit schemes are absolute links.
        var colon = rawValue.IndexOf(':');
        var separator = rawValue.IndexOfAny(new[] { '/', '?', '#' });
        var hasScheme = colon >= 0 && (separator < 0 || colon < separator);
        if (hasScheme) return CadOriginBoundary.IsSameOrigin(rawValue, baseUrl) ? new Uri(rawValue).AbsoluteUri : null;
        var relativeBase = new Uri(baseUri.GetLeftPart(UriPartial.Path).TrimEnd('/') + "/");
        return Uri.TryCreate(relativeBase, rawValue, out var combined) && CadOriginBoundary.IsSameOrigin(combined.AbsoluteUri, baseUrl) ? combined.AbsoluteUri : null;
    }
}

public sealed record CadLinkNormalizationResult
{
    public bool IsSuccess { get; init; }
    public string Key { get; init; } = string.Empty;
    public string RawValue { get; init; } = string.Empty;
    public string NormalizedUrl { get; init; } = string.Empty;
    public string ErrorCode { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
    public IReadOnlyList<string> AvailableKeys { get; init; } = Array.Empty<string>();
    public string PartNumber { get; init; } = string.Empty;

    public static CadLinkNormalizationResult Success(string key, string raw, string normalized) => new()
    {
        IsSuccess = true,
        Key = key,
        RawValue = raw,
        NormalizedUrl = normalized
    };

    public static CadLinkNormalizationResult Failure(string code, string message, IReadOnlyList<string> keys, string partNumber) => new()
    {
        IsSuccess = false,
        ErrorCode = code,
        ErrorMessage = message,
        AvailableKeys = keys,
        PartNumber = partNumber
    };
}
