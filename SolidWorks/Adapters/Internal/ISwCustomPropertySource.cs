using System.Collections.Generic;

namespace BlueBrick.SolidWorks.Adapters.Internal
{
    /// <summary>
    /// Narrow internal abstraction over the actual
    /// <c>ICustomPropertyManager</c> SOLIDWORKS interop interface. Per
    /// BB-M001 packet §18 ("When an existing mocking approach cannot
    /// represent SOLIDWORKS COM interfaces, introduce a narrow internal
    /// abstraction and test that abstraction. Do not add a large
    /// mocking framework without necessity."), this is the ONLY seam
    /// used by the adapter. Concrete tests substitute this seam with a
    /// lightweight stub — no real COM calls are exercised.
    /// </summary>
    internal interface ISwCustomPropertySource
    {
        /// <summary>The configuration name this source is scoped to; empty for the document-level manager.</summary>
        string ConfigurationName { get; }

        /// <summary>Scope label: "Document" or "Configuration".</summary>
        string Scope { get; }

        /// <summary>
        /// Returns the names of all custom properties declared in this
        /// manager. Null when the current interop does not expose
        /// enumeration (the caller records
        /// <see cref="BlueBrick.Audit.Contracts.AuditErrorCodes.INTEROP_LIMITATION"/>
        /// in that case).
        /// </summary>
        IReadOnlyList<string> GetPropertyNames();

        /// <summary>
        /// Get a single property. Mirrors the Get2/Get3 behaviour with
        /// explicit raw/resolved + was-resolved booleans. Returns false
        /// when the property is absent. Per-prop limitations are
        /// populated by the implementation when a member was unavailable
        /// on the installed interop family.
        /// </summary>
        bool TryGet(string name, out string rawValue, out string resolvedValue, out bool wasResolved, out string linkedOrExpressionStatus, out string editableStatusWhenAvailable, out string apiStatus, out List<string> limitations);
    }

    /// <summary>
    /// Narrow internal abstraction over the document identity/state
    /// accesses that the adapter needs (so tests can substitute a
    /// stub without instantiating the real <c>IModelDoc2</c> COM object).
    /// </summary>
    internal interface ISwDocumentSource
    {
        /// <summary>Document-level <c>ICustomPropertyManager</c> abstraction; null when the document does not support it.</summary>
        ISwCustomPropertySource GetDocumentLevelSource();

        /// <summary>Configuration-level <c>ICustomPropertyManager</c> abstraction for the named configuration; null when not found.</summary>
        ISwCustomPropertySource GetConfigurationSource(string configurationName);

        /// <summary>Active configuration name; empty when there is no active configuration.</summary>
        string GetActiveConfigurationName();

        /// <summary>All configuration names; null when enumeration is unavailable. Bounded callers read at most <c>ConfigurationReadLimit</c> of these.</summary>
        IReadOnlyList<string> GetConfigurationNames();

        /// <summary>Document dirty flag per the interop's <c>IModelDoc2</c>.</summary>
        bool GetDirty();

        /// <summary>Document read-only flag.</summary>
        bool GetIsReadOnly();

        /// <summary>Document type label: "Part" / "Assembly" / "Drawing" / "Unknown".</summary>
        string GetDocumentType();

        /// <summary>
        /// Canonical file-system path of the document (will be path-hashed
        /// by the caller via
        /// <see cref="BlueBrick.Audit.Core.AuditRedactionService.RedactPath"/>).
        /// </summary>
        string GetPath();
    }
}
