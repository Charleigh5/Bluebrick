using System;
using System.Collections.Generic;

namespace BlueBrick.Audit.Contracts
{
    /// <summary>
    /// Evidence record produced by an audit run. Per BB-M001 packet
    /// section 10, supports a stable evidence ID, evidence type, source,
    /// location, raw and resolved values, confidence, limitations, and
    /// evidence label. Raw and resolved values are stored separately so
    /// the deterministic state hash ignores the resolved-value channel
    /// when a property was unresolved (`was_resolved == false`).
    /// </summary>
    [Serializable]
    public sealed class AuditEvidence
    {
        /// <summary>Stable, caller-assigned evidence ID (correlation key for findings).</summary>
        public string EvidenceId { get; set; }

        /// <summary>Evidence type label (CustomProperty, DocumentIdentity, Runtime, etc.).</summary>
        public string EvidenceType { get; set; }

        /// <summary>Logical source label mirroring <see cref="AuditEvidenceLocation.SourceLabel"/>.</summary>
        public string Source { get; set; }

        /// <summary>Bounded, hashed location descriptor; never full local path.</summary>
        public AuditEvidenceLocation Location { get; set; }

        /// <summary>Raw value as read from the source, before expression/$PRP resolution.</summary>
        public string RawValue { get; set; }

        /// <summary>Resolved value (may equal RawValue when the property is not linked).</summary>
        public string ResolvedValue { get; set; }

        /// <summary>0.0..1.0 assertion confidence (1.0 = directly read, 0.5 = inferred safe-default).</summary>
        public double Confidence { get; set; }

        /// <summary>Per-evidence limitations (e.g. "interop_Get3_unsupported", "resolved_value_unavailable").</summary>
        public List<string> Limitations { get; set; } = new List<string>();

        /// <summary>Human-readable evidence label surfaced in receipts.</summary>
        public string EvidenceLabel { get; set; }
    }
}
