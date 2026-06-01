using System;
using System.Collections.Generic;

namespace BlueBrick.Agent
{
    internal class AssistantSession
    {
        public string SessionId { get; set; }
        public DateTime CreatedUtc { get; set; }
        public List<AssistantMessage> Messages { get; set; } = new List<AssistantMessage>();
    }

    internal class AssistantMessage
    {
        public string Role { get; set; }
        public string Text { get; set; }
        public List<string> AttachmentPaths { get; set; } = new List<string>();
        public DateTime CreatedUtc { get; set; }
    }

    internal class AssistantScreenshotArtifact
    {
        public string ArtifactId { get; set; }
        public string SessionId { get; set; }
        public string Path { get; set; }
        public DateTime CapturedUtc { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string SourceWindowTitle { get; set; }
        public string CaptureTarget { get; set; }
        public string CaptureSource { get; set; }
        public string SolidWorksDocumentTitle { get; set; }
        public string SolidWorksDocumentPathHash { get; set; }
        public bool RedactionApplied { get; set; }
        public bool SentToModel { get; set; }
        public string RetentionPolicy { get; set; }
        public string ModelProfileId { get; set; }
        public List<AssistantScreenshotAnnotation> Annotations { get; set; } = new List<AssistantScreenshotAnnotation>();
        public List<AssistantExtractedContact> ExtractedContacts { get; set; } = new List<AssistantExtractedContact>();
    }

    internal class AssistantScreenshotCaptureRequest
    {
        public string SessionId { get; set; }
        public string CaptureTarget { get; set; } = "solidworks_or_foreground";
    }

    internal class AssistantScreenshotAnnotation
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public string Severity { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string Source { get; set; }
    }

    internal class AssistantExtractedContact
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Company { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string OpportunityId { get; set; }
        public double Confidence { get; set; }
        public string SourceAnnotationId { get; set; }
        public string ReviewStatus { get; set; } = "pending";
        public string ReviewNote { get; set; }
    }

    internal class AssistantScreenshotAnalysisRequest
    {
        public string SessionId { get; set; }
        public string Path { get; set; }
        public string SourceWindowTitle { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string HintText { get; set; }
        public string ModelProfileId { get; set; }
        public bool CloudSendApproved { get; set; }
        public AssistantScreenshotArtifact Artifact { get; set; }
    }

    internal class AssistantScreenshotAnalysisResult
    {
        public string Status { get; set; }
        public string Message { get; set; }
        public bool MockMode { get; set; }
        public AssistantScreenshotArtifact Artifact { get; set; }
    }

    internal class AssistantSessionResponse
    {
        public string SessionId { get; set; }
        public AssistantMessage Message { get; set; }
        public string Error { get; set; }
        public string ErrorCode { get; set; }
        public bool AssistantAvailable { get; set; }
    }

    internal class AssistantPreviewStatus
    {
        public string AssistantMode { get; set; }
        public bool Configured { get; set; }
        public bool KeyConfigured { get; set; }
        public string KeySource { get; set; }
        public string ApiBaseUrl { get; set; }
        public string Model { get; set; }
        public string Detail { get; set; }
        public string AddinMode { get; set; }
        public string VaultMode { get; set; }
        public string LocalVaultRoot { get; set; }
        public string SampleSeedRoot { get; set; }
        public string WorkingFolder { get; set; }
        public string BridgeUrl { get; set; }
        public int BridgePort { get; set; }
        public bool LocalVaultExists { get; set; }
        public bool SampleSeedExists { get; set; }
        public bool AgentTokenConfigured { get; set; }
        public bool EnableUploads { get; set; }
        public bool RequireExplicitUploadConsent { get; set; }
        public bool RelayConfigured { get; set; }
        public bool RelayConnected { get; set; }
        public string RelayBaseUrl { get; set; }
        public string ChatWorkspaceUrl { get; set; }
        public AssistantModelCapabilitySummary ActiveModel { get; set; }
        public AssistantToolAvailabilitySummary ToolAvailability { get; set; }
        public string[] Checklist { get; set; } = Array.Empty<string>();
    }

    internal class AssistantModelCapabilitySummary
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Provider { get; set; }
        public string ProviderKind { get; set; }
        public string BaseUrlAlias { get; set; }
        public bool SupportsVision { get; set; }
        public bool SupportsStreaming { get; set; }
        public bool SupportsTools { get; set; }
        public bool SupportsJsonMode { get; set; }
        public bool Enabled { get; set; }
        public string Source { get; set; }

