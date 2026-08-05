using System.Text.Json.Serialization;

namespace BlueBrick.Relay.Models;

public sealed class RelayDeviceRegistration
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string Product { get; set; } = string.Empty;
    public string[] Sessions { get; set; } = Array.Empty<string>();
}

public sealed class RelaySessionRoute
{
    public string SessionId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public DateTime UpdatedUtc { get; set; }
}

public sealed class RelayToolInvocation
{
    public string SessionId { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public Dictionary<string, string> Arguments { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string RequestedBy { get; set; } = string.Empty;
}

public sealed class RelayToolResultEnvelope
{
    public string CorrelationId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public RelayToolResult Result { get; set; } = new();
}

public sealed class RelayToolResult
{
    public string SessionId { get; set; } = string.Empty;
    public string ActionName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string TraceId { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public Dictionary<string, string> Data { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class RelayConfirmationState
{
    public string ConfirmationId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string ActionName { get; set; } = string.Empty;
    public DateTime ExpiresUtc { get; set; }
}

public sealed class RelaySocketEnvelope
{
    public string Kind { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
}

public sealed class McpJsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("params")]
    public Dictionary<string, object?> Params { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class McpJsonRpcResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("result")]
    public object? Result { get; set; }

    [JsonPropertyName("error")]
    public object? Error { get; set; }
}

public sealed class McpToolDescriptor
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool ReadOnlyHint { get; set; }
    public bool DestructiveHint { get; set; }
    public bool OpenWorldHint { get; set; }
    public bool Disabled { get; set; }
}
