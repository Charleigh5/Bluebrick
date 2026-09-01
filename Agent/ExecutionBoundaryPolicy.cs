using System;

namespace BlueBrick.Agent
{
    internal static class ExecutionBoundaryPolicy
    {
        internal static ExecutionBoundaryDecision EvaluateRoute(string route, string method)
        {
            var path = (route ?? string.Empty).Trim().ToLowerInvariant();
            var verb = (method ?? string.Empty).Trim().ToUpperInvariant();

            if (path.StartsWith("/sw/", StringComparison.Ordinal) || path == "/sw")
            {
                return ExecutionBoundaryDecision.Deny(
                    "BB-CAD-EXECUTION-APPROVAL_REQUIRED",
                    "SOLIDWORKS execution is denied at the execution boundary until a trusted native approval lifecycle exists.");
            }

            if (path.StartsWith("/pdm/", StringComparison.Ordinal) || path == "/pdm")
            {
                return ExecutionBoundaryDecision.Deny(
                    "BB-PDM-EXECUTION-APPROVAL_REQUIRED",
                    "PDM execution is denied at the execution boundary; the assistant cannot authorize native PDM routes.");
            }

            if (path == "/lab/vault/reset" || path == "/sw/jobs/override")
            {
                return ExecutionBoundaryDecision.Deny(
                    "BB-RUNTIME-DESTRUCTIVE_ACTION_BLOCKED",
                    "Destructive Lab actions require a trusted native approval lifecycle.");
            }

            return ExecutionBoundaryDecision.Allow();
        }

        internal static ExecutionBoundaryDecision EvaluatePreviewAction(string actionName)
        {
            var action = (actionName ?? string.Empty).Trim().ToLowerInvariant();
            if (action == "run_local_review")
            {
                return ExecutionBoundaryDecision.Deny(
                    "BB-DEPLOY-EXTERNAL_WRITE_BLOCKED",
                    "Customer-system review submission is disabled in this acceptance sprint.");
            }

            if (action == "reset_local_vault" || action == "reindex_local_vault" || action == "open_output_folder" || action == "capture_preview_screenshot")
            {
                return ExecutionBoundaryDecision.Deny(
                    "BB-RUNTIME-LOCAL_SIDE_EFFECT_APPROVAL_REQUIRED",
                    "This local side effect requires a trusted native approval lifecycle.");
            }

            return ExecutionBoundaryDecision.Allow();
        }
    }

    internal sealed class ExecutionBoundaryDecision
    {
        internal bool Allowed { get; private set; }
        internal string Code { get; private set; }
        internal string Message { get; private set; }

        internal static ExecutionBoundaryDecision Allow()
        {
            return new ExecutionBoundaryDecision { Allowed = true, Code = "BB-EXECUTION-ALLOW", Message = "Execution boundary allows this route." };
        }

        internal static ExecutionBoundaryDecision Deny(string code, string message)
        {
            return new ExecutionBoundaryDecision { Allowed = false, Code = code, Message = message };
        }
    }
}
