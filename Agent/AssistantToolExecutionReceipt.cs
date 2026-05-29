using System;
using System.Collections.Generic;

namespace BlueBrick.Agent
{
    internal class AssistantToolExecutionReceipt
    {
        public string ReceiptId { get; set; }
        public DateTime TimestampUtc { get; set; }
        public string TraceId { get; set; }
        public string ToolName { get; set; }
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

            return new AssistantToolExecutionReceipt
            {
                ReceiptId = Guid.NewGuid().ToString("N"),
                TimestampUtc = DateTime.UtcNow,
                TraceId = traceId,
                ToolName = request.ToolName ?? string.Empty,
                RiskLevel = descriptor == null ? "unknown" : descriptor.RiskLevel ?? "unknown",
                Allowed = policy.Allowed,
                ReadOnly = descriptor == null || descriptor.ReadOnly,
                ApprovalRequired = descriptor?.RequiresConfirmation == true || policy.RequiresReceipt,
                ApprovalGranted = authorization.Granted,
                ApprovalId = authorization.ApprovalId ?? string.Empty,
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
