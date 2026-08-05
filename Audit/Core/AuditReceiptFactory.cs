using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using BlueBrick.Audit.Contracts;

namespace BlueBrick.Audit.Core
{
    /// <summary>
    /// Builds <see cref="AuditExecutionReceipt"/> instances from run
    /// context. Enforces the BB-M001 packet's invariants:
    /// <list type="bullet">
    /// <item>read-only modes (<see cref="AuditOperationMode.MOCK"/> and <see cref="AuditOperationMode.READ_ONLY_ANALYST"/>) MUST produce zero side effects;</item>
    /// <item>denied runs (e.g. policy-blocked) MUST still be recorded, never silently swallowed;</item>
    /// <item>every receipt is assigned a fresh <see cref="AuditExecutionReceipt.OperationId"/> at construction time;</item>
    /// <item>timestamp is set at construction time but excluded from the state-hash inputs the caller supplies.</item>
    /// </list>
    /// </summary>
    public sealed class AuditReceiptFactory
    {
        private readonly Func<DateTime> _utcNow;
        private readonly Func<string> _newOperationId;

        /// <summary>Default constructor uses real UTC clock and GUID-based operation IDs.</summary>
        public AuditReceiptFactory()
            : this(() => DateTime.UtcNow, () => Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture))
        {
        }

        /// <summary>Test-friendly constructor allowing clock and ID overrides.</summary>
        public AuditReceiptFactory(Func<DateTime> utcNow, Func<string> newOperationId)
        {
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
            _newOperationId = newOperationId ?? (() => Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Create a receipt for a run that produced results.
        /// Throws <see cref="InvalidOperationException"/> if the caller claims
        /// a read-only mode AND any side effects were recorded — this is a
        /// hard contract violation per BB-M001 §4 "Never perform" + §23.
        /// </summary>
        public AuditExecutionReceipt Create(
            AuditRunRequest request,
            string adapter,
            string runtimeVersion,
            string runtimeClassification,
            string pathHash,
            string documentType,
            string activeConfiguration,
            bool dirtyBefore,
            bool dirtyAfter,
            bool isReadOnly,
            string stateVersionBefore,
            string stateVersionAfter,
            IReadOnlyList<string> toolsRequested,
            IReadOnlyList<string> toolsExecuted,
            IReadOnlyList<AuditEvidence> evidence,
            IReadOnlyList<AuditFinding> findings,
            string resultStatus,
            string message,
            IReadOnlyList<AuditError> errors,
            IReadOnlyList<string> sideEffects,
            string rollbackReason)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var mode = request.Mode;
            var sx = (sideEffects ?? new string[0]).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

            // The packet's hard invariant: read-only modes produce zero side effects.
            if ((mode == AuditOperationMode.MOCK || mode == AuditOperationMode.READ_ONLY_ANALYST)
                && sx.Count > 0)
            {
                throw new InvalidOperationException(
                    "Read-only audit modes (MOCK / READ_ONLY_ANALYST) MUST have zero side effects. " +
                    "Any side effect indicates a prohibited mutation was performed. " +
                    "First side effect: " + sx[0]);
            }

            return new AuditExecutionReceipt
            {
                OperationId = _newOperationId(),
                CorrelationId = request.CorrelationId ?? string.Empty,
                TimestampUtc = _utcNow(),
                RuntimeVersion = runtimeVersion ?? "unknown",
                RuntimeClassification = runtimeClassification ?? "unknown",
                Adapter = adapter ?? "unknown",
                PathHash = pathHash ?? string.Empty,
                DocumentType = documentType ?? "Unknown",
                ActiveConfiguration = activeConfiguration ?? string.Empty,
                DirtyBefore = dirtyBefore,
                DirtyAfter = dirtyAfter,
                IsReadOnly = isReadOnly,
                StateVersionBefore = stateVersionBefore ?? string.Empty,
                StateVersionAfter = stateVersionAfter ?? string.Empty,
                ToolsRequested = (toolsRequested ?? new string[0]).ToList(),
                ToolsExecuted = (toolsExecuted ?? new string[0]).ToList(),
                EvidenceCount = evidence?.Count ?? 0,
                FindingCount = findings?.Count ?? 0,
                ResultStatus = resultStatus ?? "Completed",
                Message = message ?? string.Empty,
                Errors = (errors ?? new AuditError[0]).ToList(),
                SideEffects = sx,
                RollbackReason = rollbackReason ?? string.Empty
            };
        }

        /// <summary>
        /// Create a receipt for a denied run (e.g., policy-blocked before any read attempted).
        /// Per packet §13 (test Receipt_DeniedRun_IsStillRecorded), denied runs are still recorded.
        /// </summary>
        public AuditExecutionReceipt CreateDenied(
            AuditRunRequest request,
            string adapter,
            string runtimeVersion,
            string runtimeClassification,
            string denialReason,
            AuditError deniedError)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return new AuditExecutionReceipt
            {
                OperationId = _newOperationId(),
                CorrelationId = request.CorrelationId ?? string.Empty,
                TimestampUtc = _utcNow(),
                RuntimeVersion = runtimeVersion ?? "unknown",
                RuntimeClassification = runtimeClassification ?? "unknown",
                Adapter = adapter ?? "unknown",
                PathHash = string.Empty,
                DocumentType = "Unknown",
                ActiveConfiguration = string.Empty,
                DirtyBefore = false,
                DirtyAfter = false,
                IsReadOnly = true,
                StateVersionBefore = string.Empty,
                StateVersionAfter = string.Empty,
                ToolsRequested = new List<string>(),
                ToolsExecuted = new List<string>(),
                EvidenceCount = 0,
                FindingCount = 0,
                ResultStatus = "Denied",
                Message = denialReason ?? "Run denied.",
                Errors = deniedError == null ? new List<AuditError>() : new List<AuditError> { deniedError },
                SideEffects = new List<string>(),
                RollbackReason = string.Empty
            };
        }
    }
}
