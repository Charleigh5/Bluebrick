using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using BlueBrick.Agent;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
        public void AgentConfig_LoadFrom_BindsRepresentativeAssistantConfiguration()
        {
            var root = CreateConfigRoot();
            try
            {
                File.WriteAllText(
                    AppIdentity.ConfigPath(root),
                    "{\"Agent\":{\"BridgePort\":35001},\"Assistant\":{" +
                    "\"UseReactWebView\":true,\"EnableUploads\":true," +
                    "\"RequireExplicitUploadConsent\":true,\"Model\":\"known-test-value\"}}");

                var config = AgentConfig.LoadFrom(root);

                Assert.IsNotNull(config.Assistant, "Assistant section must bind as one explicit configuration contract.");
                Assert.AreEqual(35001, config.Agent.BridgePort);
                Assert.IsTrue(config.Assistant.UseReactWebView);
                Assert.IsTrue(config.Assistant.EnableUploads);
                Assert.IsTrue(config.Assistant.RequireExplicitUploadConsent);
                Assert.AreEqual("known-test-value", config.Assistant.Model);
            }
            finally
            {
                DeleteConfigRoot(root);
            }
        }

        [TestMethod]
        public void AgentConfig_LoadFrom_PreservesExplicitReactFalse()
        {
            var config = LoadRaw("{\"ConfigSchemaVersion\":\"1\",\"Agent\":{\"BridgePort\":17179},\"Assistant\":{\"UseReactWebView\":false,\"EnableUploads\":false,\"RequireExplicitUploadConsent\":false,\"Model\":\"test\"}}");

            Assert.IsFalse(config.Assistant.UseReactWebView);
            Assert.AreEqual("CONFIG_PRESENT_VALID", config.ConfigurationDiagnostics.ConfigurationLoadStatus);
            Assert.AreEqual("CONFIG_VALUE_EXPLICIT", config.ConfigurationDiagnostics.AssistantValueSource);
        }

        [TestMethod]
        public void AgentConfig_LoadFrom_MissingAssistantUsesDeclaredLegacyDefaults()
        {
            var config = LoadRaw("{\"ConfigSchemaVersion\":\"1\",\"Agent\":{\"BridgePort\":17179}}");

            Assert.IsNotNull(config.Assistant);
            Assert.IsFalse(config.Assistant.UseReactWebView);
            Assert.AreEqual("CONFIG_VALUE_DEFAULTED", config.ConfigurationDiagnostics.ConfigurationLoadStatus);
        }

        [TestMethod]
        public void AgentConfig_LoadFrom_InvalidReactTypeFailsVisibly()
        {
            AssertConfigFailure(
                "{\"ConfigSchemaVersion\":\"1\",\"Assistant\":{\"UseReactWebView\":\"banana\"}}",
                "CONFIG_PRESENT_INVALID");
        }

        [TestMethod]
        public void AgentConfig_LoadFrom_UnsupportedSchemaFailsVisibly()
        {
            AssertConfigFailure(
                "{\"ConfigSchemaVersion\":\"999\",\"Assistant\":{\"UseReactWebView\":true}}",
                "CONFIG_SCHEMA_UNSUPPORTED");
        }

        [TestMethod]
        public void AgentConfig_LoadFrom_LegacySchemaIsSupportedAndLabeled()
        {
            var config = LoadRaw("{\"Agent\":{\"BridgePort\":17179},\"Assistant\":{\"UseReactWebView\":true,\"EnableUploads\":true,\"RequireExplicitUploadConsent\":true,\"Model\":\"legacy\"}}");

            Assert.AreEqual("1", config.ConfigSchemaVersion);
            Assert.AreEqual("CONFIG_VALUE_DEFAULTED", config.ConfigurationDiagnostics.ConfigurationLoadStatus);
            Assert.IsTrue(config.Assistant.UseReactWebView);
        }

        [TestMethod]
        public void AgentConfig_SerializationRoundTripPreservesCriticalValues()
        {
            var original = LoadRaw("{\"ConfigSchemaVersion\":\"1\",\"Agent\":{\"BridgePort\":35001},\"Assistant\":{\"UseReactWebView\":true,\"EnableUploads\":true,\"RequireExplicitUploadConsent\":true,\"Model\":\"round-trip\"}}");
            var json = JsonConvert.SerializeObject(original);
            var copy = JsonConvert.DeserializeObject<AgentConfig>(json);

            Assert.AreEqual(35001, copy.Agent.BridgePort);
            Assert.IsTrue(copy.Assistant.UseReactWebView);
            Assert.IsTrue(copy.Assistant.EnableUploads);
            Assert.IsTrue(copy.Assistant.RequireExplicitUploadConsent);
            Assert.AreEqual("round-trip", copy.Assistant.Model);
        }

        [TestMethod]
        public void RuntimeGenerationGuard_AcceptsMatchingLabGeneration()
        {
            var root = CreateGenerationRoot();
            try
            {
                var result = RuntimeGenerationGuard.Validate(
                    Path.Combine(root, "runtime-manifest.json"), "Lab",
                    Path.Combine(root, "BlueBrick.Lab.dll"), Path.Combine(root, "appsettings.lab.json"),
                    Path.Combine(root, "dist"), "1");

                Assert.IsTrue(result.IsMatch, result.Detail);
                Assert.AreEqual("RUNTIME_GENERATION_MATCH", result.Code);
                Assert.AreEqual("test-build", result.BuildId);
            }
            finally
            {
                DeleteConfigRoot(root);
            }
        }

        [TestMethod]
        public void RuntimeGenerationGuard_ReportsExpectedAndObservedForMismatch()
        {
            var root = CreateGenerationRoot();
            try
            {
                File.AppendAllText(Path.Combine(root, "dist", "assistant-web.js"), "tampered");
                var result = RuntimeGenerationGuard.Validate(
                    Path.Combine(root, "runtime-manifest.json"), "Lab",
                    Path.Combine(root, "BlueBrick.Lab.dll"), Path.Combine(root, "appsettings.lab.json"),
                    Path.Combine(root, "dist"), "1");

                Assert.IsFalse(result.IsMatch);
                Assert.AreEqual("RUNTIME_GENERATION_MISMATCH", result.Code);
                StringAssert.Contains(result.Detail, "expected");
                StringAssert.Contains(result.Detail, "observed");
            }
            finally
            {
                DeleteConfigRoot(root);
            }
        }

        [TestMethod]
        public void RuntimeGenerationGuard_V2RequiresEmbeddedFrontendBuildIdentity()
        {
            var root = CreateV2GenerationRoot(false);
            try
            {
                File.WriteAllText(Path.Combine(root, "dist", "index.html"), "<meta name=\"bluebrick-build-id\" content=\"other-build\">");
                RewriteGenerationManifestHashes(root, false);
                var result = ValidateGenerationRoot(root);
                Assert.IsFalse(result.IsMatch);
                StringAssert.Contains(result.Detail, "embedded frontend build identity");
            }
            finally { DeleteConfigRoot(root); }
        }

        [TestMethod]
        public void RuntimeGenerationGuard_V2RejectsUnknownOrDuplicateEmbeddedFrontendBuildIdentity()
        {
            var root = CreateV2GenerationRoot(false);
            try
            {
                File.WriteAllText(Path.Combine(root, "dist", "index.html"), "<meta name=\"bluebrick-build-id\" content=\"UNKNOWN\">");
                RewriteGenerationManifestHashes(root, false);
                Assert.IsFalse(ValidateGenerationRoot(root).IsMatch);

                File.WriteAllText(Path.Combine(root, "dist", "index.html"), "<meta name=\"bluebrick-build-id\" content=\"test-build\"><meta name=\"bluebrick-build-id\" content=\"test-build\">");
                RewriteGenerationManifestHashes(root, false);
                Assert.IsFalse(ValidateGenerationRoot(root).IsMatch);
            }
            finally { DeleteConfigRoot(root); }
        }

        [TestMethod]
        public void RuntimeGenerationGuard_RejectsMalformedLegacyConfiguration()
        {
            var root = CreateGenerationRoot();
            try
            {
                File.WriteAllText(Path.Combine(root, "appsettings.lab.json"), "not-json");
                RewriteGenerationManifestHashes(root, false, true);
                var result = ValidateGenerationRoot(root);
                Assert.IsFalse(result.IsMatch);
                StringAssert.Contains(result.Detail, "invalid runtime config");
            }
            finally { DeleteConfigRoot(root); }
        }

        [TestMethod]
        public void RuntimeGenerationGuard_OptedInV2RequiresUntamperedSharedAiCatalogs()
        {
            var root = CreateV2GenerationRoot(true);
            try
            {
                Assert.IsTrue(ValidateGenerationRoot(root).IsMatch);
                File.AppendAllText(Path.Combine(root, "SharedAI", "catalog", "models.json"), "tampered");
                var result = ValidateGenerationRoot(root);
                Assert.IsFalse(result.IsMatch);
                StringAssert.Contains(result.Detail, "SharedAI catalog models.json hash");
                File.Delete(Path.Combine(root, "SharedAI", "catalog", "routes.json"));
                RewriteGenerationManifestHashes(root, true, false);
                result = ValidateGenerationRoot(root);
                Assert.IsFalse(result.IsMatch);
                StringAssert.Contains(result.Detail, "routes.json");
            }
            finally { DeleteConfigRoot(root); }
        }

        [TestMethod]
        public void RuntimeGenerationGuard_V2RejectsInvalidSharedAiReferences()
        {
            var root = CreateV2GenerationRoot(true);
            try
            {
                foreach (var ids in new[] { "null", "[]", "[1]", "[\"\"]", "[\"unknown\"]", "[\"nvidia-kimi-k3\",\"nvidia-kimi-k3\"]" })
                {
                    File.WriteAllText(Path.Combine(root, "appsettings.lab.json"), "{\"Assistant\":{\"SharedAiModelIds\":" + ids + "}}");
                    RewriteGenerationManifestHashes(root, true);
                    Assert.IsFalse(ValidateGenerationRoot(root).IsMatch, ids);
                }
            }
            finally { DeleteConfigRoot(root); }
        }

        [TestMethod]
        public void RuntimeGenerationGuard_CountsAlternateAttributeOrderAndEmptyDuplicates()
        {
            var root = CreateV2GenerationRoot(false);
            try
            {
                Assert.IsTrue(ValidateGenerationRoot(root).IsMatch);
                File.WriteAllText(Path.Combine(root, "dist", "index.html"), "<meta content=\"test-build\" name=\"bluebrick-build-id\">");
                RewriteGenerationManifestHashes(root, true);
                Assert.IsTrue(ValidateGenerationRoot(root).IsMatch);
                File.AppendAllText(Path.Combine(root, "dist", "index.html"), "<meta content=\"\" name=\"bluebrick-build-id\">");
                RewriteGenerationManifestHashes(root, true);
                Assert.IsFalse(ValidateGenerationRoot(root).IsMatch);
            }
            finally { DeleteConfigRoot(root); }
        }

        [TestMethod]
        public void RuntimeGenerationGuard_CommentsDoNotSupplyOrDuplicateIdentity()
        {
            var root = CreateV2GenerationRoot(false);
            try
            {
                var index = Path.Combine(root, "dist", "index.html");
                var meta = "<meta name=\"bluebrick-build-id\" content=\"test-build\">";
                foreach (var invalid in new[] { meta.Replace("test-build", "test<!--ignore-->-build"), "<!-- " + meta + " -->", "<script>const tag = '" + meta + "';</script>", meta.Replace(" name=", " data-name="), meta.Replace(" content=", " data-content=") })
                {
                    File.WriteAllText(index, invalid);
                    RewriteGenerationManifestHashes(root, true);
                    Assert.IsFalse(ValidateGenerationRoot(root).IsMatch, invalid);
                }
                File.WriteAllText(index, meta + "<!-- " + meta + " -->");
                RewriteGenerationManifestHashes(root, true);
                Assert.IsTrue(ValidateGenerationRoot(root).IsMatch);
            }
            finally { DeleteConfigRoot(root); }
        }

        [TestMethod]
        public void RuntimeGenerationGuard_SharedAiPropertyCannotUseLegacyManifest()
        {
            var root = CreateGenerationRoot();
            try
            {
                File.WriteAllText(Path.Combine(root, "appsettings.lab.json"), "{\"Assistant\":{\"SharedAiModelIds\":[\"nvidia-kimi-k3\"]}}");
                RewriteGenerationManifestHashes(root, false, true);
                var result = ValidateGenerationRoot(root);
                Assert.IsFalse(result.IsMatch);
                StringAssert.Contains(result.Detail, "SharedAiModelIds requires runtime manifest v2");
            }
            finally { DeleteConfigRoot(root); }
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

        [TestMethod]
        public void AgentClient_PlanUrl_UsesIdentityBridgePortByDefault()
        {
            AgentClient.Configure(null);

            Assert.AreEqual(
                "http://127.0.0.1:" + AppIdentity.BridgePort + "/agent/plan",
                AgentClient.PlanUrl);
        }

        [TestMethod]
        public void AgentClient_Configure_UsesRuntimeConfiguredPort()
        {
            try
            {
                AgentClient.Configure(new AgentConfig { Agent = new AgentSettings { BridgePort = 23456 } });

                Assert.AreEqual("http://127.0.0.1:23456/agent/plan", AgentClient.PlanUrl);
            }
            finally
            {
                AgentClient.Configure(null);
            }
        }

        [TestMethod]
        public void AgentClient_Configure_FallsBackToIdentityPortWhenUnconfigured()
        {
            try
            {
                AgentClient.Configure(new AgentConfig { Agent = new AgentSettings { BridgePort = 0 } });
                Assert.AreEqual(
                    "http://127.0.0.1:" + AppIdentity.BridgePort + "/agent/plan",
                    AgentClient.PlanUrl);

                AgentClient.Configure(new AgentConfig { Agent = new AgentSettings { BridgePort = 70000 } });
                Assert.AreEqual(
                    "http://127.0.0.1:" + AppIdentity.BridgePort + "/agent/plan",
                    AgentClient.PlanUrl);
            }
            finally
            {
                AgentClient.Configure(null);
            }
        }

        [TestMethod]
        public void AgentHttpServer_BuildQaRunUrl_UsesConfiguredPort()
        {
            var config = new AgentConfig { Agent = new AgentSettings { BridgePort = 23456 } };

            Assert.AreEqual("http://127.0.0.1:23456/qa/run", AgentHttpServer.BuildQaRunUrl(config));
        }

        [TestMethod]
        public void AgentHttpServer_BuildQaRunUrl_FallsBackToIdentityPortWhenUnconfigured()
        {
            Assert.AreEqual(
                "http://127.0.0.1:" + AppIdentity.BridgePort + "/qa/run",
                AgentHttpServer.BuildQaRunUrl(new AgentConfig { Agent = new AgentSettings { BridgePort = 0 } }));
            Assert.AreEqual(
                "http://127.0.0.1:" + AppIdentity.BridgePort + "/qa/run",
                AgentHttpServer.BuildQaRunUrl(null));
        }

        [TestMethod]
        public void AgentConfig_CreateInvalidFallback_UsesIdentityDefaultPortAndReportsStatus()
        {
            var configPath = Path.Combine(Path.GetTempPath(), "bb-fallback-" + Guid.NewGuid().ToString("N"), "config", Path.GetFileName(AppIdentity.ConfigPath("root")));
            var failure = new AgentConfigurationException("CONFIG_PRESENT_INVALID", configPath, new InvalidDataException("bad config"));

            var config = AgentConfig.CreateInvalidFallback(configPath, failure);

            Assert.AreEqual(AppIdentity.BridgePort, config.Agent.BridgePort);
            Assert.AreEqual("CONFIG_PRESENT_INVALID", config.ConfigurationDiagnostics.ConfigurationLoadStatus);
            Assert.AreEqual("CONFIG_VALUE_DEFAULTED", config.ConfigurationDiagnostics.AssistantValueSource);
            Assert.AreEqual(configPath, config.ConfigurationDiagnostics.ConfigPath);
        }

        [TestMethod]
        public void SwAddin_StartAgentBridge_FallsBackToDefaultsWhenConfigIsInvalid()
        {
            var events = new List<string>();
            AgentConfig fallbackSeen = null;
            var configPath = Path.Combine(Path.GetTempPath(), "bb-fallback-" + Guid.NewGuid().ToString("N"), "config", Path.GetFileName(AppIdentity.ConfigPath("root")));
            var failure = new AgentConfigurationException("CONFIG_PRESENT_INVALID", configPath, new InvalidDataException("bad config"));

            var server = SwAddin.StartAgentBridge(
                () =>
                {
                    events.Add("LOAD_CONFIG");
                    throw failure;
                },
                loadedConfig =>
                {
                    loadedConfig.Agent.BridgePort = AgentConfig.ResolveBridgePort(
                        loadedConfig.Agent.BridgePort,
                        AppIdentity.BridgePort);
                    events.Add("RESOLVE_CONFIGURE_PORT:" + loadedConfig.Agent.BridgePort);
                    fallbackSeen = loadedConfig;
                },
                loadedConfig =>
                {
                    events.Add("CREATE_BRIDGE_SERVER");
                    return new FakeBridgeServer();
                },
                createdServer =>
                {
                    events.Add("START_BRIDGE_SERVER");
                    createdServer.Started = true;
                },
                msg => events.Add("LOG:" + msg));

            CollectionAssert.AreEqual(
                new[]
                {
                    "LOAD_CONFIG",
                    "LOG:AgentConfig invalid (CONFIG_PRESENT_INVALID); starting bridge with default configuration: " + failure.Message,
                    "RESOLVE_CONFIGURE_PORT:" + AppIdentity.BridgePort,
                    "CREATE_BRIDGE_SERVER",
                    "START_BRIDGE_SERVER"
                },
                events);
            Assert.IsTrue(server.Started);
            Assert.IsNotNull(fallbackSeen);
            Assert.AreEqual(AppIdentity.BridgePort, fallbackSeen.Agent.BridgePort);
            Assert.AreEqual("CONFIG_PRESENT_INVALID", fallbackSeen.ConfigurationDiagnostics.ConfigurationLoadStatus);
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

        private static AgentConfig LoadRaw(string json)
        {
            var root = CreateConfigRoot();
            try
            {
                File.WriteAllText(AppIdentity.ConfigPath(root), json);
                return AgentConfig.LoadFrom(root);
            }
            finally
            {
                DeleteConfigRoot(root);
            }
        }

        private static void AssertConfigFailure(string json, string expectedStatus)
        {
            try
            {
                LoadRaw(json);
                Assert.Fail("Expected AgentConfigurationException.");
            }
            catch (AgentConfigurationException ex)
            {
                Assert.AreEqual(expectedStatus, ex.Status);
                StringAssert.Contains(ex.Message, expectedStatus);
            }
        }

        private static string CreateGenerationRoot()
        {
            var root = Path.Combine(Path.GetTempPath(), "BlueBrick-GenerationTests-" + Guid.NewGuid().ToString("N"));
            var dist = Path.Combine(root, "dist");
            Directory.CreateDirectory(dist);
            File.WriteAllText(Path.Combine(root, "BlueBrick.Lab.dll"), "dll");
            File.WriteAllText(Path.Combine(root, "appsettings.lab.json"), "{\"ConfigSchemaVersion\":\"1\",\"Assistant\":{}}");
            File.WriteAllText(Path.Combine(dist, "index.html"), "index");
            File.WriteAllText(Path.Combine(dist, "assistant-index.css"), "css");
            File.WriteAllText(Path.Combine(dist, "assistant-web.js"), "js");
            var manifest = new
            {
                product = "BlueBrick",
                channel = "Lab",
                buildId = "test-build",
                frontendBuildId = "test-build",
                configSchemaVersion = "1",
                dll = new { sha256 = Hash(Path.Combine(root, "BlueBrick.Lab.dll")) },
                config = new { sha256 = Hash(Path.Combine(root, "appsettings.lab.json")) },
                frontend = new
                {
                    artifacts = new Dictionary<string, object>
                    {
                        { "index.html", new { sha256 = Hash(Path.Combine(dist, "index.html")) } },
                        { "assistant-index.css", new { sha256 = Hash(Path.Combine(dist, "assistant-index.css")) } },
                        { "assistant-web.js", new { sha256 = Hash(Path.Combine(dist, "assistant-web.js")) } }
                    }
                }
            };
            File.WriteAllText(Path.Combine(root, "runtime-manifest.json"), JsonConvert.SerializeObject(manifest));
            return root;
        }

        private static RuntimeGenerationCheck ValidateGenerationRoot(string root)
        {
            return RuntimeGenerationGuard.Validate(
                Path.Combine(root, "runtime-manifest.json"), "Lab",
                Path.Combine(root, "BlueBrick.Lab.dll"), Path.Combine(root, "appsettings.lab.json"),
                Path.Combine(root, "dist"), "1");
        }

        private static string CreateV2GenerationRoot(bool sharedAi)
        {
            var root = CreateGenerationRoot();
            File.WriteAllText(Path.Combine(root, "appsettings.lab.json"), sharedAi
                ? "{\"ConfigSchemaVersion\":\"1\",\"Assistant\":{\"SharedAiModelIds\":[\"nvidia-kimi-k3\"]}}"
                : "{\"ConfigSchemaVersion\":\"1\",\"Assistant\":{}}");
            File.WriteAllText(Path.Combine(root, "dist", "index.html"), "<meta name=\"bluebrick-build-id\" content=\"test-build\">");
            {
                var catalog = Path.Combine(root, "SharedAI", "catalog");
                Directory.CreateDirectory(catalog);
                foreach (var name in new[] { "providers.json", "models.json", "routes.json" })
                    File.Copy(Path.Combine(FindCatalogRoot(), name), Path.Combine(catalog, name));
            }
            RewriteGenerationManifestHashes(root, true);
            return root;
        }

        private static void RewriteGenerationManifestHashes(string root, bool sharedAi, bool preserveLegacySchema = false)
        {
            var manifest = JObject.Parse(File.ReadAllText(Path.Combine(root, "runtime-manifest.json")));
            if (!preserveLegacySchema) manifest["schema"] = "bluebrick-lab-runtime-manifest.v2";
            manifest["config"]["sha256"] = Hash(Path.Combine(root, "appsettings.lab.json"));
            manifest["frontend"]["artifacts"]["index.html"]["sha256"] = Hash(Path.Combine(root, "dist", "index.html"));
            {
                var catalogs = new JObject();
                foreach (var name in new[] { "providers.json", "models.json", "routes.json" })
                {
                    var path = Path.Combine(root, "SharedAI", "catalog", name);
                    if (File.Exists(path)) catalogs[name] = new JObject { ["sha256"] = Hash(path) };
                }
                manifest["sharedAiCatalog"] = new JObject { ["artifacts"] = catalogs };
            }
            File.WriteAllText(Path.Combine(root, "runtime-manifest.json"), manifest.ToString(Formatting.None));
        }

        private static string FindCatalogRoot()
        {
            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, "SharedAI", "catalog");
                if (File.Exists(Path.Combine(candidate, "providers.json"))) return candidate;
                directory = directory.Parent;
            }
            throw new InvalidOperationException("SharedAI fixture catalog not found.");
        }

        private static string Hash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
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
