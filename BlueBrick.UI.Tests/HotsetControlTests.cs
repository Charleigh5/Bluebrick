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
    public class HotsetControlTests
    {
        private TestHttpServer _server;
        private HotsetControl _control;

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
            _server = new TestHttpServer(17189);
            _server.Start();
            AgentPanelClient.BaseUrl = _server.BaseUrl;

            _server.RegisterHandler("/agent/knowledge_base/refresh", context => new JObject { ["ok"] = true }.ToString());
            _server.RegisterHandler("/agent/knowledge_base/hotset", context =>
            {
                var json = new JObject
                {
                    ["customers"] = new JArray
                    {
                        new JObject
                        {
                            ["rank"] = 1,
                            ["customerId"] = "Acme Corp",
                            ["ruleCount"] = 5,
                            ["reason"] = "Top customer"
                        }
                    },
                    ["hotCacheSize"] = 10
                };
                return json.ToString();
            });

            _control = new HotsetControl();
        }

        [TestCleanup]
        public void Teardown()
        {
            _control?.Dispose();
            _server?.Stop();
        }

        [TestMethod]
        public void TestHotsetLoading()
        {
            InvokePrivateMethod(_control, "RefreshButton_Click", new object[] { null, EventArgs.Empty });

            var timeout = DateTime.UtcNow.AddSeconds(5);
            var list = GetPrivateField<ListView>(_control, "_list");
            while (list.VirtualListSize == 0 && DateTime.UtcNow < timeout)
            {
                System.Threading.Thread.Sleep(100);
                Application.DoEvents();
            }

            Assert.AreEqual(1, list.VirtualListSize, "Hotset list should have one item");
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