        internal static AssistantModelCapabilitySummary FromProfile(AssistantModelProfile profile)
        {
            if (profile == null) return null;
            return new AssistantModelCapabilitySummary
            {
                Id = profile.Id,
                Name = profile.Name,
                Provider = profile.Provider,
                ProviderKind = profile.ProviderKind,
                BaseUrlAlias = profile.BaseUrlAlias,
                SupportsVision = profile.SupportsVision,
                SupportsStreaming = profile.SupportsStreaming,
                SupportsTools = profile.SupportsTools,
                SupportsJsonMode = profile.SupportsJsonMode,
                Enabled = profile.Enabled,
                Source = profile.Source
            };
        }
    }

    internal class AssistantToolAvailabilitySummary
    {
        public int TotalTools { get; set; }
        public int EnabledTools { get; set; }
        public int DisabledTools { get; set; }
        public int SearchTools { get; set; }
        public int EnabledSearchTools { get; set; }
        public string[] EnabledToolNames { get; set; } = Array.Empty<string>();
        public string[] DisabledToolNames { get; set; } = Array.Empty<string>();

        internal static AssistantToolAvailabilitySummary FromCatalog(IEnumerable<AssistantToolDescriptor> catalog)
        {
            var enabled = new List<string>();
            var disabled = new List<string>();
            var total = 0;
            var search = 0;
            var enabledSearch = 0;

            if (catalog != null)
            {
                foreach (var tool in catalog)
                {
                    if (tool == null) continue;
                    total++;
                    var name = tool.Name ?? string.Empty;
                    var isSearch = name.StartsWith("search_", StringComparison.OrdinalIgnoreCase);
                    if (isSearch) search++;

                    if (tool.Enabled)
                    {
                        enabled.Add(name);
                        if (isSearch) enabledSearch++;
                    }
                    else
                    {
                        disabled.Add(name);
                    }
                }
            }

            return new AssistantToolAvailabilitySummary
            {
                TotalTools = total,
                EnabledTools = enabled.Count,
                DisabledTools = disabled.Count,
                SearchTools = search,
                EnabledSearchTools = enabledSearch,
                EnabledToolNames = enabled.ToArray(),
                DisabledToolNames = disabled.ToArray()
            };
        }
    }

    internal class AssistantModelProfile
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Provider { get; set; }
        public string ApiBaseUrl { get; set; }
        public string Model { get; set; }
        public string KeyEnvironmentVariable { get; set; }
        public bool IsDefault { get; set; }
        public string ProviderKind { get; set; }
        public string BaseUrlAlias { get; set; }
        public bool SupportsVision { get; set; }
        public bool SupportsStreaming { get; set; }
        public bool SupportsTools { get; set; }
        public bool SupportsJsonMode { get; set; }
        public int? ContextLimit { get; set; }
        public string SecretRef { get; set; }
        public bool Enabled { get; set; } = true;
        public string Source { get; set; }
        public DateTime? LastVerifiedAt { get; set; }
    }

