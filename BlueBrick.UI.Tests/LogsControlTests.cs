using System;
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

        [TestInitialize]
        public void Setup()
        {
            _server = new TestHttpServer(17190);
            _server.Start();
            AgentPanelClient.BaseUrl = _server.BaseUrl;
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
            _server.RegisterHandler("/agent/telemetry/summary", context => {
                var json = new JObject {
                    ["total_events"] = 42,
                    ["last_event_time"] = DateTime.UtcNow.ToString("O"),
                    ["error_count"] = 2
                };
                return json.ToString();
            });

            _server.RegisterHandler("/agent/telemetry/tail", context => {
                var json = new JArray {
                    new JObject {
                        ["timestamp"] = DateTime.UtcNow.ToString("O"),
                        ["level"] = "INFO",
                        ["message"] = "Test log message"
                    }
                };
                return json.ToString();
            });

            InvokePrivateMethod(_control, "RefreshButton_Click", new object[] { null, EventArgs.Empty });
            System.Threading.Thread.Sleep(500);

            var summaryLabel = GetPrivateField<Label>(_control, "_summaryLabel");
            Assert.IsTrue(summaryLabel.Text.Contains("42"), "Summary label should contain total event count");
        }

        private T GetPrivateField<T>(object obj, string fieldName)
        {
            var field = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (T)field.GetValue(obj);
        }

        private void InvokePrivateMethod(object obj, string methodName, object[] args)
        {
            var method = obj.GetType().GetMethod(methodName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method.Invoke(obj, args);
        }
    }
}
