using System;
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

        [TestInitialize]
        public void Setup()
        {
            _server = new TestHttpServer(17189);
            _server.Start();
            AgentPanelClient.BaseUrl = _server.BaseUrl;
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
            _server.RegisterHandler("/agent/knowledge_base/hotset", context => {
                var json = new JObject {
                    ["hotset"] = new JArray {
                        new JObject {
                            ["customer"] = "Acme Corp",
                            ["rank"] = 0.95,
                            ["last_seen"] = DateTime.UtcNow.ToString("O")
                        }
                    }
                };
                return json.ToString();
            });

            InvokePrivateMethod(_control, "RefreshButton_Click", new object[] { null, EventArgs.Empty });
            System.Threading.Thread.Sleep(500);

            var list = GetPrivateField<ListView>(_control, "_list");
            Assert.AreEqual(1, list.Items.Count, "Hotset list should have one item");
            Assert.AreEqual("Acme Corp", list.Items[0].Text);
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
