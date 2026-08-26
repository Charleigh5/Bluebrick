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
        private readonly SolidWorksThreadGuard _guard;
        private readonly SolidWorksRuntimeInfo _runtime;
        private readonly AuditReceiptFactory _receiptFactory;
        private readonly SolidWorksCustomPropertyReadAdapter _adapter;
        public SolidWorksAuditComposition(ISldWorks app)
        {
            _app = app;
            _guard = new SolidWorksThreadGuard();
            string rev = null; try { rev = app.RevisionNumber(); } catch { }
            _runtime = string.IsNullOrEmpty(rev) ? SolidWorksRuntimeInfoFactory.FromInstallRegistry(new SolidWorksVersion()) : SolidWorksRuntimeInfoFactory.FromLiveRevisionNumber(rev);
            _receiptFactory = new AuditReceiptFactory();
            _adapter = new SolidWorksCustomPropertyReadAdapter(_guard, _runtime, _receiptFactory, CreateDocumentSource);
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
        public AuditRunResult GetActiveDocumentSnapshot(string correlationId, string traceId)
        {
            var sw = Stopwatch.StartNew();
            var cid = string.IsNullOrWhiteSpace(correlationId) ? (traceId ?? Guid.NewGuid().ToString("N")) : correlationId;
            var req = new AuditRunRequest { CorrelationId = cid, Mode = AuditOperationMode.READ_ONLY_ANALYST };
            System.Collections.Generic.List<AuditError> errors;
            PropertyAuditSnapshot snap;
            try { snap = _adapter.ReadCustomProperties(req, out errors); }
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
    }
}
