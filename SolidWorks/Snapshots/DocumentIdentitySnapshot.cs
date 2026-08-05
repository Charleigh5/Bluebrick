using System;

namespace BlueBrick.SolidWorks.Snapshots
{
    /// <summary>
    /// Document identity snapshot: hashed identity + document type + active
    /// configuration. Per BB-M001 packet section 10, no full local paths
    /// and no COM references may live here.
    /// </summary>
    [Serializable]
    public sealed class DocumentIdentitySnapshot
    {
        /// <summary>SHA-256 hash of the canonical model path; never the raw path.</summary>
        public string DocumentIdentityHash { get; set; }

        /// <summary>Document type label (Part / Assembly / Drawing / Unknown).</summary>
        public string DocumentType { get; set; }

        /// <summary>Active configuration name at the moment of audit.</summary>
        public string ActiveConfiguration { get; set; }

        /// <summary>Optional basename (filename only, no folder) — safe to surface.</summary>
        public string Basename { get; set; }
    }
}
