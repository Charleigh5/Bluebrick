using System;
using System.Collections.Generic;

namespace BlueBrick.Audit.Contracts
{
    /// <summary>
    /// Audit finding. Per BB-M001 packet section 10, supports a stable
    /// finding ID, rule ID, severity, status, evidence IDs, recommended
    /// action, hard-coded <see cref="AutomaticCorrectionAllowed"/>=false,
    /// confidence, and data gaps.
    /// </summary>
    [Serializable]
    public sealed class AuditFinding
    {
        /// <summary>Stable, caller-assigned finding ID.</summary>
        public string FindingId { get; set; }

        /// <summary>Rule ID the finding refers to (e.g. 'VR-PROP-001').</summary>
        public string RuleId { get; set; }

        /// <summary>Severity label (Info / Warning / Error / Critical).</summary>
        public string Severity { get; set; }

        /// <summary>Status label (Open / Acknowledged / Resolved / Dismissed).</summary>
        public string Status { get; set; }

        /// <summary>Evidence IDs this finding references (correlation back to <see cref="AuditEvidence.EvidenceId"/>).</summary>
        public List<string> EvidenceIds { get; set; } = new List<string>();

        /// <summary>Recommended human-readable corrective action (never performed automatically by Slice 1/2).</summary>
        public string RecommendedAction { get; set; }

        /// <summary>
        /// Hard-coded <c>false</c>: per BB-M001 packet section 10, automatic
        /// correction is forbidden for Slices 0-2. Read-only audit kernel cannot
        /// perform any mutation. The setter is private so user code cannot flip it.
        /// </summary>
        public bool AutomaticCorrectionAllowed
        {
            get { return false; }
            private set { /* permanently false; ignored */ }
        }

        /// <summary>0.0..1.0 finding confidence.</summary>
        public double Confidence { get; set; }

        /// <summary>Data gaps that informed this finding (e.g. "missing Description property").</summary>
        public List<string> DataGaps { get; set; } = new List<string>();
    }
}
