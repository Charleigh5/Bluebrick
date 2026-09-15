using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;

namespace Vira.Next.Engine.Hardware;

public sealed class McmAuthTokenProvider : IDisposable
{
    private readonly McMasterOptions _options;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _token;
    private DateTime _expiryUtc;
    private bool _disposed;

    public McmAuthTokenProvider(McMasterOptions options, HttpClient? httpClient = null)
    {
        CadOriginBoundary.RequireBase(options.BaseUrl);
        _options = options;
        _ownsHttpClient = httpClient == null;
        _httpClient = httpClient ?? CreateHttpClient(options);
    }

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(_token) && DateTime.UtcNow + _options.TokenRefreshBuffer < _expiryUtc)
            return _token;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!string.IsNullOrEmpty(_token) && DateTime.UtcNow + _options.TokenRefreshBuffer < _expiryUtc)
                return _token;
            var login = await LoginAsync(cancellationToken);
            _token = login.AuthToken;
            _expiryUtc = DateTime.UtcNow.AddHours(24);
            if (login.ExpiresAtUtc.HasValue)
                _expiryUtc = login.ExpiresAtUtc.Value;
            return _token;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Invalidate() => _token = null;

    private async Task<McmLoginResponse> LoginAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Username) || string.IsNullOrWhiteSpace(_options.Password))
            throw new InvalidOperationException("MCM_USERNAME and MCM_PASSWORD must be configured (env).");
        var payload = new McmLoginRequest { Username = _options.Username, Password = _options.Password };
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/v1/login") { Content = JsonContent.Create(payload) };
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"MCM login failed: {(int)response.StatusCode} ");
        }
        McmLoginResponse? result;
        try { result = await McMasterHttpBody.ReadAsync(_httpClient, response.Content,
            (content, ct) => content.ReadFromJsonAsync<McmLoginResponse>(new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = false }, cancellationToken: ct), cancellationToken); }
        catch (System.Text.Json.JsonException) { throw new InvalidOperationException("MCM login response JSON is invalid."); }
        if (result == null || string.IsNullOrWhiteSpace(result.AuthToken))
            throw new InvalidOperationException("MCM login returned empty AuthToken.");
        return result;
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
        var client = new HttpClient(handler) { Timeout = options.HttpTimeout };
        return client;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _gate.Dispose();
        if (_ownsHttpClient) _httpClient.Dispose();
    }

    private sealed class McmLoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    private sealed class McmLoginResponse
    {
        [JsonPropertyName("AuthToken")]
        public string AuthToken { get; set; } = string.Empty;
        [JsonPropertyName("authToken")]
        public string AuthTokenLower { get => AuthToken; set => AuthToken = value; }
        [JsonPropertyName("expiresAt")]
        public DateTime? ExpiresAtUtc { get; set; }
        [JsonPropertyName("ExpiresAt")]
        public DateTime? ExpiresAtUpper { get => ExpiresAtUtc; set => ExpiresAtUtc = value; }
    }
}

internal static class McMasterHttpBody
{
    public static async Task<T> ReadAsync<T>(HttpClient client, HttpContent content, Func<HttpContent, CancellationToken, Task<T>> read, CancellationToken callerToken)
    {
        // HeadersRead gives us ownership before buffering/parsing can fail. Keep body reads bounded too.
        using var timeout = new CancellationTokenSource(client.Timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(callerToken, timeout.Token);
        try { return await read(content, linked.Token).WaitAsync(linked.Token); }
        catch (OperationCanceledException) when (callerToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("MCM response read cancelled.", callerToken);
        }
    }
}
