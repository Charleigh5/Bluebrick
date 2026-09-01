using System;

namespace BlueBrick.Agent
{
    internal class AssistantToolPolicy
    {
        internal AssistantToolPolicyDecision EvaluateToolName(string toolName)
        {
            var normalized = Normalize(toolName);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return AssistantToolPolicyDecision.Deny("invalid", "toolName required", false);
            }

            if (normalized.StartsWith("/", StringComparison.Ordinal))
            {
                return EvaluateRoute(normalized, "POST", AssistantToolInvocationSource.AssistantTool);
            }

            var lower = normalized.ToLowerInvariant();
            if (lower.Contains("sw/") || lower.Contains("pdm/") || lower.Contains("lab/vault/reset"))
            {
                return AssistantToolPolicyDecision.Deny("blocked_route_alias", "Assistant tools cannot invoke CAD, PDM, or destructive lab routes by alias.", true);
            }
            string[] blockedSubstrings = new[] { "save", "save_as", "rebuild", "edit", "write", "config", "drawing", "bom", "checkout", "checkin", "batch" };
            foreach (var b in blockedSubstrings)
            {
                if (lower.Contains(b) && !string.Equals(normalized, "solidworks.get_active_document_snapshot", StringComparison.OrdinalIgnoreCase) && !string.Equals(normalized, "solidworks.get_selection_snapshot", StringComparison.OrdinalIgnoreCase) && !string.Equals(normalized, "solidworks.get_feature_tree", StringComparison.OrdinalIgnoreCase))
                {
                    return AssistantToolPolicyDecision.Deny("DENY", "Blocked mutation tool '" + b + "' denied by read-only policy.", true);
                }
            }
            if (lower.Contains("pdm") && !string.Equals(normalized, "search_pdm", StringComparison.OrdinalIgnoreCase) && !string.Equals(normalized, "solidworks.get_active_document_snapshot", StringComparison.OrdinalIgnoreCase) && !string.Equals(normalized, "solidworks.get_selection_snapshot", StringComparison.OrdinalIgnoreCase) && !string.Equals(normalized, "solidworks.get_feature_tree", StringComparison.OrdinalIgnoreCase))
            {
                return AssistantToolPolicyDecision.Deny("DENY", "Blocked mutation tool 'pdm' denied by read-only policy.", true);
            }
            if (!string.Equals(normalized, "solidworks.get_active_document_snapshot", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(normalized, "solidworks.get_selection_snapshot", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(normalized, "solidworks.get_feature_tree", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(normalized, "search_local_vault", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(normalized, "search_pdm", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(normalized, "search_epicor", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(normalized, "capture_screenshot", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(normalized, "create_screenshot_review_report", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(normalized, "search_all", StringComparison.OrdinalIgnoreCase))
            {
                return AssistantToolPolicyDecision.Allow("unknown", "Unknown tool will be handled by catalog.", false);
            }



            return AssistantToolPolicyDecision.Allow("safe_tool_name", "Tool name is not a protected route.", false);
        }

        internal AssistantToolPolicyDecision EvaluateRoute(string route, string method, AssistantToolInvocationSource source)
        {
            var path = Normalize(route).ToLowerInvariant();
            var verb = Normalize(method).ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(path))
            {
                return AssistantToolPolicyDecision.Deny("invalid", "route required", false);
            }

            if (path.StartsWith("/sw/", StringComparison.Ordinal))
            {
                return AssistantToolPolicyDecision.Deny(
                    "blocked_cad_route",
                    "SolidWorks routes are blocked from assistant-driven execution until preview, approval, and receipts exist.",
                    true);
            }

            if (string.Equals(path, "/lab/vault/reset", StringComparison.Ordinal))
            {
                return AssistantToolPolicyDecision.Deny(
                    "blocked_destructive_lab_route",
                    "Lab vault reset is destructive and must remain human-confirmed.",
                    true);
            }

            if (path.StartsWith("/pdm/", StringComparison.Ordinal))
            {
                if (string.Equals(path, "/pdm/search", StringComparison.Ordinal) ||
                    string.Equals(path, "/pdm/get_props", StringComparison.Ordinal))
                {
                    return AssistantToolPolicyDecision.Deny(
                        "blocked_native_pdm_route",
                        "Native PDM routes are blocked from assistant-driven execution; use the read-only assistant wrapper instead.",
                        true);
                }

                return AssistantToolPolicyDecision.Deny(
                    "blocked_pdm_mutation_route",
                    "PDM mutation/file routes are blocked from assistant-driven execution until explicit approval and receipts exist.",
                    true);
            }

            if (string.Equals(path, "/assistant/tool", StringComparison.Ordinal) || path.StartsWith("/assistant/", StringComparison.Ordinal))
            {
                return AssistantToolPolicyDecision.Allow("assistant_route", "Assistant route is allowed by route policy.", false);
            }

            if (path.StartsWith("/agent/telemetry/", StringComparison.Ordinal) || string.Equals(path, "/agent/selfcheck", StringComparison.Ordinal))
            {
                return AssistantToolPolicyDecision.Allow("read_only_agent_route", "Read-only agent route is allowed by route policy.", false);
            }

            if (source == AssistantToolInvocationSource.AssistantTool && verb != "GET")
            {
                return AssistantToolPolicyDecision.Deny(
                    "unknown_mutation_route",
                    "Unknown non-GET routes are not assistant-callable.",
                    true);
            }

            return AssistantToolPolicyDecision.Allow("unprotected_route", "Route is not in the protected CAD/PDM/lab set.", false);
        }

        internal AssistantToolPolicyDecision EvaluateCapability(
            AssistantToolDescriptor descriptor,
            AssistantToolRequest request,
            AssistantToolInvocationSource source,
            AssistantToolAuthorization trustedAuthorization,
            string requestId,
            string sessionId,
            string environment,
            DateTime nowUtc)
        {
            if (descriptor == null)
            {
                return AssistantToolPolicyDecision.Deny("unknown", "Unknown assistant capability.", true);
            }

            // Authorization is deliberately not deserialized from /assistant/tool. A direct
            // in-process caller that presents a forged approval is denied as well.
            if (request?.Authorization != null && request.Authorization.Granted)
            {
                return AssistantToolPolicyDecision.Deny(
                    "untrusted_client_authorization",
                    "Client-supplied authorization is request context, not execution authority.",
                    true);
            }

            if (!descriptor.Enabled)
            {
                return AssistantToolPolicyDecision.Deny(
                    "disabled",
                    descriptor.UnavailableReason ?? "Capability is disabled.",
                    false);
            }

            if (source == AssistantToolInvocationSource.Chat && !descriptor.AllowedInChat)
            {
                return AssistantToolPolicyDecision.Deny(
                    "chat_not_allowed",
                    "Capability is not available from the chat caller.",
                    false);
            }

            var mutating = descriptor.Mutating ||
                           descriptor.MutatesCad ||
                           descriptor.MutatesPdm ||
                           descriptor.MutatesEpicor ||
                           descriptor.GenerationCapability;
            if (mutating || !descriptor.ReadOnly)
            {
                if (trustedAuthorization == null ||
                    !trustedAuthorization.IsValidFor(descriptor, request, requestId, sessionId, environment, nowUtc))
                {
                    return AssistantToolPolicyDecision.Deny(
                        "approval_required",
                        "Capability requires a trusted native approval bound to this request, arguments, session, and environment.",
                        true);
                }
            }

            return AssistantToolPolicyDecision.Allow("capability_allow", "Server capability policy allows this read-only capability.", descriptor.AuditRequired);
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim();
        }
    }

    internal enum AssistantToolInvocationSource
    {
        AssistantTool,
        Chat,
        HostUi,
        Relay
    }

    internal class AssistantToolPolicyDecision
    {
        public bool Allowed { get; set; }
        public string Code { get; set; }
        public string Message { get; set; }
        public bool RequiresReceipt { get; set; }

        internal static AssistantToolPolicyDecision Allow(string code, string message, bool requiresReceipt)
        {
            return new AssistantToolPolicyDecision
            {
                Allowed = true,
                Code = code,
                Message = message,
                RequiresReceipt = requiresReceipt
            };
        }

        internal static AssistantToolPolicyDecision Deny(string code, string message, bool requiresReceipt)
        {
            return new AssistantToolPolicyDecision
            {
                Allowed = false,
                Code = code,
                Message = message,
                RequiresReceipt = requiresReceipt
            };
        }
    }
}
