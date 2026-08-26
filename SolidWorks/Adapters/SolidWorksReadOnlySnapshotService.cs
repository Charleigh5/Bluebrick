using System;
using System.Collections.Generic;
using BlueBrick.Audit.Contracts;
using BlueBrick.Audit.Core;
using BlueBrick.SolidWorks.Runtime;

namespace BlueBrick.SolidWorks.Adapters
{
    /// <summary>
    /// Default composition root that combines the
    /// <see cref="SolidWorksCustomPropertyReadAdapter"/> with the
    /// <see cref="AuditReceiptFactory"/> and the
    /// <see cref="AuditStateVersionBuilder"/> to produce a
    /// <see cref="AuditRunResult"/> for one read-only audit run. Per
    /// BB-M001 packet §19, this service is intended to be invoked from
    /// the existing BlueBrick runtime via a controlled hook (the hook
    /// is in <c>swaddin.cs</c> which is a PROHIBITED file for Slice 1/2;
    /// see the final report's "Runtime Wiring" section). Until that
    /// hook exists, the service is marked
    /// <c>STAGED_NOT_WIRED</c> by this packet.
    /// </summary>
    public sealed class SolidWorksReadOnlySnapshotService : ISolidWorksReadOnlySnapshotService
    {
        private readonly ICustomPropertyReadAdapter _adapter;
        private readonly AuditReceiptFactory _receiptFactory;
        private readonly SolidWorksRuntimeInfo _runtimeInfo;

        /// <summary>Create the composition root. The arguments are normally supplied by the add-in's wiring layer.</summary>
        public SolidWorksReadOnlySnapshotService(
            ICustomPropertyReadAdapter adapter,
            AuditReceiptFactory receiptFactory,
            SolidWorksRuntimeInfo runtimeInfo)
        {
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            _receiptFactory = receiptFactory ?? throw new ArgumentNullException(nameof(receiptFactory));
            _runtimeInfo = runtimeInfo ?? throw new ArgumentNullException(nameof(runtimeInfo));
        }

        /// <inheritdoc />
        public string ServiceName => "SolidWorksReadOnlySnapshotService";

        /// <inheritdoc />
        public AuditRunResult RunReadonlySnapshot(AuditRunRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.Mode != AuditOperationMode.READ_ONLY_ANALYST &&
                request.Mode != AuditOperationMode.MOCK)
            {
                var deniedErr = new AuditError { Code = AuditErrorCodes.INVALID_MODE, CorrelationId = request.CorrelationId, Message = "Slice 1/2 supports only MOCK or READ_ONLY_ANALYST modes." };
                var deniedReceipt = _receiptFactory.CreateDenied(request, _adapter.AdapterName, _runtimeInfo.Version?.DisplayVersion ?? "unknown", _runtimeInfo.Classification.ToString(), "Unsupported mode in Slice 1/2.", deniedErr);
                return new AuditRunResult { Snapshot = null, Evidence = new List<AuditEvidence>(), Findings = new List<AuditFinding>(), Receipt = deniedReceipt, Errors = new List<AuditError> { deniedErr } };
            }

            List<AuditError> errors;
            BlueBrick.SolidWorks.Snapshots.PropertyAuditSnapshot snapshot;
            try
            {
                snapshot = _adapter.ReadCustomProperties(request, out errors);
            }
            catch (SolidWorksThreadViolationException ex)
            {
                var comViolation = new AuditError
                {
                    Code = AuditErrorCodes.COM_THREAD_VIOLATION,
                    CorrelationId = request.CorrelationId,
                    Message = ex.Message
                };
                var denied = _receiptFactory.CreateDenied(request, _adapter.AdapterName, _runtimeInfo.Version?.DisplayVersion ?? "unknown", _runtimeInfo.Classification.ToString(), "COM thread violation — no audit performed.", comViolation);
                return new AuditRunResult { Snapshot = null, Evidence = new List<AuditEvidence>(), Findings = new List<AuditFinding>(), Receipt = denied, Errors = new List<AuditError> { comViolation } };
            }

            // Errors at this point may include NO_ACTIVE_DOCUMENT, INTEROP_LIMITATION, or READ_FAILURE — all typed.
            // No exceptions thrown — typed partial errors returned.
            if (errors.Count > 0 && errors[0].Code == AuditErrorCodes.NO_ACTIVE_DOCUMENT)
            {
                var denied = _receiptFactory.CreateDenied(request, _adapter.AdapterName, _runtimeInfo.Version?.DisplayVersion ?? "unknown", _runtimeInfo.Classification.ToString(), "No active document in the session.", errors[0]);
                return new AuditRunResult { Snapshot = snapshot, Evidence = new List<AuditEvidence>(), Findings = new List<AuditFinding>(), Receipt = denied, Errors = errors };
            }

            // Compute state versions over the snapshot bundle before and after.
            // For a read-only run they MUST be equal.
            string stateVersion = AuditStateVersionBuilder.BuildStateVersion(snapshot);
            bool dirtyBefore = snapshot?.State?.DirtyBefore ?? false;
            bool dirtyAfter = snapshot?.State?.DirtyAfter ?? false;
            bool isReadOnly = snapshot?.State?.IsReadOnly ?? false;
            string activeConfig = snapshot?.Identity?.ActiveConfiguration ?? string.Empty;
            string docType = snapshot?.Identity?.DocumentType ?? "Unknown";
            string pathHash = snapshot?.Identity?.DocumentIdentityHash ?? string.Empty;

            var isPartial = errors != null && errors.Count > 0;
            var receipt = _receiptFactory.Create(
                request: request,
                adapter: _adapter.AdapterName,
                runtimeVersion: _runtimeInfo.Version?.DisplayVersion ?? "unknown",
                runtimeClassification: _runtimeInfo.Classification.ToString(),
                pathHash: pathHash,
                documentType: docType,
                activeConfiguration: activeConfig,
                dirtyBefore: dirtyBefore,
                dirtyAfter: dirtyAfter,
                isReadOnly: isReadOnly,
                stateVersionBefore: stateVersion,
                stateVersionAfter: stateVersion,
                toolsRequested: new[] { "custom_property_snapshot" },
                toolsExecuted: request.Mode == AuditOperationMode.MOCK ? new string[] { } : new[] { "custom_property_snapshot" },
                evidence: new AuditEvidence[0],
                findings: new AuditFinding[0],
                resultStatus: isPartial ? "Partial" : "Completed",
                message: isPartial ? "Partial — some properties unavailable." : null,
                errors: errors,
                sideEffects: null,
                rollbackReason: null);

            return new AuditRunResult
            {
                Snapshot = snapshot,
                Evidence = new List<AuditEvidence>(),
                Findings = new List<AuditFinding>(),
                Receipt = receipt,
                Errors = errors
            };
        }
    }
}
