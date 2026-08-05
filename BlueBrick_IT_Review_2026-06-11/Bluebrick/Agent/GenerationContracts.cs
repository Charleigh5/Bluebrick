using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BlueBrick.Agent
{
    internal enum GateOutcome
    {
        Pending,
        Pass,
        SoftFail,
        HardFail,
        Overridden
    }

    internal class GenerationIssue
    {
        public string Code { get; set; }
        public string Severity { get; set; }
        public string Message { get; set; }
        public string Stage { get; set; }
        public JObject Metadata { get; set; } = new JObject();
    }

    internal class SuggestedAction
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("action_type")]
        public string ActionType { get; set; }

        [JsonProperty("label")]
        public string Label { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("requires_confirmation")]
        public bool RequiresConfirmation { get; set; } = true;

        [JsonProperty("risk_level")]
        public string RiskLevel { get; set; } = "HIGH";

        [JsonProperty("execution_scope")]
        public string ExecutionScope { get; set; } = "artifact";

        [JsonProperty("idempotency_key")]
        public string IdempotencyKey { get; set; }

        [JsonProperty("deterministic")]
        public bool Deterministic { get; set; }

        [JsonProperty("payload")]
        public JObject Payload { get; set; } = new JObject();
    }

    internal class LiveFinding
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("issue_id")]
        public string IssueId { get; set; }

        [JsonProperty("severity")]
        public string Severity { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("rule_id")]
        public string RuleId { get; set; }

        [JsonProperty("rule_condition_ref")]
        public string RuleConditionRef { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("auto_action_eligible")]
        public bool AutoActionEligible { get; set; }

        [JsonProperty("requires_confirmation")]
        public bool RequiresConfirmation { get; set; } = true;

        [JsonProperty("standards_version")]
        public string StandardsVersion { get; set; }

        [JsonProperty("suggested_action")]
        public SuggestedAction SuggestedAction { get; set; }

        [JsonProperty("evidence")]
        public JObject Evidence { get; set; } = new JObject();

        [JsonProperty("metadata")]
        public JObject Metadata { get; set; } = new JObject();
    }

    internal class GateDecision
    {
        [JsonProperty("scope")]
        public string Scope { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("blocked")]
        public bool Blocked { get; set; }

        [JsonProperty("reason")]
        public string Reason { get; set; }

        [JsonProperty("blocking_issue_ids")]
        public List<string> BlockingIssueIds { get; set; } = new List<string>();
    }

    internal class ReviewArtifact
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("session_id")]
        public string SessionId { get; set; }

        [JsonProperty("packet_id")]
        public string PacketId { get; set; }

        [JsonProperty("checkpoint_type")]
        public string CheckpointType { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("gate_status")]
        public string GateStatus { get; set; }

        [JsonProperty("idempotency_key")]
        public string IdempotencyKey { get; set; }

        [JsonProperty("source_model_path")]
        public string SourceModelPath { get; set; }

        [JsonProperty("sheet_id")]
        public string SheetId { get; set; }

        [JsonProperty("view_id")]
        public string ViewId { get; set; }

        [JsonProperty("preview_path")]
        public string PreviewPath { get; set; }

        [JsonProperty("preview_image_url")]
        public string PreviewImageUrl { get; set; }

        [JsonProperty("artifact_hash")]
        public string ArtifactHash { get; set; }

        [JsonProperty("metadata_hash")]
        public string MetadataHash { get; set; }

        [JsonProperty("analysis_mode")]
        public string AnalysisMode { get; set; }

        [JsonProperty("standards_baseline")]
        public string StandardsBaseline { get; set; }

        [JsonProperty("standards_version")]
        public string StandardsVersion { get; set; }

        [JsonProperty("customer_ruleset_version")]
        public string CustomerRulesetVersion { get; set; }

        [JsonProperty("auto_action_policy")]
        public string AutoActionPolicy { get; set; }

        [JsonProperty("auto_action_outcome")]
        public string AutoActionOutcome { get; set; }

        [JsonProperty("metadata")]
        public JObject Metadata { get; set; } = new JObject();

        [JsonProperty("summary")]
        public JObject Summary { get; set; } = new JObject();

        [JsonProperty("findings")]
        public List<LiveFinding> Findings { get; set; } = new List<LiveFinding>();

        [JsonProperty("gate_decision")]
        public GateDecision GateDecision { get; set; }

        [JsonProperty("error")]
        public string Error { get; set; }
    }

    internal class ReviewSession
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("gate_status")]
        public string GateStatus { get; set; }

        [JsonProperty("packet_id")]
        public string PacketId { get; set; }

        [JsonProperty("intake_id")]
        public string IntakeId { get; set; }

        [JsonProperty("trace_id")]
        public string TraceId { get; set; }

        [JsonProperty("standards_baseline")]
        public string StandardsBaseline { get; set; }

        [JsonProperty("standards_version")]
        public string StandardsVersion { get; set; }

        [JsonProperty("customer_ruleset_version")]
        public string CustomerRulesetVersion { get; set; }

        [JsonProperty("analysis_mode")]
        public string AnalysisMode { get; set; }

        [JsonProperty("auto_action_policy")]
        public string AutoActionPolicy { get; set; }

        [JsonProperty("artifacts")]
        public List<ReviewArtifact> Artifacts { get; set; } = new List<ReviewArtifact>();

        [JsonProperty("gate_decision")]
        public GateDecision GateDecision { get; set; }

        [JsonProperty("manifest")]
        public JObject Manifest { get; set; } = new JObject();
    }

    internal class GenerationManifest
    {
        public string TraceId { get; set; }
        public string JobId { get; set; }
        public string SessionId { get; set; }
        public string CreatedAtUtc { get; set; } = DateTime.UtcNow.ToString("O");
        public string SolidWorksVersion { get; set; }
        public string ServicePack { get; set; }
        public string SourceModelPath { get; set; }
        public string OutputPdfPath { get; set; }
        public string OutputDxfPath { get; set; }
        public string GateStatus { get; set; } = GateOutcome.Pending.ToString().ToUpperInvariant();
        public string IdempotencyKey { get; set; }
        public string StandardsBaseline { get; set; } = "ASME";
        public string StandardsVersion { get; set; } = "Y14.100";
        public string CustomerRulesetVersion { get; set; } = "default";
        public string AnalysisMode { get; set; } = "incremental_checkpoint";
        public string AutoActionPolicy { get; set; } = "supervised_auto_fix";
        public List<GenerationIssue> Issues { get; set; } = new List<GenerationIssue>();
        public List<string> StatusLog { get; set; } = new List<string>();
    }

    internal class GenerateReviewRequest
    {
        public string Mode { get; set; } = "single";
        public string CustomerId { get; set; }
        public string PacketName { get; set; }
        public string ServerPath { get; set; }
        public string ViraBaseUrl { get; set; } = "http://localhost:8000";
        public string ViraAccessToken { get; set; }
        public bool PromoteToPdmOnPass { get; set; } = true;
        public bool AutoFinalizePacket { get; set; }
        public bool ContinueOnArtifactFail { get; set; } = true;
        public string ServicePack { get; set; } = "2024 SP3.x";
        public string IdempotencyKey { get; set; }
        public string StandardsBaseline { get; set; } = "ASME";
        public string StandardsVersion { get; set; } = "Y14.100";
        public string CustomerRulesetVersion { get; set; } = "default";
        public string AnalysisMode { get; set; } = "incremental_checkpoint";
        public string AutoActionPolicy { get; set; } = "supervised_auto_fix";
        public bool AutoApplyLowRiskActions { get; set; } = true;
        public JObject RuleParameterOverrides { get; set; } = new JObject();
    }

    internal class LiveCheckpointRequest
    {
        public string JobId { get; set; }
        public string PreviewImagePath { get; set; }
        public string SheetId { get; set; }
        public string ViewId { get; set; }
        public string SourceModelPath { get; set; }
        public string ArtifactId { get; set; }
        public string ArtifactHash { get; set; }
        public string MetadataHash { get; set; }
        public JObject Metadata { get; set; } = new JObject();
        public string IdempotencyKey { get; set; }
    }

    internal class LiveDecisionRequest
    {
        public string JobId { get; set; }
        public string ArtifactId { get; set; }
        public string FindingId { get; set; }
        public string DecisionType { get; set; } = "OVERRIDE";
        public string DecisionStatus { get; set; } = "APPROVED";
        public string Reason { get; set; }
        public string ActionId { get; set; }
        public JObject Payload { get; set; } = new JObject();
    }

    internal class GenerateReviewResult
    {
        public string JobId { get; set; }
        public string SessionId { get; set; }
        public string SessionUrl { get; set; }
        public string EventsUrl { get; set; }
        public string Status { get; set; }
        public string GateStatus { get; set; }
        public string Message { get; set; }
        public string IntakeId { get; set; }
        public string PacketId { get; set; }
        public string OverrideReason { get; set; }
        public GenerationManifest Manifest { get; set; }
        public GateDecision SessionGate { get; set; }
        public List<ReviewArtifact> Artifacts { get; set; } = new List<ReviewArtifact>();
    }
}
