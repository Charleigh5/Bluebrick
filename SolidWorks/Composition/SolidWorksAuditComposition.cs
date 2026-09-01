using System;
using System.Diagnostics;
using BlueBrick.Audit.Contracts;
using BlueBrick.Audit.Core;
using BlueBrick.SolidWorks.Adapters;
using BlueBrick.SolidWorks.Adapters.Internal;
using BlueBrick.SolidWorks.Runtime;
using BlueBrick.SolidWorks.Snapshots;
using SolidWorks.Interop.sldworks;

namespace BlueBrick.SolidWorks.Composition
{
    internal sealed class SolidWorksAuditComposition
    {
        private readonly ISldWorks _app;
        private readonly ISolidWorksMainThreadDispatcher _guard;
        private readonly SolidWorksRuntimeInfo _runtime;
        private readonly AuditReceiptFactory _receiptFactory;
        private readonly SolidWorksCustomPropertyReadAdapter _adapter;
        private readonly SolidWorksSelectionReadAdapter _selectionAdapter;
        private readonly SolidWorksFeatureTreeReadAdapter _featureAdapter;
        public SolidWorksAuditComposition(ISldWorks app)
            : this(app, new SolidWorksThreadGuard())
        {
        }
        internal SolidWorksAuditComposition(ISldWorks app, ISolidWorksMainThreadDispatcher guard)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
            _guard = guard ?? throw new ArgumentNullException(nameof(guard));
            string rev = null; try { rev = app.RevisionNumber(); } catch { }
            _runtime = string.IsNullOrEmpty(rev) ? SolidWorksRuntimeInfoFactory.FromInstallRegistry(new SolidWorksVersion()) : SolidWorksRuntimeInfoFactory.FromLiveRevisionNumber(rev);
            _receiptFactory = new AuditReceiptFactory();
            _adapter = new SolidWorksCustomPropertyReadAdapter(_guard, _runtime, _receiptFactory, CreateDocumentSource);
            _selectionAdapter = new SolidWorksSelectionReadAdapter(_guard, _runtime, _receiptFactory, CreateSelectionSource);
            _featureAdapter = new SolidWorksFeatureTreeReadAdapter(_guard, _runtime, _receiptFactory, CreateFeatureSource);
        }
        private ISwDocumentSource CreateDocumentSource()
        {
            try
            {
                var model = _app.IActiveDoc2 as IModelDoc2;
                if (model == null) return null;
                return new SwLiveDocumentSource(model, _app);
            }
            catch { return null; }
        }
        private ISwSelectionSource CreateSelectionSource()
        {
            try
            {
                var model = _app.IActiveDoc2 as IModelDoc2;
                if (model == null) return null;
                return new SwLiveSelectionSource(model);
            }
            catch { return null; }
        }
        private ISwFeatureSource CreateFeatureSource()
        {
            try
            {
                var model = _app.IActiveDoc2 as IModelDoc2;
                if (model == null) return null;
                return new SwLiveFeatureSource(model);
            }
            catch { return null; }
        }
        public AuditRunResult GetActiveDocumentSnapshot(string correlationId, string traceId)
        {
            var sw = Stopwatch.StartNew();
            var cid = string.IsNullOrWhiteSpace(correlationId) ? (traceId ?? Guid.NewGuid().ToString("N")) : correlationId;
            var req = new AuditRunRequest { CorrelationId = cid, Mode = AuditOperationMode.READ_ONLY_ANALYST };
            System.Collections.Generic.List<AuditError> errors;
            PropertyAuditSnapshot snap = null;
            System.Collections.Generic.List<AuditError> dispatchedErrors = null;
            try { _guard.Invoke(() => snap = _adapter.ReadCustomProperties(req, out dispatchedErrors)); errors = dispatchedErrors; }
            catch (SolidWorksThreadViolationException ex)
            {
                errors = new System.Collections.Generic.List<AuditError> { new AuditError { Code = AuditErrorCodes.COM_THREAD_VIOLATION, CorrelationId = cid, Message = ex.Message } };
                snap = new PropertyAuditSnapshot { Identity = new DocumentIdentitySnapshot { DocumentType = "Unknown" }, State = new DocumentStateSnapshot() };
            }
            catch (Exception ex) when (!(ex is SolidWorksThreadViolationException))
            {
                if (ex is InvalidOperationException && ex.Message != null && ex.Message.IndexOf("main thread", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    errors = new System.Collections.Generic.List<AuditError> { new AuditError { Code = AuditErrorCodes.COM_THREAD_VIOLATION, CorrelationId = cid, Message = ex.Message } };
                    snap = new PropertyAuditSnapshot { Identity = new DocumentIdentitySnapshot { DocumentType = "Unknown" }, State = new DocumentStateSnapshot() };
                }
                else throw;
            }
            if (errors == null) errors = new System.Collections.Generic.List<AuditError>();
            if (_runtime == null || string.IsNullOrWhiteSpace(_runtime.Version?.DisplayVersion))
            {
                errors.Add(new AuditError { Code = "BLOCKED", CorrelationId = cid, Message = "Missing runtime — snapshot blocked." });
                sw.Stop();
                var blockedReceipt = _receiptFactory.CreateDenied(req, _adapter.AdapterName, "", _runtime?.Classification.ToString() ?? "UnknownReadOnly", "Missing runtime — snapshot blocked.", errors[errors.Count-1]);
                blockedReceipt.TimestampUtc = DateTime.UtcNow;
                return new AuditRunResult { Snapshot = snap, Errors = errors, Receipt = blockedReceipt, Evidence = new System.Collections.Generic.List<AuditEvidence>(), Findings = new System.Collections.Generic.List<AuditFinding>() };
            }
            sw.Stop();
            var status = errors.Count==0?"Completed": "Partial";
            var message = errors.Count==0?"Snapshot captured": errors.Exists(e => e.Code == AuditErrorCodes.COM_THREAD_VIOLATION) ? "Snapshot partial — COM thread violation (call on main STA thread or marshal via dispatcher)." : "Snapshot partial — some properties unavailable.";
            var receipt = _receiptFactory.Create(req, _adapter.AdapterName, _runtime.Version?.DisplayVersion ?? "", _runtime.Classification.ToString(), snap?.Identity?.DocumentIdentityHash ?? "", snap?.Identity?.DocumentType ?? "Unknown", snap?.Identity?.ActiveConfiguration ?? "", snap?.State?.DirtyBefore ?? false, snap?.State?.DirtyAfter ?? false, snap?.State?.IsReadOnly ?? false, "", AuditStateVersionBuilder.BuildStateVersion(snap), new string[0], new string[0], new System.Collections.Generic.List<AuditEvidence>(), new System.Collections.Generic.List<AuditFinding>(), status, message, errors, new string[0], "");
            var result = new AuditRunResult { Snapshot = snap, Errors = errors, Receipt = receipt, Evidence = new System.Collections.Generic.List<AuditEvidence>(), Findings = new System.Collections.Generic.List<AuditFinding>() };
            return result;
        }
        public SolidWorksRuntimeInfo Runtime => _runtime;
        public ISolidWorksMainThreadDispatcher Guard => _guard;
        public ICustomPropertyReadAdapter Adapter => _adapter;
        public ISelectionReadAdapter SelectionAdapter => _selectionAdapter;
        public IFeatureTreeReadAdapter FeatureAdapter => _featureAdapter;
        public SelectionSnapshot GetSelectionSnapshot(string correlationId, string traceId, out System.Collections.Generic.List<AuditError> errors)
        {
            var cid = string.IsNullOrWhiteSpace(correlationId) ? (traceId ?? Guid.NewGuid().ToString("N")) : correlationId;
            var req = new AuditRunRequest { CorrelationId = cid, Mode = AuditOperationMode.READ_ONLY_ANALYST };
            SelectionSnapshot snapshot = null;
            System.Collections.Generic.List<AuditError> dispatchedErrors = null;
            try { _guard.Invoke(() => snapshot = _selectionAdapter.ReadSelection(req, out dispatchedErrors)); errors = dispatchedErrors; return snapshot; }
            catch (SolidWorksThreadViolationException ex) { errors = new System.Collections.Generic.List<AuditError> { new AuditError { Code = AuditErrorCodes.COM_THREAD_VIOLATION, CorrelationId = cid, Message = ex.Message } }; return new SelectionSnapshot { Count = 0, SelectionType = "None", SafeName = string.Empty, SelectionMark = -1, DocumentIdentityHash = string.Empty, Limitations = new System.Collections.Generic.List<string> { AuditErrorCodes.COM_THREAD_VIOLATION }, Status = "partial", Items = new System.Collections.Generic.List<SelectionEntry>() }; }
        }
        public FeatureTreeSnapshot GetFeatureTreeSnapshot(string correlationId, string traceId, out System.Collections.Generic.List<AuditError> errors)
        {
            var cid = string.IsNullOrWhiteSpace(correlationId) ? (traceId ?? Guid.NewGuid().ToString("N")) : correlationId;
            var req = new AuditRunRequest { CorrelationId = cid, Mode = AuditOperationMode.READ_ONLY_ANALYST };
            FeatureTreeSnapshot snapshot = null;
            System.Collections.Generic.List<AuditError> dispatchedErrors = null;
            try { _guard.Invoke(() => snapshot = _featureAdapter.ReadFeatureTree(req, out dispatchedErrors)); errors = dispatchedErrors; return snapshot; }
            catch (SolidWorksThreadViolationException ex) { errors = new System.Collections.Generic.List<AuditError> { new AuditError { Code = AuditErrorCodes.COM_THREAD_VIOLATION, CorrelationId = cid, Message = ex.Message } }; return new FeatureTreeSnapshot { Features = new System.Collections.Generic.List<FeatureSnapshot>(), Status = "partial", Limitations = new System.Collections.Generic.List<string> { AuditErrorCodes.COM_THREAD_VIOLATION }, DocumentIdentityHash = string.Empty, TotalCount = 0, Truncated = false }; }
        }
    }
}
