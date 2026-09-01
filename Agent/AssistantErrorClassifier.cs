using System;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json.Linq;

namespace BlueBrick.Agent
{
    /// <summary>
    /// Thrown when the upstream model provider returns a non-success HTTP
    /// response. Carries safe provenance metadata (provider, model, HTTP
    /// status, category) so the failing edge can be identified without
    /// exposing payloads, API keys, headers, or prompts.
    /// </summary>
    internal sealed class AssistantProviderException : Exception
    {
        internal AssistantProviderException(
            string provider,
            string model,
            int? httpStatus,
            string category,
            string safeMessage)
            : base(string.IsNullOrWhiteSpace(safeMessage)
                ? "Assistant request failed."
                : safeMessage)
        {
            Provider = provider;
            Model = model;
            HttpStatus = httpStatus;
            Category = category;
        }

        internal string Provider { get; private set; }
        internal string Model { get; private set; }
        internal int? HttpStatus { get; private set; }
        internal string Category { get; private set; }
    }

    internal static class AssistantErrorClassifier
    {
        internal static AssistantErrorInfo FromException(Exception ex, bool cancellationRequested = false)
        {
            var providerException = ex as AssistantProviderException;
            if (providerException != null)
            {
                return new AssistantErrorInfo(
                    "request_failed",
                    SanitizeMessage(providerException.Message, "Assistant request failed."),
                    providerException.Provider,
                    providerException.Model,
                    providerException.HttpStatus,
                    string.IsNullOrWhiteSpace(providerException.Category)
                        ? "provider_error"
                        : providerException.Category);
            }

            if (ex is OperationCanceledException)
            {
                return cancellationRequested
                    ? new AssistantErrorInfo("request_canceled", "Request canceled.")
                    : new AssistantErrorInfo("request_timeout", "Assistant request timed out.");
            }

            if (ex is HttpRequestException)
            {
                return new AssistantErrorInfo("bridge_unavailable", "Assistant bridge is unavailable.");
            }

            // Generic exceptions frequently carry empty messages; surface the
            // exception type so the failure is diagnosable from the transcript.
            var detail = ex == null
                ? "unknown error"
                : string.IsNullOrWhiteSpace(ex.Message) ? ex.GetType().Name : ex.Message;
            return new AssistantErrorInfo("request_failed", SanitizeMessage(detail, "Assistant request failed."));
        }

        internal static AssistantErrorInfo FromHttpFailure(HttpStatusCode statusCode, string body)
        {
            var code = (int)statusCode;
            if (code == 401 || code == 403)
            {
                return new AssistantErrorInfo("auth_failed", "Assistant bridge authentication failed.");
            }

            if (code == 404)
            {
                return new AssistantErrorInfo("not_found", "Assistant route was not found.");
            }

            if (code == 408 || code == 504)
            {
                return new AssistantErrorInfo("request_timeout", "Assistant request timed out.");
            }

            if (code >= 500)
            {
                return new AssistantErrorInfo("bridge_error", "Assistant bridge returned an error.");
            }

            return new AssistantErrorInfo("http_error", ExtractSafeError(body, "Assistant request failed."));
        }

        // Legacy-compatible overload retained for existing call sites.
        internal static AssistantErrorInfo FromProviderFailure(string body)
        {
            return new AssistantErrorInfo(
                "provider_error",
                ExtractSafeError(body, "AI provider request failed."));
        }

        // Legacy-compatible overload retained for existing call sites.
        internal static AssistantErrorInfo FromProviderFailure(string body, string provider, string model, int httpStatus)
        {
            return FromProviderFailure(provider, model, httpStatus, body);
        }

        // Route-identity overload: builds a safe provider error with full
        // provenance (provider/model/status/category) and a sanitized
        // provider message. Never carries keys, headers, bodies, or prompts.
        internal static AssistantErrorInfo FromProviderFailure(
            string provider,
            string model,
            int httpStatus,
            string providerBody)
        {
            var safe = ExtractSafeError(providerBody, "Assistant request failed.");

            return new AssistantErrorInfo(
                "provider_error",
                safe,
                provider,
                model,
                httpStatus,
                ClassifyProviderCategory(httpStatus));
        }

        // Maps an upstream HTTP status to a safe failure category. This only
        // classifies what the transport actually reported; unknown statuses
        // fall back to the generic provider_error bucket.
        internal static string ClassifyProviderCategory(int? httpStatus)
        {
            if (!httpStatus.HasValue)
                return "provider_error";

            switch (httpStatus.Value)
            {
                case 400:
                    return "bad_request";

                case 401:
                case 403:
                    return "auth_or_permission";

                case 404:
                    return "model_or_endpoint";

                case 408:
                    return "provider_timeout";

                case 413:
                    return "request_too_large";

                case 429:
                    return "rate_limit";

                default:
                    if (httpStatus.Value >= 500)
                        return "provider_unavailable";

                    return "provider_error";
            }
        }

