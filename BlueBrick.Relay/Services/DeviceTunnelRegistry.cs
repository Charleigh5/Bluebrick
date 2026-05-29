using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using BlueBrick.Relay.Models;

namespace BlueBrick.Relay.Services;

public sealed class DeviceTunnelRegistry
{
    private readonly ConcurrentDictionary<string, WebSocket> _sockets = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<RelayToolResultEnvelope>> _pending = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string deviceId, WebSocket socket)
    {
        _sockets[deviceId] = socket;
    }

    public void Remove(string deviceId)
    {
        _sockets.TryRemove(deviceId, out _);
    }

    public bool IsConnected(string deviceId)
    {
        return _sockets.TryGetValue(deviceId, out var socket) && socket.State == WebSocketState.Open;
    }

    public async Task<RelayToolResultEnvelope?> SendInvocationAsync(string deviceId, RelayToolInvocation invocation, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (!_sockets.TryGetValue(deviceId, out var socket) || socket.State != WebSocketState.Open)
        {
            return null;
        }

        var correlationId = Guid.NewGuid().ToString("N");
        var waiter = new TaskCompletionSource<RelayToolResultEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[correlationId] = waiter;

        var envelope = new RelaySocketEnvelope
        {
            Kind = "invoke",
            CorrelationId = correlationId,
            DeviceId = deviceId,
            Payload = JsonSerializer.Serialize(invocation)
        };

        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope));
        await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        using var registration = timeoutCts.Token.Register(() => waiter.TrySetCanceled(timeoutCts.Token));
        try
        {
            return await waiter.Task.ConfigureAwait(false);
        }
        finally
        {
            _pending.TryRemove(correlationId, out _);
        }
    }

    public void Complete(RelayToolResultEnvelope envelope)
    {
        if (_pending.TryGetValue(envelope.CorrelationId, out var waiter))
        {
            waiter.TrySetResult(envelope);
        }
    }
}
