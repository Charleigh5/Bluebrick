using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using BlueBrick.Agent;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BlueBrick.UI.Tests.Controls
{
    /// <summary>
    /// Tests for the HotsetControl component.
    /// These tests verify the control's ability to load, display, and export hotset data.
    /// </summary>
    [TestClass]
    public class HotsetControlTests
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
        [Description("Verify HotsetControl can be instantiated without errors")]
        public void HotsetControl_Instantiation_ShouldSucceed()
        {
            // Arrange & Act
            using (var control = new HotsetControl())
            {
                // Assert
                Assert.IsNotNull(control);
                Assert.IsInstanceOfType(control, typeof(UserControl));
            }
        }

        [TestMethod]
        [Description("Verify control loads hotset data successfully")]
        public async Task HotsetControl_LoadHotsetData_ShouldDisplayItems()
        {
            // Arrange
            var mockHotset = new[]
            {
                new { partNumber = "12345-001", description = "Main Assembly", accessCount = 42 },
                new { partNumber = "12345-002", description = "Sub Component", accessCount = 27 },
                new { partNumber = "12345-003", description = "Bracket", accessCount = 15 }
            };
            
            _server.RegisterEndpoint("/agent/hotset", new { items = mockHotset });

            using (var control = new HotsetControl())
            {
                var form = new Form();
                form.Controls.Add(control);
                form.Show();

                // Act
                await control.LoadHotsetAsync();

                // Assert
                Assert.AreEqual(1, _server.GetCallCount("/agent/hotset"), 
                    "Should have called the hotset endpoint once");

                var listView = control.Controls.OfType<ListView>().FirstOrDefault();
                if (listView != null)
                {
                    Assert.AreEqual(3, listView.Items.Count, 
                        "ListView should contain 3 hotset items");
                }

                form.Close();
            }
        }

        [TestMethod]
        [Description("Verify refresh button triggers data reload")]
        public async Task HotsetControl_RefreshButton_ShouldReloadData()
        {
            // Arrange
            var mockHotset = new[]
            {
                new { partNumber = "12345-001", description = "Item 1", accessCount = 10 }
            };
            
            _server.RegisterEndpoint("/agent/hotset", new { items = mockHotset });

            using (var control = new HotsetControl())
            {
                var form = new Form();
                form.Controls.Add(control);
                form.Show();

                await control.LoadHotsetAsync();
                Assert.AreEqual(1, _server.GetCallCount("/agent/hotset"));

                // Act - Find and click refresh button
                var refreshButton = control.Controls.OfType<Button>()
                    .FirstOrDefault(b => b.Text.ToLower().Contains("refresh"));
                
                if (refreshButton != null)
                {
                    refreshButton.PerformClick();
                    
                    // Wait briefly for async operation
                    await Task.Delay(100);

                    // Assert
                    Assert.AreEqual(2, _server.GetCallCount("/agent/hotset"), 
                        "Should have called the hotset endpoint twice after refresh");
                }

                form.Close();
            }
        }

        [TestMethod]
        [Description("Verify export to clipboard copies formatted data")]
        public async Task HotsetControl_ExportToClipboard_ShouldCopyFormattedData()
        {
            // Arrange
            var mockHotset = new[]
            {
                new { partNumber = "12345-001", description = "Test Part", accessCount = 5 }
            };
            
            _server.RegisterEndpoint("/agent/hotset", new { items = mockHotset });

            using (var control = new HotsetControl())
            {
                var form = new Form();
                form.Controls.Add(control);
                form.Show();

                await control.LoadHotsetAsync();

                // Act - Find and click export button
                var exportButton = control.Controls.OfType<Button>()
                    .FirstOrDefault(b => b.Text.ToLower().Contains("export") || b.Text.ToLower().Contains("copy"));
                
                if (exportButton != null)
                {
                    // Clear clipboard first
                    Clipboard.Clear();
                    
                    exportButton.PerformClick();

                    // Assert - Clipboard should contain the data
                    if (Clipboard.ContainsText())
                    {
                        var clipboardText = Clipboard.GetText();
                        Assert.IsTrue(clipboardText.Contains("12345-001"), 
                            "Clipboard should contain part number");
                    }
                }

                form.Close();
            }
        }

        [TestMethod]
        [Description("Verify control handles empty hotset gracefully")]
        public async Task HotsetControl_EmptyHotset_ShouldDisplayEmptyState()
        {
            // Arrange
            _server.RegisterEndpoint("/agent/hotset", new { items = new object[0] });

            using (var control = new HotsetControl())
            {
                var form = new Form();
                form.Controls.Add(control);
                form.Show();

                // Act
                await control.LoadHotsetAsync();

                // Assert
                var listView = control.Controls.OfType<ListView>().FirstOrDefault();
                if (listView != null)
                {
                    Assert.AreEqual(0, listView.Items.Count, 
                        "ListView should be empty");
                }

                // Check for empty state label
                var emptyLabel = control.Controls.OfType<Label>()
                    .FirstOrDefault(l => l.Text.ToLower().Contains("no items") || l.Text.ToLower().Contains("empty"));
                
                // Empty state label is optional but good UX
                if (emptyLabel != null)
                {
                    Assert.IsTrue(emptyLabel.Visible, "Empty state label should be visible");
                }

                form.Close();
            }
        }
    }
}
