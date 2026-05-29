namespace BlueBrick.Relay;

public sealed class RelayOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ChatWorkspaceUrl { get; set; } = "https://chatgpt.com/";
    public string HandoffPath { get; set; } = "chatgpt/handoff";
    public string ProtectedResourcePath { get; set; } = "/.well-known/oauth-protected-resource";
    public string SqlitePath { get; set; } = "data/relay.db";
    public int ToolTimeoutSeconds { get; set; } = 20;
    public int HeartbeatStaleSeconds { get; set; } = 90;
    public string[] AllowedScopes { get; set; } = new[] { "bluebrick.preview" };
    public string RegistrationToken { get; set; } = string.Empty;
}

public sealed class OAuthOptions
{
    public string Authority { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public bool RequireHttpsMetadata { get; set; } = true;
}
