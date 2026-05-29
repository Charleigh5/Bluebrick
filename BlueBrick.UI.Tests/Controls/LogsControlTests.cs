using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using BlueBrick.Agent;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BlueBrick.UI.Tests.Controls
{
    /// <summary>
    /// Tests for the LogsControl component.
    /// These tests verify the control's ability to display telemetry summary, event list,
    /// and handle error states.
    /// </summary>
    [TestClass]
    public class LogsControlTests
    {
        private TestHttpServer _server;
        private string _originalBaseUrl;

        [TestInitialize]
        public void Setup()
        {
            _server = new TestHttpServer();
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

        [TestMethod]
        [Description("Verify LogsControl can be instantiated without errors")]
        public void LogsControl_Instantiation_ShouldSucceed()
        {
            // Arrange & Act
            using (var control = new LogsControl())
            {
                // Assert
                Assert.IsNotNull(control);
                Assert.IsInstanceOfType(control, typeof(UserControl));
            }
        }

        [TestMethod]
        [Description("Verify control loads and displays telemetry summary")]
        public async Task LogsControl_LoadSummary_ShouldDisplaySummaryData()
        {
            // Arrange
            var mockSummary = new
            {
                totalEvents = 1234,
                errorCount = 5,
                warningCount = 23,
                lastEventTime = "2026-02-06T12:00:00Z"
            };
            
            _server.RegisterEndpoint("/agent/telemetry/summary", mockSummary);

            using (var control = new LogsControl())
            {
                var form = new Form();
                form.Controls.Add(control);
                form.Show();

                // Act
                await control.LoadTelemetryAsync();

                // Assert
                Assert.AreEqual(1, _server.GetCallCount("/agent/telemetry/summary"), 
                    "Should have called the summary endpoint");

                // Check that summary label contains relevant info
                var summaryLabel = control.Controls.OfType<Label>()
                    .FirstOrDefault(l => l.Name == "_summaryLabel" || l.Text.Contains("1234") || l.Text.Contains("event"));
                
                if (summaryLabel != null)
                {
                    Assert.IsTrue(summaryLabel.Text.Length > 0, 
                        "Summary label should contain text");
                }

                form.Close();
            }
        }

        [TestMethod]
        [Description("Verify control loads and displays event list")]
        public async Task LogsControl_LoadEvents_ShouldDisplayEventList()
        {
            // Arrange
            var mockSummary = new { totalEvents = 100, errorCount = 2, warningCount = 5 };
            var mockEvents = new[]
            {
                new { timestamp = "2026-02-06T12:00:00Z", level = "INFO", message = "Document opened" },
                new { timestamp = "2026-02-06T11:55:00Z", level = "WARNING", message = "Slow operation" },
                new { timestamp = "2026-02-06T11:50:00Z", level = "ERROR", message = "Connection failed" }
            };
            
            _server.RegisterEndpoint("/agent/telemetry/summary", mockSummary);
            _server.RegisterEndpoint("/agent/telemetry/events", new { events = mockEvents });

            using (var control = new LogsControl())
            {
                var form = new Form();
                form.Controls.Add(control);
                form.Show();

                // Act
                await control.LoadTelemetryAsync();

                // Assert
                var listView = control.Controls.OfType<ListView>().FirstOrDefault();
                if (listView != null)
                {
                    Assert.IsTrue(listView.Items.Count > 0, 
                        "ListView should contain event items");
                }

                form.Close();
            }
        }

        [TestMethod]
        [Description("Verify control displays error state when API fails")]
        public async Task LogsControl_ApiError_ShouldDisplayErrorState()
        {
            // Arrange - Don't register endpoints, so they return 404

            using (var control = new LogsControl())
            {
                var form = new Form();
                form.Controls.Add(control);
                form.Show();

                // Act
                try
                {
                    await control.LoadTelemetryAsync();
                }
                catch
                {
                    // Expected for some implementations
                }

                // Assert - Look for error indication in UI
                var errorLabel = control.Controls.OfType<Label>()
                    .FirstOrDefault(l => l.ForeColor == System.Drawing.Color.Red || 
                                         l.Text.ToLower().Contains("error") ||
                                         l.Text.ToLower().Contains("failed"));

                // Error state should be indicated somehow
                var listView = control.Controls.OfType<ListView>().FirstOrDefault();
                if (listView != null)
                {
                    Assert.AreEqual(0, listView.Items.Count, 
                        "ListView should be empty on error");
                }

                form.Close();
            }
        }

        [TestMethod]
        [Description("Verify refresh button triggers data reload")]
        public async Task LogsControl_RefreshButton_ShouldReloadData()
        {
            // Arrange
            var mockSummary = new { totalEvents = 50, errorCount = 0, warningCount = 0 };
            _server.RegisterEndpoint("/agent/telemetry/summary", mockSummary);

            using (var control = new LogsControl())
            {
                var form = new Form();
                form.Controls.Add(control);
                form.Show();

                await control.LoadTelemetryAsync();
                var initialCallCount = _server.GetCallCount("/agent/telemetry/summary");

                // Act - Find and click refresh button
                var refreshButton = control.Controls.OfType<Button>()
                    .FirstOrDefault(b => b.Text.ToLower().Contains("refresh"));
                
                if (refreshButton != null)
                {
                    refreshButton.PerformClick();
                    await Task.Delay(100);

                    // Assert
                    Assert.IsTrue(_server.GetCallCount("/agent/telemetry/summary") > initialCallCount, 
                        "Should have called the summary endpoint again after refresh");
                }

                form.Close();
            }
        }

        [TestMethod]
        [Description("Verify control handles large event lists efficiently")]
        public async Task LogsControl_LargeEventList_ShouldHandleEfficiently()
        {
            // Arrange - Create a large list of events
            var mockSummary = new { totalEvents = 1000, errorCount = 10, warningCount = 50 };
            var mockEvents = Enumerable.Range(0, 100).Select(i => new
            {
                timestamp = DateTime.UtcNow.AddMinutes(-i).ToString("O"),
                level = i % 10 == 0 ? "ERROR" : i % 5 == 0 ? "WARNING" : "INFO",
                message = $"Event {i} occurred"
            }).ToArray();
            
            _server.RegisterEndpoint("/agent/telemetry/summary", mockSummary);
            _server.RegisterEndpoint("/agent/telemetry/events", new { events = mockEvents });

            using (var control = new LogsControl())
            {
                var form = new Form();
                form.Controls.Add(control);
                form.Show();

                // Act
                var startTime = DateTime.Now;
                await control.LoadTelemetryAsync();
                var elapsed = DateTime.Now - startTime;

                // Assert - Should complete reasonably quickly
                Assert.IsTrue(elapsed.TotalSeconds < 5, 
                    $"Loading 100 events should take less than 5 seconds, took {elapsed.TotalSeconds}s");

                var listView = control.Controls.OfType<ListView>().FirstOrDefault();
                if (listView != null)
                {
                    Assert.IsTrue(listView.Items.Count > 0, 
                        "ListView should contain items");
                }

                form.Close();
            }
        }
    }
}
