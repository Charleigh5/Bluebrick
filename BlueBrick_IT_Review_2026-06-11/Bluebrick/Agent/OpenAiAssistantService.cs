using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
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
        private const int MaxToolRounds = 5;

        private static readonly HttpClient Client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(45)
        };

        private static readonly HttpClient StreamClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };

        static OpenAiAssistantService()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
        }

        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(5);

        private readonly AgentConfig _config;
        private readonly AssistantSessionStore _store;
        private readonly RegistryCache _registryCache;
        private readonly AssistantToolService _toolService;
        private volatile List<AssistantModelProfile> _profilesCache;
        private volatile JArray _toolSchemasCache;
        private readonly Stopwatch _tokenCacheStopwatch = Stopwatch.StartNew();
        private bool _tokenCachedResult;
        private bool _tokenCacheValid;

        internal OpenAiAssistantService(AgentConfig config) : this(config, new AssistantToolService(config, null))
        {
        }

        internal OpenAiAssistantService(AgentConfig config, AssistantToolService toolService)
        {
            _config = config;
            _store = new AssistantSessionStore();
            _registryCache = new RegistryCache(CacheTtl);
            _toolService = toolService;
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
            _registryCache.Invalidate(AssistantModeValueName);
            return Task.FromResult(BuildStatus());
        }

        public Task<IList<AssistantModelProfile>> GetModelsAsync()
        {
            return Task.FromResult<IList<AssistantModelProfile>>(GetCachedProfiles());
        }

        public Task<AssistantPreviewStatus> SetModelAsync(string modelId)
        {
            var profile = ResolveProfile(modelId);
            Registry.SetValue(AppIdentity.RegistryRoot, AssistantModelValueName, profile.Id, RegistryValueKind.String);
            _registryCache.Invalidate(AssistantModelValueName);
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
            var extraMessages = new JArray();
            var round = 0;
            string body;

            while (true)
            {
                body = await SendChatCompletionAsync(BuildChatRequestBody(session, false, extraMessages.Count > 0 ? extraMessages : null), keyInfo.ApiKey, profile).ConfigureAwait(false);
                var responseJson = JObject.Parse(body);
                var toolCalls = ExtractToolCalls(responseJson);

                if (toolCalls == null || toolCalls.Count == 0) break;

                round++;
                if (round > MaxToolRounds) break;

                var assistantText = ExtractAssistantText(body);
                var callMessages = BuildAssistantToolCallMessages(toolCalls, assistantText);
                foreach (var m in callMessages) extraMessages.Add(m);

                var traceId = Guid.NewGuid().ToString("N").Substring(0, 8);
                var toolResults = await ExecuteToolCallRoundAsync(toolCalls, traceId).ConfigureAwait(false);
                var toolResultMessages = toolResults["messages"] as JArray;
                if (toolResultMessages != null)
                {
                    foreach (var m in toolResultMessages) extraMessages.Add(m);
                }
            }

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

    public async Task SendMessageStreamAsync(string sessionId, string message,
        IList<string> attachmentPaths, Action<AssistantStreamChunk> onChunk, CancellationToken cancellationToken)
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
            var mockText = BuildMockResponse(userMessage, status);
            var mockMessage = new AssistantMessage
            {
                Role = "assistant",
                Text = mockText,
                CreatedUtc = DateTime.UtcNow
            };
            session.Messages.Add(mockMessage);
            TrimHistory(session);
            _store.Save(session);

            foreach (var word in mockText.Split(' '))
            {
                cancellationToken.ThrowIfCancellationRequested();
                onChunk(AssistantStreamChunk.TextDelta(word + " "));
                await Task.Delay(15, cancellationToken).ConfigureAwait(false);
            }
            onChunk(AssistantStreamChunk.Complete());
            return;
        }

        var profile = ResolveProfile();
        var keyInfo = ResolveApiKeyInfo(profile);
        if (!keyInfo.Configured)
        {
            onChunk(AssistantStreamChunk.Error("key_missing",
                "AI API key not configured. Set OPENAI_API_KEY or AssistantApiKey in the registry."));
            onChunk(AssistantStreamChunk.Complete());
            return;
        }

        var fullText = new StringBuilder();
        try
        {
            var extraMessages = new JArray();
            var round = 0;

            while (round < MaxToolRounds)
            {
            var pendingToolCalls = new List<LlmToolCall>();
            var roundText = new StringBuilder();
            var hasToolCalls = false;

                await StreamChatCompletionAsync(BuildChatRequestBody(session, true, extraMessages.Count > 0 ? extraMessages : null), keyInfo.ApiKey, profile, chunk =>
                {
                    roundText.Append(chunk);
                    onChunk(AssistantStreamChunk.TextDelta(chunk));
                }, cancellationToken, (name, id, args) =>
                {
                    hasToolCalls = true;
                    var tc = new LlmToolCall { Id = id, Name = name, Arguments = args };
                    pendingToolCalls.Add(tc);
                    onChunk(AssistantStreamChunk.ToolCall(name, id, args));
                }).ConfigureAwait(false);

                fullText.Append(roundText);

                if (!hasToolCalls) break;

                round++;
                var assistantText = roundText.ToString();
                var callMessages = BuildAssistantToolCallMessages(pendingToolCalls, assistantText);
                foreach (var m in callMessages) extraMessages.Add(m);

                var traceId = Guid.NewGuid().ToString("N").Substring(0, 8);
                var toolResults = await ExecuteToolCallRoundAsync(pendingToolCalls, traceId).ConfigureAwait(false);
                var toolResultMessages = toolResults["messages"] as JArray;
                if (toolResultMessages != null)
                {
                    foreach (var tm in toolResultMessages)
                    {
                        extraMessages.Add(tm);
                        var callId = tm.Value<string>("tool_call_id") ?? "";
                        var content = tm.Value<string>("content") ?? "";
                        onChunk(AssistantStreamChunk.ToolResult(callId, content));
                    }
                }
            }

            var assistantMessage = new AssistantMessage
            {
                Role = "assistant",
                Text = fullText.ToString(),
                CreatedUtc = DateTime.UtcNow
            };
            session.Messages.Add(assistantMessage);
            TrimHistory(session);
            _store.Save(session);
            onChunk(AssistantStreamChunk.Complete());
        }
        catch (OperationCanceledException)
        {
            if (fullText.Length > 0)
            {
                var partialMessage = new AssistantMessage
                {
                    Role = "assistant",
                    Text = fullText.ToString(),
                    CreatedUtc = DateTime.UtcNow
                };
                session.Messages.Add(partialMessage);
                TrimHistory(session);
                _store.Save(session);
            }
            onChunk(AssistantStreamChunk.Error("cancelled", "Request cancelled."));
            onChunk(AssistantStreamChunk.Complete());
        }
        catch (Exception ex)
        {
            var classified = AssistantErrorClassifier.FromException(ex);
            onChunk(AssistantStreamChunk.Error(classified.Code, classified.Message));
            onChunk(AssistantStreamChunk.Complete());
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
                ActiveModelDescriptor = AssistantModelDescriptor.FromProfile(profile, keyInfo.Configured),
                Scopes = AssistantScopeRegistry.Build(_config, _toolService.GetCatalog()).ToArray(),
                AssistantWebViewStatus = _config.Assistant.UseReactWebView ? "react-enabled" : "fallback-shell",
                Checklist = checklist.ToArray()
            };
        }

        private string BuildChatRequestBody(AssistantSession session, bool streaming = false, JArray extraMessages = null)
        {
            var profile = ResolveProfile();
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
                var content = BuildMessageContent(msg, session.SessionId);
                messages.Add(new JObject
                {
                    ["role"] = msg.Role,
                    ["content"] = content
                });
            }

            if (extraMessages != null)
            {
                foreach (var extra in extraMessages)
                {
                    messages.Add(extra);
                }
            }

            var requestObj = new JObject
            {
                ["model"] = profile.Model,
                ["messages"] = messages,
                ["stream"] = streaming
            };

            if (profile.SupportsTools)
            {
                var tools = GetToolSchemas();
                if (tools.Count > 0)
                {
                    requestObj["tools"] = tools;
                }
            }

            return requestObj.ToString(Formatting.None);
        }

        private JToken BuildMessageContent(AssistantMessage msg, string sessionId)
        {
            var content = new JArray
            {
                new JObject
                {
                    ["type"] = "text",
                    ["text"] = msg.Text ?? string.Empty
                }
            };

            if (_config.Assistant.EnableUploads && msg.Role == "user")
            {
                long totalBytes = 0;
                var maxBytes = _config.Assistant.MaxTotalAttachmentBytes;
                var enforceCap = maxBytes > 0;
                foreach (var attachmentPath in msg.AttachmentPaths.Select(path =>
                    AssistantImageTools.PrepareAttachment(sessionId, path,
                        _config.Assistant.MaxImageDimension, _config.Assistant.JpegQuality))
                    .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path)))
                {
                    var fi = new FileInfo(attachmentPath);
                    if (enforceCap && totalBytes + fi.Length > maxBytes) break;
                    totalBytes += fi.Length;

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

            return content.Count == 1 ? (JToken)content[0]["text"] : content;
        }

        private JArray GetToolSchemas()
        {
            var cached = _toolSchemasCache;
            if (cached != null) return cached;

            var catalog = _toolService.GetCatalog();
            var tools = new JArray();
            foreach (var tool in catalog)
            {
                if (!tool.Enabled) continue;
                tools.Add(new JObject
                {
                    ["type"] = "function",
                    ["function"] = new JObject
                    {
                        ["name"] = tool.Name,
                        ["description"] = tool.Description ?? string.Empty,
                        ["parameters"] = BuildToolParameters(tool)
                    }
                });
            }

            _toolSchemasCache = tools;
            return tools;
        }

        private static JObject BuildToolParameters(AssistantToolDescriptor tool)
        {
            var props = new JObject();
            var required = new JArray();

            if (tool.Name == "search_local_vault")
            {
                props["query"] = new JObject { ["type"] = "string", ["description"] = "Search query for the local vault index." };
                props["limit"] = new JObject { ["type"] = "integer", ["description"] = "Maximum number of results to return (1-25)." };
                required.Add("query");
            }
            else if (tool.Name == "search_pdm")
            {
                props["query"] = new JObject { ["type"] = "string", ["description"] = "Search query for PDM vault files." };
                props["limit"] = new JObject { ["type"] = "integer", ["description"] = "Maximum number of results to return." };
                required.Add("query");
            }
            else if (tool.Name == "search_epicor")
            {
                props["query"] = new JObject { ["type"] = "string", ["description"] = "Search query for Epicor ERP parts." };
                props["limit"] = new JObject { ["type"] = "integer", ["description"] = "Maximum number of results to return." };
                required.Add("query");
            }
            else if (tool.Name == "capture_screenshot")
            {
                props["sessionId"] = new JObject { ["type"] = "string", ["description"] = "Optional session ID for the screenshot capture." };
            }

            var schema = new JObject
            {
                ["type"] = "object",
                ["properties"] = props
            };
            if (required.Count > 0) schema["required"] = required;
            return schema;
        }

        internal class LlmToolCall
        {
            internal string Id { get; set; }
            internal string Name { get; set; }
            internal string Arguments { get; set; }
        }

        private List<LlmToolCall> ExtractToolCalls(JObject response)
        {
            var choices = response["choices"] as JArray;
            if (choices == null || choices.Count == 0) return null;
            var message = choices[0]["message"];
            var toolCalls = message?["tool_calls"] as JArray;
            if (toolCalls == null || toolCalls.Count == 0) return null;

            var result = new List<LlmToolCall>();
            foreach (var tc in toolCalls)
            {
                var id = tc.Value<string>("id") ?? Guid.NewGuid().ToString("N");
                var fn = tc["function"];
                var name = fn?.Value<string>("name") ?? string.Empty;
                var args = fn?.Value<string>("arguments") ?? "{}";
                result.Add(new LlmToolCall { Id = id, Name = name, Arguments = args });
            }
            return result;
        }

        private async Task<JObject> ExecuteToolCallRoundAsync(List<LlmToolCall> toolCalls, string traceId)
        {
            var toolMessages = new JArray();
            foreach (var tc in toolCalls)
            {
                Dictionary<string, string> parameters;
                try
                {
                    var argsObj = JObject.Parse(tc.Arguments);
                    parameters = argsObj.Properties().ToDictionary(p => p.Name, p => p.Value.ToString());
                }
                catch
                {
                    parameters = new Dictionary<string, string>();
                }

                string query;
                parameters.TryGetValue("query", out query);
                int limit;
                int.TryParse(parameters.TryGetValue("limit", out var limitStr) ? limitStr : null, out limit);

                var request = new AssistantToolRequest
                {
                    ToolName = tc.Name,
                    Query = query,
                    Limit = limit,
                    Parameters = parameters
                };

                var result = await _toolService.ExecuteAsync(request, traceId).ConfigureAwait(false);
                var resultJson = JsonConvert.SerializeObject(new
                {
                    status = result.Status,
                    message = result.Message,
                    items = result.Items.Select(i => new { i.Id, i.Title, i.Subtitle, i.Path, i.Source })
                }, Formatting.None);

                toolMessages.Add(new JObject
                {
                    ["role"] = "tool",
                    ["tool_call_id"] = tc.Id,
                    ["content"] = resultJson
                });
            }
            return new JObject { ["messages"] = toolMessages };
        }

        private JArray BuildAssistantToolCallMessages(List<LlmToolCall> toolCalls, string assistantText)
        {
            var tcArray = new JArray();
            foreach (var tc in toolCalls)
            {
                tcArray.Add(new JObject
                {
                    ["id"] = tc.Id,
                    ["type"] = "function",
                    ["function"] = new JObject
                    {
                        ["name"] = tc.Name,
                        ["arguments"] = tc.Arguments
                    }
                });
            }

            var assistantMsg = new JObject
            {
                ["role"] = "assistant",
                ["content"] = assistantText ?? string.Empty,
                ["tool_calls"] = tcArray
            };

            return new JArray { assistantMsg };
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

        private async Task StreamChatCompletionAsync(string requestBody, string apiKey,
            AssistantModelProfile profile, Action<string> onChunk, CancellationToken cancellationToken,
            Action<string, string, string> onToolCall = null)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post,
                profile.ApiBaseUrl.TrimEnd('/') + "/chat/completions"))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Headers.Add("User-Agent", "BlueBrick-AI-Assistant/1.0");
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                using (var response = await StreamClient.SendAsync(request,
                    HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        var errorBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        var classified = AssistantErrorClassifier.FromProviderFailure(errorBody);
                        throw new InvalidOperationException(classified.Message);
                    }

                    using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        var lineBuffer = new StringBuilder();
                        var buffer = new char[4096];
                        var toolCallAccumulators = new Dictionary<int, ToolCallAccumulator>();

                        while (!cancellationToken.IsCancellationRequested)
                        {
                            var readTask = reader.ReadAsync(buffer, 0, buffer.Length);
                            var completed = await Task.WhenAny(readTask,
                                Task.Delay(TimeSpan.FromSeconds(90), cancellationToken)).ConfigureAwait(false);

                            if (completed != readTask)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                throw new TimeoutException("Streaming response timed out waiting for data.");
                            }

                            var count = await readTask.ConfigureAwait(false);
                            if (count == 0) break;

                            lineBuffer.Append(buffer, 0, count);
                            var content = lineBuffer.ToString();
                            var lastNewline = content.LastIndexOf('\n');
                            if (lastNewline < 0) continue;

                            var completeLines = content.Substring(0, lastNewline + 1);
                            lineBuffer.Clear();
                            lineBuffer.Append(content.Substring(lastNewline + 1));

                            foreach (var line in completeLines.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                var trimmed = line.Trim();
                                if (!trimmed.StartsWith("data:")) continue;
                                var data = trimmed.Substring(5).Trim();
                                if (data == "[DONE]")
                                {
                                    FlushToolCalls(toolCallAccumulators, onToolCall);
                                    return;
                                }

                                try
                                {
                                    var chunk = JObject.Parse(data);
                                    var choices = chunk["choices"] as JArray;
                                    if (choices == null || choices.Count == 0) continue;
                                    var delta = choices[0]["delta"];
                                    if (delta == null) continue;

                                    var text = delta.Value<string>("content");
                                    if (!string.IsNullOrEmpty(text))
                                    {
                                        onChunk(text);
                                    }

                                    var deltaToolCalls = delta["tool_calls"] as JArray;
                                    if (deltaToolCalls != null && onToolCall != null)
                                    {
                                        foreach (var tc in deltaToolCalls)
                                        {
                                            var idx = tc.Value<int?>("index") ?? 0;
                                            ToolCallAccumulator acc;
                                            if (!toolCallAccumulators.TryGetValue(idx, out acc))
                                            {
                                                acc = new ToolCallAccumulator();
                                                toolCallAccumulators[idx] = acc;
                                            }
                                            var tcId = tc.Value<string>("id");
                                            if (!string.IsNullOrEmpty(tcId)) acc.Id = tcId;
                                            var fn = tc["function"];
                                            if (fn != null)
                                            {
                                                var fName = fn.Value<string>("name");
                                                if (!string.IsNullOrEmpty(fName)) acc.Name = fName;
                                                var fArgs = fn.Value<string>("arguments");
                                                if (!string.IsNullOrEmpty(fArgs)) acc.Arguments.Append(fArgs);
                                            }
                                        }
                                    }

                                    var finishReason = choices[0].Value<string>("finish_reason");
                                    if (finishReason == "tool_calls")
                                    {
                                        FlushToolCalls(toolCallAccumulators, onToolCall);
                                    }
                                }
                                catch
                                {
                                    continue;
                                }
                            }
                        }

                        FlushToolCalls(toolCallAccumulators, onToolCall);
                    }
                }
            }
        }

        private static void FlushToolCalls(Dictionary<int, ToolCallAccumulator> accumulators, Action<string, string, string> onToolCall)
        {
            if (onToolCall == null || accumulators == null) return;
            foreach (var kvp in accumulators)
            {
                var acc = kvp.Value;
                if (string.IsNullOrEmpty(acc.Name)) continue;
                var args = acc.Arguments.ToString();
                if (string.IsNullOrWhiteSpace(args)) args = "{}";
                onToolCall(acc.Name, acc.Id ?? Guid.NewGuid().ToString("N"), args);
            }
            accumulators.Clear();
        }

        internal class ToolCallAccumulator
        {
            internal string Id;
            internal string Name;
            internal StringBuilder Arguments = new StringBuilder();
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
            var excess = session.Messages.Count - _config.Assistant.MaxHistory;
            if (excess > 0)
            {
                session.Messages.RemoveRange(0, excess);
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

            var key = _registryCache.GetValue(AppIdentity.RegistryRoot, AssistantApiKeyValueName);
            if (!string.IsNullOrWhiteSpace(key))
            {
                return new ApiKeyInfo(key.Trim(), "registry");
            }

            return new ApiKeyInfo(null, "missing");
        }

        private AssistantModelProfile ResolveProfile(string requestedId = null)
        {
            var profiles = GetCachedProfiles();
            var activeId = requestedId;
            if (string.IsNullOrWhiteSpace(activeId))
            {
                activeId = _registryCache.GetValue(AppIdentity.RegistryRoot, AssistantModelValueName);
            }

            var profile = profiles.FirstOrDefault(p => string.Equals(p.Id, activeId, StringComparison.OrdinalIgnoreCase));
            if (profile != null) return profile;

            return profiles.FirstOrDefault(p => p.IsDefault) ?? profiles.First();
        }

        private List<AssistantModelProfile> GetCachedProfiles()
        {
            var cached = _profilesCache;
            if (cached != null) return cached;
            cached = GetModelProfiles().ToList();
            _profilesCache = cached;
            return cached;
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

            yield return NormalizeProfile(new AssistantModelProfile
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
            });
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
            var configMode = NormalizeMode(_config.Assistant.Mode);
            if (string.Equals(configMode, MockMode, StringComparison.OrdinalIgnoreCase))
            {
                return MockMode;
            }

            var overrideMode = _registryCache.GetValue(AppIdentity.RegistryRoot, AssistantModeValueName);
            var registryMode = NormalizeMode(overrideMode);
            var configuredMode = configMode ?? registryMode;

            if (string.Equals(configuredMode, RealMode, StringComparison.OrdinalIgnoreCase))
            {
                return keyConfigured ? RealMode : MockMode;
            }

            if (string.Equals(configuredMode, MockMode, StringComparison.OrdinalIgnoreCase))
            {
                return MockMode;
            }

            return keyConfigured ? RealMode : MockMode;
        }

        private static string NormalizeMode(string mode)
        {
            if (string.Equals(mode, RealMode, StringComparison.OrdinalIgnoreCase)) return RealMode;
            if (string.Equals(mode, MockMode, StringComparison.OrdinalIgnoreCase)) return MockMode;
            return null;
        }

        private bool HasAgentToken()
        {
            if (_tokenCacheValid && _tokenCacheStopwatch.Elapsed < CacheTtl)
            {
                return _tokenCachedResult;
            }

            var tokenPath = GetAgentTokenPath();
            _tokenCachedResult = File.Exists(tokenPath) && !string.IsNullOrWhiteSpace(File.ReadAllText(tokenPath));
            _tokenCacheValid = true;
            _tokenCacheStopwatch.Restart();
            return _tokenCachedResult;
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

        private sealed class RegistryCache
        {
            private readonly TimeSpan _ttl;
            private readonly Dictionary<string, CachedEntry> _entries = new Dictionary<string, CachedEntry>(StringComparer.OrdinalIgnoreCase);
            private readonly Stopwatch _clock = Stopwatch.StartNew();

            internal RegistryCache(TimeSpan ttl)
            {
                _ttl = ttl;
            }

            internal string GetValue(string keyPath, string valueName)
            {
                var key = valueName;
                CachedEntry entry;
                if (_entries.TryGetValue(key, out entry) && (_clock.ElapsedTicks - entry.Timestamp) < _ttl.Ticks)
                {
                    return (string)entry.Value;
                }

                var raw = Registry.GetValue(keyPath, valueName, null);
                var value = raw?.ToString();
                _entries[key] = new CachedEntry(value, _clock.ElapsedTicks);
                return value;
            }

            internal void Invalidate(string valueName)
            {
                _entries.Remove(valueName);
            }

            internal void InvalidateAll()
            {
                _entries.Clear();
            }

            private struct CachedEntry
            {
                internal readonly object Value;
                internal readonly long Timestamp;

                internal CachedEntry(object value, long timestamp)
                {
                    Value = value;
                    Timestamp = timestamp;
                }
            }
        }
    }
}
