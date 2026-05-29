using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BlueBrick.Agent
{
    internal sealed class RelayTunnelClient : IDisposable
    {
        private static readonly HttpClient Client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        private readonly AgentConfig _config;
        private readonly TelemetryLogger _telemetry;
        private readonly Func<IEnumerable<string>> _sessionProvider;
        private readonly Func<RelayToolInvocation, Task<PreviewActionResult>> _invocationHandler;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private Task _runLoop;
        private ClientWebSocket _socket;

        internal RelayTunnelClient(
            AgentConfig config,
            TelemetryLogger telemetry,
            Func<IEnumerable<string>> sessionProvider,
            Func<RelayToolInvocation, Task<PreviewActionResult>> invocationHandler)
        {
            _config = config;
            _telemetry = telemetry;
            _sessionProvider = sessionProvider;
            _invocationHandler = invocationHandler;
            State = new RelayTunnelState
            {
                DeviceId = _config.Relay.DeviceId,
                Enabled = _config.Relay.Enabled,
                BaseUrl = _config.Relay.BaseUrl
            };
        }

        internal RelayTunnelState State { get; }

        internal void Start()
        {
            if (!_config.Relay.Enabled || string.IsNullOrWhiteSpace(_config.Relay.BaseUrl) || _runLoop != null)
            {
                return;
            }

            _runLoop = Task.Run(() => RunAsync(_lifetime.Token));
        }

        internal void Stop()
        {
            try
            {
                _lifetime.Cancel();
                _socket?.Abort();
                _socket?.Dispose();
            }
            catch
            {
                // ignore
            }
        }

        internal async Task<RelayTunnelState> RegisterAsync()
        {
            State.BaseUrl = _config.Relay.BaseUrl;
            State.Enabled = _config.Relay.Enabled;
            if (string.IsNullOrWhiteSpace(_config.Relay.BaseUrl))
            {
                State.Connected = false;
                State.LastError = "Relay base URL is not configured.";
                return State;
            }

            try
            {
                var payload = new
                {
                    deviceId = _config.Relay.DeviceId,
                    deviceName = _config.Relay.DeviceName,
                    registrationToken = _config.Relay.RegistrationToken,
                    product = AppIdentity.ProductName,
                    sessions = _sessionProvider()
                };
                await PostAsync("/devices/register", payload).ConfigureAwait(false);
                State.LastRegisterUtc = DateTime.UtcNow;
                State.LastError = null;
            }
            catch (Exception ex)
            {
                State.LastError = ex.Message;
                State.Connected = false;
            }

            return State;
        }

        internal async Task<RelayTunnelState> HeartbeatAsync()
        {
            if (string.IsNullOrWhiteSpace(_config.Relay.BaseUrl))
            {
                State.Connected = false;
                State.LastError = "Relay base URL is not configured.";
                return State;
            }

            try
            {
                var payload = new
                {
                    deviceId = _config.Relay.DeviceId,
                    product = AppIdentity.ProductName,
                    sessions = _sessionProvider(),
                    connected = State.Connected
                };
                await PostAsync("/devices/heartbeat", payload).ConfigureAwait(false);
                State.LastHeartbeatUtc = DateTime.UtcNow;
                State.LastError = null;
            }
            catch (Exception ex)
            {
                State.LastError = ex.Message;
            }

            return State;
        }

        internal string BuildHandoffUrl(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(_config.Relay.BaseUrl))
            {
                return _config.Relay.ChatWorkspaceUrl;
            }

            var baseUrl = _config.Relay.BaseUrl.TrimEnd('/');
            var handoffPath = (_config.Relay.HandoffPath ?? "chatgpt/handoff").Trim('/');
            return baseUrl + "/" + handoffPath +
                   "?sessionId=" + Uri.EscapeDataString(sessionId) +
                   "&deviceId=" + Uri.EscapeDataString(_config.Relay.DeviceId);
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            var delaySeconds = 2;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await RegisterAsync().ConfigureAwait(false);
                    await ConnectSocketAsync(cancellationToken).ConfigureAwait(false);
                    delaySeconds = 2;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    State.Connected = false;
                    State.LastError = ex.Message;
                    _telemetry.LogEvent("RELAY_TUNNEL", "connect", false, 0, new { error = ex.Message });
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken).ConfigureAwait(false);
                    delaySeconds = Math.Min(delaySeconds * 2, 30);
                }
            }
        }

        private async Task ConnectSocketAsync(CancellationToken cancellationToken)
        {
            var wsUri = BuildWebSocketUri();
            using (var socket = new ClientWebSocket())
            {
                _socket = socket;
                if (!string.IsNullOrWhiteSpace(_config.Relay.RegistrationToken))
                {
                    socket.Options.SetRequestHeader("X-Relay-Token", _config.Relay.RegistrationToken);
                }

                socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(Math.Max(10, _config.Relay.HeartbeatIntervalSeconds));
                await socket.ConnectAsync(wsUri, cancellationToken).ConfigureAwait(false);

                State.Connected = true;
                State.LastError = null;
                await SendEnvelopeAsync(socket, new RelaySocketEnvelope
                {
                    Kind = "register",
                    DeviceId = _config.Relay.DeviceId,
Payload = JToken.FromObject(new
{
    deviceName = _config.Relay.DeviceName,
    sessions = _sessionProvider()
})
                }, cancellationToken).ConfigureAwait(false);

                var buffer = new byte[64 * 1024];
                while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    var message = await ReceiveTextAsync(socket, buffer, cancellationToken).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(message))
                    {
                        continue;
                    }

                    var envelope = JsonConvert.DeserializeObject<RelaySocketEnvelope>(message);
                    if (!string.Equals(envelope?.Kind, "invoke", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var invocation = JsonConvert.DeserializeObject<RelayToolInvocation>(envelope.Payload?.ToString() ?? "{}");
                    var result = await _invocationHandler(invocation).ConfigureAwait(false);
                    await SendEnvelopeAsync(socket, new RelaySocketEnvelope
                    {
                        Kind = "result",
                        DeviceId = _config.Relay.DeviceId,
                        CorrelationId = envelope.CorrelationId,
Payload = JToken.FromObject(new RelayToolResultEnvelope
{
    CorrelationId = envelope.CorrelationId,
    SessionId = invocation?.SessionId,
    Result = result
})
                    }, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private Uri BuildWebSocketUri()
        {
            var builder = new UriBuilder(_config.Relay.BaseUrl);
            builder.Scheme = builder.Scheme == "https" ? "wss" : "ws";
            builder.Path = "ws/agent";
            builder.Query = "deviceId=" + Uri.EscapeDataString(_config.Relay.DeviceId);
            return builder.Uri;
        }

        private static async Task SendEnvelopeAsync(ClientWebSocket socket, RelaySocketEnvelope envelope, CancellationToken cancellationToken)
        {
            var json = JsonConvert.SerializeObject(envelope);
            var bytes = Encoding.UTF8.GetBytes(json);
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<string> ReceiveTextAsync(ClientWebSocket socket, byte[] buffer, CancellationToken cancellationToken)
        {
            var segment = new ArraySegment<byte>(buffer);
            using (var stream = new System.IO.MemoryStream())
            {
                while (true)
                {
                    var result = await socket.ReceiveAsync(segment, cancellationToken).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", cancellationToken).ConfigureAwait(false);
                        return null;
                    }

                    stream.Write(buffer, 0, result.Count);
                    if (result.EndOfMessage)
                    {
                        return Encoding.UTF8.GetString(stream.ToArray());
                    }
                }
            }
        }

        private async Task PostAsync(string relativePath, object payload)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, _config.Relay.BaseUrl.TrimEnd('/') + relativePath))
            {
                request.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                if (!string.IsNullOrWhiteSpace(_config.Relay.RegistrationToken))
                {
                    request.Headers.Add("X-Relay-Token", _config.Relay.RegistrationToken);
                }

                var response = await Client.SendAsync(request).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
            }
        }

        public void Dispose()
        {
            Stop();
            _lifetime.Dispose();
        }
    }

    internal sealed class RelayToolInvocation
    {
        public string SessionId { get; set; }
        public string ToolName { get; set; }
        public Dictionary<string, string> Arguments { get; set; } = new Dictionary<string, string>();
        public string RequestedBy { get; set; }
    }

    internal sealed class RelayToolResultEnvelope
    {
        public string CorrelationId { get; set; }
        public string SessionId { get; set; }
        public PreviewActionResult Result { get; set; }
    }

internal sealed class RelaySocketEnvelope
{
    public string Kind { get; set; }
    public string CorrelationId { get; set; }
    public string DeviceId { get; set; }
    public JToken Payload { get; set; }
}
}
