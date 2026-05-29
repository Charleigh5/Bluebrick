using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BlueBrick;
using DocGenerator;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace BlueBrick.Agent
{
    internal class GenerateReviewJobManager
    {
        private readonly ISldWorks _swApp;
        private readonly TelemetryLogger _telemetry;
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        private readonly ConcurrentDictionary<string, GenerateReviewResult> _jobs =
            new ConcurrentDictionary<string, GenerateReviewResult>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, GenerateReviewRequest> _requests =
            new ConcurrentDictionary<string, GenerateReviewRequest>(StringComparer.OrdinalIgnoreCase);
        private readonly string _outboxPath;
        private readonly LiveReviewSessionCoordinator _coordinator;

        internal GenerateReviewJobManager(ISldWorks swApp, TelemetryLogger telemetry)
        {
            _swApp = swApp;
            _telemetry = telemetry;
            var appData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "VIRA", "outbox");
            Directory.CreateDirectory(dir);
            _outboxPath = Path.Combine(dir, "generate-review-jobs.json");
            _coordinator = new LiveReviewSessionCoordinator(
                swApp,
                telemetry,
                new ViraSessionClient(),
                new DrawingCheckpointExporter(swApp),
                new ReviewActionExecutor(swApp, telemetry),
                new SessionOutboxStore(_outboxPath));
        }

        internal GenerateReviewResult Queue(GenerateReviewRequest request, string traceId)
        {
            var jobId = "job_" + Guid.NewGuid().ToString("N");
            var manifest = new GenerationManifest
            {
                JobId = jobId,
                TraceId = string.IsNullOrWhiteSpace(traceId) ? "trace_" + Guid.NewGuid().ToString("N") : traceId,
                ServicePack = request.ServicePack ?? "2024 SP3.x",
                StandardsBaseline = request.StandardsBaseline ?? "ASME",
                StandardsVersion = request.StandardsVersion ?? "Y14.100",
                CustomerRulesetVersion = request.CustomerRulesetVersion ?? "default",
                AnalysisMode = request.AnalysisMode ?? "incremental_checkpoint",
                AutoActionPolicy = request.AutoActionPolicy ?? "supervised_auto_fix",
                IdempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
                    ? "idem_" + Guid.NewGuid().ToString("N")
                    : request.IdempotencyKey
            };
            var result = new GenerateReviewResult
            {
                JobId = jobId,
                Status = "queued",
                GateStatus = GateOutcome.Pending.ToString().ToUpperInvariant(),
                Message = "Live review job queued.",
                Manifest = manifest
            };
            _jobs[jobId] = result;
            _requests[jobId] = request;
            PersistOutbox();
            Task.Run(async () => await ExecuteJob(jobId, request).ConfigureAwait(false));
            return result;
        }

        internal GenerateReviewResult Get(string jobId)
        {
            return _jobs.TryGetValue(jobId, out var result)
                ? result
                : new GenerateReviewResult
                {
                    JobId = jobId,
                    Status = "not_found",
                    GateStatus = GateOutcome.Pending.ToString().ToUpperInvariant(),
                    Message = "Job not found."
                };
        }

        internal GenerateReviewResult Override(string jobId, string reason)
        {
            return SubmitDecision(jobId, new LiveDecisionRequest
            {
                JobId = jobId,
                DecisionType = "OVERRIDE",
                DecisionStatus = "APPROVED",
                Reason = reason ?? "Manual override"
            });
        }

        internal GenerateReviewResult SubmitDecision(string jobId, LiveDecisionRequest request)
        {
            if (!_jobs.TryGetValue(jobId, out var result))
            {
                return Get(jobId);
            }

            try
            {
                if (string.IsNullOrWhiteSpace(result.SessionId) || !_requests.TryGetValue(jobId, out var originalRequest))
                {
                    result.Status = "decision_unavailable";
                    result.Message = "Live review session is not available for decisions.";
                    PersistOutbox();
                    return result;
                }

                var response = _coordinator.SubmitDecision(originalRequest, result.SessionId, request);
                ApplySessionResponse(result, response);
                result.OverrideReason = request.Reason;
                result.Status = "decision_applied";
                result.Message = "Decision applied.";
            }
            catch (Exception ex)
            {
                result.Status = "decision_failed";
                result.Message = "Decision failed: " + ex.Message;
            }

            PersistOutbox();
            return result;
        }

        internal GenerateReviewResult PushCheckpoint(string jobId, LiveCheckpointRequest request)
        {
            if (!_jobs.TryGetValue(jobId, out var result))
            {
                return Get(jobId);
            }

            try
            {
                if (string.IsNullOrWhiteSpace(result.SessionId) || !_requests.TryGetValue(jobId, out var originalRequest))
                {
                    result.Status = "checkpoint_unavailable";
                    result.Message = "Live review session is not available for checkpoint upload.";
                    PersistOutbox();
                    return result;
                }
                var checkpoint = new ExportedCheckpoint
                {
                    SheetId = request.SheetId,
                    ViewId = request.ViewId,
                    PreviewPath = request.PreviewImagePath,
                    ArtifactHash = request.ArtifactHash,
                    MetadataHash = request.MetadataHash,
                    Metadata = request.Metadata ?? new JObject()
                };
                var artifactResponse = _coordinator.UploadCheckpoint(
                    result,
                    originalRequest,
                    checkpoint,
                    request.IdempotencyKey);
                ApplyArtifactResponse(result, artifactResponse);
                result.Status = "checkpoint_uploaded";
                result.Message = "Artifact checkpoint uploaded.";
            }
            catch (Exception ex)
            {
                result.Status = "checkpoint_failed";
                result.Message = "Checkpoint failed: " + ex.Message;
            }

            PersistOutbox();
            return result;
        }

        internal GenerateReviewResult Finalize(string jobId)
        {
            if (!_jobs.TryGetValue(jobId, out var result) || !_requests.TryGetValue(jobId, out var request))
            {
                return Get(jobId);
            }

            try
            {
                if (IsBlocked(result.GateStatus))
                {
                    result.Status = "finalize_blocked";
                    result.Message = "Live review still has blocking findings. Override or fix before finalization.";
                    PersistOutbox();
                    return result;
                }

                result.Status = "finalizing";
                AppendLog(result.Manifest, "Generating final packet after live review.");
                var finalPdf = GeneratePacketPdf(jobId, request, result.Manifest);
                result.Manifest.OutputPdfPath = finalPdf;
                ValidateGeneratedPdf(result, finalPdf, request.ServicePack);
                if (IsBlocked(result.GateStatus))
                {
                    PersistOutbox();
                    return result;
                }

                var submit = _coordinator.SubmitLegacyPacket(result, request, finalPdf);
                result.IntakeId = submit.Value<string>("intake_id");
                result.PacketId = submit.Value<string>("packet_id");
                result.Message = "Final packet submitted to VIRA.";
                FinalizeSession(result, request);
                if (request.PromoteToPdmOnPass)
                {
                    PromoteToPdm(request, result.Manifest);
                }
                result.Status = "done";
            }
            catch (Exception ex)
            {
                HardFail(result, "Finalize failed: " + ex.Message);
            }

            PersistOutbox();
            return result;
        }

        private async Task ExecuteJob(string jobId, GenerateReviewRequest request)
        {
            if (!_jobs.TryGetValue(jobId, out var result))
            {
                return;
            }

            var startedAt = DateTime.UtcNow;
            try
            {
                result.Status = "starting_session";
                AppendLog(result.Manifest, "Starting live review session.");
                var activeDoc = _swApp.ActiveDoc as ModelDoc2;
                if (activeDoc == null)
                {
                    HardFail(result, "No active SOLIDWORKS document.");
                    return;
                }

                result.Manifest.SourceModelPath = activeDoc.GetPathName();
                var sessionResponse = CreateSession(request, result.Manifest);
                ApplySessionResponse(result, sessionResponse);
                result.Status = "reviewing";

                var tempRoot = Path.Combine(Path.GetTempPath(), "VIRA", "live-review", jobId);
                Directory.CreateDirectory(tempRoot);
                var drawingDoc = activeDoc as DrawingDoc;
                var sheetNames = new DrawingCheckpointExporter(_swApp).GetSheetNames(drawingDoc);
                if (sheetNames.Count == 0)
                {
                    sheetNames.Add("active");
                }

                foreach (var sheetName in sheetNames)
                {
                    ExportedCheckpoint checkpoint;
                    try
                    {
                        checkpoint = _coordinator.ExportSheet(tempRoot, sheetName, request, result, result.Manifest);
                    }
                    catch (Exception ex)
                    {
                        result.Manifest.Issues.Add(new GenerationIssue
                        {
                            Code = "PREVIEW_EXPORT_FAILED",
                            Severity = "RED",
                            Stage = "live_review",
                            Message = ex.Message
                        });
                        if (!request.ContinueOnArtifactFail)
                        {
                            HardFail(result, ex.Message);
                            return;
                        }
                        continue;
                    }

                    AppendLog(result.Manifest, "Uploading review checkpoint for " + sheetName + ".");
                    var artifactResponse = _coordinator.UploadCheckpoint(
                        result,
                        request,
                        checkpoint,
                        result.Manifest.IdempotencyKey + ":" + sheetName);
                    ApplyArtifactResponse(result, artifactResponse);
                }

                _coordinator.TryAutoApplyActions(
                    result,
                    request,
                    result.Manifest,
                    artifact => RefreshArtifactCheckpoint(tempRoot, artifact, request, result));

                result.Status = "review_complete";
                result.Message = IsBlocked(result.GateStatus)
                    ? "Live review complete with blocking findings."
                    : "Live review complete. Final packet generation is available.";

                if (request.AutoFinalizePacket && !IsBlocked(result.GateStatus))
                {
                    Finalize(jobId);
                }
            }
            catch (Exception ex)
            {
                HardFail(result, "Unhandled live review failure: " + ex.Message);
            }
            finally
            {
                var elapsed = (DateTime.UtcNow - startedAt).TotalMilliseconds;
                _telemetry.LogEvent(
                    "GENERATE_REVIEW_JOB",
                    "sw/live-review",
                    result.Status == "done" || result.Status == "review_complete",
                    elapsed,
                    new { jobId, sessionId = result.SessionId, status = result.Status, gate = result.GateStatus });
                PersistOutbox();
            }
        }

        private JObject CreateSession(GenerateReviewRequest request, GenerationManifest manifest)
        {
            return _coordinator.StartSession(request, manifest);
        }

        private JObject SubmitArtifactCheckpoint(
            GenerateReviewResult result,
            GenerateReviewRequest request,
            string previewPath,
            string sheetId,
            string viewId,
            string sourceModelPath,
            JObject metadata,
            string idempotencyKey)
        {
            return _coordinator.UploadCheckpoint(
                result,
                request,
                new ExportedCheckpoint
                {
                    SheetId = sheetId,
                    ViewId = viewId,
                    PreviewPath = previewPath,
                    Metadata = metadata ?? new JObject()
                },
                idempotencyKey);
        }

        private JObject SubmitLegacyPacket(GenerateReviewResult result, GenerateReviewRequest request, string pdfPath)
        {
            return _coordinator.SubmitLegacyPacket(result, request, pdfPath);
        }

        private void FinalizeSession(GenerateReviewResult result, GenerateReviewRequest request)
        {
            var payload = new JObject
            {
                ["status"] = "FINALIZED",
                ["packet_id"] = result.PacketId ?? string.Empty,
                ["intake_id"] = result.IntakeId ?? string.Empty,
                ["gate_status"] = result.GateStatus ?? GateOutcome.Pass.ToString().ToUpperInvariant(),
                ["summary"] = new JObject
                {
                    ["artifact_count"] = result.Artifacts.Count,
                    ["blocking_artifact_count"] = result.Artifacts.Count(a => a.GateDecision != null && a.GateDecision.Blocked)
                }
            };
            var response = _coordinator.FinalizeSession(request, result.SessionId, payload);
            ApplySessionResponse(result, response);
        }

        private static JObject BuildArtifactMetadata(GenerateReviewRequest request, GenerateReviewResult result, string sheetName)
        {
            return new JObject
            {
                ["job_id"] = result.JobId ?? string.Empty,
                ["trace_id"] = result.Manifest?.TraceId ?? string.Empty,
                ["customer_id"] = request.CustomerId ?? string.Empty,
                ["packet_name"] = request.PacketName ?? string.Empty,
                ["sheet_name"] = sheetName ?? string.Empty,
                ["service_pack"] = request.ServicePack ?? string.Empty
            };
        }

        private ExportedCheckpoint RefreshArtifactCheckpoint(
            string tempRoot,
            ReviewArtifact artifact,
            GenerateReviewRequest request,
            GenerateReviewResult result)
        {
            if (artifact == null)
            {
                return null;
            }

            var sheetName = string.IsNullOrWhiteSpace(artifact.SheetId) ? artifact.ViewId : artifact.SheetId;
            if (string.IsNullOrWhiteSpace(sheetName))
            {
                return null;
            }

            return _coordinator.ExportSheet(tempRoot, sheetName, request, result, result.Manifest);
        }

        private static List<string> GetSheetNames(DrawingDoc drawingDoc)
        {
            var names = new List<string>();
            if (drawingDoc == null) return names;
            var raw = drawingDoc.GetSheetNames();
            if (raw is string[] direct)
            {
                names.AddRange(direct.Where(s => !string.IsNullOrWhiteSpace(s)));
            }
            else if (raw is object[] boxed)
            {
                names.AddRange(boxed.Select(x => x?.ToString()).Where(s => !string.IsNullOrWhiteSpace(s)));
            }
            return names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static bool TryExportPreview(ModelDoc2 activeDoc, DrawingDoc drawingDoc, string sheetName, string tempRoot, out string previewPath, out string error)
        {
            previewPath = Path.Combine(tempRoot, SanitizeFileName(sheetName) + ".jpg");
            error = null;
            try
            {
                if (drawingDoc != null && !string.Equals(sheetName, "active", StringComparison.OrdinalIgnoreCase))
                {
                    drawingDoc.ActivateSheet(sheetName);
                }

                var extension = activeDoc?.Extension;
                if (extension == null)
                {
                    error = "SOLIDWORKS document extension unavailable.";
                    return false;
                }

                var saveErrors = 0;
                var saveWarnings = 0;
                var ok = extension.SaveAs(
                    previewPath,
                    (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                    null,
                    ref saveErrors,
                    ref saveWarnings);
                if (!ok || !File.Exists(previewPath))
                {
                    error = "Failed to export preview for " + sheetName + ".";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = "Preview export failed for " + sheetName + ": " + ex.Message;
                return false;
            }
        }

        private string GeneratePacketPdf(string jobId, GenerateReviewRequest request, GenerationManifest manifest)
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "VIRA", "final-packet", jobId);
            Directory.CreateDirectory(tempRoot);
            var options = (int)ClsEnums.EnumGenOptions.Pdf + (int)ClsEnums.EnumGenOptions.Silent +
                          (int)ClsEnums.EnumGenOptions.SaveToUser;
            if (string.Equals(request.Mode, "full", StringComparison.OrdinalIgnoreCase))
            {
                options += (int)ClsEnums.EnumGenOptions.All;
                options += (int)ClsEnums.EnumGenOptions.Packet;
            }
            else
            {
                options += (int)ClsEnums.EnumGenOptions.One;
            }

            var generator = new ClsGenerators();
            generator.SetStatus += (sender, args) => { AppendLog(manifest, args.Message); };
            generator.GenerateDoc(_swApp, options, tempRoot);
            var pdf = FindBestPdf(tempRoot, true);
            if (string.IsNullOrWhiteSpace(pdf) || !File.Exists(pdf))
            {
                throw new InvalidOperationException("Generation completed without a packet PDF output.");
            }
            return pdf;
        }

        private void PromoteToPdm(GenerateReviewRequest request, GenerationManifest manifest)
        {
            AppendLog(manifest, "Promoting finalized outputs to PDM.");
            var options = (int)ClsEnums.EnumGenOptions.Pdf + (int)ClsEnums.EnumGenOptions.Silent +
                          (int)ClsEnums.EnumGenOptions.SaveToPdm;
            if (string.Equals(request.Mode, "full", StringComparison.OrdinalIgnoreCase))
            {
                options += (int)ClsEnums.EnumGenOptions.All;
                options += (int)ClsEnums.EnumGenOptions.Packet;
            }
            else
            {
                options += (int)ClsEnums.EnumGenOptions.One;
            }
            var generator = new ClsGenerators();
            generator.SetStatus += (sender, args) => { AppendLog(manifest, args.Message); };
            generator.GenerateDoc(_swApp, options, string.Empty);
        }

        private static string FindBestPdf(string root, bool preferPacket)
        {
            if (!Directory.Exists(root)) return null;
            var pdfs = Directory.GetFiles(root, "*.pdf", SearchOption.AllDirectories).ToList();
            if (pdfs.Count == 0) return null;
            if (!preferPacket) return pdfs[0];
            return pdfs.OrderByDescending(p => new FileInfo(p).Length).FirstOrDefault() ?? pdfs[0];
        }

        private static void AppendLog(GenerationManifest manifest, string message)
        {
            if (manifest == null || string.IsNullOrWhiteSpace(message)) return;
            manifest.StatusLog.Add(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + " " + message);
        }

        private static void HardFail(GenerateReviewResult result, string message)
        {
            result.Status = "failed";
            result.GateStatus = GateOutcome.HardFail.ToString().ToUpperInvariant();
            result.Message = message;
            if (result.Manifest != null)
            {
                result.Manifest.GateStatus = result.GateStatus;
                result.Manifest.Issues.Add(new GenerationIssue
                {
                    Code = "HARD_FAIL",
                    Severity = "RED",
                    Message = message,
                    Stage = "live_review"
                });
            }
        }

        private static bool IsBlocked(string gateStatus)
        {
            var normalized = (gateStatus ?? string.Empty).ToUpperInvariant();
            return normalized == GateOutcome.HardFail.ToString().ToUpperInvariant() ||
                   normalized == GateOutcome.SoftFail.ToString().ToUpperInvariant();
        }

        private static void ValidateGeneratedPdf(GenerateReviewResult result, string pdfPath, string servicePack)
        {
            var info = new FileInfo(pdfPath);
            if (!info.Exists || info.Length == 0)
            {
                HardFail(result, "Generated PDF is missing or empty.");
                return;
            }

            try
            {
                using (var stream = File.OpenRead(pdfPath))
                using (var sha = SHA256.Create())
                {
                    var hash = sha.ComputeHash(stream);
                    var digest = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    result.Manifest.Issues.Add(new GenerationIssue
                    {
                        Code = "PDF_HASH",
                        Severity = "INFO",
                        Stage = "packet_finalize",
                        Message = "Computed PDF SHA-256.",
                        Metadata = JObject.FromObject(new { sha256 = digest, bytes = info.Length })
                    });
                }
            }
            catch (Exception ex)
            {
                result.Manifest.Issues.Add(new GenerationIssue
                {
                    Code = "HASH_WARN",
                    Severity = "YELLOW",
                    Stage = "packet_finalize",
                    Message = "Failed to hash PDF: " + ex.Message
                });
            }

            if (!string.IsNullOrEmpty(servicePack) &&
                servicePack.IndexOf("SP3.1", StringComparison.OrdinalIgnoreCase) >= 0 &&
                result.GateStatus == GateOutcome.Pending.ToString().ToUpperInvariant())
            {
                result.GateStatus = GateOutcome.SoftFail.ToString().ToUpperInvariant();
            }
        }

        private static string BuildApiUrl(GenerateReviewRequest request, string relativePath)
        {
            return (request.ViraBaseUrl ?? "http://localhost:8000").TrimEnd('/') + relativePath;
        }

        internal static void ApplySessionResponse(GenerateReviewResult result, JObject response)
        {
            if (result == null || response == null) return;
            var session = response["session"] as JObject;
            if (session == null) return;
            result.SessionId = session.Value<string>("id") ?? result.SessionId;
            result.SessionUrl = response.Value<string>("status_url") ?? result.SessionUrl;
            result.EventsUrl = response.Value<string>("events_url") ?? result.EventsUrl;
            result.GateStatus = session["gate_decision"]?["status"]?.ToString() ??
                                session.Value<string>("gate_status") ??
                                result.GateStatus;
            result.SessionGate = ParseGateDecision(session["gate_decision"] as JObject);
            result.Artifacts = ParseArtifacts(session["artifacts"] as JArray);
            result.Manifest.SessionId = result.SessionId;
            result.Manifest.GateStatus = result.GateStatus;
        }

        internal static void ApplyArtifactResponse(GenerateReviewResult result, JObject response)
        {
            if (result == null || response == null) return;
            result.SessionUrl = response.Value<string>("status_url") ?? result.SessionUrl;
            result.EventsUrl = response.Value<string>("events_url") ?? result.EventsUrl;
            result.GateStatus = response["session_gate"]?["status"]?.ToString() ?? result.GateStatus;
            result.SessionGate = ParseGateDecision(response["session_gate"] as JObject);
            var artifact = ParseArtifact(response["artifact"] as JObject);
            if (artifact == null) return;
            result.Artifacts.RemoveAll(a => string.Equals(a.Id, artifact.Id, StringComparison.OrdinalIgnoreCase));
            result.Artifacts.Add(artifact);
            result.Manifest.GateStatus = result.GateStatus;
        }

        private static GateDecision ParseGateDecision(JObject value)
        {
            return value == null ? null : value.ToObject<GateDecision>();
        }

        private static List<ReviewArtifact> ParseArtifacts(JArray array)
        {
            return array == null ? new List<ReviewArtifact>() : array.ToObject<List<ReviewArtifact>>() ?? new List<ReviewArtifact>();
        }

        private static ReviewArtifact ParseArtifact(JObject value)
        {
            return value == null ? null : value.ToObject<ReviewArtifact>();
        }

        private static JObject PostJson(GenerateReviewRequest request, string url, JObject payload)
        {
            using (var http = new HttpRequestMessage(HttpMethod.Post, url))
            {
                http.Content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json");
                ApplyAuth(request, http);
                return Send(http);
            }
        }

        private static JObject SendMultipart(GenerateReviewRequest request, string url, MultipartFormDataContent content)
        {
            using (var http = new HttpRequestMessage(HttpMethod.Post, url))
            {
                http.Content = content;
                ApplyAuth(request, http);
                return Send(http);
            }
        }

        private static void ApplyAuth(GenerateReviewRequest request, HttpRequestMessage http)
        {
            if (!string.IsNullOrWhiteSpace(request.ViraAccessToken))
            {
                http.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.ViraAccessToken);
            }
        }

        private static JObject Send(HttpRequestMessage request)
        {
            var response = Http.SendAsync(request).GetAwaiter().GetResult();
            var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException("VIRA request failed (" + (int)response.StatusCode + "): " + body);
            }
            return JObject.Parse(body);
        }

        private static string SanitizeFileName(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var chars = (value ?? "sheet").Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
            return new string(chars);
        }

        private void PersistOutbox()
        {
            try
            {
                _coordinator.SaveOutbox(_jobs, _requests);
            }
            catch
            {
            }
        }
    }
}
