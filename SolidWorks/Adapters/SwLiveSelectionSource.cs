using System;
using BlueBrick.Audit.Core;
using BlueBrick.SolidWorks.Adapters.Internal;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace BlueBrick.SolidWorks.Adapters
{
    internal sealed class SwLiveSelectionSource : ISwSelectionSource
    {
        private readonly IModelDoc2 _model;
        public SwLiveSelectionSource(IModelDoc2 model) { _model = model; }
        public string GetDocumentIdentityHash()
        {
            try
            {
                var path = _model.GetPathName() ?? string.Empty;
                var h = AuditRedactionService.RedactPath(path);
                return h.PathHash ?? string.Empty;
            }
            catch { return string.Empty; }
        }
        public int GetSelectedObjectCount2(int mark)
        {
            var mgr = _model.ISelectionManager as ISelectionMgr;
            if (mgr == null) throw new InvalidOperationException("ISelectionManager unavailable");
            return mgr.GetSelectedObjectCount2(mark);
        }
        public int GetSelectedObjectType3(int index, int mark)
        {
            var mgr = _model.ISelectionManager as ISelectionMgr;
            if (mgr == null) throw new InvalidOperationException("ISelectionManager unavailable");
            return mgr.GetSelectedObjectType3(index, mark);
        }
        public string GetSafeNameForIndex(int index, int mark)
        {
            try
            {
                var mgr = _model.ISelectionManager as ISelectionMgr;
                if (mgr == null) return string.Empty;
                object obj = null;
                try { obj = mgr.GetSelectedObject6(index, mark); } catch { try { obj = mgr.GetSelectedObject5(index); } catch { obj = null; } }
                if (obj == null) return string.Empty;
                try
                {
                    if (obj is IFeature f) return f.Name ?? string.Empty;
                    if (obj is IComponent2 c) return c.Name2 ?? string.Empty;
                    if (obj is IBody2 b) return b.Name ?? string.Empty;
                    var t = obj.GetType();
                    var p = t.GetProperty("Name");
                    if (p != null) { var v = p.GetValue(obj, null) as string; if (!string.IsNullOrEmpty(v)) return v; }
                    var p2 = t.GetProperty("Name2");
                    if (p2 != null) { var v = p2.GetValue(obj, null) as string; if (!string.IsNullOrEmpty(v)) return v; }
                    var s = obj.ToString();
                    if (!string.IsNullOrEmpty(s) && s.Length > 256) s = s.Substring(0, 256);
                    if (s != null && s.IndexOf("System.__ComObject", StringComparison.OrdinalIgnoreCase) >= 0) return string.Empty;
                    return s ?? string.Empty;
                }
                catch { return string.Empty; }
            }
            catch { return string.Empty; }
        }
        public int GetSelectionMark(int index, int mark)
        {
            try
            {
                var mgr = _model.ISelectionManager as ISelectionMgr;
                if (mgr == null) return mark;
                try { return mgr.GetSelectedObjectMark(index); } catch { return mark; }
            }
            catch { return mark; }
        }
    }
}