        internal static AssistantErrorInfo FromJsonParseFailure(string body)
        {
            return new AssistantErrorInfo("json_parse_error", "Assistant returned invalid JSON.");
        }

        internal static string ExtractSafeError(string body, string fallback)
        {
            if (string.IsNullOrWhiteSpace(body)) return fallback;
            try
            {
                var json = JObject.Parse(body);
                var error = json["error"];
                if (error is JObject errorObj)
                {
                    return ExtractSafeStructuredMessage(errorObj.Value<string>("message"), fallback);
                }

                if (error != null)
                {
                    return ExtractSafeStructuredMessage(error.ToString(), fallback);
                }

                return ExtractSafeStructuredMessage(json.Value<string>("message"), fallback);
            }
            catch
            {
                return fallback;
            }
        }

        private static string ExtractSafeStructuredMessage(string value, string fallback)
        {
            if (IsSensitiveMessage(value))
                return fallback;

            var safe = SanitizeMessage(value, fallback);
            return IsSensitiveMessage(safe) ? fallback : safe;
        }

        private static bool IsSensitiveMessage(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var markers = new[]
            {
                "X-Agent-Auth", ".agent_token", "OPENAI_API_KEY", "NVIDIA_API_KEY",
                "AssistantApiKey", "Authorization", "Bearer", "api_key", "apikey",
                "access_token", "sk-", "nvapi-", "gsk_", "xai-", "AIza"
            };

            foreach (var marker in markers)
            {
                if (value.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            var genericMarkers = new[] { "secret", "token", "password", "credential" };
            foreach (var marker in genericMarkers)
            {
                var start = 0;
                while (start < value.Length)
                {
                    var index = value.IndexOf(marker, start, StringComparison.OrdinalIgnoreCase);
                    if (index < 0)
                        break;

                    var before = index == 0 ? '\0' : value[index - 1];
                    var afterIndex = index + marker.Length;
                    var after = afterIndex == value.Length ? '\0' : value[afterIndex];
                    if (!IsLetterOrDigit(before) && !IsLetterOrDigit(after))
                        return true;

                    start = index + marker.Length;
                }
            }

            return false;
        }

        private static bool IsLetterOrDigit(char value)
        {
            return value != '\0' && char.IsLetterOrDigit(value);
        }

        // Prepends a parseable provenance tag to a wire/transcript error
        // message. The tag carries only safe fields (provider, model id,
        // HTTP status, category) and never payload contents, keys, headers,
        // or prompts. Without provenance the message is returned untouched.
        internal static string FormatWithProvenance(AssistantErrorInfo error)
        {
            if (error == null)
                return "Assistant request failed.";

            var message = SanitizeMessage(error.Message, "Assistant request failed.");

            var hasProvenance =
                !string.IsNullOrWhiteSpace(error.Provider) ||
                !string.IsNullOrWhiteSpace(error.Model) ||
                error.HttpStatus.HasValue ||
                !string.IsNullOrWhiteSpace(error.Category);

            if (!hasProvenance)
                return message;

            var provider = SafeTagValue(error.Provider);
            var model = SafeTagValue(error.Model);
            var status = error.HttpStatus.HasValue
                ? error.HttpStatus.Value.ToString()
                : "unknown";
            var category = SafeTagValue(error.Category);

            return string.Format(
                "[prov provider={0} model={1} httpStatus={2} category={3}] {4}",
                provider,
                model,
                status,
                category,
                message);
        }

        // Flattens a provenance field for safe inclusion inside the bracket
        // tag: control characters removed, brackets neutralized so the tag
        // stays parseable, length capped.
        private static string SafeTagValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "unknown";

            var safe = value
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Replace("[", "(")
                .Replace("]", ")")
                .Trim();

            if (safe.Length > 120)
                safe = safe.Substring(0, 120);

            return safe;
        }

        private static string SanitizeMessage(string value, string fallback)
        {
            var text = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text)) return fallback;
            text = text.Replace("\r", " ").Replace("\n", " ");
            if (text.Length > 300) text = text.Substring(0, 300) + "...";
            return text;
        }
    }

    internal class AssistantErrorInfo
    {
        internal AssistantErrorInfo(string code, string message)
        {
            Code = code ?? "request_failed";
            Message = string.IsNullOrWhiteSpace(message)
                ? "Assistant request failed."
                : message;
        }

        internal AssistantErrorInfo(string code, string message, string provider, string model, int? httpStatus, string category)
            : this(code, message)
        {
            Provider = provider;
            Model = model;
            HttpStatus = httpStatus;
            Category = category;
        }

        public string Code { get; }
        public string Message { get; }

        // Optional safe provenance for upstream provider failures. These are
        // metadata only: never keys, headers, bodies, or prompt contents.
        public string Provider { get; set; }
        public string Model { get; set; }
        public int? HttpStatus { get; set; }
        public string Category { get; set; }
    }
}
