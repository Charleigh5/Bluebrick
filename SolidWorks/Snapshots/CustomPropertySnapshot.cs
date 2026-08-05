using System;

namespace BlueBrick.SolidWorks.Snapshots
{
    /// <summary>
    /// Single custom-property snapshot record. Per BB-M001 packet
    /// section 17 ("Property snapshot fields"), carries: name,
    /// normalized_name, scope, configuration, raw and resolved values,
    /// was_resolved, linked_or_expression_status,
    /// editable_status_when_available, api_status, and limitations.
    /// </summary>
    [Serializable]
    public sealed class CustomPropertySnapshot
    {
        /// <summary>Property name exactly as exposed by the CustomPropertyManager.</summary>
        public string Name { get; set; }

        /// <summary>Normalized name (trimmed; case-folded to invariant lower for matching only; human display uses <see cref="Name"/>).</summary>
        public string NormalizedName { get; set; }

        /// <summary>Scope label: "Document" for document-level properties, "Configuration" for configuration-level.</summary>
        public string Scope { get; set; }

        /// <summary>Configuration name this property was read under; empty for document-level scope.</summary>
        public string Configuration { get; set; }

        /// <summary>Raw value as returned by the read API before any $PRP/$PRPSHEET expression resolution.</summary>
        public string RawValue { get; set; }

        /// <summary>Value after expression resolution; may equal <see cref="RawValue"/> if not linked.</summary>
        public string ResolvedValue { get; set; }

        /// <summary>true if the read API reported the value as resolved; false if resolution was skipped or unavailable.</summary>
        public bool WasResolved { get; set; }

        /// <summary>Linked/expression status (e.g. "Linked", "Expression", "None", "Unknown").</summary>
        public string LinkedOrExpressionStatus { get; set; }

        /// <summary>Editable status as reported by the API when available; empty/Unknown if the member is not exposed by the installed interop.</summary>
        public string EditableStatusWhenAvailable { get; set; }

        /// <summary>API status label (e.g. "Get3_Supported", "Get2_Fallback", "INTEROP_LIMITATION") — never a boolean.</summary>
        public string ApiStatus { get; set; }

        /// <summary>Per-record limitations list (e.g. ["interop_Get3_unsupported", "resolved_value_unavailable"]).</summary>
        public System.Collections.Generic.List<string> Limitations { get; set; } = new System.Collections.Generic.List<string>();
    }
}
