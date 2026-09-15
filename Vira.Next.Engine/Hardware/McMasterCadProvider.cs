using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Vira.Next.Contracts;

namespace Vira.Next.Engine.Hardware;

public sealed class McMasterCadProvider : IVendorCadProvider, IDisposable
{
    private readonly McMasterOptions _options;
    private readonly McmAuthTokenProvider _auth;
    private readonly CadLinkNormalizer _normalizer;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsAuth;
    private readonly bool _ownsHttpClient;
    private readonly SemaphoreSlim _rateGate;
    private DateTime _windowStart = DateTime.UtcNow;
    private int _windowCount;
    private bool _disposed;

    public McMasterCadProvider(McMasterOptions options, McmAuthTokenProvider? auth = null, HttpClient? httpClient = null)
    {
        CadOriginBoundary.RequireBase(options.BaseUrl);
        _options = options;
        _ownsAuth = auth == null;
        _ownsHttpClient = httpClient == null;
        _auth = auth ?? new McmAuthTokenProvider(options, httpClient);
        _normalizer = new CadLinkNormalizer();
        _httpClient = httpClient ?? CreateHttpClient(options);
        _rateGate = new SemaphoreSlim(1, 1);
    }

    public async Task EnsureSubscribedAsync(string partNumber, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partNumber);
        await ThrottleAsync(cancellationToken);
        using var response = await SendAuthorizedAsync(HttpMethod.Put, $"{_options.BaseUrl.TrimEnd('/')}/v1/products", partNumber.Trim(), cancellationToken);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            throw new McMasterRateLimitedException("MCM subscription rate-limited.");
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.Conflict)
        {
            throw new HttpRequestException($"MCM subscribe failed for {partNumber}: {(int)response.StatusCode}");
        }
    }

    public async Task<McMasterProductSnapshot> GetProductAsync(string partNumber, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partNumber);
        await ThrottleAsync(cancellationToken);
        var url = $"{_options.BaseUrl.TrimEnd('/')}/v1/products/{Uri.EscapeDataString(partNumber.Trim())}";
        using var response = await SendAuthorizedAsync(HttpMethod.Get, url, null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new McMasterProductNotFoundException(partNumber);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            throw new McMasterRateLimitedException($"MCM product {partNumber} rate-limited.");
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"MCM product {partNumber} failed: {(int)response.StatusCode}");
        }
        var json = await McMasterHttpBody.ReadAsync(_httpClient, response.Content, (content, ct) => content.ReadAsStringAsync(ct), cancellationToken);
        var parsed = ParseProduct(json, partNumber.Trim());
        parsed = parsed with { SourceBaseUrl = _options.BaseUrl, FetchedAtUtc = DateTime.UtcNow };
        return parsed;
    }

    public async Task<byte[]> GetCadBytesAsync(string normalizedCadUrl, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedCadUrl);
        if (!CadOriginBoundary.IsSameOrigin(normalizedCadUrl, _options.BaseUrl))
            throw new ArgumentException("CAD URL is outside the configured HTTPS origin.", nameof(normalizedCadUrl));
        await ThrottleAsync(cancellationToken);
        using var response = await SendAuthorizedAsync(HttpMethod.Get, normalizedCadUrl, null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            throw new McMasterRateLimitedException("MCM CAD retrieval rate-limited.");
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"MCM CAD GET failed: {(int)response.StatusCode}");
        }
        return await McMasterHttpBody.ReadAsync(_httpClient, response.Content, (content, ct) => content.ReadAsByteArrayAsync(ct), cancellationToken);
    }

    public CadLinkNormalizationResult ResolveCadLink(McMasterProductSnapshot product)
    {
        return _normalizer.Normalize(product.Links, _options.BaseUrl, product.PartNumber);
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(HttpMethod method, string url, string? partNumber, CancellationToken ct)
    {
        var token = await _auth.GetTokenAsync(ct);
        for (var refresh = 0; ; refresh++)
        {
            var response = await SendWithRetryAsync(method, url, partNumber, token, ct);
            if (response.StatusCode != HttpStatusCode.Unauthorized || refresh > 0) return response;
            response.Dispose();
            _auth.Invalidate();
            token = await _auth.GetTokenAsync(ct);
        }
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(HttpMethod method, string url, string? partNumber, string token, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            TimeSpan delay;
            // Each retry gets a fresh scoped request/content; no clone outlives its source.
            using (var request = new HttpRequestMessage(method, url))
            {
                if (partNumber != null) request.Content = JsonContent.Create(new { PartNumber = partNumber });
                request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");
                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                if (response.StatusCode != HttpStatusCode.TooManyRequests || attempt >= _options.MaxRetries) return response;
                using (response)
                    delay = GetRetryAfter(response) ?? TimeSpan.FromSeconds(Math.Pow(2, attempt + 1));
            }
            if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
            await Task.Delay(delay, ct);
        }
    }

    private async Task ThrottleAsync(CancellationToken ct)
    {
        if (!_options.EnableRateLimit) return;
        await _rateGate.WaitAsync(ct);
        try
        {
            var now = DateTime.UtcNow;
            if ((now - _windowStart).TotalSeconds >= 1)
            {
                _windowStart = now;
                _windowCount = 0;
            }
            if (_windowCount >= _options.RateLimitPerSecond)
            {
                var wait = TimeSpan.FromSeconds(1) - (now - _windowStart);
                if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);
                _windowStart = DateTime.UtcNow;
                _windowCount = 0;
            }
            _windowCount++;
        }
        finally { _rateGate.Release(); }
    }

    private static TimeSpan? GetRetryAfter(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("Retry-After", out var values))
        {
            var raw = values.FirstOrDefault();
            if (int.TryParse(raw, out var seconds)) return TimeSpan.FromSeconds(seconds);
            if (DateTimeOffset.TryParse(raw, out var date)) return date - DateTimeOffset.UtcNow;
        }
        return null;
    }

    private static McMasterProductSnapshot ParseProduct(string json, string fallbackPartNumber)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count(p => p.Name is "PartNumber" or "partNumber") != 1)
                throw new InvalidOperationException("MCM response must contain exactly one product identity property.");
            string GetStr(string a, string b) => root.TryGetProperty(a, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : root.TryGetProperty(b, out var w) && w.ValueKind == JsonValueKind.String ? w.GetString() ?? string.Empty : string.Empty;
            var partNumber = GetStr("PartNumber", "partNumber");
            // Product identifiers are exact, case-sensitive ordinal values; only the request is trimmed.
            if (string.IsNullOrWhiteSpace(partNumber) || !string.Equals(partNumber, fallbackPartNumber, StringComparison.Ordinal))
                throw new InvalidOperationException("MCM response product identity is missing or does not match the requested part.");
            var status = GetStr("ProductStatus", "productStatus");
            var family = GetStr("FamilyDescription", "familyDescription");
            var detail = GetStr("DetailDescription", "detailDescription");
            var specs = new List<McMasterSpecification>();
            if (root.TryGetProperty("Specifications", out var specsEl) && specsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var s in specsEl.EnumerateArray())
                {
                    var n = s.TryGetProperty("Name", out var nv) ? nv.GetString() ?? string.Empty : s.TryGetProperty("name", out var nn) ? nn.GetString() ?? string.Empty : string.Empty;
                    var v2 = s.TryGetProperty("Value", out var vv) ? vv.GetString() ?? string.Empty : s.TryGetProperty("value", out var vw) ? vw.GetString() ?? string.Empty : string.Empty;
                    specs.Add(new McMasterSpecification { Name = n, Value = v2 });
                }
            }
            var links = new List<McMasterLink>();
            if (root.TryGetProperty("Links", out var linksEl) && linksEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var l in linksEl.EnumerateArray())
                {
                    var k = l.TryGetProperty("Key", out var kv) ? kv.GetString() ?? string.Empty : l.TryGetProperty("key", out var kk) ? kk.GetString() ?? string.Empty : string.Empty;
                    var v2 = l.TryGetProperty("Value", out var vv) ? vv.GetString() ?? string.Empty : l.TryGetProperty("value", out var vw) ? vw.GetString() ?? string.Empty : string.Empty;
                    links.Add(new McMasterLink { Key = k, Value = v2 });
                }
            }
            return new McMasterProductSnapshot
            {
                PartNumber = partNumber,
                ProductStatus = status,
                FamilyDescription = family,
                DetailDescription = detail,
                Specifications = specs,
                Links = links
            };
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("MCM product response JSON is invalid.");
        }
    }

    private static HttpClient CreateHttpClient(McMasterOptions options)
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = false };
        if (!string.IsNullOrWhiteSpace(options.ClientCertificatePath) && File.Exists(options.ClientCertificatePath))
        {
            var cert = string.IsNullOrWhiteSpace(options.ClientCertificatePassword)
                ? new X509Certificate2(options.ClientCertificatePath)
                : new X509Certificate2(options.ClientCertificatePath, options.ClientCertificatePassword, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet);
            handler.ClientCertificates.Add(cert);
        }
        return new HttpClient(handler) { Timeout = options.HttpTimeout };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_ownsAuth) _auth.Dispose();
        if (_ownsHttpClient) _httpClient.Dispose();
        _rateGate.Dispose();
    }
}

public sealed class McMasterProductNotFoundException : Exception
{
    public string PartNumber { get; }
    public McMasterProductNotFoundException(string partNumber) : base($"MCM product not found: {partNumber}") => PartNumber = partNumber;
}

public sealed class McMasterRateLimitedException : Exception
{
    public HttpResponseMessage? Response { get; }
    public McMasterRateLimitedException(string message, HttpResponseMessage? response = null) : base(message) => Response = response;
}
