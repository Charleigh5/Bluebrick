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
        private const int NonStreamingChatMaxTokens = 128;

        private static readonly object ChatRequestShapeLogLock = new object();

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

        internal OpenAiAssistantService(AgentConfig config, AssistantToolService toolService, AssistantSessionStore store = null)
        {
            _config = config;
            _store = store ?? new AssistantSessionStore();
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
            // Resolve identity before capture so a status failure cannot orphan a newly captured artifact.
            var runtimeBuildId = BuildStatus().RuntimeBuildId;
            var artifact = AssistantImageTools.CaptureWindowArtifact(request);
            _store.AttachScreenshot(artifact, _config.Assistant.Screenshots, runtimeBuildId);
            return Task.FromResult(artifact);
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
            var models = GetCachedProfiles().Select(p =>
            {
                var copy = Newtonsoft.Json.JsonConvert.DeserializeObject<AssistantModelProfile>(Newtonsoft.Json.JsonConvert.SerializeObject(p));
                var state = SharedRuntimeState(p);
                copy.RuntimeEligible = state.Available;
                copy.Available = state.Available ? (bool?)null : false;
                copy.UnavailableReason = state.Reason;
                copy.AvailabilityEvidence = "credential_presence_only; provider_health_unknown";
                return copy;
            }).ToList();
            return Task.FromResult<IList<AssistantModelProfile>>(models);
        }

        public Task<AssistantPreviewStatus> SetModelAsync(string modelId)
        {
            if (string.IsNullOrWhiteSpace(modelId))
                throw new InvalidOperationException("MODEL_UNAVAILABLE: model ID is required.");
            var profile = ResolveProfile(modelId);
            Registry.SetValue(AppIdentity.RegistryRoot, AssistantModelValueName, profile.Id, RegistryValueKind.String);
            _registryCache.Invalidate(AssistantModelValueName);
            return Task.FromResult(BuildStatus());
        }

        public async Task<AssistantConnectionTestResult> TestConnectionAsync()
        {
            // BuildStatus resolves registry/profile state and can throw; keep it
            // inside the guarded region so /assistant/test always returns a body.
            AssistantPreviewStatus status;
            try
            {
                status = BuildStatus();
            }
            catch (Exception ex)
            {
                return new AssistantConnectionTestResult
                {
                    Success = false,
                    Mode = MockMode,
                    Configured = false,
                    Message = "Status resolution failed: " + (string.IsNullOrWhiteSpace(ex.Message) ? ex.GetType().Name : ex.Message)
                };
            }

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

            AssistantModelProfile profile;
            try
            {
                profile = ResolveProfile();
            }
            catch (Exception ex)
            {
                return new AssistantConnectionTestResult
                {
                    Success = false,
                    Mode = status.AssistantMode,
                    Configured = true,
                    KeySource = status.KeySource,
                    Message = "Assistant profile could not be resolved: " + ex.Message
                };
            }

            if (profile == null)
            {
                return new AssistantConnectionTestResult
                {
                    Success = false,
                    Mode = status.AssistantMode,
                    Configured = true,
                    KeySource = status.KeySource,
                    Message = "Assistant profile is not configured. Select a model in the panel before testing the connection."
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
                ["model"] = profile.Model,
                ["messages"] = messages,
                ["max_tokens"] = 50
            }.ToString(Formatting.None);

            var stopwatch = Stopwatch.StartNew();
            try
            {
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
            IList<string> attachmentPaths, string scopeId = null)
        {
            var session = _store.Get(sessionId) ?? _store.Create();
            List<string> uploadPaths;
            try { uploadPaths = ResolveUploadPaths(session, attachmentPaths); }
            catch (Exception ex) { return new AssistantSessionResponse { SessionId = session.SessionId,
                AssistantAvailable = false, ErrorCode = "attachment_blocked", Error = ex.Message }; }

            var userMessage = new AssistantMessage
            {
                Role = "user",
                Text = message?.Trim() ?? string.Empty,
                AttachmentPaths = uploadPaths,
                AttachmentArtifactIds = new List<string>(session.PendingScreenshotIds),
                CreatedUtc = DateTime.UtcNow
            };
            // Get() returns a detached snapshot; keep the proposed turn in memory until success.
            session.Messages.Add(userMessage);
            TrimHistory(session);

            AssistantModelProfile profile;
            try { profile = ResolveMessageProfile(session); }
            catch (Exception ex) { return new AssistantSessionResponse { SessionId = session.SessionId,
                AssistantAvailable = false, ErrorCode = SessionHasImages(session) ? "vision_model_unavailable" : "model_unavailable", Error = ex.Message }; }
            userMessage.ResolvedModelId = profile.Id;
            var keyInfo = ResolveApiKeyInfo(profile);
            var status = BuildStatusForProfile(profile);
            if (SessionHasImages(session) && (!keyInfo.Configured || ResolveMode(keyInfo.Configured) == MockMode))
                return new AssistantSessionResponse { SessionId = session.SessionId, Message = userMessage,
                    AssistantAvailable = false, ErrorCode = "provider_dependency", Error = "BLOCKED_DEPENDENCY: resolved vision model requires configured credentials and real provider mode." };
            if (status.AssistantMode == "unavailable")
                return new AssistantSessionResponse { SessionId = session.SessionId, AssistantAvailable = false,
                    ErrorCode = "model_unavailable", Error = "No enabled model is configured." };
            if (!SessionHasImages(session) && string.Equals(status.AssistantMode, MockMode, StringComparison.OrdinalIgnoreCase))
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
                body = await SendChatCompletionAsync(BuildChatRequestBody(session, profile, false, extraMessages.Count > 0 ? extraMessages : null), keyInfo.ApiKey, profile,
                    () => _store.RecordTransmissionAttempt(session, profile.Id)).ConfigureAwait(false);
                var responseJson = JObject.Parse(body);
                var toolCalls = ExtractToolCalls(responseJson);

                if (toolCalls == null || toolCalls.Count == 0) break;

                round++;
                if (round > MaxToolRounds) break;

                var assistantText = ExtractAssistantText(body);
                var callMessages = BuildAssistantToolCallMessages(toolCalls, assistantText);
                foreach (var m in callMessages) extraMessages.Add(m);

                var traceId = Guid.NewGuid().ToString("N").Substring(0, 8);
                var toolResults = await ExecuteToolCallRoundAsync(toolCalls, traceId, scopeId).ConfigureAwait(false);
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
                ResolvedModelId = profile.Id,
                CreatedUtc = DateTime.UtcNow
            };
            session.Messages.Add(assistantMessage);
            _store.ConsumePendingScreenshots(session, userMessage);
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
        IList<string> attachmentPaths, Action<AssistantStreamChunk> onChunk, CancellationToken cancellationToken, string scopeId = null)
    {
        var session = _store.Get(sessionId) ?? _store.Create();
        List<string> uploadPaths;
        try { uploadPaths = ResolveUploadPaths(session, attachmentPaths); }
        catch (Exception ex) { onChunk(AssistantStreamChunk.Error("attachment_blocked", ex.Message));
            onChunk(AssistantStreamChunk.Complete()); return; }

        var userMessage = new AssistantMessage
        {
            Role = "user",
            Text = message?.Trim() ?? string.Empty,
            AttachmentPaths = uploadPaths,
                AttachmentArtifactIds = new List<string>(session.PendingScreenshotIds),
            CreatedUtc = DateTime.UtcNow
        };
        // Get() returns a detached snapshot; keep the proposed turn in memory until success.
        session.Messages.Add(userMessage);
        TrimHistory(session);

        AssistantModelProfile profile;
        try { profile = ResolveMessageProfile(session, true); }
        catch (Exception ex) { onChunk(AssistantStreamChunk.Error(SessionHasImages(session) ? "vision_model_unavailable" : "model_unavailable", ex.Message));
            onChunk(AssistantStreamChunk.Complete()); return; }
        userMessage.ResolvedModelId = profile.Id;
        onChunk(new AssistantStreamChunk { Type = "model_resolved", Text = profile.Id });
        var keyInfo = ResolveApiKeyInfo(profile);
        var status = BuildStatusForProfile(profile);
        if (SessionHasImages(session) && (!keyInfo.Configured || ResolveMode(keyInfo.Configured) == MockMode))
        {
            onChunk(AssistantStreamChunk.Error("provider_dependency", "BLOCKED_DEPENDENCY: resolved vision model requires configured credentials and real provider mode."));
            onChunk(AssistantStreamChunk.Complete()); return;
        }
        if (status.AssistantMode == "unavailable")
        {
            onChunk(AssistantStreamChunk.Error("model_unavailable", "No enabled model is configured."));
            return;
        }
        if (!SessionHasImages(session) && string.Equals(status.AssistantMode, MockMode, StringComparison.OrdinalIgnoreCase))
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


        if (!keyInfo.Configured)
        {
            onChunk(AssistantStreamChunk.Error("key_missing",
                "AI API key not configured. Set OPENAI_API_KEY or AssistantApiKey in the registry."));
            onChunk(AssistantStreamChunk.Complete());
            return;
        }

        if (!profile.SupportsStreaming)
        {
            try
            {
                var fallbackBody = await SendChatCompletionAsync(
                    BuildChatRequestBody(session, profile, false, null),
                    keyInfo.ApiKey,
                    profile, () => _store.RecordTransmissionAttempt(session, profile.Id)).ConfigureAwait(false);
                var fallbackText = ExtractAssistantText(fallbackBody);
                onChunk(AssistantStreamChunk.TextDelta(fallbackText));
                var fallbackMessage = new AssistantMessage
                {
                    Role = "assistant",
                    Text = fallbackText,
                    ResolvedModelId = profile.Id,
                    CreatedUtc = DateTime.UtcNow
                };
                session.Messages.Add(fallbackMessage);
                _store.ConsumePendingScreenshots(session, userMessage);
                TrimHistory(session);
                _store.Save(session);
                onChunk(AssistantStreamChunk.Complete());
            }
            catch (Exception ex)
            {
                var classified = AssistantErrorClassifier.FromException(ex);
                var safeWireMessage =
                    AssistantErrorClassifier.FormatWithProvenance(classified);
                onChunk(AssistantStreamChunk.Error(classified.Code, safeWireMessage));
                onChunk(AssistantStreamChunk.Complete());
            }
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

                await StreamChatCompletionAsync(BuildChatRequestBody(session, profile, true, extraMessages.Count > 0 ? extraMessages : null), keyInfo.ApiKey, profile, chunk =>
                {
                    roundText.Append(chunk);
                    onChunk(AssistantStreamChunk.TextDelta(chunk));
                }, cancellationToken, (name, id, args) =>
                {
                    hasToolCalls = true;
                    var tc = new LlmToolCall { Id = id, Name = name, Arguments = args };
                    pendingToolCalls.Add(tc);
                    onChunk(AssistantStreamChunk.ToolCall(name, id, args));
                }, () => _store.RecordTransmissionAttempt(session, profile.Id)).ConfigureAwait(false);

                fullText.Append(roundText);

                if (!hasToolCalls) break;

                round++;
                var assistantText = roundText.ToString();
                var callMessages = BuildAssistantToolCallMessages(pendingToolCalls, assistantText);
                foreach (var m in callMessages) extraMessages.Add(m);

                var traceId = Guid.NewGuid().ToString("N").Substring(0, 8);
                var toolResults = await ExecuteToolCallRoundAsync(pendingToolCalls, traceId, scopeId).ConfigureAwait(false);
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
                ResolvedModelId = profile.Id,
                CreatedUtc = DateTime.UtcNow
            };
            session.Messages.Add(assistantMessage);
            _store.ConsumePendingScreenshots(session, userMessage);
            TrimHistory(session);
            _store.Save(session);
            onChunk(AssistantStreamChunk.Complete());
        }
        catch (OperationCanceledException)
        {
            // Partial output was not a successful provider transaction; do not persist the proposed turn.
            onChunk(AssistantStreamChunk.Error("cancelled", "Request cancelled."));
            onChunk(AssistantStreamChunk.Complete());
        }
        catch (Exception ex)
        {
            var classified = AssistantErrorClassifier.FromException(ex);
            var safeWireMessage =
                AssistantErrorClassifier.FormatWithProvenance(classified);
            onChunk(AssistantStreamChunk.Error(classified.Code, safeWireMessage));
            onChunk(AssistantStreamChunk.Complete());
        }
    }

    private AssistantPreviewStatus BuildStatus()
        {
            return BuildStatusForProfile(null);
        }

    private AssistantPreviewStatus BuildStatusForProfile(AssistantModelProfile resolvedProfile)
        {
            AssistantModelProfile profile;
            try { profile = resolvedProfile ?? ResolveProfile(); }
            catch (InvalidOperationException)
            {
                return new AssistantPreviewStatus
                {
                    Model = "UNKNOWN", Configured = false, KeyConfigured = false,
                    KeySource = "not_resolved", AssistantMode = "unavailable",
                    ConfigPath = _config.ConfigurationDiagnostics?.ConfigPath,
                    ConfigHash = _config.ConfigurationDiagnostics?.ConfigHash,
                    ConfigSchemaVersion = _config.ConfigSchemaVersion,
                    ConfigurationLoadStatus = _config.ConfigurationDiagnostics?.ConfigurationLoadStatus,
                    BridgePort = _config.Agent?.BridgePort ?? AppIdentity.BridgePort,
                    UseReactWebView = _config.Assistant.UseReactWebView,
                    RuntimeGenerationStatus = "NOT_VERIFIED",
                    Checklist = new[] { "MODEL_UNAVAILABLE: no enabled model is configured." }
                };
            }
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

            RuntimeGenerationCheck generation = null;
            if (AppIdentity.IsLabBuild)
            {
                var assemblyPath = typeof(OpenAiAssistantService).Assembly.Location;
                var assemblyRoot = Path.GetDirectoryName(assemblyPath);
                generation = RuntimeGenerationGuard.Validate(
                    Path.Combine(assemblyRoot, "runtime-manifest.json"),
                    "Lab",
                    assemblyPath,
                    _config.ConfigurationDiagnostics?.ConfigPath,
                    Path.Combine(assemblyRoot, "AssistantWeb", "dist"),
                    _config.ConfigSchemaVersion);
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
                UseReactWebView = _config.Assistant.UseReactWebView,
                ConfigPath = _config.ConfigurationDiagnostics?.ConfigPath,
                ConfigHash = _config.ConfigurationDiagnostics?.ConfigHash,
                ConfigSchemaVersion = _config.ConfigurationDiagnostics?.ConfigSchemaVersion,
                ConfigurationLoadStatus = _config.ConfigurationDiagnostics?.ConfigurationLoadStatus,
                AssistantValueSource = _config.ConfigurationDiagnostics?.AssistantValueSource,
                RuntimeGenerationStatus = generation?.Code ?? "NOT_REQUIRED_FOR_PRODUCTION",
                RuntimeGenerationDetail = generation?.Detail,
                RuntimeBuildId = generation?.BuildId,
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

        internal string BuildChatRequestBody(AssistantSession session, AssistantModelProfile profile, bool streaming = false, JArray extraMessages = null)
        {
            if (profile == null) throw new InvalidOperationException("MODEL_UNAVAILABLE: no resolved model.");
            if (SessionHasImages(session) && !profile.SupportsVision)
                throw new InvalidOperationException("VISION_MODEL_UNAVAILABLE: image input requires a compatible model.");
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

            if (!streaming)
            {
                requestObj["max_tokens"] = NonStreamingChatMaxTokens;
            }

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

        internal JToken BuildMessageContent(AssistantMessage msg, string sessionId, string screenshotRoot = null)
        {
            var content = new JArray
            {
                new JObject
                {
                    ["type"] = "text",
                    ["text"] = msg.Text ?? string.Empty
                }
            };

            if (msg.Role == "user" && msg.AttachmentPaths.Count > 0 && !_config.Assistant.EnableUploads)
                throw new InvalidOperationException("ATTACHMENT_BLOCKED: uploads are disabled.");
            if (_config.Assistant.EnableUploads && msg.Role == "user")
            {
                long totalBytes = 0;
                var maxBytes = _config.Assistant.MaxTotalAttachmentBytes;
                var enforceCap = maxBytes > 0;
                foreach (var originalPath in msg.AttachmentPaths)
                {
                    ValidateImagePath(originalPath);
                    // Deny concurrent replacement while consent is checked and the image is converted/read.
                    using (var sourceLock = File.Open(originalPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        AssistantScreenshotArtifactStore.EnsureAttachmentTransmissionAllowed(originalPath, screenshotRoot);
                        var attachmentPath = AssistantImageTools.PrepareAttachment(sessionId, originalPath,
                            _config.Assistant.MaxImageDimension, _config.Assistant.JpegQuality);
                        if (string.IsNullOrWhiteSpace(attachmentPath) || !File.Exists(attachmentPath))
                            throw new InvalidOperationException("ATTACHMENT_BLOCKED: image preparation failed.");
                        var bytes = File.ReadAllBytes(attachmentPath);
                        if (enforceCap && totalBytes + bytes.LongLength > maxBytes)
                            throw new InvalidOperationException("ATTACHMENT_BLOCKED: image size limit exceeded.");
                        totalBytes += bytes.LongLength;
                        AssistantScreenshotArtifactStore.EnsureAttachmentTransmissionAllowed(originalPath, screenshotRoot);
                        content.Add(new JObject
                        {
                            ["type"] = "image_url",
                            ["image_url"] = new JObject
                            {
                                ["url"] = "data:image/jpeg;base64," + Convert.ToBase64String(bytes),
                                ["detail"] = _config.Assistant.Detail
                            }
                        });
                    }
                }
            }

            return content.Count == 1 ? (JToken)content[0]["text"] : content;
        }

        private List<string> ResolveUploadPaths(AssistantSession session, IList<string> paths)
        {
            var result = (paths ?? new List<string>()).Concat(_store.ResolvePendingScreenshotPaths(session))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (result.Count > 0 && !_config.Assistant.EnableUploads)
                throw new InvalidOperationException("ATTACHMENT_BLOCKED: uploads are disabled.");
            foreach (var path in result) ValidateImagePath(path);
            return result;
        }

        private static void ValidateImagePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new InvalidOperationException("ATTACHMENT_BLOCKED: image is missing.");
            var extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension != ".png" && extension != ".jpg" && extension != ".jpeg" && extension != ".bmp" && extension != ".gif")
                throw new InvalidOperationException("ATTACHMENT_BLOCKED: document input is not supported by this image transport.");
        }

        private static bool SessionHasImages(AssistantSession session)
        {
            return session.Messages.Any(m => m.Role == "user" && m.AttachmentPaths != null && m.AttachmentPaths.Count > 0);
        }

        private AssistantModelProfile ResolveMessageProfile(AssistantSession session, bool streaming = false)
        {
            if (_config.SharedAiCatalog != null)
            {
                var profiles = GetCachedProfiles();
                var vision = SessionHasImages(session);
                var selected = _registryCache.GetValue(AppIdentity.RegistryRoot, AssistantModelValueName);
                var resolved = SharedAiProfiles.Resolve(_config.SharedAiCatalog,
                    profiles.ToDictionary(p => p.Id, SharedRuntimeState), vision, streaming, selected);
                return profiles.Single(p => p.Id == resolved);
            }
            return ResolveImageProfile(ResolveProfile(), GetCachedProfiles(), SessionHasImages(session),
                _config.Assistant.PreferredVisionModelId, _config.Assistant.VisionFallbackModelId);
        }

        // Capability declarations select the route. Credential presence is a separate dependency gate.
        internal static AssistantModelProfile ResolveImageProfile(AssistantModelProfile selected,
            IEnumerable<AssistantModelProfile> profiles, bool requiresImage, string preferredId, string fallbackId)
        {
            if (selected != null && selected.Enabled && selected.SupportsText && (!requiresImage || selected.SupportsVision))
                return selected;
            if (!requiresImage) throw new InvalidOperationException("MODEL_UNAVAILABLE: selected model cannot accept text.");
            foreach (var id in new[] { preferredId, fallbackId })
            {
                if (string.IsNullOrWhiteSpace(id)) continue;
                var candidate = profiles.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
                if (candidate != null && candidate.Enabled && candidate.SupportsText && candidate.SupportsVision) return candidate;
            }
            throw new InvalidOperationException("VISION_MODEL_UNAVAILABLE: configure an enabled preferred or fallback vision model. No image was sent.");
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
                if (!tool.AllowedInChat) continue;
                if (tool.ManualOnly) continue;
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

        internal JArray GetToolSchemasForTest()
        {
            return GetToolSchemas();
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
            else if (tool.Name == "search_salesforce")
            {
                props["query"] = new JObject { ["type"] = "string", ["description"] = "Search query for Salesforce accounts, contacts, opportunities, tasks, or linked documents." };
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

        private async Task<JObject> ExecuteToolCallRoundAsync(List<LlmToolCall> toolCalls, string traceId, string scopeId = null)
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
                    ScopeId = scopeId,
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

    // Builds and throws a provenance-carrying provider failure. Only safe
    // metadata (provider/model/status/category/sanitized message) escapes;
    // bodies, headers, keys, and prompts never reach diagnostics or the
    // transcript. Provider label falls back to the host-derived label when
    // the profile does not declare one explicitly.
    private static void ThrowProviderFailure(
        AssistantModelProfile profile,
        HttpResponseMessage response,
        string responseBody)
    {
        var provider = profile != null ? AssistantProvenance.ResolveProviderLabel(profile) : null;
        var model = profile != null ? profile.Model : null;
        var status = response != null ? (int?)response.StatusCode : null;

        AssistantErrorInfo classified;

        if (status.HasValue)
        {
            classified = AssistantErrorClassifier.FromProviderFailure(
                provider,
                model,
                status.Value,
                responseBody);
        }
        else
        {
            classified = new AssistantErrorInfo(
                "provider_error",
                "Assistant request failed.",
                provider,
                model,
                null,
                "provider_error");
        }

        throw new AssistantProviderException(
            classified.Provider,
            classified.Model,
            classified.HttpStatus,
            classified.Category,
            classified.Message);
    }

    private async Task<string> SendChatCompletionAsync(string requestBody, string apiKey, AssistantModelProfile profile, Action beforeTransport = null)
    {
        LogChatRequestShape(requestBody, profile);

        using (var request = new HttpRequestMessage(HttpMethod.Post,
            profile.ApiBaseUrl.TrimEnd('/') + "/chat/completions"))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Headers.Add("User-Agent", "BlueBrick-AI-Assistant/1.0");
            request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

            try
            {
                beforeTransport?.Invoke();
                var response = await Client.SendAsync(request).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    ThrowProviderFailure(profile, response, body);
                }

                return body;
            }
            catch (TaskCanceledException)
            {
                throw new AssistantProviderException(
                    profile != null ? profile.Provider : null,
                    profile != null ? profile.Model : null,
                    null,
                    "client_timeout",
                    "Assistant request timed out after 45 seconds.");
            }
        }
    }

    // Best-effort, content-free diagnostic for outbound chat requests. Records
    // only safe shape metadata (provider, model, stream flag, token bound,
    // message count, total character count). Never logs payloads, prompts,
    // headers, or keys. Failures here must never affect the request path.
    private static void LogChatRequestShape(string requestBody, AssistantModelProfile profile)
    {
        try
        {
            var messageCount = 0;
            long inputChars = 0;
            int? maxTokens = null;
            bool? stream = null;

            try
            {
                var parsed = JObject.Parse(requestBody);
                var messages = parsed["messages"] as JArray;
                if (messages != null)
                {
                    messageCount = messages.Count;
                    foreach (var message in messages)
                    {
                        inputChars += CountContentCharacters(message["content"]);
                    }
                }

                if (parsed["max_tokens"] != null && parsed["max_tokens"].Type == JTokenType.Integer)
                {
                    maxTokens = parsed.Value<int>("max_tokens");
                }

                if (parsed["stream"] != null && parsed["stream"].Type == JTokenType.Boolean)
                {
                    stream = parsed.Value<bool>("stream");
                }
            }
            catch
            {
                // Shape extraction is best-effort; the raw body is never logged.
            }

            var line = string.Format(
                "{0} provider={1} model={2} stream={3} maxTokens={4} messageCount={5} inputChars={6} utc={7}",
                "CHAT_REQUEST_SHAPE",
                SanitizeShapeValue(profile != null ? profile.Provider : null),
                SanitizeShapeValue(profile != null ? profile.Model : null),
                stream.HasValue ? stream.Value.ToString().ToLowerInvariant() : "unknown",
                maxTokens.HasValue ? maxTokens.Value.ToString() : "unknown",
                messageCount,
                inputChars,
                DateTime.UtcNow.ToString("o"));

            var dir = Path.Combine(Path.GetTempPath(), "BlueBrick", "AssistantDiagnostics");
            Directory.CreateDirectory(dir);
            lock (ChatRequestShapeLogLock)
            {
                File.AppendAllText(
                    Path.Combine(dir, "chat-request-shape.log"),
                    line + Environment.NewLine,
                    Encoding.UTF8);
            }
        }
        catch
        {
            // Diagnostics are best-effort only.
        }
    }

    private static long CountContentCharacters(JToken content)
    {
        if (content == null) return 0;
        if (content.Type == JTokenType.String) return ((string)content).Length;

        long total = 0;
        var array = content as JArray;
        if (array == null) return 0;
        foreach (var item in array)
        {
            if (item == null) continue;
            var text = item.Type == JTokenType.String
                ? (string)item
                : item.Value<string>("text");
            if (!string.IsNullOrEmpty(text)) total += text.Length;
        }

        return total;
    }

    private static string SanitizeShapeValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "unknown";
        return value.Replace("\r", " ").Replace("\n", " ").Replace(" ", "_");
    }

        private async Task StreamChatCompletionAsync(string requestBody, string apiKey,
            AssistantModelProfile profile, Action<string> onChunk, CancellationToken cancellationToken,
            Action<string, string, string> onToolCall = null, Action beforeTransport = null)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post,
                profile.ApiBaseUrl.TrimEnd('/') + "/chat/completions"))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Headers.Add("User-Agent", "BlueBrick-AI-Assistant/1.0");
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                cancellationToken.ThrowIfCancellationRequested();
                beforeTransport?.Invoke();
                using (var response = await StreamClient.SendAsync(request,
                    HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        var errorBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        ThrowProviderFailure(profile, response, errorBody);
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

        private SharedAI.RuntimeState SharedRuntimeState(AssistantModelProfile profile)
        {
            var configured = ResolveApiKeyInfo(profile).Configured;
            return new SharedAI.RuntimeState { Available = profile.Enabled && configured,
                Reason = !profile.Enabled ? "model_unavailable" : !configured ? "credential_missing" : null };
        }

        private ApiKeyInfo ResolveApiKeyInfo(AssistantModelProfile profile)
        {
            if (string.IsNullOrWhiteSpace(profile.KeyEnvironmentVariable))
                return new ApiKeyInfo(null, "missing_explicit_key_binding");
            var envName = profile.KeyEnvironmentVariable;
            var env = Environment.GetEnvironmentVariable(envName);
            if (!string.IsNullOrWhiteSpace(env))
            {
                return new ApiKeyInfo(env.Trim(), "environment:" + envName);
            }

            // SharedAI binds each provider independently; never inherit another provider's registry credential.
            if (profile.Source == "SharedAI") return new ApiKeyInfo(null, "missing");

            // The legacy registry key belongs only to the configured primary endpoint.
            if (!string.Equals(profile.ApiBaseUrl?.TrimEnd('/'), _config.Assistant.ApiBaseUrl?.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                return new ApiKeyInfo(null, "missing");
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
            if (!string.IsNullOrWhiteSpace(requestedId))
            {
                if (profile == null || !profile.Enabled)
                    throw new InvalidOperationException("MODEL_UNAVAILABLE: requested model is unknown or disabled.");
                return profile;
            }
            if (profile != null && profile.Enabled) return profile;
            return profiles.FirstOrDefault(p => p.Enabled && p.IsDefault)
                ?? profiles.FirstOrDefault(p => p.Enabled)
                ?? throw new InvalidOperationException("MODEL_UNAVAILABLE: no enabled model is configured.");
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
            if (configured != null)
            {
                foreach (var profile in configured)
                {
                    if (profile != null && !string.IsNullOrWhiteSpace(profile.Id) &&
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

    /// <summary>
    /// Resolves the provider label used in provenance receipts. Prefers the
    /// profile's declared provider; otherwise derives a conservative label
    /// from the configured base URL host. No value is fabricated beyond what
    /// existing configuration already states.
    /// </summary>
    internal static class AssistantProvenance
    {
        internal static string ResolveProviderLabel(AssistantModelProfile profile)
        {
            if (profile != null && !string.IsNullOrWhiteSpace(profile.Provider))
            {
                return profile.Provider.Trim();
            }

            var baseUrl = profile?.ApiBaseUrl;
            Uri parsed;
            if (!string.IsNullOrWhiteSpace(baseUrl) && Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out parsed)
                && !string.IsNullOrWhiteSpace(parsed.Host))
            {
                if (parsed.Host.IndexOf("nvidia", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "NVIDIA";
                }

                return parsed.Host;
            }

            return "unknown-provider";
        }
    }
}
