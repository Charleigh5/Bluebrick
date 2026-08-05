using System;
using System.Collections.Generic;

namespace BlueBrick.Audit.Contracts
{
    /// <summary>
    /// Tamper-evident execution receipt for one audit run. Per BB-M001
    /// packet section 10, supports operation and correlation IDs, runtime
    /// version, adapter, path hash, document type/configuration,
    /// dirty/read-only state before and after, state versions before and
    /// after, tools requested/executed, evidence/finding counts, result,
    /// errors, side effects, and rollback reason.
    /// </summary>
    [Serializable]
    public sealed class AuditExecutionReceipt
    {
        /// <summary>Generated at receipt construction time; never caller-supplied.</summary>
        public string OperationId { get; set; }

        /// <summary>Caller correlation ID from <see cref="AuditRunRequest.CorrelationId"/>.</summary>
        public string CorrelationId { get; set; }

        /// <summary>Run capture timestamp in ISO-8601 UTC. EXCLUDED from state hash inputs.</summary>
        public DateTime TimestampUtc { get; set; }

        /// <summary>SOLIDWORKS runtime version captured by <see cref="BlueBrick.SolidWorks.Runtime.SolidWorksRuntimeInfo"/> (or "MOCK" for synthetic runs).</summary>
        public string RuntimeVersion { get; set; }

        /// <summary>Runtime classification label (e.g. 'Sw2025Target', 'UnknownReadOnly', 'Mock').</summary>
        public string RuntimeClassification { get; set; }

        /// <summary>Adapter label, e.g. 'SolidWorksCustomPropertyReadAdapter'.</summary>
        public string Adapter { get; set; }

        /// <summary>SHA-256 path hash of the audited document; never the raw path.</summary>
        public string PathHash { get; set; }

        /// <summary>Document type label (Part / Assembly / Drawing / Unknown).</summary>
        public string DocumentType { get; set; }

        /// <summary>Active configuration name at audit time.</summary>
        public string ActiveConfiguration { get; set; }

        /// <summary>Document dirty state immediately before the audit.</summary>
        public bool DirtyBefore { get; set; }

        /// <summary>Document dirty state immediately after the audit. Must equal <see cref="DirtyBefore"/> for read-only modes.</summary>
        public bool DirtyAfter { get; set; }

        /// <summary>Document read-only flag at audit time.</summary>
        public bool IsReadOnly { get; set; }

        /// <summary>SHA-256 state version computed over the canonical snapshot BEFORE the audit.</summary>
        public string StateVersionBefore { get; set; }

        /// <summary>SHA-256 state version computed over the canonical snapshot AFTER the audit. Must equal <see cref="StateVersionBefore"/> for read-only modes.</summary>
        public string StateVersionAfter { get; set; }

        /// <summary>Tool names requested by the caller (audit-internal tools, not LLM tools).</summary>
        public List<string> ToolsRequested { get; set; } = new List<string>();

        /// <summary>Tool names actually executed by the adapter. Always empty for MOCK; populated for READ_ONLY_ANALYST.</summary>
        public List<string> ToolsExecuted { get; set; } = new List<string>();

        /// <summary>Number of evidence records produced.</summary>
        public int EvidenceCount { get; set; }

        /// <summary>Number of findings produced.</summary>
        public int FindingCount { get; set; }

        /// <summary>Run result label (Completed / Denied / Partial / Failed).</summary>
        public string ResultStatus { get; set; }

        /// <summary>Caller-facing status message.</summary>
        public string Message { get; set; }

        /// <summary>Typed partial errors recorded during the run.</summary>
        public List<AuditError> Errors { get; set; } = new List<AuditError>();

        /// <summary>
        /// Side effects recorded during the run. For MOCK and
        /// <see cref="AuditOperationMode.READ_ONLY_ANALYST"/> runs this
        /// list MUST be empty (enforced by
        /// <see cref="BlueBrick.Audit.Core.AuditReceiptFactory"/>; tested
        /// by Receipt_ReadOnlyRun_HasNoSideEffects /
        /// Receipt_DeniedRun_IsStillRecorded).
        /// </summary>
        public List<string> SideEffects { get; set; } = new List<string>();

        /// <summary>Optional rollback reason if (and only if) a later packet performed rollback. Empty for read-only.</summary>
        public string RollbackReason { get; set; }
    }
}
