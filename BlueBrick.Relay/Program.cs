using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using BlueBrick.Relay;
using BlueBrick.Relay.Models;
using BlueBrick.Relay.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<RelayOptions>(builder.Configuration.GetSection("Relay"));
builder.Services.Configure<OAuthOptions>(builder.Configuration.GetSection("OAuth"));
builder.Services.Configure<ExecutionBoardOptions>(builder.Configuration.GetSection("ExecutionBoard"));
builder.Services.AddSingleton<IRelayRepository, SqliteRelayRepository>();
builder.Services.AddSingleton<DeviceTunnelRegistry>();
builder.Services.AddSingleton<McpToolCatalog>();
builder.Services.AddSingleton<ToolRoutingService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var oauth = builder.Configuration.GetSection("OAuth").Get<OAuthOptions>() ?? new OAuthOptions();
        options.Authority = oauth.Authority;
        options.Audience = oauth.Audience;
        options.RequireHttpsMetadata = oauth.RequireHttpsMetadata;
    });
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseWebSockets();
app.UseAuthentication();
app.UseAuthorization();

using (var scope = app.Services.CreateScope())
{
    var repo = scope.ServiceProvider.GetRequiredService<IRelayRepository>();
    await repo.EnsureCreatedAsync(CancellationToken.None);
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapMethods("/execution-board/query", new[] { "OPTIONS", "POST" }, async (HttpContext context, IOptions<ExecutionBoardOptions> executionBoard, IRelayRepository repo) =>
{
    var options = executionBoard.Value;
    if (!options.Enabled)
    {
        return Results.NotFound(new { error = "Local execution-board fixture is disabled." });
    }

    if (context.Connection.RemoteIpAddress is not null && !System.Net.IPAddress.IsLoopback(context.Connection.RemoteIpAddress))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var origin = context.Request.Headers.Origin.ToString();
    if (options.AllowedOrigins.Length > 0 && !options.AllowedOrigins.Contains(origin, StringComparer.Ordinal))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (!string.IsNullOrWhiteSpace(origin))
    {
        context.Response.Headers.AccessControlAllowOrigin = origin;
        context.Response.Headers.AccessControlAllowMethods = "POST, OPTIONS";
        context.Response.Headers.AccessControlAllowHeaders = "content-type";
        context.Response.Headers.Vary = "Origin";
    }

    if (HttpMethods.IsOptions(context.Request.Method))
    {
        return Results.NoContent();
    }

    var request = await context.Request.ReadFromJsonAsync<ExecutionBoardFixtureRequest>(context.RequestAborted) ?? new ExecutionBoardFixtureRequest();
    var response = ExecutionBoardFixtureRouter.Route(request.Query, request.SessionId);
    await repo.WriteAuditAsync("execution-board", "fixture-query", request.SessionId, response.Status, "local-fixture", context.RequestAborted);
    response.PersistedReceipt = true;
    return Results.Ok(response);
});

app.MapGet("/.well-known/oauth-protected-resource", (IOptions<RelayOptions> relay, IOptions<OAuthOptions> oauth) =>
{
    return Results.Ok(new
    {
        resource = relay.Value.BaseUrl,
        authorization_servers = new[] { oauth.Value.Authority },
        bearer_methods_supported = new[] { "header" },
        scopes_supported = relay.Value.AllowedScopes
    });
});

app.MapPost("/devices/register", async (HttpContext context, IRelayRepository repo, IOptions<RelayOptions> relay) =>
{
    if (!ValidateRegistrationToken(context, relay.Value))
    {
        return Results.Unauthorized();
    }

    var payload = await context.Request.ReadFromJsonAsync<RelayDeviceRegistration>() ?? new RelayDeviceRegistration();
    await repo.UpsertDeviceAsync(payload, context.RequestAborted);
    foreach (var sessionId in payload.Sessions)
    {
        await repo.UpsertSessionRouteAsync(new RelaySessionRoute
        {
            SessionId = sessionId,
            DeviceId = payload.DeviceId,
            UpdatedUtc = DateTime.UtcNow
        }, context.RequestAborted);
    }

    await repo.WriteAuditAsync("relay", "register", null, "ok", payload.DeviceId, context.RequestAborted);
    return Results.Ok(new { ok = true });
});

app.MapPost("/devices/heartbeat", async (HttpContext context, IRelayRepository repo, IOptions<RelayOptions> relay) =>
{
    if (!ValidateRegistrationToken(context, relay.Value))
    {
        return Results.Unauthorized();
    }

    var payload = await context.Request.ReadFromJsonAsync<RelayDeviceRegistration>() ?? new RelayDeviceRegistration();
    await repo.TouchHeartbeatAsync(payload.DeviceId, context.RequestAborted);
    await repo.WriteAuditAsync("relay", "heartbeat", null, "ok", payload.DeviceId, context.RequestAborted);
    return Results.Ok(new { ok = true });
});

app.MapGet("/chatgpt/handoff", (HttpContext context, IOptions<RelayOptions> relay) =>
{
    var sessionId = context.Request.Query["sessionId"].ToString();
    var deviceId = context.Request.Query["deviceId"].ToString();
    if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(deviceId))
    {
        return Results.BadRequest(new { error = "sessionId and deviceId are required." });
    }

    // Containment: viewing a handoff must not establish or replace a route.

    var html = $"""
<!doctype html>
<html>
<head><meta charset="utf-8"><title>BlueBrick Relay Handoff</title></head>
<body style="font-family:Segoe UI,Arial,sans-serif;padding:24px;">
  <h1>BlueBrick handoff unavailable</h1>
  <p>Relay tool execution is disabled pending ownership, scope, and response validation. No session routing was changed.</p>
  <p><a href="{System.Net.WebUtility.HtmlEncode(relay.Value.ChatWorkspaceUrl)}">Open ChatGPT workspace</a></p>
</body>
</html>
""";
    return Results.Content(html, "text/html", Encoding.UTF8);
});

