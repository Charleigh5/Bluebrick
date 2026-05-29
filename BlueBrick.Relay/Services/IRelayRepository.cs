using BlueBrick.Relay.Models;

namespace BlueBrick.Relay.Services;

public interface IRelayRepository
{
    Task EnsureCreatedAsync(CancellationToken cancellationToken);
    Task UpsertDeviceAsync(RelayDeviceRegistration device, CancellationToken cancellationToken);
    Task TouchHeartbeatAsync(string deviceId, CancellationToken cancellationToken);
    Task UpsertSessionRouteAsync(RelaySessionRoute route, CancellationToken cancellationToken);
    Task<RelaySessionRoute?> GetSessionRouteAsync(string sessionId, CancellationToken cancellationToken);
    Task WriteAuditAsync(string category, string action, string? sessionId, string status, string detail, CancellationToken cancellationToken);
}
