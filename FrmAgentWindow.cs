using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using BlueBrick.Agent;

namespace BlueBrick
{
    public class FrmAgentWindow : Form
    {
        private readonly Panel _navPanel;
        private readonly Panel _contentPanel;
        private readonly Button _navTrace;
        private readonly Button _navHotset;
        private readonly Button _navLogs;
        private readonly Panel _statusDot;
        private readonly Label _statusLabel;
        private readonly Panel _contextPanel;
        private readonly Panel _traceFilters;
        private readonly Panel _hotsetFilters;
        private readonly ComboBox _traceLimitCombo;
        private readonly TrackBar _hotsetTopSlider;
        private readonly Label _hotsetTopLabel;

        private readonly TraceViewerControl _traceView;
        private readonly HotsetControl _hotsetView;
        private readonly LogsControl _logsView;

        private readonly Timer _statusTimer;

        public FrmAgentWindow()
        {
            Text = "VIRA Agent";
            Width = 980;
            Height = 680;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = AgentPanelTheme.Base;
            MinimumSize = new Size(860, 540);

            _navPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 240,
                BackColor = AgentPanelTheme.Base
            };

            var brandPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                Padding = new Padding(16, 14, 16, 8)
            };
            var brandLabel = new Label
            {
                Text = "VIRA Agent",
                Dock = DockStyle.Top,
                Height = 26
            };
            AgentPanelTheme.ApplyHeaderLabel(brandLabel);

            var statusRow = new Panel { Dock = DockStyle.Top, Height = 18 };
            _statusDot = new Panel
            {
                Width = 10,
                Height = 10,
                BackColor = AgentPanelTheme.StatusWarning,
                Location = new Point(0, 4)
            };
            _statusLabel = new Label
            {
                Text = "Connecting...",
                AutoSize = true,
                Location = new Point(16, 2)
            };
            AgentPanelTheme.ApplySubtleLabel(_statusLabel);
            statusRow.Controls.Add(_statusDot);
            statusRow.Controls.Add(_statusLabel);

            brandPanel.Controls.Add(statusRow);
            brandPanel.Controls.Add(brandLabel);

            _navTrace = new Button { Text = "Trace Viewer" };
            _navHotset = new Button { Text = "KB Hot-Set" };
            _navLogs = new Button { Text = "Logs" };
            _navTrace.Width = 200;
            _navHotset.Width = 200;
            _navLogs.Width = 200;
            _navTrace.AutoSize = false;
            _navHotset.AutoSize = false;
            _navLogs.AutoSize = false;
            _navTrace.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            _navHotset.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            _navLogs.Anchor = AnchorStyles.Left | AnchorStyles.Right;

            _navTrace.Click += (s, e) => ShowView("trace");
            _navHotset.Click += (s, e) => ShowView("hotset");
            _navLogs.Click += (s, e) => ShowView("logs");

