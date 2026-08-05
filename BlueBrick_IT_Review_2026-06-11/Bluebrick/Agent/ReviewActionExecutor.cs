using System;
using Newtonsoft.Json.Linq;
using SolidWorks.Interop.sldworks;

namespace BlueBrick.Agent
{
    internal sealed class ReviewActionExecutor
    {
        private readonly ISldWorks _swApp;
        private readonly TelemetryLogger _telemetry;

        internal ReviewActionExecutor(ISldWorks swApp, TelemetryLogger telemetry)
        {
            _swApp = swApp;
            _telemetry = telemetry;
        }

        internal AutoActionExecutionResult TryAutoApply(ReviewArtifact artifact, LiveFinding finding, GenerationManifest manifest)
        {
            var action = finding?.SuggestedAction;
            if (action == null)
            {
                return AutoActionExecutionResult.Noop("missing_action");
            }

            if (action.RequiresConfirmation || !action.Deterministic)
            {
                return AutoActionExecutionResult.Noop("confirmation_required");
            }

            var risk = (action.RiskLevel ?? string.Empty).ToUpperInvariant();
            if (risk != "LOW" && risk != "INFO")
            {
                return AutoActionExecutionResult.Noop("risk_too_high");
            }

            switch ((action.ActionType ?? string.Empty).ToLowerInvariant())
            {
                case "rerender_sheet":
                case "refresh_preview":
                case "rerun_checkpoint":
                    AppendManifestLog(manifest, "Auto-applied low-risk action: " + action.ActionType);
                    _telemetry.LogEvent("AUTO_ACTION", action.ActionType, true, 0, new { artifactId = artifact?.Id, findingId = finding?.Id });
                    return AutoActionExecutionResult.RefreshCheckpoint(action.ActionType);
                case "refresh_metadata":
                case "refresh_bom":
                    AppendManifestLog(manifest, "Auto-applied low-risk action: " + action.ActionType);
                    _telemetry.LogEvent("AUTO_ACTION", action.ActionType, true, 0, new { artifactId = artifact?.Id, findingId = finding?.Id });
                    return AutoActionExecutionResult.MetadataRefresh(action.ActionType);
                default:
                    return AutoActionExecutionResult.Noop("unsupported_action");
            }
        }

        private static void AppendManifestLog(GenerationManifest manifest, string message)
        {
            if (manifest == null || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            manifest.StatusLog.Add(DateTime.UtcNow.ToString("O") + " " + message);
        }
    }

    internal sealed class AutoActionExecutionResult
    {
        internal bool Applied { get; private set; }
        internal bool RequiresCheckpointRefresh { get; private set; }
        internal bool RequiresMetadataRefresh { get; private set; }
        internal string Outcome { get; private set; }

        internal static AutoActionExecutionResult Noop(string outcome)
        {
            return new AutoActionExecutionResult { Outcome = outcome };
        }

        internal static AutoActionExecutionResult RefreshCheckpoint(string outcome)
        {
            return new AutoActionExecutionResult
            {
                Applied = true,
                RequiresCheckpointRefresh = true,
                Outcome = outcome
            };
        }

        internal static AutoActionExecutionResult MetadataRefresh(string outcome)
        {
            return new AutoActionExecutionResult
            {
                Applied = true,
                RequiresMetadataRefresh = true,
                Outcome = outcome
            };
        }
    }
}
