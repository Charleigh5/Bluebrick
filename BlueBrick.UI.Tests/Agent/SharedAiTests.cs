using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BlueBrick.Agent;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using SharedAI;

namespace BlueBrick.UI.Tests.Agent
{
    [TestClass]
    public class SharedAiTests
    {
        private string CatalogPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SharedAI", "catalog");
        private Catalog Load() => Catalog.Load(CatalogPath);
        private Dictionary<string, RuntimeState> States(bool kimi = true) => new Dictionary<string, RuntimeState> {
            ["nvidia-kimi-k3"] = new RuntimeState { Available = kimi, Reason = kimi ? null : "credential_missing" },
            ["google-gemini-3-8-flash"] = new RuntimeState { Available = true } };

        [TestMethod] public void CanonicalCatalogIncludesApprovedOpenRouterProviderOnly()
        {
            var c = Load();
            Assert.AreEqual(3, c.Providers.Length);
            var openRouter = c.Providers.Single(x => x.Id == "openrouter");
            Assert.AreEqual("openai-chat-completions", openRouter.Protocol);
            Assert.AreEqual("https://openrouter.ai/api/v1", openRouter.BaseUrl);
            Assert.AreEqual("OPENROUTER_API_KEY", openRouter.CredentialBinding);
            Assert.AreEqual(2, c.Models.Length);
            Assert.IsFalse(c.Models.Any(x => x.ProviderId == "openrouter"));
            Assert.IsFalse(c.Routes.Values.SelectMany(x => x).Any(x => x.IndexOf("openrouter", StringComparison.OrdinalIgnoreCase) >= 0));
        }

