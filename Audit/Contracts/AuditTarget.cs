using System;
using System.Collections.Generic;

namespace BlueBrick.Audit.Contracts
{
    /// <summary>
    /// Audit target descriptor. Carries hashed/redacted document identity,
    /// document type, the active configuration at run time, the expected
    /// state version (if the caller pre-computed one), and the expected
    /// dirty state for invariant verification.
    /// Per BB-M001 packet section 10, no full local paths and no COM
    /// references may live here.
    /// </summary>
    [Serializable]
    public sealed class AuditTarget
    {
        /// <summary>Stable, hash-derived opaque document identity. Never a raw path.</summary>
        public string DocumentIdentityHash { get; set; }

        /// <summary>Document type label (Part, Assembly, Drawing, Unknown).</summary>
        public string DocumentType { get; set; }

        /// <summary>Active configuration name at the moment of audit.</summary>
        public string ActiveConfiguration { get; set; }

        /// <summary>Optional expected SHA-256 state version supplied by the caller.</summary>
        public string ExpectedStateVersion { get; set; }

        /// <summary>Expected dirty state, if the caller has prior knowledge.</summary>
        public bool? ExpectedDirty { get; set; }

        /// <summary>Optional path hash (SHA-256 of the canonicalized path) — never the raw path.</summary>
        public string PathHash { get; set; }

        /// <summary>Optional basename (filename only, no folder) — safe to surface.</summary>
        public string PathBasename { get; set; }
    }
}