app.Map("/ws/agent", async (HttpContext context, DeviceTunnelRegistry tunnels, IRelayRepository repo, IOptions<RelayOptions> relay) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    if (!ValidateRegistrationToken(context, relay.Value))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }

    var deviceId = context.Request.Query["deviceId"].ToString();
    if (string.IsNullOrWhiteSpace(deviceId))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    tunnels.Register(deviceId, socket);
    var buffer = new byte[64 * 1024];

    try
    {
        while (socket.State == WebSocketState.Open && !context.RequestAborted.IsCancellationRequested)
        {
            var message = await ReceiveTextAsync(socket, buffer, context.RequestAborted);
            if (string.IsNullOrWhiteSpace(message))
            {
                continue;
            }

            var envelope = JsonSerializer.Deserialize<RelaySocketEnvelope>(message);
            if (envelope == null)
            {
                continue;
            }

            if (string.Equals(envelope.Kind, "register", StringComparison.OrdinalIgnoreCase))
            {
                var registration = JsonSerializer.Deserialize<RelayDeviceRegistration>(envelope.Payload) ?? new RelayDeviceRegistration();
                registration.DeviceId = deviceId;
                await repo.UpsertDeviceAsync(registration, context.RequestAborted);
                foreach (var sessionId in registration.Sessions)
                {
                    await repo.UpsertSessionRouteAsync(new RelaySessionRoute
                    {
                        SessionId = sessionId,
                        DeviceId = deviceId,
                        UpdatedUtc = DateTime.UtcNow
                    }, context.RequestAborted);
                }
                continue;
            }

            if (string.Equals(envelope.Kind, "result", StringComparison.OrdinalIgnoreCase))
            {
                var result = JsonSerializer.Deserialize<RelayToolResultEnvelope>(envelope.Payload);
                if (result != null)
                {
                    tunnels.Complete(result);
                }
            }
        }
    }
    finally
    {
        tunnels.Remove(deviceId);
    }
});

app.MapMethods("/mcp", new[] { "GET", "POST" }, async (HttpContext context, McpToolCatalog catalog) =>
{
    if (!context.User.Identity?.IsAuthenticated ?? true)
    {
        context.Response.Headers.WWWAuthenticate = @"Bearer resource_metadata=""/.well-known/oauth-protected-resource""";
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }

    if (HttpMethods.IsGet(context.Request.Method))
    {
        context.Response.ContentType = "text/event-stream";
        await context.Response.WriteAsync(": bluebrick relay mcp\n\n");
        await context.Response.Body.FlushAsync();
        return;
    }

    McpJsonRpcRequest request;
    try { request = await context.Request.ReadFromJsonAsync<McpJsonRpcRequest>() ?? new McpJsonRpcRequest(); }
    catch (JsonException)
    {
        await Results.BadRequest(new McpJsonRpcResponse { Error = new { code = -32700, message = "Invalid JSON-RPC payload." } }).ExecuteAsync(context);
        return;
    }
    if (request.Id is JsonElement id && id.ValueKind != JsonValueKind.String && id.ValueKind != JsonValueKind.Number && id.ValueKind != JsonValueKind.Null)
    {
        await Results.BadRequest(new McpJsonRpcResponse { Error = new { code = -32600, message = "Invalid JSON-RPC ID." } }).ExecuteAsync(context);
        return;
    }
    McpJsonRpcResponse response;

    switch (request.Method)
    {
        case "initialize":
            response = new McpJsonRpcResponse
            {
                Id = request.Id,
                Result = new
                {
                    protocolVersion = "2025-11-25",
                    serverInfo = new { name = "bluebrick-relay", version = "0.1.0" },
                    capabilities = new { tools = new { listChanged = false } }
                }
            };
            break;
        case "tools/list":
            response = new McpJsonRpcResponse
            {
                Id = request.Id,
                Result = new { tools = catalog.GetAll() }
            };
            break;
        case "tools/call":
            // Containment only: do not parse arguments or dispatch any tool until
            // trusted ownership, scope enforcement, and response correlation exist.
            response = new McpJsonRpcResponse
            {
                Id = request.Id,
                Error = new
                {
                    code = -32003,
                    message = "relay_security_not_ready",
                    data = new
                    {
                        status = "relay_security_not_ready",
                        retryable = false,
                        detail = "Relay tool execution is disabled pending ownership, scope, and response validation."
                    }
                }
            };
            break;
        default:
            response = new McpJsonRpcResponse
            {
                Id = request.Id,
                Error = new { code = -32601, message = "Method not found" }
            };
            break;
    }

    await context.Response.WriteAsJsonAsync(response);
});

app.Run();

static bool ValidateRegistrationToken(HttpContext context, RelayOptions relay)
{
    if (string.IsNullOrWhiteSpace(relay.RegistrationToken))
    {
        return false;
    }

    return string.Equals(context.Request.Headers["X-Relay-Token"], relay.RegistrationToken, StringComparison.Ordinal);
}

static async Task<string?> ReceiveTextAsync(WebSocket socket, byte[] buffer, CancellationToken cancellationToken)
{
    using var stream = new MemoryStream();
    while (true)
    {
        var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
        if (result.MessageType == WebSocketMessageType.Close)
        {
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", cancellationToken);
            return null;
        }

        stream.Write(buffer, 0, result.Count);
        if (result.EndOfMessage)
        {
            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }
}
