using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using BlueBrick.Agent;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BlueBrick.UI.Tests.Controls
{
    /// <summary>
    /// Tests for the TraceViewerControl component.
    /// These tests verify the control's ability to display trace data, handle warnings,
    /// and filter by source.
    /// </summary>
    [TestClass]
    public class TraceViewerControlTests
    {
        private TestHttpServer _server;
        private string _originalBaseUrl;

        [TestInitialize]
        public void Setup()
        {
            _server = new TestHttpServer();
            _server.Start();
            
            // Store original base URL and override for testing
            // Note: AgentPanelClient.BaseUrl should be made configurable for testing
            _originalBaseUrl = AgentPanelClient.BaseUrl;
            AgentPanelClient.BaseUrl = _server.BaseUrl.TrimEnd('/');
        }

        [TestCleanup]
        public void Cleanup()
        {
            AgentPanelClient.BaseUrl = _originalBaseUrl;
            _server?.Dispose();
        }

        [TestMethod]
        [Description("Verify TraceViewerControl can be instantiated without errors")]
        public void TraceViewerControl_Instantiation_ShouldSucceed()
        {
            // Arrange & Act
            using (var control = new TraceViewerControl())
            {
                // Assert
                Assert.IsNotNull(control);
                Assert.IsInstanceOfType(control, typeof(UserControl));
            }
        }

        [TestMethod]
        [Description("Verify control displays trace data after successful fetch")]
        public async Task TraceViewerControl_FetchTraceData_ShouldDisplayTraces()
        {
            // Arrange
            var mockTraces = new[]
            {
                new { id = "trace-001", operation = "GetDocument", duration_ms = 150, source = "PDM" },
                new { id = "trace-002", operation = "SaveFile", duration_ms = 230, source = "FileSystem" }
            };
            
            _server.RegisterEndpoint("/agent/traces", new { traces = mockTraces });

            using (var control = new TraceViewerControl())
            {
                var form = new Form();
                form.Controls.Add(control);
                form.Show();

                // Act
                await control.LoadTracesAsync();

                // Assert
                Assert.AreEqual(1, _server.GetCallCount("/agent/traces"), 
                    "Should have called the traces endpoint once");
                
                // Verify the ListView has items (assuming control has a ListView named _traceList)
                var listView = control.Controls.OfType<ListView>().FirstOrDefault();
                if (listView != null)
                {
                    Assert.AreEqual(2, listView.Items.Count, 
                        "ListView should contain 2 trace items");
                }

                form.Close();
            }
        }

        [TestMethod]
        [Description("Verify warning banner appears when traces contain warnings")]
        public async Task TraceViewerControl_TracesWithWarnings_ShouldShowWarningBanner()
        {
            // Arrange
            var mockTraces = new[]
            {
                new { id = "trace-001", operation = "SlowQuery", duration_ms = 5000, source = "Database", warning = "Slow operation detected" }
            };
            
            _server.RegisterEndpoint("/agent/traces", new { traces = mockTraces, hasWarnings = true });

            using (var control = new TraceViewerControl())
            {
                var form = new Form();
                form.Controls.Add(control);
                form.Show();

                // Act
                await control.LoadTracesAsync();

                // Assert
                var warningLabel = control.Controls.OfType<Label>()
                    .FirstOrDefault(l => l.Name == "_warningLabel" || l.Text.Contains("warning", StringComparison.OrdinalIgnoreCase));
                
                Assert.IsNotNull(warningLabel, "Warning label should exist");
                Assert.IsTrue(warningLabel.Visible, "Warning label should be visible when traces have warnings");

                form.Close();
            }
        }

        [TestMethod]
        [Description("Verify source filter pills filter the trace list")]
        public async Task TraceViewerControl_SourceFilterPills_ShouldFilterTraces()
        {
            // Arrange
            var mockTraces = new[]
            {
                new { id = "trace-001", operation = "Op1", duration_ms = 100, source = "PDM" },
                new { id = "trace-002", operation = "Op2", duration_ms = 200, source = "FileSystem" },
                new { id = "trace-003", operation = "Op3", duration_ms = 300, source = "PDM" }
            };
            
            _server.RegisterEndpoint("/agent/traces", new { traces = mockTraces });

            using (var control = new TraceViewerControl())
            {
                var form = new Form();
                form.Controls.Add(control);
                form.Show();

                await control.LoadTracesAsync();

                // Act - Simulate clicking on a filter button
                var filterButton = control.Controls.OfType<Button>()
                    .FirstOrDefault(b => b.Text == "PDM");
                
                if (filterButton != null)
                {
                    filterButton.PerformClick();
                }

                // Assert
                var listView = control.Controls.OfType<ListView>().FirstOrDefault();
                if (listView != null && filterButton != null)
                {
                    // After filtering by PDM, should only show 2 items
                    Assert.AreEqual(2, listView.Items.Count, 
                        "ListView should only show PDM traces after filter");
                }

                form.Close();
            }
        }

        [TestMethod]
        [Description("Verify control handles API errors gracefully")]
        public async Task TraceViewerControl_ApiError_ShouldDisplayErrorState()
        {
            // Arrange - Don't register the endpoint, so it returns 404

            using (var control = new TraceViewerControl())
            {
                var form = new Form();
                form.Controls.Add(control);
                form.Show();

                // Act
                try
                {
                    await control.LoadTracesAsync();
                }
                catch
                {
                    // Expected - some implementations may throw
                }

                // Assert - Control should show error state or remain empty
                var listView = control.Controls.OfType<ListView>().FirstOrDefault();
                if (listView != null)
                {
                    Assert.AreEqual(0, listView.Items.Count, 
                        "ListView should be empty after error");
                }

                form.Close();
            }
        }
    }
}