    internal class AssistantToolDescriptor
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public bool ReadOnly { get; set; }
        public bool RequiresConfirmation { get; set; }
        public bool Enabled { get; set; }
        public string UnavailableReason { get; set; }
        public string RiskLevel { get; set; }
        public bool AuditRequired { get; set; }
    }

    internal class AssistantToolRequest
    {
        public string ToolName { get; set; }
        public string Query { get; set; }
        public int Limit { get; set; }
        public AssistantToolAuthorization Authorization { get; set; } = AssistantToolAuthorization.None();
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
    }

    internal class AssistantToolResult
    {
        public string ToolName { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
        public bool ReadOnly { get; set; }
        public string TraceId { get; set; }
        public AssistantToolExecutionReceipt Receipt { get; set; }
        public List<AssistantToolResultItem> Items { get; set; } = new List<AssistantToolResultItem>();
    }

    internal class AssistantToolResultItem
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string Path { get; set; }
        public string Source { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    internal class AssistantIntegrationDescriptor
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Status { get; set; }
        public string Summary { get; set; }
        public bool ReadOnlyFirst { get; set; }
        public bool RequiresOAuth { get; set; }
        public bool RequiresSecrets { get; set; }
        public string[] RecommendedScopes { get; set; } = Array.Empty<string>();
        public string[] FirstObjects { get; set; } = Array.Empty<string>();
        public string[] Blockers { get; set; } = Array.Empty<string>();
        public string[] NextSteps { get; set; } = Array.Empty<string>();
    }

    internal class AssistantDocumentDescriptor
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Purpose { get; set; }
        public string SourceSubsystem { get; set; }
        public string[] OutputFormats { get; set; } = Array.Empty<string>();
        public bool Implemented { get; set; }
        public bool RequiresSolidWorks { get; set; }
        public bool RequiresPdmApproval { get; set; }
        public string[] AssistantUses { get; set; } = Array.Empty<string>();
    }

    internal class AssistantConnectionTestResult
    {
        public bool Success { get; set; }
        public string Mode { get; set; }
        public bool Configured { get; set; }
        public string KeySource { get; set; }
        public double LatencyMs { get; set; }
        public string Message { get; set; }
    }

    internal class PreviewSession
    {
        public string SessionId { get; set; }
        public DateTime CreatedUtc { get; set; }
        public string ActiveDocumentPath { get; set; }
        public string ActiveDocumentTitle { get; set; }
        public string LocalVaultRoot { get; set; }
        public string WorkingFolder { get; set; }
        public string LastScreenshotPath { get; set; }
        public string HandoffUrl { get; set; }
        public RelayTunnelState RelayState { get; set; } = new RelayTunnelState();
        public List<PreviewFinding> Findings { get; set; } = new List<PreviewFinding>();
        public List<PreviewActionResult> History { get; set; } = new List<PreviewActionResult>();
        public List<PreviewConfirmationRequest> PendingConfirmations { get; set; } = new List<PreviewConfirmationRequest>();
        public List<string> AllowedActions { get; set; } = new List<string>();
    }

    internal class PreviewFinding
    {
        public string Id { get; set; }
        public string Severity { get; set; }
        public string Summary { get; set; }
        public string Details { get; set; }
    }

    internal class PreviewActionRequest
    {
        public string SessionId { get; set; }
        public string ActionName { get; set; }
        public string RequestedBy { get; set; }
        public bool RequiresConfirmation { get; set; }
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
    }

    internal class PreviewActionResult
    {
        public string SessionId { get; set; }
        public string ActionName { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
        public string TraceId { get; set; }
        public DateTime CreatedUtc { get; set; }
        public Dictionary<string, string> Data { get; set; } = new Dictionary<string, string>();
    }

    internal class PreviewConfirmationRequest
    {
        public string SessionId { get; set; }
        public string ConfirmationId { get; set; }
        public string ActionName { get; set; }
        public bool Approved { get; set; }
        public string Reason { get; set; }
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
    }

    internal class RelayTunnelState
    {
        public string DeviceId { get; set; }
        public bool Enabled { get; set; }
        public bool Connected { get; set; }
        public DateTime LastRegisterUtc { get; set; }
        public DateTime LastHeartbeatUtc { get; set; }
        public string LastError { get; set; }
        public string BaseUrl { get; set; }
    }

    internal class AssistantStreamChunk
    {
        public string Type { get; set; }
        public string Text { get; set; }
        public string ToolName { get; set; }
        public string ToolCallId { get; set; }
        public string ToolArguments { get; set; }
        public string ToolResultContent { get; set; }
        public string ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
        public bool Done { get; set; }

        internal static AssistantStreamChunk TextDelta(string text)
        {
            return new AssistantStreamChunk { Type = "text_delta", Text = text ?? string.Empty };
        }

        internal static AssistantStreamChunk ToolCall(string name, string id, string arguments)
        {
            return new AssistantStreamChunk { Type = "tool_call", ToolName = name, ToolCallId = id, ToolArguments = arguments ?? string.Empty };
        }

        internal static AssistantStreamChunk ToolResult(string toolCallId, string content)
        {
            return new AssistantStreamChunk { Type = "tool_result", ToolCallId = toolCallId, ToolResultContent = content ?? string.Empty };
        }

        internal static AssistantStreamChunk Error(string code, string message)
        {
            return new AssistantStreamChunk { Type = "error", ErrorCode = code, ErrorMessage = message };
        }

        internal static AssistantStreamChunk Complete()
        {
            return new AssistantStreamChunk { Type = "done", Done = true };
        }
    }

    internal class ChatGptHandoffPayload
    {
        public string SessionId { get; set; }
        public string HandoffUrl { get; set; }
        public string ChatWorkspaceUrl { get; set; }
        public string RelayUrl { get; set; }
        public bool RelayConfigured { get; set; }
        public string Message { get; set; }
    }
}
