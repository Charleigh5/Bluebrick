using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BlueBrick.Agent
{
    internal sealed class RelayBridgeClient : IDisposable
    {
        private static readonly HttpClient Client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        private readonly AgentConfig _config;
        private readonly TelemetryLogger _telemetry;
        private readonly Func<IEnumerable<string>> _sessionProvider;
        private Timer _heartbeatTimer;

        internal RelayBridgeClient(AgentConfig config, TelemetryLogger telemetry, Func<IEnumerable<string>> sessionProvider)
        {
            _config = config;
            _telemetry = telemetry;
            _sessionProvider = sessionProvider;
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
            if (!_config.Relay.Enabled) return;
            _heartbeatTimer = new Timer(async _ => await SafeHeartbeatAsync().ConfigureAwait(false), null,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(_config.Relay.HeartbeatIntervalSeconds));
        }

        internal void Stop()
        {
            _heartbeatTimer?.Dispose();
            _heartbeatTimer = null;
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
                State.Connected = true;
                State.LastRegisterUtc = DateTime.UtcNow;
                State.LastError = null;
                _telemetry.LogEvent("RELAY_REGISTER", "register", true, 0, new { deviceId = _config.Relay.DeviceId });
            }
            catch (Exception ex)
            {
                State.Connected = false;
                State.LastError = ex.Message;
                _telemetry.LogEvent("RELAY_REGISTER", "register", false, 0, new { error = ex.Message });
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
                    lastRegisterUtc = State.LastRegisterUtc
                };
                await PostAsync("/devices/heartbeat", payload).ConfigureAwait(false);
                State.Connected = true;
                State.LastHeartbeatUtc = DateTime.UtcNow;
                State.LastError = null;
            }
            catch (Exception ex)
            {
                State.Connected = false;
                State.LastError = ex.Message;
            }

            return State;
        }

        internal string BuildHandoffUrl(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(_config.Relay.BaseUrl)) return _config.Relay.ChatWorkspaceUrl;
            var baseUrl = _config.Relay.BaseUrl.TrimEnd('/');
            var handoffPath = (_config.Relay.HandoffPath ?? "chatgpt/handoff").Trim('/');
            return baseUrl + "/" + handoffPath + "?sessionId=" + Uri.EscapeDataString(sessionId) +
                   "&deviceId=" + Uri.EscapeDataString(_config.Relay.DeviceId);
        }

        private async Task SafeHeartbeatAsync()
        {
            try
            {
                await HeartbeatAsync().ConfigureAwait(false);
            }
            catch
            {
                // Best-effort background heartbeat only.
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
        }
    }
}
