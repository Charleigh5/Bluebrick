using System.Collections.Generic;
using BlueBrick.Audit.Contracts;
using BlueBrick.SolidWorks.Snapshots;

namespace BlueBrick.SolidWorks.Adapters
{
    /// <summary>
    /// Read-only contract for one SOLIDWORKS document's custom
    /// properties. Per BB-M001 packet §17, the adapter:
    /// <list type="bullet">
    /// <item>never launches SOLIDWORKS (build controlled construction with an existing instance);</item>
    /// <item>verifies main-thread access;</item>
    /// <item>never invokes any writer / save / rebuild / SetSaveFlag;</item>
    /// <item>never displays a MessageBox;</item>
    /// <item>returns only serializable snapshots;</item>
    /// <item>returns typed partial errors when individual property reads fail.</item>
    /// </list>
    /// </summary>
    public interface ICustomPropertyReadAdapter
    {
        /// <summary>The adapter's stable label (used by <see cref="AuditExecutionReceipt.Adapter"/>).</summary>
        string AdapterName { get; }

        /// <summary>
        /// Read the document-level custom properties + the active
        /// configuration property manager by default. When
        /// <paramref name="readAllConfigurations"/> is true and
        /// <paramref name="configurationReadLimit"/> is positive, also
        /// read up to that many configurations (bounded; remaining
        /// configurations are listed under the snapshot with limitation
        /// <see cref="AuditErrorCodes.CONFIG_LIMIT_REACHED"/>).
        /// </summary>
        PropertyAuditSnapshot ReadCustomProperties(
            AuditRunRequest request,
            out List<AuditError> errors);
    }
}
