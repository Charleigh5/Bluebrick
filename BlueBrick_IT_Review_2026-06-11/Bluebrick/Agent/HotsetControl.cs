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
    internal class HotsetControl : UserControl
    {
        private readonly ListView _list;
        private readonly Label _summaryLabel;
        private readonly Label _statusLabel;
        private readonly Button _refreshButton;
        private readonly Button _exportButton;
        private JObject _lastPayload;
        private bool _loading;
        private int _topN = 10;
        private readonly List<HotsetEntry> _entries = new List<HotsetEntry>();
        private List<HotsetEntry> _visibleEntries = new List<HotsetEntry>();

        internal HotsetControl()
        {
            Dock = DockStyle.Fill;
            BackColor = AgentPanelTheme.Surface;

            var header = new Label
            {
                Text = "KB Hot-Set",
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

            _refreshButton = new Button { Text = "Refresh", Width = 86, Height = 30, Location = new Point(520, 12) };
            AgentPanelTheme.ApplyPrimaryButton(_refreshButton);
            _refreshButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _refreshButton.Click += RefreshButton_Click;

            _exportButton = new Button { Text = "Export", Width = 86, Height = 30, Location = new Point(612, 12) };
            AgentPanelTheme.ApplySecondaryButton(_exportButton);
            _exportButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _exportButton.Click += ExportButton_Click;

            topBar.Controls.Add(_summaryLabel);
            topBar.Controls.Add(_refreshButton);
            topBar.Controls.Add(_exportButton);

            topBar.Resize += (s, e) =>
            {
                var rightPad = 12;
                _exportButton.Location = new Point(topBar.Width - _exportButton.Width - rightPad, _exportButton.Location.Y);
                _refreshButton.Location = new Point(_exportButton.Left - _refreshButton.Width - 8, _refreshButton.Location.Y);
            };

            _list = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                VirtualMode = true,
                FullRowSelect = true
            };
            AgentPanelTheme.ApplyListView(_list);
            _list.Columns.Add("Rank", 60);
            _list.Columns.Add("Customer", 220);
            _list.Columns.Add("Rules", 80);
            _list.Columns.Add("Reason", 240);
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
            _ = LoadHotsetAsync();
        }

        internal void SetTopN(int topN)
        {
            if (topN <= 0) return;
            _topN = topN;
            ApplyFilter();
        }

        private void RefreshButton_Click(object sender, EventArgs e)
        {
            _ = RefreshHotsetAsync();
        }

        private void ExportButton_Click(object sender, EventArgs e)
        {
            if (_lastPayload == null)
            {
                SetStatus("Nothing to export.", isError: true);
                return;
            }

            try
            {
                Clipboard.SetText(_lastPayload.ToString(Formatting.Indented));
                SetStatus("Hot-set JSON copied to clipboard.");
            }
            catch (Exception ex)
            {
                SetStatus("Clipboard error: " + ex.Message, isError: true);
            }
        }

        private async Task RefreshHotsetAsync()
        {
            if (_loading) return;
            _loading = true;
            _refreshButton.Enabled = false;
            SetStatus("Refreshing knowledge base...");

            var refreshResult = await AgentPanelClient.PostJsonAsync("/agent/knowledge_base/refresh");
            if (!refreshResult.Ok)
            {
                SetStatus("Refresh failed: " + refreshResult.Error, isError: true);
            }
            else
            {
                SetStatus("Knowledge base refreshed.");
            }

            await LoadHotsetAsync();

            _refreshButton.Enabled = true;
            _loading = false;
        }

        internal async Task LoadHotsetAsync()
        {
            var result = await AgentPanelClient.GetJsonAsync("/agent/knowledge_base/hotset");
            if (!result.Ok)
            {
                SetStatus("Hot-set fetch failed: " + result.Error, isError: true);
                return;
            }

            _lastPayload = result.Data;
            _entries.Clear();
            var customers = result.Data?["customers"] as JArray ?? new JArray();
            foreach (var token in customers)
            {
                if (token is JObject obj)
                {
                    _entries.Add(HotsetEntry.FromJson(obj));
                }
            }

            ApplyFilter();
            var hotCacheSize = result.Data?["hotCacheSize"]?.ToObject<int?>() ?? _topN;
            _summaryLabel.Text = $"Hot cache {hotCacheSize} | Showing {_visibleEntries.Count}";
            SetStatus(_entries.Count == 0 ? "No hot-set data returned." : $"Loaded {_entries.Count} customers.");
        }

        private void ApplyFilter()
        {
            _visibleEntries = _entries
                .OrderBy(e => e.Rank)
                .Take(_topN)
                .ToList();
            _list.VirtualListSize = _visibleEntries.Count;
            _list.Invalidate();
        }

        private void SetStatus(string message, bool isError = false)
        {
            _statusLabel.Text = message;
            _statusLabel.ForeColor = isError ? AgentPanelTheme.StatusError : AgentPanelTheme.TextSecondary;
        }

        private void List_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            if (e.ItemIndex < 0 || e.ItemIndex >= _visibleEntries.Count)
            {
                e.Item = new ListViewItem(string.Empty);
                return;
            }

            var entry = _visibleEntries[e.ItemIndex];
            var item = new ListViewItem(entry.Rank.ToString(CultureInfo.InvariantCulture));
            item.SubItems.Add(entry.CustomerId ?? "-");
            item.SubItems.Add(entry.RuleCount.ToString(CultureInfo.InvariantCulture));
            item.SubItems.Add(entry.Reason ?? "-");
            e.Item = item;
        }
    }

    internal class HotsetEntry
    {
        internal int Rank { get; set; }
        internal string CustomerId { get; set; }
        internal int RuleCount { get; set; }
        internal string Reason { get; set; }

        internal static HotsetEntry FromJson(JObject obj)
        {
            return new HotsetEntry
            {
                Rank = obj["rank"]?.ToObject<int?>() ?? 0,
                CustomerId = obj["customerId"]?.ToString() ?? obj["name"]?.ToString(),
                RuleCount = obj["ruleCount"]?.ToObject<int?>() ?? 0,
                Reason = obj["reason"]?.ToString()
            };
        }
    }
}
