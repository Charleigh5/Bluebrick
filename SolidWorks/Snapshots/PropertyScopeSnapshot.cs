using System;
using System.Collections.Generic;

namespace BlueBrick.SolidWorks.Snapshots
{
    /// <summary>
    /// Per-scope snapshot: either the document-level property collection
    /// or the per-configuration property collection. Per BB-M001 packet
    /// section 17, the adapter reads document-level and active-config
    /// scope by default, with bounded all-config support through an
    /// explicit limit. Multiple <see cref="PropertyScopeSnapshot"/>
    /// records may be present in the <see cref="PropertyAuditSnapshot"/>
    /// bundle — one per scope/configuration pair.
    /// </summary>
    [Serializable]
    public sealed class PropertyScopeSnapshot
    {
        /// <summary>Scope label: "Document" or "Configuration".</summary>
        public string Scope { get; set; }

        /// <summary>Configuration name (empty for document-level).</summary>
        public string Configuration { get; set; }

        /// <summary>Properties found in this scope. Caller-sorted in property-name order.</summary>
        public List<CustomPropertySnapshot> Properties { get; set; } = new List<CustomPropertySnapshot>();

        /// <summary>Per-scope limitation list (e.g. "config_limit_reached" for bounded all-config runs).</summary>
        public List<string> Limitations { get; set; } = new List<string>();
    }
}
