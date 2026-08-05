using System;
using System.Collections.Generic;

namespace BlueBrick.SolidWorks.Snapshots
{
    /// <summary>
    /// Document state snapshot: dirty + read-only + original active
    /// configuration (to prove the audit did not change it). Per BB-M001
    /// packet section 17, the adapter preserves the original active
    /// configuration and verifies it remains unchanged; this snapshot
    /// captures both the before-state and an after-state field so the
    /// invariant is observable in the artifact.
    /// </summary>
    [Serializable]
    public sealed class DocumentStateSnapshot
    {
        /// <summary>Whether the document was marked dirty before the audit.</summary>
        public bool DirtyBefore { get; set; }

        /// <summary>Whether the document is read-only. Read-only flag is one of the strongest invariant guarantees (per packet §4).</summary>
        public bool IsReadOnly { get; set; }

        /// <summary>Active configuration name captured before any property read.</summary>
        public string ActiveConfigurationBefore { get; set; }

        /// <summary>Active configuration name captured after all property reads. Must equal <see cref="ActiveConfigurationBefore"/> for read-only modes.</summary>
        public string ActiveConfigurationAfter { get; set; }

        /// <summary>Whether the document was marked dirty after the audit. Must equal <see cref="DirtyBefore"/> for read-only modes.</summary>
        public bool DirtyAfter { get; set; }

        /// <summary>Optional list of discovered configuration names (bounded by the caller's ConfigurationReadLimit).</summary>
        public List<string> AvailableConfigurations { get; set; } = new List<string>();
    }
}
