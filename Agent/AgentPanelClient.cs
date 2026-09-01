using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BlueBrick.Agent
{
    internal static class AgentPanelClient
    {
        private static readonly HttpClient Client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        private static readonly HttpClient StreamClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };

        internal static string BaseUrl = "http://127.0.0.1:" + AppIdentity.BridgePort;

        internal static void Configure(AgentConfig config)
        {
            var bridgePort = AgentConfig.ResolveBridgePort(config?.Agent?.BridgePort ?? 0, AppIdentity.BridgePort);

            BaseUrl = "http://127.0.0.1:" + bridgePort.ToString(CultureInfo.InvariantCulture);
        }

        internal static async Task<ApiResult<JObject>> GetJsonAsync(string path, IDictionary<string, string> query = null)
        {
            try
            {
                var url = BuildUrl(path, query);
                using (var request = CreateRequest(HttpMethod.Get, url))
                {
                    var response = await Client.SendAsync(request).ConfigureAwait(false);
                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        return ApiResult<JObject>.Fail(response.StatusCode, body);
                    }

                    var json = ParseJson(body);
                    return ApiResult<JObject>.Success(json, response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                return ApiResult<JObject>.Fail(0, ex, false);
            }
        }

        /// <summary>
        /// Convenience method for GET requests with strongly-typed response.
        /// </summary>
        internal static async Task<T> GetAsync<T>(string path, IDictionary<string, string> query = null)
        {
            var result = await GetJsonAsync(path, query).ConfigureAwait(false);
            if (!result.Ok)
            {
                throw new HttpRequestException($"Request failed: {result.Error}");
            }
            return result.Data.ToObject<T>();
        }

        internal static async Task<ApiResult<JObject>> PostJsonAsync(string path, JObject payload = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var url = BuildUrl(path, null);
                var body = payload == null ? "{}" : payload.ToString(Formatting.None);
                using (var request = CreateRequest(HttpMethod.Post, url))
                {
                    request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                    var response = await Client.SendAsync(request, cancellationToken).ConfigureAwait(false);
                    var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        return ApiResult<JObject>.Fail(response.StatusCode, text);
                    }

                    var json = ParseJson(text);
                    return ApiResult<JObject>.Success(json, response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                return ApiResult<JObject>.Fail(0, ex, cancellationToken.IsCancellationRequested);
            }
        }

    internal static async Task PostStreamingAsync(string path, JObject payload, Action<string> onChunk, CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(path, null);
        var body = payload == null ? "{}" : payload.ToString(Formatting.None);
        using (var request = CreateRequest(HttpMethod.Post, url))
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            using (var response = await StreamClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    var buffer = new char[4096];
                    var lineBuffer = new StringBuilder();
                    var readTimeout = TimeSpan.FromSeconds(90);

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var readTask = reader.ReadAsync(buffer, 0, buffer.Length);
                        var completed = await Task.WhenAny(readTask, Task.Delay(readTimeout, cancellationToken)).ConfigureAwait(false);

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
                            if (trimmed.StartsWith("data:"))
                            {
                                var data = trimmed.Substring(5).Trim();
                                if (data == "[DONE]") return;
                                onChunk(data);
                            }
                            else if (trimmed.StartsWith("{"))
                            {
                                onChunk(trimmed);
                            }
                        }
                    }
                }
            }
        }
    }

        internal static async Task<JArray> FetchModelsAsync()
        {
            try
            {
                var result = await GetJsonAsync("/assistant/models").ConfigureAwait(false);
                if (result.Ok && result.Data["models"] is JArray arr) return arr;
                return new JArray();
            }
            catch
            {
                return new JArray();
            }
        }

        internal static async Task<JArray> FetchToolsAsync()
        {
            try
            {
                var result = await GetJsonAsync("/assistant/tools").ConfigureAwait(false);
                if (result.Ok && result.Data["tools"] is JArray arr) return arr;
                return new JArray();
            }
            catch
            {
                return new JArray();
            }
        }

        internal static async Task<JArray> FetchScopesAsync()
        {
            try
            {
                var result = await GetJsonAsync("/assistant/scopes").ConfigureAwait(false);
                if (result.Ok && result.Data["scopes"] is JArray arr) return arr;
                return new JArray();
            }
            catch
            {
                return new JArray();
            }
        }

        internal static async Task<JArray> FetchToolAuditAsync(int limit)
        {
            try
            {
                var result = await GetJsonAsync("/assistant/tool-audit", new Dictionary<string, string>
                {
                    { "limit", Math.Max(1, Math.Min(limit, 50)).ToString() }
                }).ConfigureAwait(false);
                if (result.Ok && result.Data["receipts"] is JArray arr) return arr;
                return new JArray();
            }
            catch
            {
                return new JArray();
            }
        }

        internal static async Task<JArray> FetchIntegrationsAsync()
        {
            try
            {
                var result = await GetJsonAsync("/assistant/integrations").ConfigureAwait(false);
                if (result.Ok && result.Data["integrations"] is JArray arr) return arr;
                return new JArray();
            }
            catch
            {
                return new JArray();
            }
        }

        internal static async Task<JArray> FetchDocumentCatalogAsync()
        {
            try
            {
                var result = await GetJsonAsync("/assistant/document-catalog").ConfigureAwait(false);
                if (result.Ok && result.Data["documents"] is JArray arr) return arr;
                return new JArray();
            }
            catch
            {
                return new JArray();
            }
        }

        internal static Task<ApiResult<JObject>> ExecuteToolAsync(string toolName, string query, int limit)
        {
            var payload = new JObject
            {
                ["toolName"] = toolName,
                ["query"] = query ?? string.Empty,
                ["limit"] = limit
            };
            return PostJsonAsync("/assistant/tool", payload);
        }

        internal static Task<ApiResult<JObject>> ExecuteToolAsync(string toolName, string query, int limit, string scopeId)
        {
            var payload = new JObject
            {
                ["toolName"] = toolName,
                ["query"] = query ?? string.Empty,
                ["limit"] = limit,
                ["scopeId"] = scopeId ?? string.Empty
            };
            return PostJsonAsync("/assistant/tool", payload);
        }

        internal static Task<ApiResult<JObject>> ExecuteToolAsync(string toolName, string query, int limit, JObject parameters)
        {
            var payload = new JObject
            {
                ["toolName"] = toolName,
                ["query"] = query ?? string.Empty,
                ["limit"] = limit,
                ["parameters"] = parameters ?? new JObject()
            };
            return PostJsonAsync("/assistant/tool", payload);
        }

        internal static Task<ApiResult<JObject>> ReviewScreenshotItemAsync(
            string screenshotId,
            string targetType,
            string targetId,
            string reviewStatus,
            string reviewNote = null)
        {
            var payload = new JObject
            {
                ["screenshotId"] = screenshotId ?? string.Empty,
                ["targetType"] = targetType ?? string.Empty,
                ["targetId"] = targetId ?? string.Empty,
                ["reviewStatus"] = reviewStatus ?? string.Empty,
                ["reviewedBy"] = "BlueBrick task pane",
                ["reviewNote"] = reviewNote ?? string.Empty
            };
            return PostJsonAsync("/assistant/review", payload);
        }

        internal static Task<ApiResult<JObject>> SaveScreenshotAnnotationsAsync(
            string screenshotId,
            JArray annotations,
            int imageWidth = 0,
            int imageHeight = 0)
        {
            var payload = new JObject
            {
                ["schemaVersion"] = AssistantApiEnvelope.CurrentSchemaVersion,
                ["screenshotId"] = screenshotId ?? string.Empty,
                ["imageWidth"] = Math.Max(0, imageWidth),
                ["imageHeight"] = Math.Max(0, imageHeight),
                ["annotations"] = annotations ?? new JArray()
            };
            return PostJsonAsync("/assistant/annotations", payload);
        }

        private static HttpRequestMessage CreateRequest(HttpMethod method, string url)
        {
            var request = new HttpRequestMessage(method, url);
            var token = TryReadAgentToken();
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Add("X-Agent-Auth", token);
            }

            return request;
        }

        private static string BuildUrl(string path, IDictionary<string, string> query)
        {
            var baseUrl = BaseUrl.TrimEnd('/');
            var normalizedPath = path.StartsWith("/") ? path : "/" + path;
            if (query == null || query.Count == 0)
            {
                return baseUrl + normalizedPath;
            }

            var parts = new List<string>();
            foreach (var pair in query)
            {
                var key = Uri.EscapeDataString(pair.Key ?? string.Empty);
                var value = Uri.EscapeDataString(pair.Value ?? string.Empty);
                parts.Add(key + "=" + value);
            }

            return baseUrl + normalizedPath + "?" + string.Join("&", parts);
        }

        private static JObject ParseJson(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return new JObject();
            }

            try
            {
                var parsed = JObject.Parse(body);
                var okToken = parsed["ok"] ?? parsed["Ok"];
                var dataToken = parsed["data"] ?? parsed["Data"];
                if (okToken != null && dataToken != null)
                {
                    var ok = okToken.Value<bool?>() ?? false;
                    if (ok)
                    {
                        var dataObject = dataToken as JObject;
                        if (dataObject != null)
                        {
                            dataObject["ok"] = true;
                            dataObject["correlationId"] = parsed.Value<string>("correlationId") ?? parsed.Value<string>("CorrelationId") ?? string.Empty;
                            dataObject["schemaVersion"] = parsed.Value<string>("schemaVersion") ?? parsed.Value<string>("SchemaVersion") ?? string.Empty;
                            return dataObject;
                        }

                        return parsed;
                    }

                    var error = parsed["error"] as JObject ?? parsed["Error"] as JObject;
                    return new JObject
                    {
                        ["ok"] = false,
                        ["errorCode"] = error?.Value<string>("code") ?? error?.Value<string>("Code") ?? "request_failed",
                        ["error"] = error?.Value<string>("message") ?? error?.Value<string>("Message") ?? "Assistant request failed.",
                        ["correlationId"] = parsed.Value<string>("correlationId") ?? parsed.Value<string>("CorrelationId") ?? string.Empty,
                        ["schemaVersion"] = parsed.Value<string>("schemaVersion") ?? parsed.Value<string>("SchemaVersion") ?? string.Empty
                    };
                }

                return parsed;
            }
            catch
            {
                var error = AssistantErrorClassifier.FromJsonParseFailure(body);
                return new JObject
                {
                    ["raw"] = body,
                    ["errorCode"] = error.Code,
                    ["error"] = error.Message
                };
            }
        }

        private static string TryReadAgentToken()
        {
            try
            {
                var tokenPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "VIRA",
                    ".agent_token");
                return File.Exists(tokenPath) ? File.ReadAllText(tokenPath).Trim() : null;
            }
            catch
            {
                return null;
            }
        }
    }

    internal class ApiResult<T>
    {
        internal bool Ok { get; private set; }
        internal T Data { get; private set; }
        internal int StatusCode { get; private set; }
        internal string Error { get; private set; }
        internal string ErrorCode { get; private set; }

        internal static ApiResult<T> Success(T data, System.Net.HttpStatusCode statusCode)
        {
            return new ApiResult<T> { Ok = true, Data = data, StatusCode = (int)statusCode };
        }

        internal static ApiResult<T> Fail(System.Net.HttpStatusCode statusCode, string error)
        {
            var classified = AssistantErrorClassifier.FromHttpFailure(statusCode, error);
            return new ApiResult<T> { Ok = false, StatusCode = (int)statusCode, Error = classified.Message, ErrorCode = classified.Code };
        }

        internal static ApiResult<T> Fail(int statusCode, string error)
        {
            return new ApiResult<T> { Ok = false, StatusCode = statusCode, Error = error, ErrorCode = "request_failed" };
        }

        internal static ApiResult<T> Fail(int statusCode, Exception ex, bool cancellationRequested)
        {
            var classified = AssistantErrorClassifier.FromException(ex, cancellationRequested);
            return new ApiResult<T> { Ok = false, StatusCode = statusCode, Error = classified.Message, ErrorCode = classified.Code };
        }
    }
}
