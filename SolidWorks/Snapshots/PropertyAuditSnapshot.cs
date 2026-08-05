using System;
using System.Collections.Generic;

namespace BlueBrick.SolidWorks.Snapshots
{
    /// <summary>
    /// Top-level aggregate property audit snapshot. Per BB-M001 packet
    /// section 17, the adapter returns "only serializable snapshots"
    /// (requirement 9). This bundle contains only POCO fields. NO COM
    /// object may be present anywhere in this graph.
    /// </summary>
    [Serializable]
    public sealed class PropertyAuditSnapshot
    {
        /// <summary>Document identity (hashed) + document type + active configuration at audit time.</summary>
        public DocumentIdentitySnapshot Identity { get; set; }

        /// <summary>Document state (dirty, read-only, before/after active config, before/after dirty, available configurations).</summary>
        public DocumentStateSnapshot State { get; set; }

        /// <summary>Per-scope snapshots (one per document-level + zero or more per-configuration).</summary>
        public List<PropertyScopeSnapshot> Scopes { get; set; } = new List<PropertyScopeSnapshot>();

        /// <summary>List of governed property names that the adapter attempted to read (per packet §17 initial list + caller-supplied names).</summary>
        public List<string> GovernedPropertyNames { get; set; } = new List<string>();

        /// <summary>
        /// All discovered property names (when the current API supports
        /// enumeration). Empty when bounded enumeration is not available
        /// on the installed interop.
        /// </summary>
        public List<string> DiscoveredPropertyNames { get; set; } = new List<string>();

        /// <summary>Top-level limitations that affect the bundle as a whole (e.g. installed interop revision family mismatch).</summary>
        public List<string> Limitations { get; set; } = new List<string>();

        /// <summary>SOLIDWORKS runtime classification label as captured by the adapter at audit time.</summary>
        public string RuntimeClassification { get; set; }

        /// <summary>SOLIDWORKS runtime revision string (when proven) or "MOCK"/"FromInstallRegistry" placeholder.</summary>
        public string RuntimeVersion { get; set; }
    }
}
