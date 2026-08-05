using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32;
using Newtonsoft.Json;
using BlueBrick.Agent;

namespace BlueBrick.IntegrationTest
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            var allPass = true;

            Console.WriteLine("=== Real Integration Test: Assistant Mode Resolution ===");
            Console.WriteLine("Time: " + DateTime.Now.ToString("O"));
            Console.WriteLine();

            var registryRoot = BlueBrick.AppIdentity.RegistryRoot;
            Console.WriteLine("RegistryRoot: " + registryRoot);
            Console.WriteLine("IsLabBuild: " + BlueBrick.AppIdentity.IsLabBuild);
            Console.WriteLine();

            var liveMode = Registry.GetValue(registryRoot, "AssistantMode", null) as string;
            var liveApiKey = Registry.GetValue(registryRoot, "AssistantApiKey", null) as string;
            var liveEnvOpenAi = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            var liveEnvNvidia = Environment.GetEnvironmentVariable("NVIDIA_API_KEY");
            Console.WriteLine("Live registry AssistantMode: " + (liveMode ?? "(null)"));
            Console.WriteLine("Live registry AssistantApiKey: " + (string.IsNullOrEmpty(liveApiKey) ? "(null/empty)" : "present(len=" + liveApiKey.Length + ")"));
            Console.WriteLine("Live env OPENAI_API_KEY: " + (liveEnvOpenAi ?? "(null)"));
            Console.WriteLine("Live env NVIDIA_API_KEY: " + (liveEnvNvidia ?? "(null)"));
            Console.WriteLine();

            Console.WriteLine("--- Scenario 1: Auto-detect with registry key present (should resolve real) ---");
            {
                var config = MakeConfig("");
                config.ApplyDefaults(BlueBrick.AppIdentity.DefaultWorkingFolder);
                var service = new OpenAiAssistantService(config);
                var status = await service.GetStatusAsync();
                Console.WriteLine("  Mode=" + status.AssistantMode + ", KeyConfigured=" + status.KeyConfigured + ", KeySource=" + status.KeySource);
                var expected = string.IsNullOrEmpty(liveApiKey) ? "mock" : "real";
                var pass = status.AssistantMode == expected;
                Console.WriteLine("  EXPECTED: " + expected + "  ACTUAL: " + status.AssistantMode + "  " + (pass ? "PASS" : "FAIL"));
                if (!pass) allPass = false;
            }

            Console.WriteLine();
            Console.WriteLine("--- Scenario 2: Mode=mock overrides everything (should resolve mock) ---");
            {
                var config = MakeConfig("mock");
                config.ApplyDefaults(BlueBrick.AppIdentity.DefaultWorkingFolder);
                var service = new OpenAiAssistantService(config);
                var status = await service.GetStatusAsync();
                Console.WriteLine("  Mode=" + status.AssistantMode + ", KeyConfigured=" + status.KeyConfigured);
                var pass = status.AssistantMode == "mock";
                Console.WriteLine("  EXPECTED: mock  ACTUAL: " + status.AssistantMode + "  " + (pass ? "PASS" : "FAIL"));
                if (!pass) allPass = false;

                var response = await service.SendMessageAsync(null, "hello mock override test", Array.Empty<string>());
                var mockText = response.Message.Text ?? "";
                var hasMockText = mockText.IndexOf("Mock preview mode", StringComparison.OrdinalIgnoreCase) >= 0;
                Console.WriteLine("  Mock response text present: " + hasMockText + "  " + (hasMockText ? "PASS" : "FAIL"));
                if (!hasMockText) allPass = false;
            }

            Console.WriteLine();
            Console.WriteLine("--- Scenario 3: Mode=real with key present (should resolve real) ---");
            {
                var config = MakeConfig("real");
                config.ApplyDefaults(BlueBrick.AppIdentity.DefaultWorkingFolder);
                var service = new OpenAiAssistantService(config);
                var status = await service.GetStatusAsync();
                Console.WriteLine("  Mode=" + status.AssistantMode + ", KeyConfigured=" + status.KeyConfigured + ", KeySource=" + status.KeySource);
                var expected = string.IsNullOrEmpty(liveApiKey) ? "mock" : "real";
                var pass = status.AssistantMode == expected;
                Console.WriteLine("  EXPECTED: " + expected + "  ACTUAL: " + status.AssistantMode + "  " + (pass ? "PASS" : "FAIL"));
                if (!pass) allPass = false;
            }

            Console.WriteLine();
            Console.WriteLine("--- Scenario 4: Mode=real with key cleared (should fall back to mock) ---");
            {
                var prevApiKey = Registry.GetValue(registryRoot, "AssistantApiKey", null) as string;
                try
                {
                    ClearRegistryValue("AssistantApiKey");
                    Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);
                    Environment.SetEnvironmentVariable("NVIDIA_API_KEY", null);

                    var config = MakeConfig("real");
                    config.ApplyDefaults(BlueBrick.AppIdentity.DefaultWorkingFolder);
                    var service = new OpenAiAssistantService(config);
                    var status = await service.GetStatusAsync();
                    Console.WriteLine("  Mode=" + status.AssistantMode + ", KeyConfigured=" + status.KeyConfigured + ", KeySource=" + status.KeySource);
                    var pass = status.AssistantMode == "mock" && !status.KeyConfigured;
                    Console.WriteLine("  EXPECTED: mock+KeyConfigured=false  ACTUAL: " + status.AssistantMode + "+KeyConfigured=" + status.KeyConfigured + "  " + (pass ? "PASS" : "FAIL"));
                    if (!pass) allPass = false;

                    var response = await service.SendMessageAsync(null, "hello real-no-key test", Array.Empty<string>());
                    var mockText = response.Message.Text ?? "";
                    var hasMockText = mockText.IndexOf("Mock preview mode", StringComparison.OrdinalIgnoreCase) >= 0;
                    Console.WriteLine("  Mock fallback response: " + hasMockText + "  " + (hasMockText ? "PASS" : "FAIL"));
                    if (!hasMockText) allPass = false;
                }
                finally
                {
                    if (prevApiKey != null)
                        Registry.SetValue(registryRoot, "AssistantApiKey", prevApiKey, RegistryValueKind.String);
                    Environment.SetEnvironmentVariable("OPENAI_API_KEY", liveEnvOpenAi);
                    Environment.SetEnvironmentVariable("NVIDIA_API_KEY", liveEnvNvidia);
                }
            }

            Console.WriteLine();
            Console.WriteLine("--- Scenario 5: Real connection test with registry key (NVIDIA) ---");
            {
                var config = MakeConfig("real");
                config.ApplyDefaults(BlueBrick.AppIdentity.DefaultWorkingFolder);
                var service = new OpenAiAssistantService(config);
                var status = await service.GetStatusAsync();
                if (status.KeyConfigured && status.AssistantMode == "real")
                {
                    Console.WriteLine("  Calling TestConnectionAsync against NVIDIA API...");
                    try
                    {
                        var result = await service.TestConnectionAsync();
                        Console.WriteLine("  Success=" + result.Success + ", Mode=" + result.Mode + ", Latency=" + result.LatencyMs.ToString("F0") + "ms");
                        Console.WriteLine("  KeySource=" + result.KeySource);
                        var msg = result.Message ?? "";
                        Console.WriteLine("  Response: " + msg.Substring(0, Math.Min(200, msg.Length)));
                        Console.WriteLine("  " + (result.Success ? "PASS" : "FAIL"));
                        if (!result.Success) allPass = false;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("  ERROR: " + ex.GetType().Name + ": " + ex.Message);
                        Console.WriteLine("  FAIL");
                        allPass = false;
                    }
                }
                else
                {
                    Console.WriteLine("  SKIPPED (mode=" + status.AssistantMode + ", keyConfigured=" + status.KeyConfigured + ")");
                }
            }

            Console.WriteLine();
            Console.WriteLine("=== Registry state after test ===");
            var postApiKey = Registry.GetValue(registryRoot, "AssistantApiKey", null) as string;
            var postMode = Registry.GetValue(registryRoot, "AssistantMode", null) as string;
            Console.WriteLine("  AssistantApiKey: " + (string.IsNullOrEmpty(postApiKey) ? "(null/empty)" : "present(len=" + postApiKey.Length + ")"));
            Console.WriteLine("  AssistantMode: " + (postMode ?? "(null)"));
            var restored = postApiKey == liveApiKey && postMode == liveMode;
            Console.WriteLine("  State restored: " + restored + "  " + (restored ? "PASS" : "FAIL"));
            if (!restored) allPass = false;

            Console.WriteLine();
            Console.WriteLine("=== Overall: " + (allPass ? "ALL PASS" : "SOME FAILED") + " ===");
            return allPass ? 0 : 1;
        }

        static AgentConfig MakeConfig(string mode)
        {
            return new AgentConfig
            {
                Agent = new AgentSettings { BridgePort = 17179, OverlayColor = "#D9FF5A" },
                Vault = new VaultSettings
                {
                    Root = Path.Combine(Path.GetTempPath(), "bb-lab-vault"),
                    SampleSeedRoot = Path.Combine(Path.GetTempPath(), "bb-lab-samples")
                },
                Assistant = new AssistantSettings
                {
                    ApiBaseUrl = "https://integrate.api.nvidia.com/v1",
                    Model = "meta/llama-3.1-70b-instruct",
                    Mode = mode,
                    SystemPrompt = "You are the BlueBrick Lab assistant.",
                    Detail = "low",
                    EnableUploads = true,
                    MaxImageDimension = 1600,
                    JpegQuality = 75,
                    ConnectionTestPrompt = "Reply with the word READY and one short sentence confirming the BlueBrick Lab assistant connection is working.",
                    RequireExplicitUploadConsent = true,
                    MaxHistory = 10
                }
            };
        }

        static void ClearRegistryValue(string valueName)
        {
            try
            {
                var subKeyPath = BlueBrick.AppIdentity.RegistryRoot.Replace(@"HKEY_CURRENT_USER\", "");
                using (var key = Registry.CurrentUser.OpenSubKey(subKeyPath, true))
                {
                    if (key != null) key.DeleteValue(valueName, false);
                }
            }
            catch { }
        }
    }
}
