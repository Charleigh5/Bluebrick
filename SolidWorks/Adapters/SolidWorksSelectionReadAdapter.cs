using System;
using System.Collections.Generic;
using System.Linq;
using BlueBrick.Audit.Contracts;
using BlueBrick.Audit.Core;
using BlueBrick.SolidWorks.Adapters.Internal;
using BlueBrick.SolidWorks.Runtime;
using BlueBrick.SolidWorks.Snapshots;

namespace BlueBrick.SolidWorks.Adapters
{
    public sealed class SolidWorksSelectionReadAdapter : ISelectionReadAdapter
    {
        private readonly ISolidWorksMainThreadDispatcher _dispatcher;
        private readonly SolidWorksRuntimeInfo _runtimeInfo;
        private readonly AuditReceiptFactory _receiptFactory;
        private readonly Func<ISwSelectionSource> _selectionSourceFactory;
        public string AdapterName => "SolidWorksSelectionReadAdapter";
        internal SolidWorksSelectionReadAdapter(ISolidWorksMainThreadDispatcher dispatcher, SolidWorksRuntimeInfo runtimeInfo, AuditReceiptFactory receiptFactory, Func<ISwSelectionSource> selectionSourceFactory)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _runtimeInfo = runtimeInfo ?? throw new ArgumentNullException(nameof(runtimeInfo));
            _receiptFactory = receiptFactory ?? throw new ArgumentNullException(nameof(receiptFactory));
            _selectionSourceFactory = selectionSourceFactory ?? throw new ArgumentNullException(nameof(selectionSourceFactory));
        }
        public SelectionSnapshot ReadSelection(AuditRunRequest request, out List<AuditError> errors)
        {
            var localErrors = new List<AuditError>();
            errors = localErrors;
            if (request == null)
            {
                localErrors.Add(new AuditError { Code = AuditErrorCodes.INVALID_MODE, Message = "AuditRunRequest was null.", CorrelationId = string.Empty });
                return EmptySnapshot(string.Empty, "partial");
            }
            _dispatcher.VerifyAccess();
            if (request.Mode != AuditOperationMode.READ_ONLY_ANALYST && request.Mode != AuditOperationMode.MOCK)
            {
                localErrors.Add(new AuditError { Code = AuditErrorCodes.INVALID_MODE, CorrelationId = request.CorrelationId, Message = "S3 supports only MOCK or READ_ONLY_ANALYST modes." });
                return EmptySnapshot(string.Empty, "partial");
            }
            ISwSelectionSource src;
            try { src = _selectionSourceFactory(); }
            catch (Exception ex)
            {
                localErrors.Add(new AuditError { Code = AuditErrorCodes.NO_ACTIVE_DOCUMENT, CorrelationId = request.CorrelationId, Message = "No active document/ selection source: " + ex.Message });
                return EmptySnapshot(string.Empty, "empty");
            }
            if (src == null)
            {
                localErrors.Add(new AuditError { Code = AuditErrorCodes.NO_ACTIVE_DOCUMENT, CorrelationId = request.CorrelationId, Message = "No active document in the SOLIDWORKS session." });
                return EmptySnapshot(string.Empty, "empty");
            }
            string docHash = string.Empty;
            try { docHash = src.GetDocumentIdentityHash() ?? string.Empty; } catch (Exception ex) { localErrors.Add(new AuditError { Code = AuditErrorCodes.READ_FAILURE, CorrelationId = request.CorrelationId, Message = "GetDocumentIdentityHash failed: " + ex.Message }); }
            int count;
            try { count = src.GetSelectedObjectCount2(-1); }
            catch (Exception ex)
            {
                localErrors.Add(new AuditError { Code = AuditErrorCodes.READ_FAILURE, CorrelationId = request.CorrelationId, Message = "GetSelectedObjectCount2 failed: " + ex.Message });
                var snap = EmptySnapshot(docHash, "partial");
                snap.Limitations.Add(AuditErrorCodes.READ_FAILURE);
                return snap;
            }
            if (count < 0) count = 0;
            bool truncated = count > SelectionSnapshot.MaxSelectionCount;
            int fetch = truncated ? SelectionSnapshot.MaxSelectionCount : count;
            var items = new List<SelectionEntry>();
            var limitations = new List<string>();
            if (truncated) { limitations.Add(SelectionSnapshot.LimitReachedCode); localErrors.Add(new AuditError { Code = SelectionSnapshot.LimitReachedCode, CorrelationId = request.CorrelationId, Message = "Selection truncated to " + SelectionSnapshot.MaxSelectionCount + " of " + count }); }
            for (int i = 1; i <= fetch; i++)
            {
                int typeInt;
                try { typeInt = src.GetSelectedObjectType3(i, -1); }
                catch (Exception ex)
                {
                    localErrors.Add(new AuditError { Code = AuditErrorCodes.READ_FAILURE, CorrelationId = request.CorrelationId, Scope = "Selection:" + i, Message = "GetSelectedObjectType3 failed at " + i + ": " + ex.Message });
                    limitations.Add(AuditErrorCodes.READ_FAILURE);
                    continue;
                }
                string typeStr = MapType(typeInt);
                if (typeStr == "UNSUPPORTED" || typeStr == "UNKNOWN") limitations.Add("UNSUPPORTED_SELECTION_TYPE");
                string safeName = string.Empty;
                try { safeName = src.GetSafeNameForIndex(i, -1) ?? string.Empty; } catch (Exception ex) { localErrors.Add(new AuditError { Code = AuditErrorCodes.READ_FAILURE, CorrelationId = request.CorrelationId, Scope = "Selection:" + i, Message = "GetSafeName failed at " + i + ": " + ex.Message }); limitations.Add(AuditErrorCodes.READ_FAILURE); safeName = string.Empty; }
                if (safeName.Length > 256) safeName = safeName.Substring(0, 256);
                safeName = AuditRedactionService.RedactSecrets(safeName);
                int mark = -1;
                try { mark = src.GetSelectionMark(i, -1); } catch { mark = -1; }
                items.Add(new SelectionEntry { Index = i, SelectionType = typeStr, SafeName = safeName, SelectionMark = mark });
            }
            string status;
            if (count == 0) status = "empty";
            else if (truncated || localErrors.Exists(e => e.Code == AuditErrorCodes.READ_FAILURE) || limitations.Contains("UNSUPPORTED_SELECTION_TYPE")) status = "partial";
            else if (localErrors.Count > 0) status = "partial";
            else status = "ok";
            if (truncated) status = "partial";
            var first = items.FirstOrDefault();
            var snapshot = new SelectionSnapshot
            {
                Count = count,
                SelectionType = first?.SelectionType ?? (count == 0 ? "None" : "Mixed"),
                SafeName = first?.SafeName ?? string.Empty,
                SelectionMark = first?.SelectionMark ?? -1,
                DocumentIdentityHash = docHash,
                Limitations = limitations.Distinct().ToList(),
                Status = status,
                Items = items
            };
            if (items.Count > 1)
            {
                bool mixed = items.Select(x => x.SelectionType).Distinct(StringComparer.Ordinal).Count() > 1;
                if (mixed) snapshot.SelectionType = "Mixed";
            }
            return snapshot;
        }
        private static SelectionSnapshot EmptySnapshot(string docHash, string status)
        {
            return new SelectionSnapshot { Count = 0, SelectionType = "None", SafeName = string.Empty, SelectionMark = -1, DocumentIdentityHash = docHash ?? string.Empty, Limitations = new List<string>(), Status = status, Items = new List<SelectionEntry>() };
        }
        private static string MapType(int t)
        {
            try
            {
                var e = (global::SolidWorks.Interop.swconst.swSelectType_e)t;
                var name = e.ToString();
                if (string.IsNullOrEmpty(name) || name == t.ToString()) return t == 0 ? "UNKNOWN" : "UNSUPPORTED";
                return name;
            }
            catch { return t == 0 ? "UNKNOWN" : "UNSUPPORTED"; }
        }
    }
}