        [TestMethod] public void CanonicalModelsAdaptWithoutChangingProviderStrings()
        {
            var c = Load(); var profiles = SharedAiProfiles.Adapt(c, c.Routes["default"]);
            Assert.AreEqual("moonshotai/kimi-k3", profiles[0].Model);
            Assert.AreEqual("gemini-3.8-flash", profiles[1].Model);
            Assert.AreEqual("google-gemini", profiles[1].ProviderKind);
            Assert.IsTrue(profiles.All(p => p.SupportsVision && p.SupportsTools && p.SupportsStreaming && p.SupportsJsonMode && p.ContextLimit == 1048576));
        }
        [TestMethod] public void RoutesRespectAllRequiredCapabilitiesAndRuntimeState()
        {
            var c = Load();
            foreach (var route in new[] { "default", "vision", "tools" }) {
                CollectionAssert.AreEqual(new[] { "nvidia-kimi-k3", "google-gemini-3-8-flash" }, c.Resolve(route, new Capabilities { Text = true }, States()));
                CollectionAssert.AreEqual(new[] { "google-gemini-3-8-flash" }, c.Resolve(route, new Capabilities { Vision = true, Tools = true, Streaming = true, StructuredOutput = true }, States(false)));
            }
            c.Models[0].Capabilities.Tools = false;
            CollectionAssert.AreEqual(new[] { "google-gemini-3-8-flash" }, c.Resolve("tools", new Capabilities { Tools = true }, States()));
            Assert.AreEqual(0, c.Resolve("default", new Capabilities(), new Dictionary<string, RuntimeState>()).Length);
        }
        [TestMethod] public void SharedCrossLanguageFixturesResolveIdentically()
        {
            var fixtures = JArray.Parse(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SharedAI", "tests", "fixtures.json")));
            foreach (var fixture in fixtures)
            {
                var c = Load();
                var missing = (string)fixture["missingKimiCapability"];
                if (missing != null) {
                    var caps = JObject.FromObject(c.Models[0].Capabilities);
                    caps.Properties().Single(p => string.Equals(p.Name, missing, StringComparison.OrdinalIgnoreCase)).Value = false;
                    c.Models[0].Capabilities = caps.ToObject<Capabilities>();
                }
                CollectionAssert.AreEqual(fixture["expected"].ToObject<string[]>(),
                    c.Resolve("default", fixture["requirements"].ToObject<Capabilities>(), States(!(bool)fixture["unavailableKimi"])));
            }
            Assert.AreEqual(38, fixtures.Count);
        }
        [TestMethod] public void InvalidCatalogFailsClosedWithoutEchoingPayload()
        {
            var p = File.ReadAllText(Path.Combine(CatalogPath, "providers.json"));
            var m = File.ReadAllText(Path.Combine(CatalogPath, "models.json"));
            var r = File.ReadAllText(Path.Combine(CatalogPath, "routes.json"));
            foreach (var edit in new Action<JArray>[] {
                x => x[0]["capabilities"]["vision"] = "true", x => x[1]["id"] = x[0]["id"],
                x => x[0]["apiKey"] = "synthetic-sensitive-marker", x => x[0]["providerId"] = "missing",
                x => x[0]["displayName"] = "Bearer synthetic-sensitive-marker", x => x[0]["roles"] = new JArray() }) {
                var changed = JArray.Parse(m); edit(changed);
                var error = Assert.ThrowsException<InvalidDataException>(() => Catalog.FromJson(p, changed.ToString(), r));
                Assert.AreEqual("SHAREDAI_CATALOG_INVALID", error.Message);
            }
            Assert.ThrowsException<InvalidDataException>(() => Catalog.FromJson(p, m, r.Replace("nvidia-kimi-k3", "missing")));
            Assert.ThrowsException<InvalidDataException>(() => Catalog.FromJson(p.Replace("https://integrate.api.nvidia.com/v1", "https://unsafe.invalid/?key=synthetic"), m, r));
            Assert.ThrowsException<InvalidDataException>(() => Catalog.FromJson(p.Replace("https://openrouter.ai/api/v1", "https://openrouter.ai/v1"), m, r));
            Assert.ThrowsException<InvalidDataException>(() => Catalog.FromJson(p.Replace("OPENROUTER_API_KEY", "OPENROUTER_TOKEN"), m, r));
            var duplicateProviders = JArray.Parse(p); duplicateProviders.Add(duplicateProviders[2].DeepClone());
            Assert.ThrowsException<InvalidDataException>(() => Catalog.FromJson(duplicateProviders.ToString(), m, r));
            var secretBearingProvider = JArray.Parse(p); secretBearingProvider[2]["apiKey"] = "synthetic-sensitive-marker";
            Assert.ThrowsException<InvalidDataException>(() => Catalog.FromJson(secretBearingProvider.ToString(), m, r));
        }
        [TestMethod] public void ProviderFailurePolicyDoesNotRetryToolOrPermissionBoundaries()
        {
            foreach (var code in new[] { "credential_missing", "provider_unavailable", "model_unavailable", "rate_limited", "timeout" }) {
                Assert.IsTrue(Catalog.AllowsFallback("provider", code));
                foreach (var boundary in new[] { "permission", "tool_execution", "tool_result", "request_construction" }) Assert.IsFalse(Catalog.AllowsFallback(boundary, code));
            }
            foreach (var code in new[] { "authentication_failed", "malformed_response", "unsupported_payload", "tool_call_parse_failure", "context_limit", "stream_interrupted", "bluebrick_tool_execution_failure" }) Assert.IsFalse(Catalog.AllowsFallback("provider", code));
        }
        [TestMethod] public void ExplicitGeminiSelectionIsHonoredAndStreamingIsRequired()
        {
            var c = Load();
            Assert.AreEqual("google-gemini-3-8-flash", SharedAiProfiles.Resolve(c, States(), true, true, "google-gemini-3-8-flash"));
            Assert.AreEqual("nvidia-kimi-k3", SharedAiProfiles.Resolve(c, States(), true, true));
            c.Models[0].Capabilities.Streaming = false;
            Assert.AreEqual("google-gemini-3-8-flash", SharedAiProfiles.Resolve(c, States(), false, true));
        }
        [TestMethod] public async Task ConfigAndModelsExposeStaticTruthAndMissingCredential()
        {
            var root = Path.Combine(Path.GetTempPath(), "SharedAI-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "SharedAI", "catalog"));
            Directory.CreateDirectory(Path.Combine(root, "config"));
            try {
                foreach (var file in Directory.GetFiles(CatalogPath, "*.json")) File.Copy(file, Path.Combine(root, "SharedAI", "catalog", Path.GetFileName(file)));
                File.WriteAllText(AppIdentity.ConfigPath(root), "{\"Assistant\":{\"SharedAiModelIds\":[\"nvidia-kimi-k3\",\"google-gemini-3-8-flash\"]}}");
                var config = AgentConfig.LoadFrom(root);
                Assert.IsNotNull(config.SharedAiCatalog);
                // Synthetic bindings avoid touching or resolving user credentials.
                config.Assistant.ModelProfiles[0].KeyEnvironmentVariable = "SHAREDAI_NONEXISTENT_" + Guid.NewGuid().ToString("N");
                config.Assistant.ModelProfiles[1].KeyEnvironmentVariable = config.Assistant.ModelProfiles[0].KeyEnvironmentVariable;
                var service = new OpenAiAssistantService(config, new AssistantToolService(config, null), new AssistantSessionStore(Path.Combine(root, "sessions")));
                var models = await service.GetModelsAsync();
                Assert.AreEqual(2, models.Count);
                Assert.IsTrue(models.All(x => x.Available == false && !x.RuntimeEligible && x.UnavailableReason == "credential_missing" && x.SupportsVision));
                var method = typeof(OpenAiAssistantService).GetMethod("ResolveMessageProfile", BindingFlags.NonPublic | BindingFlags.Instance);
                var failure = Assert.ThrowsException<TargetInvocationException>(() => method.Invoke(service, new object[] { new AssistantSession(), false }));
                Assert.IsInstanceOfType(failure.InnerException, typeof(InvalidOperationException));
                var binding = "SHAREDAI_SYNTHETIC_" + Guid.NewGuid().ToString("N");
                config.Assistant.ModelProfiles[1].KeyEnvironmentVariable = binding;
                config.Assistant.Mode = "real";
                Environment.SetEnvironmentVariable(binding, "synthetic-test-only");
                try {
                    models = await service.GetModelsAsync();
                    Assert.IsTrue(models[1].RuntimeEligible);
                    Assert.IsNull(models[1].Available, "Credential presence cannot prove provider availability.");
                    var selected = (AssistantModelProfile)method.Invoke(service, new object[] { new AssistantSession(), false });
                    Assert.AreEqual("google-gemini-3-8-flash", selected.Id);
                    var statusMethod = typeof(OpenAiAssistantService).GetMethod("BuildStatusForProfile", BindingFlags.NonPublic | BindingFlags.Instance);
                    var status = (AssistantPreviewStatus)statusMethod.Invoke(service, new object[] { selected });
                    Assert.IsTrue(status.Configured);
                    Assert.AreEqual("real", status.AssistantMode, "Fallback must not inherit missing-primary mock mode.");
                } finally { Environment.SetEnvironmentVariable(binding, null); }
            } finally { Directory.Delete(root, true); }
        }
    }
}
