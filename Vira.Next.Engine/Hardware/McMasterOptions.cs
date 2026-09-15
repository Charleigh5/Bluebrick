namespace Vira.Next.Engine.Hardware;

public sealed record McMasterOptions
{
    public string BaseUrl { get; init; } = "https://api.mcmaster.com";
    public string ClientCertificatePath { get; init; } = string.Empty;
    public string ClientCertificatePassword { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public TimeSpan TokenRefreshBuffer { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan HttpTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public int MaxRetries { get; init; } = 2;
    public bool EnableRateLimit { get; init; } = true;
    public int RateLimitPerSecond { get; init; } = 2;

    public static McMasterOptions FromEnvironment()
    {
        return new McMasterOptions
        {
            BaseUrl = Environment.GetEnvironmentVariable("MCM_API_BASE_URL") ?? "https://api.mcmaster.com",
            ClientCertificatePath = Environment.GetEnvironmentVariable("MCM_CLIENT_CERT_PATH") ?? string.Empty,
            ClientCertificatePassword = Environment.GetEnvironmentVariable("MCM_CLIENT_CERT_PASSWORD") ?? string.Empty,
            Username = Environment.GetEnvironmentVariable("MCM_USERNAME") ?? string.Empty,
            Password = Environment.GetEnvironmentVariable("MCM_PASSWORD") ?? string.Empty
        };
    }
}
