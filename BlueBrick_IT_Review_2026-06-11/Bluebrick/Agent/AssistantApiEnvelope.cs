using System;

namespace BlueBrick.Agent
{
    internal class AssistantApiEnvelope
    {
        internal const string CurrentSchemaVersion = "2026-06-01.v1";

        public bool Ok { get; set; }
        public string CorrelationId { get; set; }
        public string SchemaVersion { get; set; }
        public object Data { get; set; }
        public AssistantApiError Error { get; set; }

        internal static AssistantApiEnvelope Success(object data, string correlationId)
        {
            return new AssistantApiEnvelope
            {
                Ok = true,
                CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId,
                SchemaVersion = CurrentSchemaVersion,
                Data = data,
                Error = null
            };
        }

        internal static AssistantApiEnvelope Fail(string code, string message, string correlationId)
        {
            return new AssistantApiEnvelope
            {
                Ok = false,
                CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId,
                SchemaVersion = CurrentSchemaVersion,
                Data = null,
                Error = new AssistantApiError
                {
                    Code = string.IsNullOrWhiteSpace(code) ? "request_failed" : code,
                    Message = string.IsNullOrWhiteSpace(message) ? "Assistant request failed." : message
                }
            };
        }
    }

    internal class AssistantApiError
    {
        public string Code { get; set; }
        public string Message { get; set; }
    }
}
