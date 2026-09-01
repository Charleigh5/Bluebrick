using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace BlueBrick.Agent
{
    internal class AssistantToolAuthorization
    {
        public string ApprovalId { get; set; }
        public string ApprovedBy { get; set; }
        public string Reason { get; set; }
        public DateTime? ApprovedUtc { get; set; }
        public string ApprovedRoute { get; set; }
        public string ApprovedMethod { get; set; }
        public string ActorSource { get; set; }
        public string RequestId { get; set; }
        public string CapabilityId { get; set; }
        public string ArgumentDigest { get; set; }
        public string SessionId { get; set; }
        public string Environment { get; set; }
        public DateTime? ExpiresUtc { get; set; }
        public DateTime? ConsumedUtc { get; set; }
        public bool Granted { get; set; }
        [JsonIgnore]
        internal bool IsServerIssued { get; private set; }

        internal static AssistantToolAuthorization None()
        {
            return new AssistantToolAuthorization
            {
                Granted = false
            };
        }

        internal static AssistantToolAuthorization CreateServerApproval(
            string requestId,
            string capabilityId,
            AssistantToolRequest request,
            string sessionId,
            string environment,
            string approvedBy,
            string reason,
            DateTime expiresUtc)
        {
            if (string.IsNullOrWhiteSpace(requestId)) throw new ArgumentException("requestId required", nameof(requestId));
            if (string.IsNullOrWhiteSpace(capabilityId)) throw new ArgumentException("capabilityId required", nameof(capabilityId));
            if (expiresUtc <= DateTime.UtcNow) throw new ArgumentOutOfRangeException(nameof(expiresUtc));

            return new AssistantToolAuthorization
            {
                ApprovalId = Guid.NewGuid().ToString("N"),
                ApprovedBy = approvedBy ?? "trusted_native_session",
                Reason = reason ?? string.Empty,
                ApprovedUtc = DateTime.UtcNow,
                ApprovedRoute = "/assistant/tool",
                ApprovedMethod = "POST",
                ActorSource = "trusted_native_session",
                RequestId = requestId,
                CapabilityId = capabilityId,
                ArgumentDigest = ComputeArgumentDigest(request),
                SessionId = sessionId ?? string.Empty,
                Environment = environment ?? string.Empty,
                ExpiresUtc = expiresUtc,
                Granted = true,
                IsServerIssued = true
            };
        }

        internal bool IsValidFor(AssistantToolDescriptor descriptor, AssistantToolRequest request, string requestId, string sessionId, string environment, DateTime nowUtc)
        {
            return Granted &&
                   IsServerIssued &&
                   descriptor != null &&
                   string.Equals(CapabilityId, descriptor.CapabilityId ?? descriptor.Name, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(RequestId, requestId, StringComparison.Ordinal) &&
                   string.Equals(SessionId ?? string.Empty, sessionId ?? string.Empty, StringComparison.Ordinal) &&
                   string.Equals(Environment ?? string.Empty, environment ?? string.Empty, StringComparison.Ordinal) &&
                   string.Equals(ArgumentDigest, ComputeArgumentDigest(request), StringComparison.Ordinal) &&
                   ExpiresUtc.HasValue && ExpiresUtc.Value > nowUtc &&
                   !ConsumedUtc.HasValue;
        }

        internal static string ComputeArgumentDigest(AssistantToolRequest request)
        {
            request = request ?? new AssistantToolRequest();
            var builder = new StringBuilder();
            builder.Append(request.ToolName ?? string.Empty).Append('\n');
            builder.Append(request.Query ?? string.Empty).Append('\n');
            builder.Append(request.Limit).Append('\n');
            builder.Append(request.ScopeId ?? string.Empty).Append('\n');
            if (request.Parameters != null)
            {
                foreach (var item in request.Parameters.OrderBy(item => item.Key, StringComparer.Ordinal))
                {
                    builder.Append(item.Key ?? string.Empty).Append('=').Append(item.Value ?? string.Empty).Append('\n');
                }
            }

            using (var sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString())))
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }
    }
}
