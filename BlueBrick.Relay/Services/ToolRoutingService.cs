using BlueBrick.Relay.Models;
using Microsoft.Extensions.Options;

namespace BlueBrick.Relay.Services;

public sealed class ToolRoutingService
{
    private readonly IRelayRepository _repository;
    private readonly DeviceTunnelRegistry _tunnels;
    private readonly RelayOptions _options;

    public ToolRoutingService(IRelayRepository repository, DeviceTunnelRegistry tunnels, IOptions<RelayOptions> options)
    {
        _repository = repository;
        _tunnels = tunnels;
        _options = options.Value;
    }

    public async Task<RelayToolResultEnvelope> RouteAsync(RelayToolInvocation invocation, CancellationToken cancellationToken)
    {
        var route = await _repository.GetSessionRouteAsync(invocation.SessionId, cancellationToken);
        if (route == null)
        {
            return Error(invocation, "not_found", "MCP session route was not found.");
        }

        if (!_tunnels.IsConnected(route.DeviceId))
        {
            return Error(invocation, "offline", "The BlueBrick Lab workstation tunnel is offline.");
        }

        var result = await _tunnels.SendInvocationAsync(route.DeviceId, invocation, TimeSpan.FromSeconds(_options.ToolTimeoutSeconds), cancellationToken);
        if (result == null)
        {
            return Error(invocation, "offline", "The BlueBrick Lab workstation tunnel is offline.");
        }

        return result;
    }

    private static RelayToolResultEnvelope Error(RelayToolInvocation invocation, string status, string message)
    {
        return new RelayToolResultEnvelope
        {
            CorrelationId = Guid.NewGuid().ToString("N"),
            SessionId = invocation.SessionId,
            Result = new RelayToolResult
            {
                SessionId = invocation.SessionId,
                ActionName = invocation.ToolName,
                Status = status,
                Message = message,
                TraceId = Guid.NewGuid().ToString("N"),
                CreatedUtc = DateTime.UtcNow
            }
        };
    }
}
