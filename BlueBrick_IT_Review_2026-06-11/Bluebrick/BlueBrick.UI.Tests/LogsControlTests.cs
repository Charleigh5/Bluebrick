using System;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BlueBrick.Agent;
using BlueBrick.UI.Tests.Stubs;
using Newtonsoft.Json.Linq;

namespace BlueBrick.UI.Tests
{
    [TestClass]
    public class LogsControlTests
    {
        private TestHttpServer _server;
        private LogsControl _control;

        private static void RequireWinFormsUi()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                Assert.Inconclusive("WinForms UI control test requires Windows.");
            }
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                Assert.Inconclusive("WinForms UI control test requires STA thread.");
            }
            try
            {
                using (var probe = new Control())
                {
                    var handle = probe.Handle;
                }
            }
            catch (Exception ex) when (
                ex is InvalidOperationException ||
                ex is TypeInitializationException ||
                ex is System.ComponentModel.Win32Exception)
            {
                Assert.Inconclusive("WinForms UI control test requires an interactive/headed UI environment: " + ex.Message);
            }
        }

        [TestInitialize]
        public void Setup()
        {
            RequireWinFormsUi();
            _server = new TestHttpServer(17190);
            _server.Start();
            AgentPanelClient.BaseUrl = _server.BaseUrl;

            _server.RegisterHandler("/agent/telemetry/summary", context =>
            {
                var json = new JObject
                {
                    ["totalRequests"] = 42,
                    ["errorRate"] = "0.05",
                    ["averageLatencyMs"] = 120
                };
                return json.ToString();
            });

            _server.RegisterHandler("/agent/telemetry/events", context =>
            {
                var json = new JObject
                {
                    ["events"] = new JArray
                    {
                        new JObject
                        {
                            ["timestamp"] = 1700000000.0,
                            ["type"] = "request",
                            ["operation"] = "TestOperation",
                            ["success"] = true,
                            ["duration_ms"] = 50.0
                        }
                    }
                };
                return json.ToString();
            });

            _control = new LogsControl();
        }

        [TestCleanup]
        public void Teardown()
        {
            _control?.Dispose();
            _server?.Stop();
        }

        [TestMethod]
        public void TestTelemetryLoading()
        {
            InvokePrivateMethod(_control, "RefreshButton_Click", new object[] { null, EventArgs.Empty });

            var timeout = DateTime.UtcNow.AddSeconds(5);
            var summaryLabel = GetPrivateField<Label>(_control, "_summaryLabel");
            while (!summaryLabel.Text.Contains("42") && DateTime.UtcNow < timeout)
            {
                System.Threading.Thread.Sleep(100);
                Application.DoEvents();
            }

            Assert.IsTrue(summaryLabel.Text.Contains("42"), "Summary label should contain total request count");
        }

        private T GetPrivateField<T>(object obj, string fieldName)
        {
            var field = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field == null) throw new NullReferenceException($"Field '{fieldName}' not found on {obj.GetType().Name}");
            return (T)field.GetValue(obj);
        }

        private void InvokePrivateMethod(object obj, string methodName, object[] args)
        {
            var method = obj.GetType().GetMethod(methodName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method.Invoke(obj, args);
        }
    }
}
