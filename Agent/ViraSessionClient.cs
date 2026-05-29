using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BlueBrick.Agent
{
    internal sealed class ViraSessionClient
    {
        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };

        internal JObject CreateSession(GenerateReviewRequest request, GenerationManifest manifest)
        {
            var payload = new JObject
            {
                ["customer_id"] = request.CustomerId ?? string.Empty,
                ["packet_name"] = request.PacketName ?? string.Empty,
                ["server_path"] = request.ServerPath ?? string.Empty,
                ["source_model_path"] = manifest.SourceModelPath ?? string.Empty,
                ["trace_id"] = manifest.TraceId ?? string.Empty,
                ["idempotency_key"] = manifest.IdempotencyKey ?? string.Empty,
                ["rule_parameter_overrides"] = request.RuleParameterOverrides ?? new JObject(),
                ["manifest"] = JObject.FromObject(manifest),
                ["standards_baseline"] = request.StandardsBaseline ?? "ASME",
                ["standards_version"] = request.StandardsVersion ?? "Y14.100",
                ["customer_ruleset_version"] = request.CustomerRulesetVersion ?? "default",
                ["analysis_mode"] = request.AnalysisMode ?? "incremental_checkpoint",
                ["auto_action_policy"] = request.AutoActionPolicy ?? "supervised_auto_fix"
            };
            return PostJson(request, BuildApiUrl(request, "/api/v1/integrations/bluebrick/sessions"), payload);
        }

        internal JObject SubmitArtifactCheckpoint(
            GenerateReviewResult result,
            GenerateReviewRequest request,
            ExportedCheckpoint checkpoint,
            string idempotencyKey)
        {
            if (!File.Exists(checkpoint.PreviewPath))
            {
                throw new FileNotFoundException("Preview image not found.", checkpoint.PreviewPath);
            }

            var url = BuildApiUrl(
                request,
                "/api/v1/integrations/bluebrick/sessions/" + result.SessionId + "/artifacts");
            using (var multipart = new MultipartFormDataContent())
            using (var fileContent = new StreamContent(File.OpenRead(checkpoint.PreviewPath)))
            {
                var mediaType = string.Equals(Path.GetExtension(checkpoint.PreviewPath), ".jpg", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(Path.GetExtension(checkpoint.PreviewPath), ".jpeg", StringComparison.OrdinalIgnoreCase)
                    ? "image/jpeg"
                    : "image/png";
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
                multipart.Add(fileContent, "preview_image", Path.GetFileName(checkpoint.PreviewPath));
                var artifactPayload = new JObject
                {
                    ["idempotency_key"] = idempotencyKey,
                    ["checkpoint_type"] = "sheet_preview",
                    ["source_model_path"] = checkpoint.Metadata.Value<string>("source_model_path") ?? string.Empty,
                    ["sheet_id"] = checkpoint.SheetId ?? string.Empty,
                    ["view_id"] = checkpoint.ViewId ?? string.Empty,
                    ["artifact_hash"] = checkpoint.ArtifactHash ?? string.Empty,
                    ["metadata_hash"] = checkpoint.MetadataHash ?? string.Empty,
                    ["metadata"] = checkpoint.Metadata ?? new JObject()
                };
                multipart.Add(new StringContent(artifactPayload.ToString(Formatting.None), Encoding.UTF8), "artifact_json");
                return SendMultipart(request, url, multipart);
            }
        }

        internal JObject SubmitLegacyPacket(GenerateReviewResult result, GenerateReviewRequest request, string pdfPath)
        {
            var url = BuildApiUrl(request, "/api/v1/integrations/bluebrick/submit");
            using (var multi = new MultipartFormDataContent())
            using (var fileContent = new StreamContent(File.OpenRead(pdfPath)))
            {
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                multi.Add(fileContent, "packet_file", Path.GetFileName(pdfPath));
                multi.Add(new StringContent(JsonConvert.SerializeObject(result.Manifest), Encoding.UTF8), "manifest_json");
                multi.Add(new StringContent(request.CustomerId ?? string.Empty, Encoding.UTF8), "customer_id");
                multi.Add(new StringContent(request.PacketName ?? string.Empty, Encoding.UTF8), "packet_name");
                multi.Add(new StringContent(request.ServerPath ?? string.Empty, Encoding.UTF8), "server_path");
                multi.Add(new StringContent(result.Manifest.SourceModelPath ?? string.Empty, Encoding.UTF8), "source_model_path");
                multi.Add(new StringContent(result.SessionId ?? string.Empty, Encoding.UTF8), "session_id");
                multi.Add(new StringContent(
                    request.RuleParameterOverrides != null
                        ? request.RuleParameterOverrides.ToString(Formatting.None)
                        : "{}",
                    Encoding.UTF8), "rule_parameter_overrides_json");
                multi.Add(new StringContent(result.Manifest.IdempotencyKey ?? string.Empty, Encoding.UTF8), "idempotency_key");
                multi.Add(new StringContent(result.Manifest.TraceId ?? string.Empty, Encoding.UTF8), "trace_id");
                return SendMultipart(request, url, multi);
            }
        }

        internal JObject SubmitDecision(GenerateReviewRequest request, string sessionId, LiveDecisionRequest decision)
        {
            var payload = new JObject
            {
                ["artifact_id"] = decision.ArtifactId,
                ["finding_id"] = decision.FindingId,
                ["decision_type"] = decision.DecisionType ?? "OVERRIDE",
                ["decision_status"] = decision.DecisionStatus ?? "APPROVED",
                ["reason"] = decision.Reason,
                ["action_id"] = decision.ActionId,
                ["payload"] = decision.Payload ?? new JObject()
            };
            return PostJson(
                request,
                BuildApiUrl(request, "/api/v1/integrations/bluebrick/sessions/" + sessionId + "/decisions"),
                payload);
        }

        internal JObject FinalizeSession(GenerateReviewRequest request, string sessionId, JObject payload)
        {
            return PostJson(
                request,
                BuildApiUrl(request, "/api/v1/integrations/bluebrick/sessions/" + sessionId + "/finalize"),
                payload);
        }

        internal async Task StreamEventsAsync(
            GenerateReviewRequest request,
            string eventsUrl,
            Action<string, JObject> onEvent,
            CancellationToken cancellationToken)
        {
            using (var message = new HttpRequestMessage(HttpMethod.Get, eventsUrl))
            {
                message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
                if (!string.IsNullOrWhiteSpace(request.ViraAccessToken))
                {
                    message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.ViraAccessToken);
                }

                using (var response = await Http.SendAsync(
                           message,
                           HttpCompletionOption.ResponseHeadersRead,
                           cancellationToken).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var reader = new StreamReader(stream))
                    {
                        string eventType = null;
                        var data = new StringBuilder();
                        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
                        {
                            var line = await reader.ReadLineAsync().ConfigureAwait(false);
                            if (string.IsNullOrWhiteSpace(line))
                            {
                                if (data.Length > 0 && onEvent != null)
                                {
                                    try
                                    {
                                        onEvent(eventType ?? "message", JObject.Parse(data.ToString()));
                                    }
                                    catch
                                    {
                                        onEvent(eventType ?? "message", new JObject { ["raw"] = data.ToString() });
                                    }
                                }
                                eventType = null;
                                data.Clear();
                                continue;
                            }

                            if (line.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
                            {
                                eventType = line.Substring(6).Trim();
                                continue;
                            }

                            if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                            {
                                if (data.Length > 0)
                                {
                                    data.AppendLine();
                                }
                                data.Append(line.Substring(5).Trim());
                            }
                        }
                    }
                }
            }
        }

        private static JObject PostJson(GenerateReviewRequest request, string url, JObject payload)
        {
            using (var message = new HttpRequestMessage(HttpMethod.Post, url))
            {
                if (!string.IsNullOrWhiteSpace(request.ViraAccessToken))
                {
                    message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.ViraAccessToken);
                }
                message.Content = new StringContent(
                    (payload ?? new JObject()).ToString(Formatting.None),
                    Encoding.UTF8,
                    "application/json");
                using (var response = Http.SendAsync(message).GetAwaiter().GetResult())
                {
                    return ParseResponse(response);
                }
            }
        }

        private static JObject SendMultipart(GenerateReviewRequest request, string url, MultipartFormDataContent content)
        {
            using (var message = new HttpRequestMessage(HttpMethod.Post, url))
            {
                if (!string.IsNullOrWhiteSpace(request.ViraAccessToken))
                {
                    message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.ViraAccessToken);
                }
                message.Content = content;
                using (var response = Http.SendAsync(message).GetAwaiter().GetResult())
                {
                    return ParseResponse(response);
                }
            }
        }

        private static JObject ParseResponse(HttpResponseMessage response)
        {
            var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException("VIRA request failed: " + (int)response.StatusCode + " " + content);
            }

            return string.IsNullOrWhiteSpace(content) ? new JObject() : JObject.Parse(content);
        }

        private static string BuildApiUrl(GenerateReviewRequest request, string relativePath)
        {
            return (request.ViraBaseUrl ?? "http://localhost:8000").TrimEnd('/') + relativePath;
        }
    }
}
