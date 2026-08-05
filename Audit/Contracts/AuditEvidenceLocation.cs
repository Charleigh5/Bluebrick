using System;

namespace BlueBrick.Audit.Contracts
{
    /// <summary>
    /// Bounded evidence location descriptor. Carries a stable path hash and
    /// an optional basename; never a full local path. Per BB-M001 packet
    /// section 10/12, full local paths and user profile folder names must
    /// never appear in user-visible artifacts.
    /// </summary>
    [Serializable]
    public sealed class AuditEvidenceLocation
    {
        /// <summary>Logical/source label for the evidence (e.g. "CustomPropertyManager", "Configuration").</summary>
        public string SourceLabel { get; set; }

        /// <summary>Stable SHA-256 hash of the canonicalized location path; safe to expose.</summary>
        public string PathHash { get; set; }

        /// <summary>Optional basename (filename only, no folder) — safe to surface.</summary>
        public string Basename { get; set; }
    }
}
