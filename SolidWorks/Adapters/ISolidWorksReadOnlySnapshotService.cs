using System.Collections.Generic;
using BlueBrick.Audit.Contracts;
using BlueBrick.SolidWorks.Snapshots;

namespace BlueBrick.SolidWorks.Adapters
{
    /// <summary>
    /// High-level read-only snapshot service entry point. Per BB-M001
    /// packet §17 and §19, the service is the composition root that
    /// produces a <see cref="PropertyAuditSnapshot"/> bundle plus the
    /// <see cref="AuditExecutionReceipt"/> describing what was run.
    /// </summary>
    public interface ISolidWorksReadOnlySnapshotService
    {
        /// <summary>Service label surfaced in receipts.</summary>
        string ServiceName { get; }

        /// <summary>
        /// Run an audit snapshot for the supplied request. Per packet
        /// §17, returns a fully serializable result; never throws
        /// outside of a COM-thread violation (which is fatal and is
        /// rethrown for the caller to wrap as
        /// <see cref="AuditErrorCodes.COM_THREAD_VIOLATION"/>).
        /// </summary>
        AuditRunResult RunReadonlySnapshot(AuditRunRequest request);
    }
}
