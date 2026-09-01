using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace BlueBrick.Agent
{
    internal sealed class PreviewActionExecutor
    {
        private readonly IAssistantService _assistantService;
        private readonly GenerateReviewJobManager _generateReviewJobs;
        private readonly ChatGptSessionStore _sessions;
        private readonly RelayTunnelClient _relayTunnel;

        internal PreviewActionExecutor(
            IAssistantService assistantService,
            GenerateReviewJobManager generateReviewJobs,
            ChatGptSessionStore sessions,
            RelayTunnelClient relayTunnel)
        {
            _assistantService = assistantService;
            _generateReviewJobs = generateReviewJobs;
            _sessions = sessions;
            _relayTunnel = relayTunnel;
        }

        internal async Task<PreviewActionResult> ExecuteAsync(PreviewSession session, PreviewActionRequest request, string traceId)
        {
            var actionName = (request.ActionName ?? string.Empty).Trim().ToLowerInvariant();

            var boundary = ExecutionBoundaryPolicy.EvaluatePreviewAction(actionName);
            if (!boundary.Allowed)
            {
                return Build(session?.SessionId, request?.ActionName, "denied", boundary.Message + " [" + boundary.Code + "]", traceId);
            }

            try
            {
                switch (actionName)
                {
                    case "get_preview_status":
                    {
                        var status = await _assistantService.GetStatusAsync().ConfigureAwait(false);
                        return Build(session.SessionId, request.ActionName, "ok", "Preview status retrieved.", traceId,
                            new Dictionary<string, string>
                            {
                                ["assistantMode"] = status.AssistantMode,
                                ["relayConnected"] = (_relayTunnel?.State?.Connected ?? false).ToString()
                            });
                    }
                    case "get_active_context":
                    {
                        return Build(session.SessionId, request.ActionName, "ok", "Active document context retrieved.", traceId,
                            new Dictionary<string, string>
                            {
                                ["title"] = session.ActiveDocumentTitle ?? string.Empty,
                                ["path"] = session.ActiveDocumentPath ?? string.Empty
                            });
                    }
                    case "search_local_vault":
                    {
                        var query = request.Parameters.ContainsKey("query") ? request.Parameters["query"] : string.Empty;
                        var result = Vault.VaultWorkspaceFactory.Current.Search(query, 10);
                        return Build(session.SessionId, request.ActionName, "ok", "Local vault search completed.", traceId,
                            new Dictionary<string, string> { ["count"] = result.Count.ToString() });
                    }
                    case "get_review_findings":
                    {
                        return Build(session.SessionId, request.ActionName, "ok", "Preview findings retrieved.", traceId,
                            new Dictionary<string, string> { ["count"] = session.Findings.Count.ToString() });
                    }
                    case "capture_preview_screenshot":
                    {
                        var path = await _assistantService.CaptureScreenshotAsync(session.SessionId).ConfigureAwait(false);
                        session.LastScreenshotPath = path;
                        _sessions.Save(session);
                        return Build(session.SessionId, request.ActionName, "ok", "Preview screenshot captured.", traceId,
                            new Dictionary<string, string> { ["path"] = path ?? string.Empty });
                    }
                    case "open_output_folder":
                    {
                        Directory.CreateDirectory(AppIdentity.DefaultWorkingFolder);
                        Process.Start("explorer.exe", AppIdentity.DefaultWorkingFolder);
                        return Build(session.SessionId, request.ActionName, "ok", "Working folder opened.", traceId,
                            new Dictionary<string, string> { ["path"] = AppIdentity.DefaultWorkingFolder });
                    }
                    case "get_session_history":
                    {
                        return Build(session.SessionId, request.ActionName, "ok", "Preview session history retrieved.", traceId,
                            new Dictionary<string, string> { ["count"] = session.History.Count.ToString() });
                    }
                    case "reindex_local_vault":
                    {
                        Vault.VaultWorkspaceFactory.Current.ReindexSampleFiles();
                        return Build(session.SessionId, request.ActionName, "ok", "Local vault reindexed.", traceId);
                    }
                    case "reset_local_vault":
                    {
                        Vault.VaultWorkspaceFactory.Current.Reset();
                        return Build(session.SessionId, request.ActionName, "ok", "Local vault reset.", traceId);
                    }
                    case "run_local_review":
                    {
                        if (!request.Parameters.ContainsKey("customerId") || !request.Parameters.ContainsKey("viraAccessToken"))
                        {
                            return Build(session.SessionId, request.ActionName, "denied",
                                "run_local_review requires customerId and viraAccessToken.", traceId);
                        }

                        var reviewRequest = new GenerateReviewRequest
                        {
                            CustomerId = request.Parameters["customerId"],
                            ViraAccessToken = request.Parameters["viraAccessToken"]
                        };
                        var queueResult = _generateReviewJobs.Queue(reviewRequest, traceId);
                        return Build(session.SessionId, request.ActionName, "ok", "Local review queued.", traceId,
                            new Dictionary<string, string> { ["jobId"] = queueResult.JobId ?? string.Empty });
                    }
                    default:
                        return Build(session.SessionId, request.ActionName, "denied",
                            "Action is unsupported or blocked in preview mode.", traceId);
                }
            }
            catch (Exception ex)
            {
                return Build(session.SessionId, request.ActionName, "error", ex.Message, traceId);
            }
        }

        private static PreviewActionResult Build(string sessionId, string actionName, string status, string message, string traceId,
            Dictionary<string, string> data = null)
        {
            return new PreviewActionResult
            {
                SessionId = sessionId,
                ActionName = actionName,
                Status = status,
                Message = message,
                TraceId = traceId,
                CreatedUtc = DateTime.UtcNow,
                Data = data ?? new Dictionary<string, string>()
            };
        }
    }
}
