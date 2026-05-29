using System;
using System.Collections.Generic;
using System.Threading;
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
            _server = new TestHttpServer(17188);
            _server.Start();
            AgentPanelClient.BaseUrl = _server.BaseUrl;

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
            _server.RegisterHandler("/agent/telemetry/trace", context =>
            {
                var events = new JArray
                {
                    new JObject
                    {
                        ["timestamp"] = 1700000000.0,
                        ["type"] = "task",
                        ["operation"] = "TestOperation",
                        ["success"] = true,
                        ["duration_ms"] = 50.0,
                        ["source"] = "agent"
                    }
                };
                var json = new JObject
                {
                    ["events"] = events
                };
                return json.ToString();
            });

            var traceInput = GetPrivateField<TextBox>(_control, "_traceInput");
            traceInput.Text = "test-123";

            var fetchButton = GetPrivateField<Button>(_control, "_fetchButton");
            InvokePrivateMethod(_control, "FetchButton_Click", new object[] { fetchButton, EventArgs.Empty });

            var timeout = DateTime.UtcNow.AddSeconds(5);
            var list = GetPrivateField<ListView>(_control, "_list");
            while (list.VirtualListSize == 0 && DateTime.UtcNow < timeout)
            {
                System.Threading.Thread.Sleep(100);
                Application.DoEvents();
            }

            Assert.AreEqual(1, list.VirtualListSize, "List should have one event loaded");
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
