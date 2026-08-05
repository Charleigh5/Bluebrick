using System;
using System.Collections.Generic;

namespace BlueBrick.Audit.Contracts
{
    /// <summary>
    /// Audit run result bundle. Per BB-M001 packet section 10, contains the
    /// snapshot bundle (document + property audit snapshot), the evidence
    /// list, the finding list, the receipt, and any typed errors. The
    /// snapshot bundle is wholly serializable POCO — never a COM object.
    /// </summary>
    [Serializable]
    public sealed class AuditRunResult
    {
        /// <summary>Top-level property audit snapshot referencing per-scope sub-snapshots.</summary>
        public BlueBrick.SolidWorks.Snapshots.PropertyAuditSnapshot Snapshot { get; set; }

        /// <summary>All evidence records produced during the run.</summary>
        public List<AuditEvidence> Evidence { get; set; } = new List<AuditEvidence>();

        /// <summary>All findings produced during the run.</summary>
        public List<AuditFinding> Findings { get; set; } = new List<AuditFinding>();

        /// <summary>Tamper-evident execution receipt for this run.</summary>
        public AuditExecutionReceipt Receipt { get; set; }

        /// <summary>Typed partial errors recorded during the run (mirrors <see cref="AuditExecutionReceipt.Errors"/>).</summary>
        public List<AuditError> Errors { get; set; } = new List<AuditError>();

        /// <summary>Convenience flag: true if the run completed without errors and without denied tools.</summary>
        public bool Succeeded => Errors == null || Errors.Count == 0;
    }
}
