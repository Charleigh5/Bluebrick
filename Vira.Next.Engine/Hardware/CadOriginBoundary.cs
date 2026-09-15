namespace Vira.Next.Engine.Hardware;

internal static class CadOriginBoundary
{
    public static bool TryHttps(string? value, out Uri uri)
    {
        uri = null!;
        return !string.IsNullOrWhiteSpace(value) && !value.Contains('\\') &&
            Uri.TryCreate(value, UriKind.Absolute, out uri!) && uri.Scheme == Uri.UriSchemeHttps &&
            !string.IsNullOrWhiteSpace(uri.IdnHost) && string.IsNullOrEmpty(uri.UserInfo) && !HasUserInfoDelimiter(value);
    }

    private static bool HasUserInfoDelimiter(string value)
    {
        var start = value.IndexOf("://", StringComparison.Ordinal);
        if (start < 0) return true;
        start += 3;
        var end = value.IndexOfAny(new[] { '/', '?', '#' }, start);
        return value.AsSpan(start, (end < 0 ? value.Length : end) - start).Contains('@');
    }

    public static bool IsSameOrigin(string? value, string? baseUrl) =>
        TryHttps(baseUrl, out var origin) && TryHttps(value, out var candidate) &&
        string.Equals(origin.Scheme, candidate.Scheme, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(origin.IdnHost, candidate.IdnHost, StringComparison.OrdinalIgnoreCase) && origin.Port == candidate.Port;

    public static void RequireBase(string baseUrl)
    {
        if (!TryHttps(baseUrl, out _)) throw new ArgumentException("McMaster BaseUrl must be an absolute HTTPS URI without userinfo.", nameof(baseUrl));
    }
}
