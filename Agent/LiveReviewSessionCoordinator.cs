using System;
using System.Collections.Concurrent;
using System.Linq;
using Newtonsoft.Json.Linq;
using SolidWorks.Interop.sldworks;

namespace BlueBrick.Agent
{
    internal sealed class LiveReviewSessionCoordinator
    {
        private readonly ISldWorks _swApp;
        private readonly TelemetryLogger _telemetry;
        private readonly ViraSessionClient _sessionClient;
        private readonly DrawingCheckpointExporter _exporter;
        private readonly ReviewActionExecutor _actionExecutor;
        private readonly SessionOutboxStore _outboxStore;

        internal LiveReviewSessionCoordinator(
            ISldWorks swApp,
            TelemetryLogger telemetry,
            ViraSessionClient sessionClient,
            DrawingCheckpointExporter exporter,
            ReviewActionExecutor actionExecutor,
            SessionOutboxStore outboxStore)
        {
            _swApp = swApp;
            _telemetry = telemetry;
            _sessionClient = sessionClient;
            _exporter = exporter;
            _actionExecutor = actionExecutor;
            _outboxStore = outboxStore;
        }

        internal void SaveOutbox(
            ConcurrentDictionary<string, GenerateReviewResult> jobs,
            ConcurrentDictionary<string, GenerateReviewRequest> requests)
        {
            _outboxStore.Save(jobs, requests);
        }

        internal JObject StartSession(GenerateReviewRequest request, GenerationManifest manifest)
        {
            return _sessionClient.CreateSession(request, manifest);
        }

        internal ExportedCheckpoint ExportSheet(
            string tempRoot,
            string sheetName,
            GenerateReviewRequest request,
            GenerateReviewResult result,
            GenerationManifest manifest)
        {
            return _exporter.ExportSheet(tempRoot, sheetName, request, result, manifest);
        }

        internal JObject UploadCheckpoint(
            GenerateReviewResult result,
            GenerateReviewRequest request,
            ExportedCheckpoint checkpoint,
            string idempotencyKey)
        {
            return _sessionClient.SubmitArtifactCheckpoint(result, request, checkpoint, idempotencyKey);
        }

        internal JObject SubmitDecision(GenerateReviewRequest request, string sessionId, LiveDecisionRequest decision)
        {
            return _sessionClient.SubmitDecision(request, sessionId, decision);
        }

        internal JObject FinalizeSession(GenerateReviewRequest request, string sessionId, JObject payload)
        {
            return _sessionClient.FinalizeSession(request, sessionId, payload);
        }

        internal JObject SubmitLegacyPacket(GenerateReviewResult result, GenerateReviewRequest request, string pdfPath)
        {
            return _sessionClient.SubmitLegacyPacket(result, request, pdfPath);
        }

        internal void TryAutoApplyActions(
            GenerateReviewResult result,
            GenerateReviewRequest request,
            GenerationManifest manifest,
            Func<ReviewArtifact, ExportedCheckpoint> refreshCheckpoint)
        {
            if (!request.AutoApplyLowRiskActions)
            {
                return;
            }

            foreach (var artifact in result.Artifacts.ToList())
            {
                foreach (var finding in artifact.Findings.ToList())
                {
                    var execution = _actionExecutor.TryAutoApply(artifact, finding, manifest);
                    if (!execution.Applied)
                    {
                        continue;
                    }

                    artifact.AutoActionOutcome = execution.Outcome;
                    var decisionPayload = new LiveDecisionRequest
                    {
                        ArtifactId = artifact.Id,
                        FindingId = finding.Id,
                        ActionId = finding.SuggestedAction?.Id,
                        DecisionType = "APPLY_ACTION",
                        DecisionStatus = "APPROVED",
                        Reason = "BlueBrick auto-applied deterministic low-risk action.",
                        Payload = new JObject
                        {
                            ["auto"] = true,
                            ["outcome"] = execution.Outcome
                        }
                    };
                    var response = SubmitDecision(request, result.SessionId, decisionPayload);
                    GenerateReviewJobManager.ApplySessionResponse(result, response);

                    if (execution.RequiresCheckpointRefresh && refreshCheckpoint != null)
                    {
                        var refreshed = refreshCheckpoint(artifact);
                        if (refreshed == null)
                        {
                            continue;
                        }

                        var checkpointResponse = UploadCheckpoint(
                            result,
                            request,
                            refreshed,
                            artifact.IdempotencyKey ?? refreshed.MetadataHash ?? Guid.NewGuid().ToString("N"));
                        GenerateReviewJobManager.ApplyArtifactResponse(result, checkpointResponse);
                    }
                }
            }
        }
    }
}