            var navStack = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                Padding = new Padding(8, 0, 8, 0)
            };
            navStack.Controls.Add(_navTrace);
            navStack.Controls.Add(_navHotset);
            navStack.Controls.Add(_navLogs);

            _contextPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 160,
                Padding = new Padding(16, 12, 16, 12)
            };
            AgentPanelTheme.ApplyPanel(_contextPanel);

            var contextLabel = new Label
            {
                Text = "Quick Filters",
                AutoSize = true,
                Location = new Point(0, 0)
            };
            AgentPanelTheme.ApplySubtleLabel(contextLabel);
            _contextPanel.Controls.Add(contextLabel);

            _traceFilters = new Panel
            {
                Location = new Point(0, 24),
                Width = 200,
                Height = 60
            };
            var traceLimitLabel = new Label
            {
                Text = "Trace Limit",
                AutoSize = true,
                Location = new Point(0, 0)
            };
            AgentPanelTheme.ApplySubtleLabel(traceLimitLabel);
            _traceLimitCombo = new ComboBox
            {
                Location = new Point(0, 22),
                Width = 120,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _traceLimitCombo.BackColor = AgentPanelTheme.SurfaceAlt;
            _traceLimitCombo.ForeColor = AgentPanelTheme.TextPrimary;
            _traceLimitCombo.FlatStyle = FlatStyle.Flat;
            _traceLimitCombo.Font = AgentPanelTheme.BodyFont(9.5f, FontStyle.Bold);
            _traceLimitCombo.Items.AddRange(new object[] { "100", "500", "2000" });
            _traceLimitCombo.SelectedIndexChanged += TraceLimitCombo_SelectedIndexChanged;
            _traceLimitCombo.SelectedIndex = 1;
            _traceFilters.Controls.Add(traceLimitLabel);
            _traceFilters.Controls.Add(_traceLimitCombo);

            _hotsetFilters = new Panel
            {
                Location = new Point(0, 24),
                Width = 200,
                Height = 80,
                Visible = false
            };
            var hotsetLabel = new Label
            {
                Text = "Top N",
                AutoSize = true,
                Location = new Point(0, 0)
            };
            AgentPanelTheme.ApplySubtleLabel(hotsetLabel);
            _hotsetTopSlider = new TrackBar
            {
                Minimum = 5,
                Maximum = 50,
                TickFrequency = 5,
                Value = 10,
                Width = 160,
                Location = new Point(0, 22)
            };
            _hotsetTopSlider.BackColor = AgentPanelTheme.Surface;
            _hotsetTopSlider.Scroll += HotsetTopSlider_Scroll;
            _hotsetTopLabel = new Label
            {
                Text = "10",
                AutoSize = true,
                Location = new Point(168, 28)
            };
            AgentPanelTheme.ApplySubtleLabel(_hotsetTopLabel);
            _hotsetFilters.Controls.Add(hotsetLabel);
            _hotsetFilters.Controls.Add(_hotsetTopSlider);
            _hotsetFilters.Controls.Add(_hotsetTopLabel);

            _contextPanel.Controls.Add(_traceFilters);
            _contextPanel.Controls.Add(_hotsetFilters);

            _navPanel.Controls.Add(_contextPanel);
            _navPanel.Controls.Add(navStack);
            _navPanel.Controls.Add(brandPanel);

            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = AgentPanelTheme.Surface
            };

            _traceView = new TraceViewerControl();
            _hotsetView = new HotsetControl();
            _logsView = new LogsControl();

            _contentPanel.Controls.Add(_traceView);
            _contentPanel.Controls.Add(_hotsetView);
            _contentPanel.Controls.Add(_logsView);

            Controls.Add(_contentPanel);
            Controls.Add(_navPanel);

            _statusTimer = new Timer { Interval = 10000 };
            _statusTimer.Tick += async (s, e) => await UpdateStatusAsync();

            ShowView("trace");
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _statusTimer.Start();
            _ = UpdateStatusAsync();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _statusTimer.Stop();
            _statusTimer.Dispose();
            base.OnFormClosed(e);
        }

        private void TraceLimitCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (int.TryParse(_traceLimitCombo.SelectedItem?.ToString(), out var limit))
            {
                _traceView.SetLimit(limit);
            }
        }

        private void HotsetTopSlider_Scroll(object sender, EventArgs e)
        {
            _hotsetTopLabel.Text = _hotsetTopSlider.Value.ToString();
            _hotsetView.SetTopN(_hotsetTopSlider.Value);
        }

        private void ShowView(string view)
        {
            _traceView.Visible = view == "trace";
            _hotsetView.Visible = view == "hotset";
            _logsView.Visible = view == "logs";

            AgentPanelTheme.ApplyNavButton(_navTrace, view == "trace");
            AgentPanelTheme.ApplyNavButton(_navHotset, view == "hotset");
            AgentPanelTheme.ApplyNavButton(_navLogs, view == "logs");

            _traceFilters.Visible = view == "trace";
            _hotsetFilters.Visible = view == "hotset";
        }

        private async Task UpdateStatusAsync()
        {
            var result = await AgentPanelClient.GetJsonAsync("/agent/telemetry/summary");
            if (result.Ok)
            {
                _statusDot.BackColor = AgentPanelTheme.StatusSuccess;
                _statusLabel.Text = "Online";
            }
            else
            {
                _statusDot.BackColor = AgentPanelTheme.StatusError;
                _statusLabel.Text = "Offline";
            }
        }
    }
}
