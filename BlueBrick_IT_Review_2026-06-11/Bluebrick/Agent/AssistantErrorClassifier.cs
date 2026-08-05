using System;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json.Linq;

namespace BlueBrick.Agent
{
    internal static class AssistantErrorClassifier
    {
        internal static AssistantErrorInfo FromException(Exception ex, bool cancellationRequested = false)
        {
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

            return new AssistantErrorInfo("request_failed", SanitizeMessage(ex?.Message, "Assistant request failed."));
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

        internal static AssistantErrorInfo FromProviderFailure(string body)
        {
            return new AssistantErrorInfo("provider_error", ExtractSafeError(body, "AI provider request failed."));
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
                    return SanitizeMessage(errorObj.Value<string>("message"), fallback);
                }

                if (error != null)
                {
                    return SanitizeMessage(error.ToString(), fallback);
                }

                return SanitizeMessage(json.Value<string>("message"), fallback);
            }
            catch
            {
                return SanitizeMessage(body, fallback);
            }
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
            Code = code;
            Message = message;
        }

        public string Code { get; }
        public string Message { get; }
    }
}
