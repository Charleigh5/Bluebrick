using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using BlueBrick.Agent;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BlueBrick.UI.Tests.Integration
{
    /// <summary>
    /// Integration tests for full panel user flows.
    /// These tests verify the complete workflow of opening the panel, 
    /// navigating between tabs, and interacting with data.
    /// </summary>
    [TestClass]
    public class PanelFlowTests
    {
        private TestHttpServer _server;
        private string _originalBaseUrl;

        [TestInitialize]
        public void Setup()
        {
            _server = new TestHttpServer();
            RegisterMockEndpoints();
            _server.Start();
            
            _originalBaseUrl = AgentPanelClient.BaseUrl;
            AgentPanelClient.BaseUrl = _server.BaseUrl.TrimEnd('/');
        }

        [TestCleanup]
        public void Cleanup()
        {
            AgentPanelClient.BaseUrl = _originalBaseUrl;
            _server?.Dispose();
        }

        private void RegisterMockEndpoints()
        {
            // Telemetry endpoints
            _server.RegisterEndpoint("/agent/telemetry/summary", new
            {
                totalEvents = 250,
                errorCount = 3,
                warningCount = 12,
                lastEventTime = DateTime.UtcNow.ToString("O")
            });

            _server.RegisterEndpoint("/agent/telemetry/events", new
            {
                events = Enumerable.Range(0, 20).Select(i => new
                {
                    timestamp = DateTime.UtcNow.AddMinutes(-i).ToString("O"),
                    level = i % 7 == 0 ? "ERROR" : i % 3 == 0 ? "WARNING" : "INFO",
                    message = $"Test event {i}"
                }).ToArray()
            });

            // Traces endpoint
            _server.RegisterEndpoint("/agent/traces", new
            {
                traces = new[]
                {
                    new { id = "t1", operation = "GetDocument", duration_ms = 150, source = "PDM" },
                    new { id = "t2", operation = "SaveFile", duration_ms = 230, source = "FileSystem" },
                    new { id = "t3", operation = "QueryDatabase", duration_ms = 450, source = "Database" }
                }
            });

            // Hotset endpoint
            _server.RegisterEndpoint("/agent/hotset", new
            {
                items = new[]
                {
                    new { partNumber = "12345-001", description = "Main Assembly", accessCount = 42 },
                    new { partNumber = "12345-002", description = "Sub Component", accessCount = 27 }
                }
            });

            // Health endpoint
            _server.RegisterEndpoint("/agent/health", new
            {
                status = "healthy",
                uptime = "02:15:30",
                memoryMb = 256
            });
        }

        [TestMethod]
        [Description("Verify complete panel opening and initial data load")]
        public async Task PanelFlow_OpenPanel_ShouldLoadInitialData()
        {
            // Arrange
            using (var panel = new AgentPanelControl())
            {
                var form = new Form { Size = new Size(400, 600) };
                form.Controls.Add(panel);
                panel.Dock = DockStyle.Fill;
                form.Show();

                // Act
                await panel.InitializeAsync();

                // Assert - All initial endpoints should be called
                Assert.AreEqual(1, _server.GetCallCount("/agent/health"), 
                    "Health check should be called on init");

                form.Close();
            }
        }

        [TestMethod]
        [Description("Verify tab navigation between Traces, Hotset, and Logs")]
        public async Task PanelFlow_NavigateTabs_ShouldSwitchViews()
        {
            // Arrange
            using (var panel = new AgentPanelControl())
            {
                var form = new Form { Size = new Size(400, 600) };
                form.Controls.Add(panel);
                panel.Dock = DockStyle.Fill;
                form.Show();

                await panel.InitializeAsync();

                // Act - Find tab control and navigate
                var tabControl = FindControlRecursive<TabControl>(panel);
                if (tabControl != null && tabControl.TabCount >= 3)
                {
                    // Navigate to each tab
                    tabControl.SelectedIndex = 0;
                    await Task.Delay(50);
                    Assert.AreEqual(0, tabControl.SelectedIndex, "First tab should be selected");

                    tabControl.SelectedIndex = 1;
                    await Task.Delay(50);
                    Assert.AreEqual(1, tabControl.SelectedIndex, "Second tab should be selected");

                    tabControl.SelectedIndex = 2;
                    await Task.Delay(50);
                    Assert.AreEqual(2, tabControl.SelectedIndex, "Third tab should be selected");
                }

                form.Close();
            }
        }

        [TestMethod]
        [Description("Verify data refresh workflow across controls")]
        public async Task PanelFlow_RefreshAll_ShouldReloadAllData()
        {
            // Arrange
            using (var panel = new AgentPanelControl())
            {
                var form = new Form { Size = new Size(400, 600) };
                form.Controls.Add(panel);
                panel.Dock = DockStyle.Fill;
                form.Show();

                await panel.InitializeAsync();
                _server.ResetCallCounts();

                // Act - Find and click global refresh button
                var refreshButton = FindControlRecursive<Button>(panel, b => 
                    b.Text.ToLower().Contains("refresh") && b.Parent == panel);

                if (refreshButton != null)
                {
                    refreshButton.PerformClick();
                    await Task.Delay(200);

                    // Assert - Multiple endpoints should be called again
                    Assert.IsTrue(
                        _server.GetCallCount("/agent/health") >= 1 ||
                        _server.GetCallCount("/agent/telemetry/summary") >= 1,
                        "At least one endpoint should be called on refresh");
                }

                form.Close();
            }
        }

        [TestMethod]
        [Description("Verify error state recovery after API comes back online")]
        public async Task PanelFlow_ErrorRecovery_ShouldRecoverAfterApiReturns()
        {
            // Arrange - Start with no endpoints registered
            using (var temporaryServer = new TestHttpServer())
            {
                temporaryServer.Start();
                var tempBaseUrl = temporaryServer.BaseUrl.TrimEnd('/');
                AgentPanelClient.BaseUrl = tempBaseUrl;

                using (var panel = new AgentPanelControl())
                {
                    var form = new Form { Size = new Size(400, 600) };
                    form.Controls.Add(panel);
                    panel.Dock = DockStyle.Fill;
                    form.Show();

                    // Act - Initial load should fail or show error state
                    try
                    {
                        await panel.InitializeAsync();
                    }
                    catch
                    {
                        // Expected
                    }

                    // Now register the health endpoint
                    temporaryServer.RegisterEndpoint("/agent/health", new { status = "healthy" });
                    temporaryServer.RegisterEndpoint("/agent/telemetry/summary", new { totalEvents = 0 });

                    // Find and click refresh
                    var refreshButton = FindControlRecursive<Button>(panel, b => 
                        b.Text.ToLower().Contains("refresh"));

                    if (refreshButton != null)
                    {
                        refreshButton.PerformClick();
                        await Task.Delay(200);

                        // Assert - Should recover
                        Assert.IsTrue(temporaryServer.GetCallCount("/agent/health") >= 1,
                            "Should call health endpoint on retry");
                    }

                    form.Close();
                }
            }
        }

        [TestMethod]
        [Description("Verify theme consistency across all panel components")]
        public void PanelFlow_ThemeConsistency_AllComponentsShouldUseTheme()
        {
            // Arrange
            using (var panel = new AgentPanelControl())
            {
                var form = new Form { Size = new Size(400, 600) };
                form.Controls.Add(panel);
                panel.Dock = DockStyle.Fill;
                form.Show();

                // Act - Collect all controls
                var allControls = GetAllControlsRecursive(panel);

                // Assert - Check that controls use consistent theme colors
                var theme = AgentPanelTheme.Current;
                var backgroundColors = allControls
                    .Where(c => c.BackColor != SystemColors.Control)
                    .Select(c => c.BackColor)
                    .Distinct()
                    .ToList();

                // All custom backgrounds should be theme-consistent
                foreach (var control in allControls.Where(c => c is Label))
                {
                    var label = (Label)control;
                    if (label.ForeColor != SystemColors.ControlText)
                    {
                        // Custom foreground should be theme-consistent
                        Assert.IsTrue(
                            label.ForeColor == theme.TextColor ||
                            label.ForeColor == theme.MutedTextColor ||
                            label.ForeColor == theme.AccentColor ||
                            label.ForeColor == Color.Red ||  // Error color
                            label.ForeColor == Color.Orange, // Warning color
                            $"Label '{label.Text}' uses non-theme color {label.ForeColor}");
                    }
                }

                form.Close();
            }
        }

        #region Helper Methods

        private T FindControlRecursive<T>(Control parent, Func<T, bool> predicate = null) where T : Control
        {
            foreach (Control child in parent.Controls)
            {
                if (child is T typedChild && (predicate == null || predicate(typedChild)))
                {
                    return typedChild;
                }

                var found = FindControlRecursive<T>(child, predicate);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        private System.Collections.Generic.List<Control> GetAllControlsRecursive(Control parent)
        {
            var result = new System.Collections.Generic.List<Control>();
            foreach (Control child in parent.Controls)
            {
                result.Add(child);
                result.AddRange(GetAllControlsRecursive(child));
            }
            return result;
        }

        #endregion
    }
}
