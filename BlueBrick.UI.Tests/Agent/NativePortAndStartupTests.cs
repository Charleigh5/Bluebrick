using System;
using System.Collections.Generic;
using System.IO;
using BlueBrick.Agent;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BlueBrick.UI.Tests.Agent
{
    [TestClass]
    public class NativePortAndStartupTests
    {
        [TestMethod]
        public void AgentConfig_LoadFrom_UsesConfiguredPortFromIdentitySpecificConfigPath()
        {
            var root = CreateConfigRoot();
            try
            {
                WriteConfig(root, 23456);

                var config = AgentConfig.LoadFrom(root);

                Assert.AreEqual(23456, config.Agent.BridgePort);
            }
            finally
            {
                DeleteConfigRoot(root);
            }
        }

        [TestMethod]
        public void AgentConfig_LoadFrom_UsesIdentityDefaultWhenConfigIsMissing()
        {
            var root = CreateConfigRoot();
            try
            {
                var config = AgentConfig.LoadFrom(root);

                Assert.AreEqual(AppIdentity.BridgePort, config.Agent.BridgePort);
            }
            finally
            {
                DeleteConfigRoot(root);
            }
        }

        [TestMethod]
        public void AgentConfig_LoadFrom_UsesIdentityDefaultWhenConfiguredPortIsZero()
        {
            var root = CreateConfigRoot();
            try
            {
                WriteConfig(root, 0);

                var config = AgentConfig.LoadFrom(root);

                Assert.AreEqual(AppIdentity.BridgePort, config.Agent.BridgePort);
            }
            finally
            {
                DeleteConfigRoot(root);
            }
        }

        [TestMethod]
        public void AgentConfig_LoadFrom_UsesIdentityDefaultWhenConfiguredPortIsInvalid()
        {
            var root = CreateConfigRoot();
            try
            {
                WriteConfig(root, 70000);

                var config = AgentConfig.LoadFrom(root);

                Assert.AreEqual(AppIdentity.BridgePort, config.Agent.BridgePort);
            }
            finally
            {
                DeleteConfigRoot(root);
            }
        }

        [TestMethod]
        public void ResolveBridgePort_ReturnsConfiguredValidPort()
        {
            Assert.AreEqual(23456, AgentConfig.ResolveBridgePort(23456, 17178));
        }

        [TestMethod]
        public void ResolveBridgePort_UsesProductionFallbackForZero()
        {
            Assert.AreEqual(17178, AgentConfig.ResolveBridgePort(0, 17178));
        }

        [TestMethod]
        public void ResolveBridgePort_UsesLabFallbackForZero()
        {
            Assert.AreEqual(17179, AgentConfig.ResolveBridgePort(0, 17179));
        }

        [TestMethod]
        public void ResolveBridgePort_UsesFallbackForNegativePort()
        {
            Assert.AreEqual(17178, AgentConfig.ResolveBridgePort(-1, 17178));
        }

        [TestMethod]
        public void ResolveBridgePort_UsesFallbackForPortAboveMaximum()
        {
            Assert.AreEqual(17178, AgentConfig.ResolveBridgePort(65536, 17178));
        }

        [TestMethod]
        public void AppIdentity_UsesConsistentBuildIdentityAndBridgePort()
        {
            var expectedPort = AppIdentity.IsLabBuild ? 17179 : 17178;
            var expectedConfigFile = AppIdentity.IsLabBuild ? "appsettings.lab.json" : "appsettings.json";

            Assert.AreEqual(expectedPort, AppIdentity.BridgePort);
            Assert.AreEqual(expectedConfigFile, Path.GetFileName(AppIdentity.ConfigPath("root")));
        }

        [TestMethod]
        public void SwAddin_StartAgentBridge_ExecutesLoadResolveCreateStartOrder()
        {
            var events = new List<string>();
            var config = new AgentConfig
            {
                Agent = new AgentSettings { BridgePort = 0 }
            };

            var server = SwAddin.StartAgentBridge(
                () =>
                {
                    events.Add("LOAD_CONFIG");
                    return config;
                },
                loadedConfig =>
                {
                    loadedConfig.Agent.BridgePort = AgentConfig.ResolveBridgePort(
                        loadedConfig.Agent.BridgePort,
                        17179);
                    events.Add("RESOLVE_CONFIGURE_PORT:" + loadedConfig.Agent.BridgePort);
                },
                loadedConfig =>
                {
                    Assert.AreEqual(17179, loadedConfig.Agent.BridgePort);
                    events.Add("CREATE_BRIDGE_SERVER");
                    return new FakeBridgeServer();
                },
                createdServer =>
                {
                    events.Add("START_BRIDGE_SERVER");
                    createdServer.Started = true;
                });

            CollectionAssert.AreEqual(
                new[]
                {
                    "LOAD_CONFIG",
                    "RESOLVE_CONFIGURE_PORT:17179",
                    "CREATE_BRIDGE_SERVER",
                    "START_BRIDGE_SERVER"
                },
                events);
            Assert.IsTrue(server.Started);
        }

        private static string CreateConfigRoot()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "BlueBrick-NativePortTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "config"));
            return root;
        }

        private static void WriteConfig(string root, int bridgePort)
        {
            File.WriteAllText(
                AppIdentity.ConfigPath(root),
                "{\"Agent\":{\"BridgePort\":" + bridgePort + "}}");
        }

        private static void DeleteConfigRoot(string root)
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        private sealed class FakeBridgeServer
        {
            internal bool Started { get; set; }
        }
    }
}
