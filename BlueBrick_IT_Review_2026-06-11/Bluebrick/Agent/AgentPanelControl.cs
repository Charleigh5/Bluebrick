using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BlueBrick.Agent
{
    /// <summary>
    /// Main container control for the VIRA Agent Panel.
    /// Hosts TabControl with Traces, Hotset, and Logs tabs.
    /// </summary>
    public class AgentPanelControl : UserControl
    {
        private TabControl _tabControl;
        private TraceViewerControl _traceViewer;
        private HotsetControl _hotsetControl;
        private LogsControl _logsControl;

        public AgentPanelControl()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Apply theme
            this.BackColor = AgentPanelTheme.Current.BackgroundColor;

            // Create tab control
            _tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = AgentPanelTheme.Current.BodyFont
            };

            // Create tabs
            var tracesTab = new TabPage("Traces");
            _traceViewer = new TraceViewerControl { Dock = DockStyle.Fill };
            tracesTab.Controls.Add(_traceViewer);
            _tabControl.TabPages.Add(tracesTab);

            var hotsetTab = new TabPage("Hotset");
            _hotsetControl = new HotsetControl { Dock = DockStyle.Fill };
            hotsetTab.Controls.Add(_hotsetControl);
            _tabControl.TabPages.Add(hotsetTab);

            var logsTab = new TabPage("Logs");
            _logsControl = new LogsControl { Dock = DockStyle.Fill };
            logsTab.Controls.Add(_logsControl);
            _tabControl.TabPages.Add(logsTab);

            this.Controls.Add(_tabControl);

            this.ResumeLayout(false);
        }

        /// <summary>
        /// Initializes the panel and loads initial data.
        /// </summary>
        public async Task InitializeAsync()
        {
            // Check health first
            try
            {
                var healthResult = await AgentPanelClient.GetJsonAsync("/agent/telemetry/summary");
                // Health check passed, load data
            }
            catch (Exception ex)
            {
                // Health check failed - show error state
                System.Diagnostics.Debug.WriteLine($"Health check failed: {ex.Message}");
            }

            // Load initial data for visible tab
            await RefreshCurrentTabAsync();
        }

        /// <summary>
        /// Refreshes data for the currently selected tab.
        /// </summary>
        public async Task RefreshCurrentTabAsync()
        {
            switch (_tabControl.SelectedIndex)
            {
                case 0:
                    await _traceViewer.LoadTracesAsync();
                    break;
                case 1:
                    await _hotsetControl.LoadHotsetAsync();
                    break;
                case 2:
                    await _logsControl.LoadTelemetryAsync();
                    break;
            }
        }
    }
}
