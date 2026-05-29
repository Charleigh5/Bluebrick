using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace BlueBrick.Agent
{
    internal class LogsControl : UserControl
    {
        private readonly ListView _list;
        private readonly Label _summaryLabel;
        private readonly Label _statusLabel;
        private readonly Button _refreshButton;
        private readonly Button _healthCheckButton;
        private readonly List<TelemetryEvent> _events = new List<TelemetryEvent>();
        private bool _loading;

        internal LogsControl()
        {
            Dock = DockStyle.Fill;
            BackColor = AgentPanelTheme.Surface;

            var header = new Label
            {
                Text = "Logs",
                Dock = DockStyle.Top,
                Height = 32,
                Padding = new Padding(12, 6, 12, 0)
            };
            AgentPanelTheme.ApplyHeaderLabel(header);

            var topBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 54,
                Padding = new Padding(12, 6, 12, 6)
            };
            AgentPanelTheme.ApplyPanel(topBar);

            _summaryLabel = new Label { AutoSize = true, Location = new Point(0, 16) };
            AgentPanelTheme.ApplySubtleLabel(_summaryLabel);

            _healthCheckButton = new Button { Text = "Health Check", Width = 100, Height = 30, Location = new Point(400, 12) };
            AgentPanelTheme.ApplySecondaryButton(_healthCheckButton);
            _healthCheckButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _healthCheckButton.Click += HealthCheckButton_Click;

            _refreshButton = new Button { Text = "Refresh", Width = 86, Height = 30, Location = new Point(520, 12) };
            AgentPanelTheme.ApplyPrimaryButton(_refreshButton);
            _refreshButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _refreshButton.Click += RefreshButton_Click;

            topBar.Controls.Add(_summaryLabel);
            topBar.Controls.Add(_healthCheckButton);
            topBar.Controls.Add(_refreshButton);

            topBar.Resize += (s, e) =>
            {
                var rightPad = 12;
                _refreshButton.Location = new Point(topBar.Width - _refreshButton.Width - rightPad, _refreshButton.Location.Y);
                _healthCheckButton.Location = new Point(_refreshButton.Location.X - _healthCheckButton.Width - 8, _healthCheckButton.Location.Y);
            };

            _list = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                VirtualMode = true,
                FullRowSelect = true
            };
            AgentPanelTheme.ApplyListView(_list);
            _list.Columns.Add("Time", 120);
            _list.Columns.Add("Type", 90);
            _list.Columns.Add("Operation", 220);
            _list.Columns.Add("Status", 80);
            _list.Columns.Add("Latency", 80);
            _list.RetrieveVirtualItem += List_RetrieveVirtualItem;

            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 24,
                Padding = new Padding(12, 2, 12, 2)
            };
            AgentPanelTheme.ApplyPanel(footer);
            _statusLabel = new Label { AutoSize = true };
            AgentPanelTheme.ApplySubtleLabel(_statusLabel);
            footer.Controls.Add(_statusLabel);

            Controls.Add(_list);
            Controls.Add(footer);
            Controls.Add(topBar);
            Controls.Add(header);

            SetStatus("Ready.");
            _ = LoadTelemetryAsync();
        }

        private async void HealthCheckButton_Click(object sender, EventArgs e)
        {
            _healthCheckButton.Enabled = false;
            SetStatus("Running health check...");

            var result = await AgentPanelClient.GetJsonAsync("/agent/selfcheck");
            if (!result.Ok)
            {
                SetStatus("Health check failed: " + result.Error, isError: true);
                _healthCheckButton.Enabled = true;
                return;
            }

            var data = result.Data;
            var status = data?["status"]?.ToString() ?? "unknown";
            var uptime = data?["uptime_seconds"]?.ToObject<double?>() ?? 0;
            var timestamp = data?["timestamp"]?.ToString() ?? "-";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Overall Status: {status.ToUpper()}");
            sb.AppendLine($"Uptime: {uptime:N0} seconds");
            sb.AppendLine($"Timestamp: {timestamp}");
            sb.AppendLine();
            sb.AppendLine("Components:");
            sb.AppendLine("-------------------");

            var components = data?["components"] as JArray ?? new JArray();
            foreach (var comp in components)
            {
                var name = comp["name"]?.ToString() ?? "-";
                var compStatus = comp["status"]?.ToString() ?? "-";
                var latency = comp["latency_ms"]?.ToObject<double?>() ?? 0;
                var message = comp["message"]?.ToString();
                var icon = compStatus == "healthy" ? "✓" : (compStatus == "degraded" ? "⚠" : "✗");
                sb.AppendLine($"  {icon} {name}: {compStatus} ({latency:N1}ms)");
                if (!string.IsNullOrEmpty(message))
                {
                    sb.AppendLine($"     → {message}");
                }
            }

            MessageBox.Show(sb.ToString(), "Agent Health Check", MessageBoxButtons.OK,
                status == "healthy" ? MessageBoxIcon.Information :
                status == "degraded" ? MessageBoxIcon.Warning : MessageBoxIcon.Error);

            SetStatus($"Health: {status}");
            _healthCheckButton.Enabled = true;
        }

        private void RefreshButton_Click(object sender, EventArgs e)
        {
            _ = LoadTelemetryAsync();
        }

        internal async Task LoadTelemetryAsync()
        {
            if (_loading) return;
            _loading = true;
            _refreshButton.Enabled = false;
            SetStatus("Loading telemetry...");

            var summaryResult = await AgentPanelClient.GetJsonAsync("/agent/telemetry/summary");
            if (summaryResult.Ok)
            {
                var summary = summaryResult.Data ?? new JObject();
                _summaryLabel.Text = $"Req {summary["totalRequests"] ?? 0} | Err {summary["errorRate"] ?? 0} | Avg {summary["averageLatencyMs"] ?? 0} ms";
            }
            else
            {
                _summaryLabel.Text = "Telemetry summary unavailable";
            }

            var eventsResult = await AgentPanelClient.GetJsonAsync("/agent/telemetry/events",
                new Dictionary<string, string> { { "limit", "200" } });
            if (!eventsResult.Ok)
            {
                SetStatus("Telemetry fetch failed: " + eventsResult.Error, isError: true);
                _refreshButton.Enabled = true;
                _loading = false;
                return;
            }

            _events.Clear();
            var events = eventsResult.Data?["events"] as JArray ?? new JArray();
            foreach (var token in events)
            {
                if (token is JObject obj)
                {
                    _events.Add(TelemetryEvent.FromJson(obj));
                }
            }

            _list.VirtualListSize = _events.Count;
            _list.Invalidate();
            SetStatus(_events.Count == 0 ? "No telemetry events." : $"Loaded {_events.Count} events.");
            _refreshButton.Enabled = true;
            _loading = false;
        }

        private void SetStatus(string message, bool isError = false)
        {
            _statusLabel.Text = message;
            _statusLabel.ForeColor = isError ? AgentPanelTheme.StatusError : AgentPanelTheme.TextSecondary;
        }

        private void List_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            if (e.ItemIndex < 0 || e.ItemIndex >= _events.Count)
            {
                e.Item = new ListViewItem(string.Empty);
                return;
            }

            var evt = _events[e.ItemIndex];
            var timeText = evt.Timestamp > 0
                ? DateTimeOffset.FromUnixTimeSeconds((long)evt.Timestamp).ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture)
                : "-";
            var item = new ListViewItem(timeText);
            item.SubItems.Add(evt.Type ?? "-");
            item.SubItems.Add(evt.Operation ?? "-");
            item.SubItems.Add(evt.Success ? "PASS" : "FAIL");
            item.SubItems.Add(evt.DurationMs > 0 ? evt.DurationMs.ToString("0", CultureInfo.InvariantCulture) + " ms" : "-");
            item.ForeColor = evt.Success ? AgentPanelTheme.TextPrimary : AgentPanelTheme.StatusError;
            e.Item = item;
        }
    }

    internal class TelemetryEvent
    {
        internal double Timestamp { get; set; }
        internal string Type { get; set; }
        internal string Operation { get; set; }
        internal bool Success { get; set; }
        internal double DurationMs { get; set; }

        internal static TelemetryEvent FromJson(JObject obj)
        {
            return new TelemetryEvent
            {
                Timestamp = obj["timestamp"]?.ToObject<double?>() ?? 0,
                Type = obj["type"]?.ToString(),
                Operation = obj["operation"]?.ToString(),
                Success = obj["success"]?.ToObject<bool?>() ?? false,
                DurationMs = obj["duration_ms"]?.ToObject<double?>() ?? 0
            };
        }
    }
}
