using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BlueBrick.Agent
{
    internal class TraceViewerControl : UserControl
    {
        private readonly TextBox _traceInput;
        private readonly Button _fetchButton;
        private readonly Button _pillAgent;
        private readonly Button _pillAddin;
        private readonly Button _pillBoth;
        private readonly Label _statusLabel;
        private readonly Label _summaryLabel;
        private readonly ListView _list;
        private readonly RichTextBox _details;
        private readonly SplitContainer _split;
        private readonly Panel _warningPanel;
        private readonly Label _warningLabel;

        private readonly List<TraceEvent> _allEvents = new List<TraceEvent>();
        private List<TraceEvent> _filteredEvents = new List<TraceEvent>();
        private string _sourceFilter = "both";
        private int _limit = 500;
        private bool _loading;

        internal event Action<int> LimitChanged;

        internal TraceViewerControl()
        {
            Dock = DockStyle.Fill;
            BackColor = AgentPanelTheme.Surface;

            var header = new Label
            {
                Text = "Trace Viewer",
                Dock = DockStyle.Top,
                Height = 32,
                Padding = new Padding(12, 6, 12, 0)
            };
            AgentPanelTheme.ApplyHeaderLabel(header);

            var topBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 66,
                Padding = new Padding(12, 6, 12, 6)
            };
            AgentPanelTheme.ApplyPanel(topBar);

            var traceLabel = new Label
            {
                Text = "Trace ID",
                AutoSize = true,
                Location = new Point(0, 6)
            };
            AgentPanelTheme.ApplySubtleLabel(traceLabel);

            _traceInput = new TextBox
            {
                Width = 220,
                Location = new Point(0, 26)
            };
            AgentPanelTheme.ApplyTextBox(_traceInput);
            _traceInput.KeyDown += TraceInput_KeyDown;

            _fetchButton = new Button
            {
                Text = "Fetch",
                Width = 72,
                Height = 30,
                Location = new Point(230, 24)
            };
            AgentPanelTheme.ApplyPrimaryButton(_fetchButton);
            _fetchButton.Click += FetchButton_Click;

            _pillAgent = new Button { Text = "Agent", Location = new Point(320, 24) };
            _pillAddin = new Button { Text = "Add-in", Location = new Point(398, 24) };
            _pillBoth = new Button { Text = "Both", Location = new Point(476, 24) };

            AgentPanelTheme.ApplyPill(_pillAgent, false);
            AgentPanelTheme.ApplyPill(_pillAddin, false);
            AgentPanelTheme.ApplyPill(_pillBoth, true);

            _pillAgent.Click += (s, e) => SetSourceFilter("agent");
            _pillAddin.Click += (s, e) => SetSourceFilter("addin");
            _pillBoth.Click += (s, e) => SetSourceFilter("both");

            _summaryLabel = new Label
            {
                AutoSize = true,
                Location = new Point(560, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            AgentPanelTheme.ApplySubtleLabel(_summaryLabel);

            topBar.Resize += (s, e) =>
            {
                var rightPad = 12;
                _summaryLabel.Location = new Point(Math.Max(rightPad, topBar.Width - _summaryLabel.Width - rightPad), _summaryLabel.Location.Y);
            };

            topBar.Controls.Add(traceLabel);
            topBar.Controls.Add(_traceInput);
            topBar.Controls.Add(_fetchButton);
            topBar.Controls.Add(_pillAgent);
            topBar.Controls.Add(_pillAddin);
            topBar.Controls.Add(_pillBoth);
            topBar.Controls.Add(_summaryLabel);

            _warningPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 26,
                Visible = false,
                Padding = new Padding(12, 2, 12, 2),
                BackColor = AgentPanelTheme.SurfaceAlt
            };
            _warningLabel = new Label
            {
                AutoSize = true,
                ForeColor = AgentPanelTheme.StatusWarning,
                Font = AgentPanelTheme.BodyFont(9.5f, FontStyle.Bold),
                Text = ""
            };
            _warningPanel.Controls.Add(_warningLabel);

            _split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 260,
                BackColor = AgentPanelTheme.Surface
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
            _list.Columns.Add("Source", 70);
            _list.Columns.Add("Operation", 220);
            _list.Columns.Add("Status", 80);
            _list.Columns.Add("Latency", 80);
            _list.RetrieveVirtualItem += List_RetrieveVirtualItem;
            _list.SelectedIndexChanged += List_SelectedIndexChanged;

            _details = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = AgentPanelTheme.SurfaceAlt,
                ForeColor = AgentPanelTheme.TextPrimary,
                Font = AgentPanelTheme.BodyFont(9.5f, FontStyle.Regular)
            };

            _split.Panel1.Controls.Add(_list);
            _split.Panel2.Controls.Add(_details);

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

            Controls.Add(_split);
            Controls.Add(footer);
            Controls.Add(_warningPanel);
            Controls.Add(topBar);
            Controls.Add(header);

            UpdateSummary();
            SetStatus("Ready.");
        }

        internal void SetLimit(int limit)
        {
            if (limit <= 0) return;
            _limit = limit;
            LimitChanged?.Invoke(limit);
            UpdateSummary();
        }

        internal void SetTraceId(string traceId)
        {
            _traceInput.Text = traceId ?? string.Empty;
        }

        internal async Task LoadTracesAsync()
        {
            if (!string.IsNullOrWhiteSpace(_traceInput.Text))
            {
                await FetchTraceAsync().ConfigureAwait(false);
                return;
            }
            SetStatus("Enter a trace ID to load traces.");
        }

        private void TraceInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                _ = FetchTraceAsync();
            }
        }

        private void FetchButton_Click(object sender, EventArgs e)
        {
            _ = FetchTraceAsync();
        }

        private async Task FetchTraceAsync()
        {
            if (_loading) return;
            var traceId = _traceInput.Text.Trim();
            if (string.IsNullOrEmpty(traceId))
            {
                SetStatus("Trace ID is required.", isError: true);
                return;
            }

            _loading = true;
            SetStatus("Fetching trace data...");
            _fetchButton.Enabled = false;
            _warningPanel.Visible = false;

            var result = await AgentPanelClient.GetJsonAsync("/agent/telemetry/trace",
                new Dictionary<string, string>
                {
                    { "traceId", traceId },
                    { "limit", _limit.ToString(CultureInfo.InvariantCulture) }
                });

            if (!result.Ok)
            {
                SetStatus("Trace fetch failed: " + result.Error, isError: true);
                _fetchButton.Enabled = true;
                _loading = false;
                return;
            }

            var payload = result.Data ?? new JObject();
            var events = payload["events"] as JArray ?? new JArray();
            _allEvents.Clear();
            foreach (var token in events)
            {
                if (token is JObject obj)
                {
                    _allEvents.Add(TraceEvent.FromJson(obj));
                }
            }

            var addinError = payload["addinError"]?.ToString();
            if (!string.IsNullOrEmpty(addinError))
            {
                _warningPanel.Visible = true;
                _warningLabel.Text = "Add-in unavailable: " + addinError;
            }
            else
            {
                _warningPanel.Visible = false;
            }

            ApplyFilter();
            UpdateSummary(payload);
            SetStatus(_allEvents.Count == 0
                ? "No events found for trace ID."
                : $"Loaded {_allEvents.Count} events.");

            _fetchButton.Enabled = true;
            _loading = false;
        }

        private void SetSourceFilter(string source)
        {
            _sourceFilter = source;
            AgentPanelTheme.ApplyPill(_pillAgent, source == "agent");
            AgentPanelTheme.ApplyPill(_pillAddin, source == "addin");
            AgentPanelTheme.ApplyPill(_pillBoth, source == "both");
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            _filteredEvents = _allEvents
                .Where(evt => _sourceFilter == "both" || string.Equals(evt.Source, _sourceFilter, StringComparison.OrdinalIgnoreCase))
                .OrderBy(evt => evt.Timestamp)
                .ToList();
            _list.VirtualListSize = _filteredEvents.Count;
            _list.Invalidate();
            _details.Clear();
        }

        private void UpdateSummary(JObject payload = null)
        {
            var counts = payload?["counts"] as JObject;
            var agentCount = counts?["agent"]?.ToObject<int?>() ?? _allEvents.Count(e => e.Source == "agent");
            var addinCount = counts?["addin"]?.ToObject<int?>() ?? _allEvents.Count(e => e.Source == "addin");
            _summaryLabel.Text = $"Agent {agentCount} | Add-in {addinCount} | Limit {_limit}";
        }

        private void SetStatus(string message, bool isError = false)
        {
            _statusLabel.Text = message;
            _statusLabel.ForeColor = isError ? AgentPanelTheme.StatusError : AgentPanelTheme.TextSecondary;
        }

        private void List_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            if (e.ItemIndex < 0 || e.ItemIndex >= _filteredEvents.Count)
            {
                e.Item = new ListViewItem(string.Empty);
                return;
            }

            var evt = _filteredEvents[e.ItemIndex];
            var timeText = evt.Timestamp > 0
                ? DateTimeOffset.FromUnixTimeSeconds((long)evt.Timestamp).ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture)
                : "-";
            var item = new ListViewItem(timeText);
            item.SubItems.Add(evt.Source ?? "-");
            item.SubItems.Add(evt.Operation ?? evt.Type ?? "-");
            item.SubItems.Add(evt.Success ? "PASS" : "FAIL");
            item.SubItems.Add(evt.DurationMs > 0 ? evt.DurationMs.ToString("0", CultureInfo.InvariantCulture) + " ms" : "-");
            item.ForeColor = evt.Success ? AgentPanelTheme.TextPrimary : AgentPanelTheme.StatusError;
            e.Item = item;
        }

        private void List_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_list.SelectedIndices.Count == 0) return;
            var idx = _list.SelectedIndices[0];
            if (idx < 0 || idx >= _filteredEvents.Count) return;
            var evt = _filteredEvents[idx];
            _details.Text = evt.Raw.ToString(Formatting.Indented);
        }
    }

    internal class TraceEvent
    {
        internal double Timestamp { get; set; }
        internal string Type { get; set; }
        internal string Operation { get; set; }
        internal bool Success { get; set; }
        internal double DurationMs { get; set; }
        internal string Source { get; set; }
        internal JObject Raw { get; set; }

        internal static TraceEvent FromJson(JObject obj)
        {
            return new TraceEvent
            {
                Timestamp = obj["timestamp"]?.ToObject<double?>() ?? 0,
                Type = obj["type"]?.ToString(),
                Operation = obj["operation"]?.ToString(),
                Success = obj["success"]?.ToObject<bool?>() ?? false,
                DurationMs = obj["duration_ms"]?.ToObject<double?>() ?? 0,
                Source = obj["source"]?.ToString(),
                Raw = obj
            };
        }
    }
}
