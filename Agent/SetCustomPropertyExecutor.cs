using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BlueBrick.Audit.Contracts;
using BlueBrick.Audit.Core;
using BlueBrick.SolidWorks.Adapters;
using BlueBrick.SolidWorks.Runtime;
using BlueBrick.SolidWorks.Snapshots;
using Newtonsoft.Json;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace BlueBrick.Agent
{
    // Minimal mutation-lifecycle telemetry sink (Sprint 04 contract C6). Tests inject a fake;
    // production wraps TelemetryLogger on the required (unsampled) path.
    internal interface IMutationTelemetrySink
    {
        void Record(string terminal, bool success, double durationMs, object metadata);
    }

    internal sealed class MutationTelemetryLoggerSink : IMutationTelemetrySink
    {
        private readonly TelemetryLogger _telemetry;

        internal MutationTelemetryLoggerSink(TelemetryLogger telemetry)
        {
            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        }

        public void Record(string terminal, bool success, double durationMs, object metadata)
        {
            _telemetry.LogRequiredEvent("MUTATION_LIFECYCLE", "assistant/mutation", success, durationMs, metadata);
        }
    }

    // Live-CAD session seam for the one mutation (Sprint 04 contract C5/C6). All members run
    // on the main thread inside the executor's single synchronous final unit (or the
    // marshaled pre-prompt check); implementations must not marshal internally. Tests use a
    // scriptable fake that logs every call (zero-COM assertions); production drives COM.
    internal interface ICustomPropertyMutationSession
    {
        string GetActiveDocumentPath();
        bool TryWriteProperty(string name, string value, out string error);
        bool TrySave(out string error);
        bool TryReleaseAndReopen(out string error);
    }

    // Production session over the retained proven document identity. Every method fails
    // closed into an error string; Lab smoke proves the COM vocabulary live.
    internal sealed class SwLiveMutationSession : ICustomPropertyMutationSession
    {
        private readonly IModelDoc2 _model;
        private readonly ISldWorks _app;

        internal SwLiveMutationSession(IModelDoc2 model, ISldWorks app)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _app = app ?? throw new ArgumentNullException(nameof(app));
        }

        public string GetActiveDocumentPath()
        {
            try
            {
                return _model.GetPathName();
            }
            catch
            {
                return null;
            }
        }

        public bool TryWriteProperty(string name, string value, out string error)
        {
            error = null;
            try
            {
                var manager = _model.Extension.CustomPropertyManager[""];
                if (manager == null)
                {
                    error = "CustomPropertyManager unavailable.";
                    return false;
                }
                var status = manager.Add3(
                    name,
                    (int)swCustomInfoType_e.swCustomInfoText,
                    value,
                    (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                if (status == 0)
                {
                    error = "CustomPropertyManager.Add3 reported failure.";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + " during property write.";
                return false;
            }
        }

        public bool TrySave(out string error)
        {
            error = null;
            try
            {
                int errors = 0;
                int warnings = 0;
                var saved = _model.Save3(
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                    ref errors,
                    ref warnings);
                if (!saved || errors != 0)
                {
                    error = "Save3 reported failure.";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + " during save.";
                return false;
            }
        }

        public bool TryReleaseAndReopen(out string error)
        {
            error = null;
            try
            {
                var path = GetActiveDocumentPath();
                if (string.IsNullOrWhiteSpace(path))
                {
                    error = "Active document path unavailable for reopen.";
                    return false;
                }
                _app.CloseDoc(path);
                int errors = 0;
                int warnings = 0;
                var reopened = _app.OpenDoc6(
                    path,
                    (int)swDocumentTypes_e.swDocPART,
                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                    string.Empty,
                    ref errors,
                    ref warnings) as IModelDoc2;
                if (reopened == null || errors != 0)
                {
                    error = "Reopen reported failure.";
                    return false;
                }
                string reopenedPath = null;
                try
                {
                    reopenedPath = reopened.GetPathName();
                }
                catch
                {
                    reopenedPath = null;
                }
                if (!string.Equals(reopenedPath, path, StringComparison.OrdinalIgnoreCase))
                {
                    error = "Reopened path mismatch.";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + " during release/reopen.";
                return false;
            }
        }
    }

    // set_custom_property mutation executor: the activation slice (Sprint 04 contract C2–C7).
    // Single transaction per call; fully seam-injected for deterministic tests. Never touches
    // WithReceipt (all mutation-path terminals use the dedicated receipt seam), never mutates
    // the caller's request, never stores the server token anywhere but a local.
    internal sealed class SetCustomPropertyExecutor
    {
        internal const string ToolName = "solidworks.set_custom_property";
        private const int ConfigurationReadLimit = 64;
        private const int EvidenceIdentifierLimit = 128;

        private static readonly object _busySync = new object();
        private static readonly Dictionary<string, bool> _busyTargets = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        private readonly AgentConfig _config;
        private readonly AssistantToolPolicy _policy;
        private readonly AssistantToolAuditLog _auditLog;
        private readonly IMutationTelemetrySink _telemetrySink;
        private readonly IApprovalLedgerSink _ledgerSink;
        private readonly IApprovalLifecycleTelemetrySink _issuerTelemetrySink;
        private readonly ISolidWorksMainThreadDispatcher _dispatcher;
        private readonly ICustomPropertyReadAdapter _adapter;
        private readonly IReadOnlyList<AssistantToolDescriptor> _catalog;
        private readonly Func<IApprovalPrompt> _promptFactory;
        private readonly Func<IApprovalPrompt, IPreviewBuilder, IApprovalLedgerSink, IApprovalLifecycleTelemetrySink, AssistantApprovalService> _issuerFactory;
        private readonly Func<ICustomPropertyMutationSession> _sessionFactory;
        private readonly bool _isLabBuild;

        internal SetCustomPropertyExecutor(
            AgentConfig config,
            AssistantToolPolicy policy,
            AssistantToolAuditLog auditLog,
            IMutationTelemetrySink telemetrySink,
            IApprovalLedgerSink ledgerSink,
            IApprovalLifecycleTelemetrySink issuerTelemetrySink,
            ISolidWorksMainThreadDispatcher dispatcher,
            ICustomPropertyReadAdapter adapter,
            IReadOnlyList<AssistantToolDescriptor> catalog,
            Func<IApprovalPrompt> promptFactory,
            Func<IApprovalPrompt, IPreviewBuilder, IApprovalLedgerSink, IApprovalLifecycleTelemetrySink, AssistantApprovalService> issuerFactory,
            Func<ICustomPropertyMutationSession> sessionFactory)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _auditLog = auditLog ?? throw new ArgumentNullException(nameof(auditLog));
            _telemetrySink = telemetrySink ?? throw new ArgumentNullException(nameof(telemetrySink));
            _ledgerSink = ledgerSink ?? throw new ArgumentNullException(nameof(ledgerSink));
            _issuerTelemetrySink = issuerTelemetrySink ?? throw new ArgumentNullException(nameof(issuerTelemetrySink));
            // Null dispatcher/adapter is a runtime configuration state (missing composition),
            // not a programmer error: ExecuteAsync denies before prompt instead of throwing.
            _dispatcher = dispatcher;
            _adapter = adapter;
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _promptFactory = promptFactory ?? throw new ArgumentNullException(nameof(promptFactory));
            _issuerFactory = issuerFactory ?? throw new ArgumentNullException(nameof(issuerFactory));
            _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
            _isLabBuild = AppIdentity.IsLabBuild;
        }

#if DEBUG
        // Test-only build-identity override (Sprint 02 C4 precedent): the runtime path above
        // always binds AppIdentity.IsLabBuild. The Lab build (no DEBUG) has no such ctor.
        internal SetCustomPropertyExecutor(
            AgentConfig config,
            AssistantToolPolicy policy,
            AssistantToolAuditLog auditLog,
            IMutationTelemetrySink telemetrySink,
            IApprovalLedgerSink ledgerSink,
            IApprovalLifecycleTelemetrySink issuerTelemetrySink,
            ISolidWorksMainThreadDispatcher dispatcher,
            ICustomPropertyReadAdapter adapter,
            IReadOnlyList<AssistantToolDescriptor> catalog,
            Func<IApprovalPrompt> promptFactory,
            Func<IApprovalPrompt, IPreviewBuilder, IApprovalLedgerSink, IApprovalLifecycleTelemetrySink, AssistantApprovalService> issuerFactory,
            Func<ICustomPropertyMutationSession> sessionFactory,
            bool isLabBuild)
            : this(config, policy, auditLog, telemetrySink, ledgerSink, issuerTelemetrySink, dispatcher, adapter, catalog, promptFactory, issuerFactory, sessionFactory)
        {
            _isLabBuild = isLabBuild;
        }
#endif

        internal async Task<AssistantToolResult> ExecuteAsync(AssistantToolRequest request, string traceId)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            request = request ?? new AssistantToolRequest();
            traceId = traceId ?? string.Empty;

            // Frozen pre-activation terminal vocabulary: gate-off and tool-disabled keep
            // the exact Status strings the descriptor ticket pinned ("disabled"/"unknown").
            if (!GateIsOpen())
            {
                return await FinishTerminal(stopwatch, null, null, null, null, "disabled",
                    "Tool is not enabled.", false, 0, "no-change",
                    "complete", request.ToolName, traceId).ConfigureAwait(false);
            }

            if (_dispatcher == null || _adapter == null)
            {
                return await FinishTerminal(stopwatch, null, null, null, null, "denied",
                    "Denied — no change. (mutation composition unavailable)", false, 0, "no-change",
                    "complete", request.ToolName, traceId).ConfigureAwait(false);
            }

            FrozenRequest frozen;
            string freezeError;
            if (!TryFreeze(request, traceId, out frozen, out freezeError))
            {
                return await FinishTerminal(stopwatch, null, null, null, null, "denied",
                    "Denied — no change. (" + freezeError + ")", false, 0, "no-change",
                    "complete", request.ToolName, traceId).ConfigureAwait(false);
            }

            var descriptor = _catalog.FirstOrDefault(d => string.Equals(d.Name, ToolName, StringComparison.OrdinalIgnoreCase));
            if (descriptor == null)
            {
                return await FinishTerminal(stopwatch, frozen, null, null, null, "unknown",
                    "Unknown assistant tool.", false, 0, "no-change",
                    "complete", ToolName, traceId).ConfigureAwait(false);
            }
            if (!descriptor.Enabled)
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, null, null, "disabled",
                    descriptor.UnavailableReason ?? "Tool is not enabled.", false, 0, "no-change",
                    "complete", ToolName, traceId).ConfigureAwait(false);
            }

            string targetFull;
            string targetError;
            if (!TryValidateTargetPath(frozen.FilePath, out targetFull, out targetError))
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, null, null, "denied",
                    "Denied — no change. (" + targetError + ")", false, 0, "no-change",
                    "complete", ToolName, traceId).ConfigureAwait(false);
            }
            frozen.TargetFullPath = targetFull;
            frozen.TargetId = targetFull;

            var journal = new MutationExecutionJournal(MutationExecutionJournal.DefaultJournalPath(ResolvedTestRoot()));
            try
            {
                MutationExecutionJournal.EnsureScanned(journal.JournalPath);
            }
            catch (MutationJournalCorruptException)
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, null, null, "denied",
                    "Denied — no change. (recovery journal unreadable; all mutation blocked)", false, 0, "no-change",
                    "complete", ToolName, traceId).ConfigureAwait(false);
            }
            if (MutationExecutionJournal.IsBlockedCached(journal.JournalPath, frozen.TargetId)
                || journal.IsBlocked(frozen.TargetId))
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, null, null, "denied",
                    "Denied — no change. (target blocked by unfinished recovery record)", false, 0, "no-change",
                    "complete", ToolName, traceId).ConfigureAwait(false);
            }

            if (!TryEnterTarget(frozen.TargetId))
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, null, null, "denied",
                    "Denied — no change. (approval_in_progress for this target)", false, 0, "no-change",
                    "complete", ToolName, traceId).ConfigureAwait(false);
            }
            // Retained live-target session: resolved once per admission and reused for the
            // pre-prompt path check, the final write unit, and save/reopen. Never re-resolved.
            var session = _sessionFactory();
            if (session == null)
            {
                ReleaseTarget(frozen.TargetId);
                return await FinishTerminal(stopwatch, frozen, descriptor, null, null, "denied",
                    "Denied — no change. (no live target session)", false, 0, "no-change",
                    "complete", ToolName, traceId).ConfigureAwait(false);
            }
            try
            {
                return await RunTransaction(stopwatch, frozen, descriptor, journal, session, traceId).ConfigureAwait(false);
            }
            finally
            {
                ReleaseTarget(frozen.TargetId);
            }
        }

        private async Task<AssistantToolResult> RunTransaction(
            System.Diagnostics.Stopwatch stopwatch,
            FrozenRequest frozen,
            AssistantToolDescriptor descriptor,
            MutationExecutionJournal journal,
            ICustomPropertyMutationSession session,
            string traceId)
        {
            // Pre-prompt evidence: typed proof + destinations, all before any dialog.
            TypedProof proof;
            string proofError;
            if (!TryCaptureProof(frozen, session, out proof, out proofError))
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, null, null, "denied",
                    "Denied — no change. (" + proofError + ")", false, 0, "no-change",
                    "complete", ToolName, traceId).ConfigureAwait(false);
            }
            frozen.BaselineHash = proof.DiskHash;
            frozen.ValidatedLiteral = proof.Literal;
            frozen.ConfigurationInventory = proof.Configurations;

            string evidenceError;
            if (!DestinationsWritable(journal, out evidenceError))
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, null, null, "denied",
                    "Denied — no change. (" + evidenceError + ")", false, 0, "no-change",
                    "complete", ToolName, traceId).ConfigureAwait(false);
            }

            // Bound preview + issuer call. The wrapper cross-checks the frozen Sprint 03
            // builder display against the validated literal; mismatch fails closed.
            var dialog = _promptFactory();
            if (dialog == null)
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, null, null, "denied",
                    "Denied — no change. (prompt unavailable)", false, 0, "no-change",
                    "complete", ToolName, traceId).ConfigureAwait(false);
            }
            var displayBuilder = new CustomPropertyApprovalPreviewBuilder(_adapter, _dispatcher);
            var boundBuilder = new BoundPreviewWrapper(displayBuilder, frozen.ValidatedLiteral);
            var issuer = _issuerFactory(dialog, boundBuilder, _ledgerSink, _issuerTelemetrySink);
            if (issuer == null)
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, null, null, "denied",
                    "Denied — no change. (issuer unavailable)", false, 0, "no-change",
                    "complete", ToolName, traceId).ConfigureAwait(false);
            }
            // Authority binds the frozen request (its RequestId rides inside execRequest);
            // the live traceId travels only as the issuer's correlation parameter.
            var token = await issuer.RequestApprovalAsync(descriptor, frozen.ExecRequest, traceId).ConfigureAwait(false);
            if (token == null)
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, null, null, "denied",
                    "Denied — no change. (approval denied, expired, or orphaned)", false, 0, "no-change",
                    "complete", ToolName, traceId).ConfigureAwait(false);
            }

            // Policy check on frozen values with the server token (never in request.Authorization).
            var capabilityPolicy = _policy.EvaluateCapability(
                descriptor,
                frozen.ExecRequest,
                AssistantToolInvocationSource.AssistantTool,
                token,
                frozen.RequestId,
                frozen.SessionId,
                frozen.Environment,
                DateTime.UtcNow);
            if (!capabilityPolicy.Allowed)
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, token, null, "denied",
                    "Denied — no change. (post-approval policy refusal)", false, 0, "no-change",
                    "complete", ToolName, traceId).ConfigureAwait(false);
            }

            // Post-approval recheck: anything changed means no write and fresh approval.
            // The approved-but-stale token is abandoned to expiry (never returned, never stored).
            TypedProof recheck;
            string recheckError;
            if (!TryCaptureProof(frozen, session, out recheck, out recheckError) || !recheck.Matches(proof))
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, token, null, "denied",
                    "Denied — no change. (target changed after approval; fresh approval required)", false, 0, "no-change",
                    "complete", ToolName, traceId).ConfigureAwait(false);
            }

            // Checkpoint: copy unchanged bytes, verify hash equality.
            var checkpointPath = MutationExecutionJournal.DefaultCheckpointPath(ResolvedTestRoot(), TargetHash(frozen.TargetId), frozen.RequestId);
            try
            {
                var checkpointDirectory = Path.GetDirectoryName(checkpointPath);
                if (!string.IsNullOrWhiteSpace(checkpointDirectory))
                {
                    Directory.CreateDirectory(checkpointDirectory);
                }
                File.Copy(frozen.TargetFullPath, checkpointPath, true);
                var checkpointHash = ComputeFileHash(checkpointPath);
                if (!string.Equals(checkpointHash, frozen.BaselineHash, StringComparison.Ordinal))
                {
                    return await FinishTerminal(stopwatch, frozen, descriptor, token, null, "failed_before_write",
                        "Failed before write. No CAD or disk change was made. (checkpoint hash mismatch)", false, 0, "no-change",
                        "complete", ToolName, traceId).ConfigureAwait(false);
                }
                frozen.CheckpointHash = checkpointHash;
            }
            catch (Exception ex)
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, token, null, "failed_before_write",
                    "Failed before write. No CAD or disk change was made. (checkpoint: " + ex.GetType().Name + ")", false, 0, "no-change",
                    "complete", ToolName, traceId).ConfigureAwait(false);
            }

            // Journal prepared (block in place before anything irreversible).
            try
            {
                journal.Append(StageRecord(frozen, token, MutationStage.Prepared));
            }
            catch (Exception ex)
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, token, null, "failed_before_write",
                    "Failed before write. No CAD or disk change was made. (journal: " + ex.GetType().Name + ")", false, 0, "no-change",
                    "complete", ToolName, traceId).ConfigureAwait(false);
            }

            // Consume the single-use token with ledger evidence. The in-memory mark lands
            // first: even if the ledger append below throws, the token cannot be reused.
            token.ConsumedUtc = DateTime.UtcNow;
            try
            {
                _ledgerSink.Append(ConsumedEntry(frozen, token, traceId));
            }
            catch
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, token, null, "failed_before_write",
                    "Failed before write. No CAD or disk change was made. (consumption evidence)", false, 0, "no-change",
                    "complete", ToolName, traceId).ConfigureAwait(false);
            }

            try
            {
                journal.Append(StageRecord(frozen, token, MutationStage.WriteStarted));
            }
            catch (Exception ex)
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, token, null, "failed_before_write",
                    "Failed before write. No CAD or disk change was made. (journal: " + ex.GetType().Name + ")", false, 0, "no-change",
                    "complete", ToolName, traceId).ConfigureAwait(false);
            }

            // Final unit: ONE synchronous main-thread dispatch holding recheck + COM write
            // with no await, prompt, marshal-boundary, or unvalidated lookup between them.
            // The session is the admission-retained identity (never re-resolved).
            string finalOutcome = null;
            string finalError = null;
            bool marshaled = false;
            try
            {
                marshaled = _dispatcher.TryInvoke(() =>
                {
                    TypedProof finalProof;
                    string finalProofError;
                    if (!TryCaptureProofInline(frozen, session, out finalProof, out finalProofError) || !finalProof.Matches(proof))
                    {
                        finalOutcome = "check-failed";
                        finalError = finalProofError ?? "final recheck mismatch";
                        return;
                    }
                    string writeError;
                    if (!session.TryWriteProperty(frozen.Property, frozen.NewValue, out writeError))
                    {
                        finalOutcome = "write-failed";
                        finalError = writeError ?? "property write failed";
                        return;
                    }
                    finalOutcome = "ok";
                });
            }
            catch (Exception ex)
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, token, null, "failed_before_write",
                    "Failed before write. No CAD or disk change was made. (dispatch: " + ex.GetType().Name + ")", false, 0, "no-change",
                    "complete", ToolName, traceId).ConfigureAwait(false);
            }
            if (!marshaled)
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, token, null, "failed_before_write",
                    "Failed before write. No CAD or disk change was made. (main-thread marshal unavailable)", false, 0, "no-change",
                    "complete", ToolName, traceId).ConfigureAwait(false);
            }
            if (string.Equals(finalOutcome, "check-failed", StringComparison.Ordinal))
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, token, null, "failed_before_write",
                    "Failed before write. No CAD or disk change was made. (" + finalError + ")", false, 0, "no-change",
                    "complete", ToolName, traceId).ConfigureAwait(false);
            }
            if (!string.Equals(finalOutcome, "ok", StringComparison.Ordinal))
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, token, checkpointPath, "save_uncertain",
                    "In-memory change or save uncertain — recovery required. (" + (finalError ?? "property write failed") + ")", false, 1, "uncertain",
                    "complete", ToolName, traceId).ConfigureAwait(false);
            }
            frozen.MutatorInvoked = true;

            // save_attempted persists BEFORE the save (order pinned).
            try
            {
                journal.Append(StageRecord(frozen, token, MutationStage.SaveAttempted));
            }
            catch (Exception ex)
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, token, checkpointPath, "save_uncertain",
                    "In-memory change or save uncertain — recovery required. (journal: " + ex.GetType().Name + ")", false, 1, "uncertain",
                    "complete", ToolName, traceId).ConfigureAwait(false);
            }
            string saveError;
            if (!session.TrySave(out saveError))
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, token, checkpointPath, "save_uncertain",
                    "In-memory change or save uncertain — recovery required. (" + (saveError ?? "save failed") + ")", false, 1, "uncertain",
                    "complete", ToolName, traceId).ConfigureAwait(false);
            }

            string reopenError;
            if (!session.TryReleaseAndReopen(out reopenError))
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, token, checkpointPath, "save_uncertain",
                    "In-memory change or save uncertain — recovery required. (" + (reopenError ?? "reopen failed") + ")", false, 1, "uncertain",
                    "complete", ToolName, traceId).ConfigureAwait(false);
            }
            TypedProof verify;
            string verifyError;
            if (!TryCaptureProof(frozen, session, out verify, out verifyError)
                || !string.Equals(verify.Literal, frozen.NewValue, StringComparison.Ordinal)
                || !string.Equals(verify.DocumentPath, frozen.TargetFullPath, StringComparison.OrdinalIgnoreCase))
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, token, checkpointPath, "save_uncertain",
                    "In-memory change or save uncertain — recovery required. (" + (verifyError ?? "reopened value mismatch") + ")", false, 1, "uncertain",
                    "complete", ToolName, traceId).ConfigureAwait(false);
            }
            try
            {
                journal.Append(StageRecord(frozen, token, MutationStage.SavedVerified));
            }
            catch
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, token, checkpointPath, "evidence_incomplete",
                    "Saved but evidence incomplete — reconciliation required. (journal)", true, 1, "saved-verified",
                    "incomplete", ToolName, traceId).ConfigureAwait(false);
            }

            string receiptError;
            var appliedReceipt = BuildReceipt(stopwatch, frozen, descriptor, token, "applied", "Saved and verified.");
            if (!TryPersistReceipt(appliedReceipt, out receiptError))
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, token, checkpointPath, "evidence_incomplete",
                    "Saved but evidence incomplete — reconciliation required. (" + receiptError + ")", true, 1, "saved-verified",
                    "incomplete", ToolName, traceId).ConfigureAwait(false);
            }

            // Required telemetry before the evidence_complete journal write, so a telemetry
            // failure still lands on a journal state consistent with the terminal.
            try
            {
                _telemetrySink.Record("applied", true, stopwatch.Elapsed.TotalMilliseconds, new
                {
                    traceId = traceId ?? string.Empty,
                    requestId = frozen.RequestId,
                    sessionId = frozen.SessionId,
                    approvalId = token != null ? token.ApprovalId ?? string.Empty : string.Empty,
                    receiptId = appliedReceipt.ReceiptId,
                    physical = "saved-verified",
                    evidence = "complete",
                    checkpoint = checkpointPath ?? string.Empty
                });
            }
            catch
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, token, checkpointPath, "evidence_incomplete",
                    "Saved but evidence incomplete — reconciliation required. (telemetry)", true, 1, "saved-verified",
                    "incomplete", ToolName, traceId).ConfigureAwait(false);
            }

            try
            {
                journal.Append(StageRecord(frozen, token, MutationStage.EvidenceComplete));
            }
            catch
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, token, checkpointPath, "evidence_incomplete",
                    "Saved but evidence incomplete — reconciliation required. (journal)", true, 1, "saved-verified",
                    "incomplete", ToolName, traceId).ConfigureAwait(false);
            }

            return FinishApplied(frozen, descriptor, token, checkpointPath, appliedReceipt, traceId);
        }

        private bool GateIsOpen()
        {
            return _isLabBuild && (_config.Assistant != null && _config.Assistant.Mutations != null && _config.Assistant.Mutations.Enabled);
        }

        private string ResolvedTestRoot()
        {
            var root = _config.Assistant != null && _config.Assistant.Mutations != null ? _config.Assistant.Mutations.TestFileRoot : null;
            if (string.IsNullOrWhiteSpace(root))
            {
                root = Path.Combine(AppIdentity.DefaultWorkingFolder, "AssistantTestFiles");
            }
            try
            {
                return Path.GetFullPath(root);
            }
            catch
            {
                return string.Empty;
            }
        }

        private bool TryFreeze(AssistantToolRequest request, string traceId, out FrozenRequest frozen, out string error)
        {
            frozen = null;
            error = null;
            string toolName = request.ToolName ?? string.Empty;
            string query = request.Query ?? string.Empty;
            int limit = request.Limit;
            string requestId = string.IsNullOrWhiteSpace(request.RequestId) ? traceId : request.RequestId;
            string sessionId = request.SessionId ?? string.Empty;
            string environment = _isLabBuild ? "Lab" : "Production";
            Dictionary<string, string> parameters = request.Parameters == null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(request.Parameters, StringComparer.Ordinal);
            string filePath;
            string property;
            string value;
            if (!parameters.TryGetValue("file_path", out filePath) || string.IsNullOrWhiteSpace(filePath)
                || !parameters.TryGetValue("property", out property) || string.IsNullOrWhiteSpace(property)
                || !parameters.TryGetValue("value", out value) || string.IsNullOrWhiteSpace(value))
            {
                error = "invalid_request";
                return false;
            }
            string scopeId;
            try
            {
                scopeId = AssistantScopeRegistry.Resolve(_config, _catalog, request.ScopeId).Id;
            }
            catch
            {
                scopeId = AssistantScopeRegistry.Normalize(request.ScopeId);
            }
            if (string.IsNullOrWhiteSpace(requestId))
            {
                error = "invalid_request";
                return false;
            }
            var execRequest = new AssistantToolRequest
            {
                ToolName = toolName,
                Query = query,
                Limit = limit,
                ScopeId = scopeId,
                SessionId = sessionId,
                RequestId = requestId,
                Environment = environment,
                Authorization = AssistantToolAuthorization.None(),
                Parameters = parameters
            };
            frozen = new FrozenRequest
            {
                ExecRequest = execRequest,
                RequestId = requestId,
                SessionId = sessionId,
                Environment = environment,
                FilePath = filePath.Trim(),
                Property = property.Trim(),
                NewValue = value,
                TraceId = traceId ?? string.Empty
            };
            return true;
        }

        private bool TryValidateTargetPath(string filePath, out string fullPath, out string error)
        {
            fullPath = null;
            error = null;
            string root = ResolvedTestRoot();
            if (string.IsNullOrWhiteSpace(root))
            {
                error = "test root unresolvable";
                return false;
            }
            string candidate;
            try
            {
                candidate = Path.GetFullPath(filePath);
            }
            catch
            {
                error = "path malformed";
                return false;
            }
            string prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                error = "target outside test root";
                return false;
            }
            if (!string.Equals(Path.GetExtension(candidate), ".sldprt", StringComparison.OrdinalIgnoreCase))
            {
                error = "not a test part";
                return false;
            }
            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(candidate);
            }
            catch
            {
                error = "target missing";
                return false;
            }
            if ((attributes & FileAttributes.Directory) != 0)
            {
                error = "target is a directory";
                return false;
            }
            if ((attributes & FileAttributes.ReadOnly) != 0)
            {
                error = "target read-only or vault-locked";
                return false;
            }
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                error = "target is a reparse point";
                return false;
            }
            try
            {
                using (var probe = new FileStream(candidate, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                }
            }
            catch
            {
                error = "target locked";
                return false;
            }
            fullPath = candidate;
            return true;
        }

        private bool TryCaptureProof(FrozenRequest frozen, ICustomPropertyMutationSession session, out TypedProof proof, out string error)
        {
            proof = null;
            error = null;
            TypedProof captured = null;
            string capturedError = null;
            bool marshaled = false;
            try
            {
                marshaled = _dispatcher.TryInvoke(() =>
                {
                    if (!TryCaptureProofInline(frozen, session, out captured, out capturedError))
                    {
                        captured = null;
                    }
                });
            }
            catch (Exception ex)
            {
                error = "proof dispatch: " + ex.GetType().Name;
                return false;
            }
            if (!marshaled)
            {
                error = "main-thread marshal unavailable";
                return false;
            }
            if (captured == null)
            {
                error = capturedError ?? "typed proof unavailable";
                return false;
            }
            proof = captured;
            return true;
        }

        private bool TryCaptureProofInline(FrozenRequest frozen, ICustomPropertyMutationSession session, out TypedProof proof, out string error)
        {
            proof = null;
            error = null;
            var auditRequest = new AuditRunRequest
            {
                Mode = AuditOperationMode.READ_ONLY_ANALYST,
                RequestedPropertyNames = new List<string> { frozen.Property },
                CorrelationId = frozen.RequestId,
                ReadAllConfigurations = true,
                ConfigurationReadLimit = ConfigurationReadLimit
            };
            List<AuditError> errors;
            PropertyAuditSnapshot snapshot;
            try
            {
                snapshot = _adapter.ReadCustomProperties(auditRequest, out errors);
            }
            catch (Exception ex)
            {
                error = "adapter: " + ex.GetType().Name;
                return false;
            }
            if (snapshot == null)
            {
                error = "adapter returned no snapshot";
                return false;
            }
            errors = errors ?? new List<AuditError>();
            if (errors.Count != 0)
            {
                error = "adapter errors present";
                return false;
            }
            if (snapshot.Scopes == null)
            {
                error = "snapshot has no scopes";
                return false;
            }
            if (snapshot.Limitations != null && snapshot.Limitations.Count != 0)
            {
                error = "snapshot limitations present";
                return false;
            }
            string wanted = frozen.Property.Trim().ToLowerInvariant();
            PropertyScopeSnapshot documentScope = null;
            foreach (var scope in snapshot.Scopes)
            {
                if (scope == null || scope.Properties == null) continue;
                if (scope.Limitations != null && scope.Limitations.Count != 0)
                {
                    error = "scope limitations present";
                    return false;
                }
                foreach (var candidate in scope.Properties)
                {
                    if (candidate == null) continue;
                    string normalized = candidate.NormalizedName;
                    if (string.IsNullOrEmpty(normalized))
                    {
                        normalized = (candidate.Name ?? string.Empty).Trim().ToLowerInvariant();
                    }
                    if (!string.Equals(normalized, wanted, StringComparison.Ordinal)) continue;
                    bool isConfiguration = string.Equals(scope.Scope, "Configuration", StringComparison.OrdinalIgnoreCase);
                    if (isConfiguration)
                    {
                        error = "same-name override in configuration scope";
                        return false;
                    }
                    if (documentScope != null)
                    {
                        error = "duplicate document-level match";
                        return false;
                    }
                    if (candidate.WasResolved != true)
                    {
                        error = "value unresolved";
                        return false;
                    }
                    if (candidate.RawValue == null || candidate.ResolvedValue == null
                        || !string.Equals(candidate.RawValue, candidate.ResolvedValue, StringComparison.Ordinal))
                    {
                        error = "raw/resolved divergence";
                        return false;
                    }
                    if (!string.Equals(candidate.LinkedOrExpressionStatus, "None", StringComparison.Ordinal))
                    {
                        error = "linked or expression value";
                        return false;
                    }
                    if (string.IsNullOrWhiteSpace(candidate.EditableStatusWhenAvailable)
                        || string.Equals(candidate.EditableStatusWhenAvailable, "Unknown", StringComparison.OrdinalIgnoreCase))
                    {
                        error = "editability unknown";
                        return false;
                    }
                    documentScope = scope;
                    proof = new TypedProof
                    {
                        Literal = candidate.ResolvedValue,
                        Configurations = snapshot.State != null && snapshot.State.AvailableConfigurations != null
                            ? new List<string>(snapshot.State.AvailableConfigurations)
                            : new List<string>()
                    };
                }
            }
            if (proof == null)
            {
                error = "property absent";
                return false;
            }
            var state = snapshot.State;
            if (state == null || state.DirtyBefore || state.DirtyAfter || state.IsReadOnly)
            {
                error = "document not clean";
                proof = null;
                return false;
            }
            string diskHash;
            try
            {
                diskHash = ComputeFileHash(frozen.TargetFullPath);
            }
            catch (Exception ex)
            {
                error = "disk hash: " + ex.GetType().Name;
                proof = null;
                return false;
            }
            proof.DiskHash = diskHash;
            string sessionPath = null;
            try
            {
                if (session != null)
                {
                    sessionPath = session.GetActiveDocumentPath();
                }
            }
            catch
            {
                sessionPath = null;
            }
            if (string.IsNullOrWhiteSpace(sessionPath))
            {
                error = "no live target session";
                proof = null;
                return false;
            }
            if (!string.Equals(sessionPath, frozen.TargetFullPath, StringComparison.OrdinalIgnoreCase))
            {
                error = "active document mismatch";
                proof = null;
                return false;
            }
            proof.DocumentPath = sessionPath;
            return true;
        }

        private bool DestinationsWritable(MutationExecutionJournal journal, out string error)
        {
            error = null;
            if (!IsDirectoryWritable(Path.GetDirectoryName(journal.JournalPath)))
            {
                error = "execution journal unwritable";
                return false;
            }
            if (!IsDirectoryWritable(Path.GetDirectoryName(MutationExecutionJournal.DefaultCheckpointPath(ResolvedTestRoot(), "probe", "probe"))))
            {
                error = "checkpoint location unwritable";
                return false;
            }
            if (string.IsNullOrWhiteSpace(_auditLog.CurrentLogPath()))
            {
                error = "tool audit destination unconfigured";
                return false;
            }
            try
            {
                var directory = Path.GetDirectoryName(_auditLog.CurrentLogPath());
                if (string.IsNullOrWhiteSpace(directory)) return false;
                Directory.CreateDirectory(directory);
            }
            catch
            {
                error = "tool audit destination unwritable";
                return false;
            }
            return true;
        }

        private static bool IsDirectoryWritable(string directory)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(directory)) return false;
                Directory.CreateDirectory(directory);
                var probe = Path.Combine(directory, ".writability");
                File.WriteAllBytes(probe, new byte[0]);
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private MutationJournalRecord StageRecord(FrozenRequest frozen, AssistantToolAuthorization token, string stage)
        {
            return new MutationJournalRecord
            {
                TargetId = frozen.TargetId,
                RequestId = frozen.RequestId,
                ApprovalId = token != null ? token.ApprovalId ?? string.Empty : string.Empty,
                Stage = stage,
                BaselineHash = frozen.BaselineHash ?? string.Empty,
                CheckpointHash = frozen.CheckpointHash ?? string.Empty
            };
        }

        private AssistantApprovalLedgerEntry ConsumedEntry(FrozenRequest frozen, AssistantToolAuthorization token, string traceId)
        {
            return new AssistantApprovalLedgerEntry
            {
                TimestampUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                TraceId = Evidence(traceId),
                SessionId = Evidence(frozen.SessionId),
                ApprovalId = Evidence(token.ApprovalId),
                CapabilityId = Evidence(token.CapabilityId),
                ArgumentDigest = token.ArgumentDigest,
                Environment = Evidence(token.Environment),
                Event = AssistantApprovalLedgerEntry.Consumed,
                Outcome = "consumed"
            };
        }

        private static string Evidence(string value)
        {
            var redacted = AuditRedactionService.RedactSecrets(value ?? string.Empty);
            return redacted.Length <= EvidenceIdentifierLimit ? redacted : redacted.Substring(0, EvidenceIdentifierLimit);
        }

        // Durable acknowledgement on existing APIs only (contract C6): append-flush plus
        // same-receipt-ID disk read-back. The receipt object passed in is the one returned
        // to the caller — never a second orphan receipt.
        private bool TryPersistReceipt(AssistantToolExecutionReceipt receipt, out string error)
        {
            error = null;
            if (receipt == null)
            {
                error = "no receipt";
                return false;
            }
            try
            {
                _auditLog.Record(receipt);
            }
            catch (Exception ex)
            {
                error = "audit append: " + ex.GetType().Name;
                return false;
            }
            try
            {
                var persisted = _auditLog.TailPersisted(25);
                foreach (var candidate in persisted)
                {
                    if (candidate != null && string.Equals(candidate.ReceiptId, receipt.ReceiptId, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
                error = "receipt not on disk";
                return false;
            }
            catch (Exception ex)
            {
                error = "receipt read-back: " + ex.GetType().Name;
                return false;
            }
        }

        private AssistantToolExecutionReceipt BuildReceipt(
            System.Diagnostics.Stopwatch stopwatch,
            FrozenRequest frozen,
            AssistantToolDescriptor descriptor,
            AssistantToolAuthorization token,
            string status,
            string message)
        {
            return new AssistantToolExecutionReceipt
            {
                ReceiptId = Guid.NewGuid().ToString("N"),
                TimestampUtc = DateTime.UtcNow,
                CorrelationId = frozen.TraceId,
                TraceId = frozen.TraceId,
                ToolName = ToolName,
                RequestId = frozen.RequestId,
                SessionId = frozen.SessionId,
                Environment = frozen.Environment,
                CapabilityId = descriptor != null ? descriptor.CapabilityId ?? descriptor.Name ?? string.Empty : string.Empty,
                ExecutionBoundary = descriptor != null ? descriptor.ExecutionBoundary ?? "assistant_tool_service" : "assistant_tool_service",
                AuthorizationState = token != null ? "server_issued" : "none",
                Mode = "HUMAN_APPROVED_MUTATION",
                Version = "1",
                DocumentType = "Unknown",
                State = status,
                DurationMs = stopwatch.Elapsed.TotalMilliseconds,
                MutationCount = frozen.MutatorInvoked ? 1 : 0,
                RiskLevel = descriptor != null ? descriptor.RiskLevel ?? "unknown" : "unknown",
                Allowed = token != null,
                ReadOnly = false,
                ApprovalRequired = true,
                ApprovalGranted = token != null,
                ApprovalId = token != null ? token.ApprovalId ?? string.Empty : string.Empty,
                PolicyCode = token != null ? "capability_allow" : "approval_required",
                ResultStatus = status,
                Message = status,
                InputSummary = new Dictionary<string, string>
                {
                    { "parameterCount", "3" },
                    { "property", frozen.Property ?? string.Empty }
                }
            };
        }

        private async Task<AssistantToolResult> FinishTerminal(
            System.Diagnostics.Stopwatch stopwatch,
            FrozenRequest frozen,
            AssistantToolDescriptor descriptor,
            AssistantToolAuthorization token,
            string checkpointPath,
            string status,
            string message,
            bool telemetrySuccess,
            int attemptCount,
            string physical,
            string evidence,
            string toolName,
            string traceId)
        {
            string requestId = frozen != null ? frozen.RequestId : traceId;
            string receiptId = string.Empty;
            var receipt = new AssistantToolExecutionReceipt
            {
                ReceiptId = Guid.NewGuid().ToString("N"),
                TimestampUtc = DateTime.UtcNow,
                CorrelationId = traceId ?? string.Empty,
                TraceId = traceId ?? string.Empty,
                ToolName = toolName ?? ToolName,
                RequestId = requestId ?? string.Empty,
                SessionId = frozen != null ? frozen.SessionId : string.Empty,
                Environment = frozen != null ? frozen.Environment : string.Empty,
                CapabilityId = descriptor != null ? descriptor.CapabilityId ?? descriptor.Name ?? string.Empty : string.Empty,
                ExecutionBoundary = "assistant_tool_service",
                AuthorizationState = token != null ? "server_issued" : "none",
                Mode = "HUMAN_APPROVED_MUTATION",
                Version = "1",
                DocumentType = "Unknown",
                State = status,
                DurationMs = stopwatch.Elapsed.TotalMilliseconds,
                MutationCount = attemptCount,
                RiskLevel = descriptor != null ? descriptor.RiskLevel ?? "unknown" : "unknown",
                Allowed = token != null,
                ReadOnly = false,
                ApprovalRequired = true,
                ApprovalGranted = token != null,
                ApprovalId = token != null ? token.ApprovalId ?? string.Empty : string.Empty,
                PolicyCode = token != null ? "capability_allow" : "approval_required",
                ResultStatus = status,
                Message = message,
                InputSummary = frozen != null
                    ? new Dictionary<string, string> { { "parameterCount", "3" }, { "property", frozen.Property ?? string.Empty } }
                    : new Dictionary<string, string>()
            };
            receiptId = receipt.ReceiptId;
            try
            {
                _auditLog.Record(receipt);
            }
            catch
            {
            }
            try
            {
                _telemetrySink.Record(status, telemetrySuccess, stopwatch.Elapsed.TotalMilliseconds, new
                {
                    traceId = traceId ?? string.Empty,
                    requestId = requestId ?? string.Empty,
                    sessionId = frozen != null ? frozen.SessionId : string.Empty,
                    approvalId = token != null ? token.ApprovalId ?? string.Empty : string.Empty,
                    receiptId = receiptId,
                    physical = physical,
                    evidence = evidence,
                    checkpoint = checkpointPath ?? string.Empty
                });
            }
            catch
            {
            }
            var items = new List<AssistantToolResultItem>
            {
                new AssistantToolResultItem
                {
                    Id = "mutation",
                    Title = frozen != null ? ("Set " + frozen.Property + " in " + DisplayName(frozen)) : "Set custom property",
                    Subtitle = message,
                    Source = "mutation",
                    Metadata = new Dictionary<string, string>
                    {
                        ["physical"] = physical,
                        ["evidence"] = evidence,
                        ["receiptId"] = receiptId,
                        ["requestId"] = requestId ?? string.Empty,
                        ["approvalId"] = token != null ? token.ApprovalId ?? string.Empty : string.Empty,
                        ["target"] = frozen != null ? DisplayName(frozen) : string.Empty,
                        ["property"] = frozen != null ? frozen.Property ?? string.Empty : string.Empty
                    }
                }
            };
            if (frozen != null && !string.IsNullOrEmpty(checkpointPath))
            {
                items[0].Metadata["recovery"] = "receipt " + receiptId + " / checkpoint " + checkpointPath;
            }
            await Task.FromResult(0).ConfigureAwait(false);
            return new AssistantToolResult
            {
                ToolName = toolName ?? ToolName,
                Status = status,
                Message = message,
                ReadOnly = false,
                TraceId = traceId ?? string.Empty,
                Items = items,
                Receipt = receipt
            };
        }

        // Applied return for the already-persisted-and-acked receipt with telemetry
        // already recorded: builds items and returns the result WITHOUT re-recording.
        private AssistantToolResult FinishApplied(
            FrozenRequest frozen,
            AssistantToolDescriptor descriptor,
            AssistantToolAuthorization token,
            string checkpointPath,
            AssistantToolExecutionReceipt receipt,
            string traceId)
        {
            var items = new List<AssistantToolResultItem>
            {
                new AssistantToolResultItem
                {
                    Id = "mutation",
                    Title = "Set " + frozen.Property + " in " + DisplayName(frozen),
                    Subtitle = "Saved and verified.",
                    Source = "mutation",
                    Metadata = new Dictionary<string, string>
                    {
                        ["physical"] = "saved-verified",
                        ["evidence"] = "complete",
                        ["receiptId"] = receipt != null ? receipt.ReceiptId ?? string.Empty : string.Empty,
                        ["requestId"] = frozen.RequestId,
                        ["approvalId"] = token != null ? token.ApprovalId ?? string.Empty : string.Empty,
                        ["target"] = DisplayName(frozen),
                        ["property"] = frozen.Property ?? string.Empty,
                        ["recovery"] = "receipt " + (receipt != null ? receipt.ReceiptId ?? string.Empty : string.Empty) + " / checkpoint " + (checkpointPath ?? string.Empty)
                    }
                }
            };
            return new AssistantToolResult
            {
                ToolName = ToolName,
                Status = "applied",
                Message = "Saved and verified.",
                ReadOnly = false,
                TraceId = traceId ?? string.Empty,
                Items = items,
                Receipt = receipt
            };
        }

        private static string DisplayName(FrozenRequest frozen)
        {
            string basename = null;
            try
            {
                basename = AuditRedactionService.RedactPath(frozen.TargetFullPath).Basename;
            }
            catch
            {
                basename = null;
            }
            if (string.IsNullOrWhiteSpace(basename)) basename = "unknown file";
            basename = basename.Trim();
            return basename.Length <= 80 ? basename : basename.Substring(0, 80);
        }

        private static bool TryEnterTarget(string targetId)
        {
            lock (_busySync)
            {
                if (_busyTargets.ContainsKey(targetId)) return false;
                _busyTargets[targetId] = true;
                return true;
            }
        }

        private static void ReleaseTarget(string targetId)
        {
            lock (_busySync)
            {
                _busyTargets.Remove(targetId);
            }
        }

        private static string TargetHash(string targetId)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(targetId ?? string.Empty);
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static string ComputeFileHash(string path)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = File.ReadAllBytes(path);
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private sealed class FrozenRequest
        {
            internal AssistantToolRequest ExecRequest { get; set; }
            internal string RequestId { get; set; }
            internal string SessionId { get; set; }
            internal string Environment { get; set; }
            internal string FilePath { get; set; }
            internal string Property { get; set; }
            internal string NewValue { get; set; }
            internal string TraceId { get; set; }
            internal string TargetFullPath { get; set; }
            internal string TargetId { get; set; }
            internal string BaselineHash { get; set; }
            internal string ValidatedLiteral { get; set; }
            internal List<string> ConfigurationInventory { get; set; }
            internal string CheckpointHash { get; set; }
            internal bool MutatorInvoked { get; set; }
        }

        private sealed class TypedProof
        {
            internal string Literal { get; set; }
            internal string DiskHash { get; set; }
            internal string DocumentPath { get; set; }
            internal List<string> Configurations { get; set; }

            internal bool Matches(TypedProof other)
            {
                if (other == null) return false;
                if (!string.Equals(Literal, other.Literal, StringComparison.Ordinal)) return false;
                if (!string.Equals(DiskHash, other.DiskHash, StringComparison.Ordinal)) return false;
                if (!string.Equals(DocumentPath, other.DocumentPath, StringComparison.OrdinalIgnoreCase)) return false;
                var left = Configurations ?? new List<string>();
                var right = other.Configurations ?? new List<string>();
                if (left.Count != right.Count) return false;
                for (int i = 0; i < left.Count; i++)
                {
                    if (!string.Equals(left[i], right[i], StringComparison.Ordinal)) return false;
                }
                return true;
            }
        }

        // Bound preview wrapper: rechecks via the accepted frozen builder and fails closed
        // unless its displayed Before equals the independently validated literal.
        private sealed class BoundPreviewWrapper : IPreviewBuilder
        {
            private readonly IPreviewBuilder _inner;
            private readonly string _validatedLiteral;

            internal BoundPreviewWrapper(IPreviewBuilder inner, string validatedLiteral)
            {
                _inner = inner;
                _validatedLiteral = validatedLiteral;
            }

            public ApprovalPreview Build(AssistantToolDescriptor descriptor, AssistantToolRequest request)
            {
                var preview = _inner.Build(descriptor, request);
                if (preview == null) return null;
                if (!string.Equals(preview.Before, _validatedLiteral, StringComparison.Ordinal)) return null;
                return preview;
            }
        }
    }
}
