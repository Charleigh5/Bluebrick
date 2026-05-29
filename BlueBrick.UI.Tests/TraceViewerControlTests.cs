using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BlueBrick.Agent;
using BlueBrick.UI.Tests.Stubs;
using Newtonsoft.Json.Linq;

namespace BlueBrick.UI.Tests
{
    [TestClass]
    public class TraceViewerControlTests
    {
        private TestHttpServer _server;
        private TraceViewerControl _control;

        [TestInitialize]
        public void Setup()
        {
            _server = new TestHttpServer(17188); // Use different port than default
            _server.Start();
            AgentPanelClient.BaseUrl = _server.BaseUrl;
            
            // Note: In a real environment, we'd need an STA thread for WinForms
            _control = new TraceViewerControl();
        }

        [TestCleanup]
        public void Teardown()
        {
            _control?.Dispose();
            _server?.Stop();
        }

        [TestMethod]
        public void TestTraceLoading()
        {
            // Setup mock handler
            _server.RegisterHandler("/agent/telemetry/trace/test-123", context => {
                var json = new JObject {
                    ["traceId"] = "test-123",
                    ["events"] = new JArray {
                        new JObject {
                            ["timestamp"] = DateTime.UtcNow.ToString("O"),
                            ["source"] = "agent",
                            ["event"] = "TaskStarted",
                            ["data"] = new JObject { ["task"] = "Test Task" }
                        }
                    }
                };
                return json.ToString();
            });

            // Trigger fetch (using reflection if private, but let's assume public or accessible)
            var traceIdBox = GetPrivateField<TextBox>(_control, "_traceIdBox");
            traceIdBox.Text = "test-123";
            
            var fetchBtn = GetPrivateField<Button>(_control, "_fetchBtn");
            
            // We can't easily wait for async in WinForms event handlers in tests without extra sync
            // but we can call the method directly if we make it accessible or use reflection
            InvokePrivateMethod(_control, "FetchButton_Click", new object[] { fetchBtn, EventArgs.Empty });

            // Since it's async, we might need a small delay or a better way to wait
            System.Threading.Thread.Sleep(500);

            var list = GetPrivateField<ListView>(_control, "_list");
            Assert.AreEqual(1, list.VirtualListSize, "List should have one event loaded");
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
