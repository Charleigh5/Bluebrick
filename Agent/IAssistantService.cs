using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BlueBrick.Agent
{
    internal interface IAssistantService
    {
        Task<AssistantSession> CreateSessionAsync();
        Task<AssistantSessionResponse> SendMessageAsync(string sessionId, string message, IList<string> attachmentPaths, string scopeId = null);
        Task SendMessageStreamAsync(string sessionId, string message, IList<string> attachmentPaths, Action<AssistantStreamChunk> onChunk, CancellationToken cancellationToken, string scopeId = null);
        Task<string> CaptureScreenshotAsync(string sessionId);
        Task<AssistantScreenshotArtifact> CaptureScreenshotArtifactAsync(string sessionId);
        Task<AssistantScreenshotArtifact> CaptureScreenshotArtifactAsync(AssistantScreenshotCaptureRequest request);
        Task<AssistantScreenshotAnalysisResult> AnalyzeScreenshotAsync(AssistantScreenshotAnalysisRequest request);
        Task<AssistantSession> GetSessionAsync(string sessionId);
        Task<AssistantPreviewStatus> GetStatusAsync();
        Task<AssistantPreviewStatus> SetModeAsync(string mode);
        Task<IList<AssistantModelProfile>> GetModelsAsync();
        Task<AssistantPreviewStatus> SetModelAsync(string modelId);
        Task<AssistantConnectionTestResult> TestConnectionAsync();
    }
}
