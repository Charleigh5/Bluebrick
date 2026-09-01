using System;
using System.Collections.Generic;

namespace BlueBrick.Agent
{
    internal class AssistantToolExecutionReceipt
    {
        public string ReceiptId { get; set; }
        public DateTime TimestampUtc { get; set; }
        public string CorrelationId { get; set; }
        public string TraceId { get; set; }
        public string ToolName { get; set; }
        public string RequestId { get; set; }
        public string SessionId { get; set; }
        public string Environment { get; set; }
        public string CapabilityId { get; set; }
        public string ExecutionBoundary { get; set; }
        public string AuthorizationState { get; set; }
        public string Mode { get; set; } = "READ_ONLY_ANALYST";
        public string Version { get; set; } = string.Empty;
        public string DocumentType { get; set; } = "Unknown";
        public string State { get; set; } = string.Empty;
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> ErrorCodes { get; set; } = new List<string>();
        public double DurationMs { get; set; }
        public int MutationCount { get; set; } = 0;
        public string RiskLevel { get; set; }
        public bool Allowed { get; set; }
        public bool ReadOnly { get; set; }
        public bool ApprovalRequired { get; set; }
        public bool ApprovalGranted { get; set; }
        public string ApprovalId { get; set; }
        public string PolicyCode { get; set; }
        public string ResultStatus { get; set; }
        public string Message { get; set; }
        public Dictionary<string, string> InputSummary { get; set; } = new Dictionary<string, string>();

        internal static AssistantToolExecutionReceipt Create(
            AssistantToolRequest request,
            AssistantToolPolicyDecision policy,
            AssistantToolDescriptor descriptor,
            AssistantToolAuthorization authorization,
            string resultStatus,
            string message,
            string traceId)
        {
            request = request ?? new AssistantToolRequest();
            policy = policy ?? AssistantToolPolicyDecision.Deny("unknown_policy", "Policy decision missing.", true);
            authorization = authorization ?? AssistantToolAuthorization.None();
            var serverApprovalGranted = authorization.Granted && authorization.IsServerIssued;
            var warnings = new List<string>();
            var errorCodes = new List<string>();
            if (!policy.Allowed) errorCodes.Add(policy.Code ?? "DENY");

            return new AssistantToolExecutionReceipt
            {
                ReceiptId = Guid.NewGuid().ToString("N"),
                TimestampUtc = DateTime.UtcNow,
                CorrelationId = traceId ?? string.Empty,
                TraceId = traceId,
                ToolName = request.ToolName ?? string.Empty,
                RequestId = request.RequestId ?? traceId ?? string.Empty,
                SessionId = request.SessionId ?? string.Empty,
                Environment = request.Environment ?? string.Empty,
                CapabilityId = descriptor?.CapabilityId ?? descriptor?.Name ?? string.Empty,
                ExecutionBoundary = descriptor?.ExecutionBoundary ?? "assistant_tool_service",
                AuthorizationState = serverApprovalGranted ? "server_issued" : (authorization.Granted ? "client_claim_ignored" : "none"),
                Mode = "READ_ONLY_ANALYST",
                Version = descriptor?.AllowedModes != null && descriptor.AllowedModes.Length > 0 ? string.Join(",", descriptor.AllowedModes) : "READ_ONLY_ANALYST",
                DocumentType = "Unknown",
                State = resultStatus ?? "unknown",
                Warnings = warnings,
                ErrorCodes = errorCodes,
                DurationMs = 0,
                MutationCount = 0,
                RiskLevel = descriptor == null ? "unknown" : descriptor.RiskLevel ?? "unknown",
                Allowed = policy.Allowed,
                ReadOnly = descriptor == null || descriptor.ReadOnly,
                ApprovalRequired = descriptor?.RequiresConfirmation == true || policy.RequiresReceipt,
                ApprovalGranted = serverApprovalGranted,
                ApprovalId = serverApprovalGranted ? authorization.ApprovalId ?? string.Empty : string.Empty,
                PolicyCode = policy.Code,
                ResultStatus = resultStatus,
                Message = message,
                InputSummary = SummarizeInput(request)
            };
        }

        private static Dictionary<string, string> SummarizeInput(AssistantToolRequest request)
        {
            var summary = new Dictionary<string, string>
            {
                { "queryLength", (request.Query ?? string.Empty).Length.ToString() },
                { "limit", request.Limit.ToString() },
                { "parameterCount", (request.Parameters?.Count ?? 0).ToString() }
            };

            if (request.Parameters != null)
            {
                foreach (var key in request.Parameters.Keys)
                {
                    summary["param:" + key] = "present";
                }
            }

            return summary;
        }
    }
}
