using System;
using System.Collections.Generic;

namespace BlueBrick.Agent
{
    internal sealed class PreviewActionPolicy
    {
        private static readonly HashSet<string> AllowedActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "get_preview_status",
            "get_active_context",
            "search_local_vault",
            "get_review_findings",
            "capture_preview_screenshot",
            "open_output_folder",
            "run_local_review",
            "get_session_history",
            "reindex_local_vault",
            "reset_local_vault"
        };

        private static readonly HashSet<string> DisabledActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "start_local_generation",
            "apply_safe_action"
        };

        internal PreviewPolicyDecision Evaluate(PreviewSession session, PreviewActionRequest request)
        {
            if (session == null)
            {
                return PreviewPolicyDecision.Deny("Preview session was not found.");
            }

            if (request == null || string.IsNullOrWhiteSpace(request.ActionName))
            {
                return PreviewPolicyDecision.Deny("Action name is required.");
            }

            if (DisabledActions.Contains(request.ActionName))
            {
                return PreviewPolicyDecision.Deny("Action is defined but disabled in the hosted validation pass.");
            }

            if (!AllowedActions.Contains(request.ActionName))
            {
                return PreviewPolicyDecision.Deny("Action is unsupported or blocked in preview mode.");
            }

            if (session.AllowedActions != null && session.AllowedActions.Count > 0 &&
                !session.AllowedActions.Contains(request.ActionName))
            {
                return PreviewPolicyDecision.Deny("Action is not allowed for this preview session.");
            }

            return request.RequiresConfirmation
                ? PreviewPolicyDecision.RequireConfirmation()
                : PreviewPolicyDecision.Allow();
        }
    }

    internal sealed class PreviewPolicyDecision
    {
        internal bool Allowed { get; private set; }
        internal bool RequiresConfirmation { get; private set; }
        internal string Reason { get; private set; }

        internal static PreviewPolicyDecision Allow()
        {
            return new PreviewPolicyDecision { Allowed = true };
        }

        internal static PreviewPolicyDecision RequireConfirmation()
        {
            return new PreviewPolicyDecision { Allowed = true, RequiresConfirmation = true };
        }

        internal static PreviewPolicyDecision Deny(string reason)
        {
            return new PreviewPolicyDecision { Allowed = false, Reason = reason };
        }
    }
}
