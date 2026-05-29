using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BlueBrick.Agent
{
    internal sealed class OpenAiAssistantService : IAssistantService
    {
        private const string MockMode = "mock";
        private const string RealMode = "real";
        private const string AssistantModeValueName = "AssistantMode";
        private const string AssistantApiKeyValueName = "AssistantApiKey";
        private const string AssistantModelValueName = "AssistantModel";

        private static readonly HttpClient Client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(45)
        };

        static OpenAiAssistantService()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
        }

        private readonly AgentConfig _config;
        private readonly AssistantSessionStore _store;

        internal OpenAiAssistantService(AgentConfig config)
        {
            _config = config;
            _store = new AssistantSessionStore();
        }

        public Task<AssistantSession> CreateSessionAsync()
        {
            return Task.FromResult(_store.Create());
        }

        public Task<AssistantSession> GetSessionAsync(string sessionId)
        {
            return Task.FromResult(_store.Get(sessionId));
        }

        public Task<string> CaptureScreenshotAsync(string sessionId)
        {
            return Task.FromResult(AssistantImageTools.CaptureForegroundWindowArtifact(sessionId)?.Path ?? string.Empty);
        }

        public Task<AssistantScreenshotArtifact> CaptureScreenshotArtifactAsync(string sessionId)
        {
            return CaptureScreenshotArtifactAsync(new AssistantScreenshotCaptureRequest { SessionId = sessionId });
        }

        public Task<AssistantScreenshotArtifact> CaptureScreenshotArtifactAsync(AssistantScreenshotCaptureRequest request)
        {
            return Task.FromResult(AssistantImageTools.CaptureWindowArtifact(request));
        }

        public Task<AssistantScreenshotAnalysisResult> AnalyzeScreenshotAsync(AssistantScreenshotAnalysisRequest request)
        {
            request = request ?? new AssistantScreenshotAnalysisRequest();
            var profile = ResolveProfile();
            AssistantScreenshotAnalyzer.EnsurePrivacyMetadata(request.Artifact, profile.Id, false);
            if (!profile.SupportsVision)
            {
                return Task.FromResult(new AssistantScreenshotAnalysisResult
                {
                    Status = "unsupported_model",
                    Message = "Selected model does not advertise vision support. Switch to a vision-capable model before screenshot analysis.",
                    MockMode = true,
                    Artifact = request.Artifact
                });
            }

            request.ModelProfileId = profile.Id;
            return Task.FromResult(AssistantScreenshotAnalyzer.AnalyzeMock(request));
        }

        public Task<AssistantPreviewStatus> GetStatusAsync()
        {
            return Task.FromResult(BuildStatus());
        }

        public Task<AssistantPreviewStatus> SetModeAsync(string mode)
        {
            var normalized = NormalizeMode(mode);
            Registry.SetValue(AppIdentity.RegistryRoot, AssistantModeValueName, normalized, RegistryValueKind.String);
            return Task.FromResult(BuildStatus());
        }

        public Task<IList<AssistantModelProfile>> GetModelsAsync()
        {
            return Task.FromResult<IList<AssistantModelProfile>>(GetModelProfiles().Select(NormalizeProfile).ToList());
        }

        public Task<AssistantPreviewStatus> SetModelAsync(string modelId)
        {
            var profile = ResolveProfile(modelId);
            Registry.SetValue(AppIdentity.RegistryRoot, AssistantModelValueName, profile.Id, RegistryValueKind.String);
            return Task.FromResult(BuildStatus());
        }

        public async Task<AssistantConnectionTestResult> TestConnectionAsync()
        {
            var status = BuildStatus();
            if (string.Equals(status.AssistantMode, MockMode, StringComparison.OrdinalIgnoreCase))
            {
                return new AssistantConnectionTestResult
                {
                    Success = true,
                    Mode = MockMode,
                    Configured = false,
                    KeySource = status.KeySource,
                    Message = "Mock preview mode is active. No external AI request was sent."
                };
            }

            if (!status.KeyConfigured)
            {
                return new AssistantConnectionTestResult
                {
                    Success = false,
                    Mode = status.AssistantMode,
                    Configured = false,
                    KeySource = status.KeySource,
                    Message = "AI API key not configured. Set OPENAI_API_KEY or AssistantApiKey in the lab registry."
                };
            }

            var messages = new JArray
            {
                new JObject
                {
                    ["role"] = "user",
                    ["content"] = _config.Assistant.ConnectionTestPrompt
                }
            };

            var requestBody = new JObject
            {
                ["model"] = _config.Assistant.Model,
                ["messages"] = messages,
                ["max_tokens"] = 50
            }.ToString(Formatting.None);

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var profile = ResolveProfile();
                var body = await SendChatCompletionAsync(requestBody, ResolveApiKeyInfo(profile).ApiKey, profile).ConfigureAwait(false);
                stopwatch.Stop();
                return new AssistantConnectionTestResult
                {
                    Success = true,
                    Mode = status.AssistantMode,
                    Configured = true,
                    KeySource = status.KeySource,
                    LatencyMs = stopwatch.Elapsed.TotalMilliseconds,
                    Message = ExtractAssistantText(body)
                };
            }
            catch (Exception ex)
            {
                var classified = AssistantErrorClassifier.FromException(ex);
                stopwatch.Stop();
                return new AssistantConnectionTestResult
                {
                    Success = false,
                    Mode = status.AssistantMode,
                    Configured = true,
                    KeySource = status.KeySource,
                    LatencyMs = stopwatch.Elapsed.TotalMilliseconds,
                    Message = classified.Message
                };
            }
        }

        public async Task<AssistantSessionResponse> SendMessageAsync(string sessionId, string message,
            IList<string> attachmentPaths)
        {
            var session = _store.Get(sessionId) ?? _store.Create();
            var uploadPaths = _config.Assistant.EnableUploads
                ? attachmentPaths?.Where(File.Exists).ToList() ?? new List<string>()
                : new List<string>();

            var userMessage = new AssistantMessage
            {
                Role = "user",
                Text = message?.Trim() ?? string.Empty,
                AttachmentPaths = uploadPaths,
                CreatedUtc = DateTime.UtcNow
            };
            session.Messages.Add(userMessage);
            TrimHistory(session);
            _store.Save(session);

            var status = BuildStatus();
            if (string.Equals(status.AssistantMode, MockMode, StringComparison.OrdinalIgnoreCase))
            {
                var mockMessage = new AssistantMessage
                {
                    Role = "assistant",
                    Text = BuildMockResponse(userMessage, status),
                    CreatedUtc = DateTime.UtcNow
                };
                session.Messages.Add(mockMessage);
                TrimHistory(session);
                _store.Save(session);
                return new AssistantSessionResponse
                {
                    SessionId = session.SessionId,
                    AssistantAvailable = true,
                    Message = mockMessage
                };
            }

            var profile = ResolveProfile();
            var keyInfo = ResolveApiKeyInfo(profile);
            if (!keyInfo.Configured)
            {
                return new AssistantSessionResponse
                {
                    SessionId = session.SessionId,
                    AssistantAvailable = false,
                    Error = "AI API key not configured. Set OPENAI_API_KEY or AssistantApiKey in the registry."
                };
            }

            try
            {
                var body = await SendChatCompletionAsync(BuildChatRequestBody(session), keyInfo.ApiKey, profile).ConfigureAwait(false);
                var assistantMessage = new AssistantMessage
                {
                    Role = "assistant",
                    Text = ExtractAssistantText(body),
                    CreatedUtc = DateTime.UtcNow
                };
                session.Messages.Add(assistantMessage);
                TrimHistory(session);
                _store.Save(session);
                return new AssistantSessionResponse
                {
                    SessionId = session.SessionId,
                    AssistantAvailable = true,
                    Message = assistantMessage
                };
            }
            catch (Exception ex)
            {
                var classified = AssistantErrorClassifier.FromException(ex);
                return new AssistantSessionResponse
                {
                    SessionId = session.SessionId,
                    AssistantAvailable = false,
                    Error = classified.Message,
                    ErrorCode = classified.Code
                };
            }
        }

        private AssistantPreviewStatus BuildStatus()
        {
            var profile = ResolveProfile();
            var keyInfo = ResolveApiKeyInfo(profile);
            var effectiveMode = ResolveMode(keyInfo.Configured);
            var checklist = new List<string>();
            var localVaultExists = Directory.Exists(_config.Vault.Root);
            var sampleSeedExists = Directory.Exists(_config.Vault.SampleSeedRoot);
            var tokenConfigured = HasAgentToken();

            checklist.Add(localVaultExists
                ? "Local vault root is ready."
                : "Local vault root is missing and will be created when needed.");
            checklist.Add(sampleSeedExists
                ? "Sample seed folder is available."
                : "Sample seed folder is missing. Feeling Lucky results will be limited until samples are added.");
            checklist.Add(tokenConfigured
                ? "Agent auth token is configured."
                : "Agent auth token is missing. The local preview agent will create one on startup if possible.");
            if (string.Equals(effectiveMode, MockMode, StringComparison.OrdinalIgnoreCase))
            {
                checklist.Add("Assistant is running in mock preview mode.");
            }
            else if (keyInfo.Configured)
            {
                checklist.Add("Assistant is configured for real AI responses (" + profile.ApiBaseUrl + ").");
            }
            else
            {
                checklist.Add("Assistant real mode is selected but no API key is configured.");
            }

            return new AssistantPreviewStatus
            {
                AssistantMode = effectiveMode,
                Configured = keyInfo.Configured,
                KeyConfigured = keyInfo.Configured,
                KeySource = keyInfo.KeySource,
                ApiBaseUrl = profile.ApiBaseUrl,
                Model = profile.Name,
                ActiveModel = AssistantModelCapabilitySummary.FromProfile(profile),
                Detail = _config.Assistant.Detail,
                AddinMode = AppIdentity.IsLabBuild ? "Lab" : "Production",
                VaultMode = AppIdentity.IsLabBuild ? "Local" : "PDM",
                LocalVaultRoot = _config.Vault.Root,
                SampleSeedRoot = _config.Vault.SampleSeedRoot,
                WorkingFolder = AppIdentity.DefaultWorkingFolder,
                BridgePort = _config.Agent.BridgePort,
                BridgeUrl = "http://127.0.0.1:" + _config.Agent.BridgePort,
                LocalVaultExists = localVaultExists,
                SampleSeedExists = sampleSeedExists,
                AgentTokenConfigured = tokenConfigured,
                EnableUploads = _config.Assistant.EnableUploads,
                RequireExplicitUploadConsent = _config.Assistant.RequireExplicitUploadConsent,
                Checklist = checklist.ToArray()
            };
        }

        private string BuildChatRequestBody(AssistantSession session)
        {
            var messages = new JArray();
            messages.Add(new JObject
            {
                ["role"] = "system",
                ["content"] = _config.Assistant.SystemPrompt + Environment.NewLine +
                              "Build profile: " + AppIdentity.ProductName + Environment.NewLine +
                              "Local vault root: " + _config.Vault.Root + Environment.NewLine +
                              "Working folder: " + AppIdentity.DefaultWorkingFolder + Environment.NewLine +
                              "Policy: advisory only, no CAD edits, no PDM actions, no external publishing."
            });

            foreach (var msg in session.Messages)
            {
                var content = new JArray
                {
                    new JObject
                    {
                        ["type"] = "text",
                        ["text"] = msg.Text ?? string.Empty
                    }
                };

                if (_config.Assistant.EnableUploads)
                {
                    foreach (var attachmentPath in msg.AttachmentPaths.Select(path =>
                                 AssistantImageTools.PrepareAttachment(session.SessionId, path,
                                     _config.Assistant.MaxImageDimension, _config.Assistant.JpegQuality))
                             .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path)))
                    {
                        var base64 = Convert.ToBase64String(File.ReadAllBytes(attachmentPath));
                        content.Add(new JObject
                        {
                            ["type"] = "image_url",
                            ["image_url"] = new JObject
                            {
                                ["url"] = "data:image/jpeg;base64," + base64,
                                ["detail"] = _config.Assistant.Detail
                            }
                        });
                    }
                }

                messages.Add(new JObject
                {
                    ["role"] = msg.Role,
                    ["content"] = content.Count == 1 ? (JToken)content[0]["text"] : content
                });
            }

            return new JObject
            {
                ["model"] = ResolveProfile().Model,
                ["messages"] = messages
            }.ToString(Formatting.None);
        }

        private async Task<string> SendChatCompletionAsync(string requestBody, string apiKey, AssistantModelProfile profile)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post,
                       profile.ApiBaseUrl.TrimEnd('/') + "/chat/completions"))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Headers.Add("User-Agent", "BlueBrick-AI-Assistant/1.0");
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                var response = await Client.SendAsync(request).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    var classified = AssistantErrorClassifier.FromProviderFailure(body);
                    throw new InvalidOperationException(classified.Message);
                }

                return body;
            }
        }

        private static string ExtractAssistantText(string body)
        {
            var json = JObject.Parse(body);
            var choices = json["choices"] as JArray;
            if (choices != null && choices.Count > 0)
            {
                var message = choices[0]["message"];
                if (message != null)
                {
                    var text = message.Value<string>("content");
                    if (!string.IsNullOrWhiteSpace(text)) return text;
                }
            }

            return "No response text returned.";
        }

        private void TrimHistory(AssistantSession session)
        {
            while (session.Messages.Count > _config.Assistant.MaxHistory)
            {
                session.Messages.RemoveAt(0);
            }
        }

        private ApiKeyInfo ResolveApiKeyInfo(AssistantModelProfile profile)
        {
            var envName = string.IsNullOrWhiteSpace(profile.KeyEnvironmentVariable)
                ? "OPENAI_API_KEY"
                : profile.KeyEnvironmentVariable;
            var env = Environment.GetEnvironmentVariable(envName);
            if (!string.IsNullOrWhiteSpace(env))
            {
                return new ApiKeyInfo(env.Trim(), "environment:" + envName);
            }

            env = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (!string.IsNullOrWhiteSpace(env))
            {
                return new ApiKeyInfo(env.Trim(), "environment:OPENAI_API_KEY");
            }

            var key = Registry.GetValue(AppIdentity.RegistryRoot, AssistantApiKeyValueName, null)?.ToString();
            if (!string.IsNullOrWhiteSpace(key))
            {
                return new ApiKeyInfo(key.Trim(), "registry");
            }

            return new ApiKeyInfo(null, "missing");
        }

        private AssistantModelProfile ResolveProfile(string requestedId = null)
        {
            var profiles = GetModelProfiles().ToList();
            var activeId = requestedId;
            if (string.IsNullOrWhiteSpace(activeId))
            {
                activeId = Registry.GetValue(AppIdentity.RegistryRoot, AssistantModelValueName, null)?.ToString();
            }

            var profile = profiles.FirstOrDefault(p => string.Equals(p.Id, activeId, StringComparison.OrdinalIgnoreCase));
            if (profile != null) return profile;

            return profiles.FirstOrDefault(p => p.IsDefault) ?? profiles.First();
        }

        private IEnumerable<AssistantModelProfile> GetModelProfiles()
        {
            var configured = _config.Assistant.ModelProfiles;
            if (configured != null && configured.Length > 0)
            {
                foreach (var profile in configured)
                {
                    if (!string.IsNullOrWhiteSpace(profile.Id) &&
                        !string.IsNullOrWhiteSpace(profile.Model) &&
                        !string.IsNullOrWhiteSpace(profile.ApiBaseUrl))
                    {
                        yield return NormalizeProfile(profile);
                    }
                }

                yield break;
            }

            yield return new AssistantModelProfile
            {
                Id = "configured-default",
                Name = _config.Assistant.Model,
                Provider = "AionUI",
                ApiBaseUrl = _config.Assistant.ApiBaseUrl,
                Model = _config.Assistant.Model,
                KeyEnvironmentVariable = "OPENAI_API_KEY",
                IsDefault = true,
                ProviderKind = "aionui_broker",
                BaseUrlAlias = "AIONUI_BROKER",
                SupportsVision = false,
                SupportsStreaming = false,
                SupportsTools = false,
                SupportsJsonMode = false,
                SecretRef = "runtime-only",
                Enabled = true,
                Source = "bluebrick"
            };
        }

        private static AssistantModelProfile NormalizeProfile(AssistantModelProfile profile)
        {
            if (profile == null) return null;
            profile.ProviderKind = DefaultIfEmpty(profile.ProviderKind, InferProviderKind(profile.Provider));
            profile.BaseUrlAlias = DefaultIfEmpty(profile.BaseUrlAlias, InferBaseUrlAlias(profile.Provider, profile.ApiBaseUrl));
            profile.SecretRef = DefaultIfEmpty(profile.SecretRef, "runtime-only");
            profile.Source = DefaultIfEmpty(profile.Source, "config_example");
            if (!profile.Enabled)
            {
                profile.Enabled = true;
            }
            return profile;
        }

        private static string InferProviderKind(string provider)
        {
            var value = (provider ?? string.Empty).Trim().ToLowerInvariant();
            if (value.Contains("nvidia")) return "nvidia";
            if (value.Contains("openai")) return "openai";
            if (value.Contains("aion")) return "aionui_broker";
            if (value.Contains("local")) return "local";
            return "openai_compatible";
        }

        private static string InferBaseUrlAlias(string provider, string apiBaseUrl)
        {
            var kind = InferProviderKind(provider);
            if (kind == "nvidia") return "NVIDIA";
            if (kind == "openai") return "OPENAI";
            if (kind == "aionui_broker") return "AIONUI_BROKER";
            if ((apiBaseUrl ?? string.Empty).IndexOf("localhost", StringComparison.OrdinalIgnoreCase) >= 0) return "LOCAL";
            return "OPENAI_COMPATIBLE";
        }

        private static string DefaultIfEmpty(string current, string fallback)
        {
            return string.IsNullOrWhiteSpace(current) ? fallback : current;
        }

        private string ResolveMode(bool keyConfigured)
        {
            var overrideMode = Registry.GetValue(AppIdentity.RegistryRoot, AssistantModeValueName, null)?.ToString();
            var configuredMode = NormalizeMode(overrideMode) ?? NormalizeMode(_config.Assistant.Mode);
            if (!string.IsNullOrWhiteSpace(configuredMode))
            {
                if (string.Equals(configuredMode, RealMode, StringComparison.OrdinalIgnoreCase) && !keyConfigured)
                {
                    return RealMode;
                }

                return configuredMode;
            }

            return keyConfigured ? RealMode : MockMode;
        }

        private static string NormalizeMode(string mode)
        {
            if (string.Equals(mode, RealMode, StringComparison.OrdinalIgnoreCase)) return RealMode;
            if (string.Equals(mode, MockMode, StringComparison.OrdinalIgnoreCase)) return MockMode;
            return null;
        }

        private static bool HasAgentToken()
        {
            var tokenPath = GetAgentTokenPath();
            return File.Exists(tokenPath) && !string.IsNullOrWhiteSpace(File.ReadAllText(tokenPath));
        }

        private static string GetAgentTokenPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VIRA",
                ".agent_token");
        }

        private static string BuildMockResponse(AssistantMessage userMessage, AssistantPreviewStatus status)
        {
            var prompt = (userMessage.Text ?? string.Empty).Trim();
            var hasAttachment = userMessage.AttachmentPaths != null && userMessage.AttachmentPaths.Count > 0;

            if (hasAttachment)
            {
                return "Mock preview mode is active. I can see that you attached a screenshot or document, but no external AI call was made. In real mode, this image would be analyzed for UI errors, drawing issues, and next-step guidance.";
            }

            if (prompt.IndexOf("lucky", StringComparison.OrdinalIgnoreCase) >= 0 ||
                prompt.IndexOf("search", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Mock preview mode is active. Feeling Lucky in BlueBrick Lab searches only the local vault sample index, not PDM. If results look empty, reindex the local vault and confirm your sample seed folder exists.";
            }

            if (prompt.IndexOf("generate", StringComparison.OrdinalIgnoreCase) >= 0 ||
                prompt.IndexOf("pdf", StringComparison.OrdinalIgnoreCase) >= 0 ||
                prompt.IndexOf("dxf", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Mock preview mode is active. Generation in BlueBrick Lab saves to the local working folder and local vault only. No PDM writes, SQL writes, or network-vault paths are used in this preview.";
            }

            return "Mock preview mode is active. The assistant UI, routing, and local preview wiring are working. To use real AI responses, configure an API key and switch the assistant to real mode.";
        }

        private sealed class ApiKeyInfo
        {
            internal ApiKeyInfo(string apiKey, string keySource)
            {
                ApiKey = apiKey;
                KeySource = keySource;
            }

            internal string ApiKey { get; }
            internal string KeySource { get; }
            internal bool Configured => !string.IsNullOrWhiteSpace(ApiKey);
        }
    }
}
