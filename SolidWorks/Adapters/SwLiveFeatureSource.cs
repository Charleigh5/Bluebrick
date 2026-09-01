using System;
using BlueBrick.SolidWorks.Adapters.Internal;
using SolidWorks.Interop.sldworks;

namespace BlueBrick.SolidWorks.Adapters
{
    internal sealed class SwLiveFeatureSource : ISwFeatureSource
    {
        private readonly IModelDoc2 _model;
        public SwLiveFeatureSource(IModelDoc2 model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
        }

        public string GetDocumentIdentityHash()
        {
            try
            {
                var path = _model.GetPathName() ?? string.Empty;
                return BlueBrick.Audit.Core.AuditRedactionService.RedactPath(path).Item1;
            }
            catch { return string.Empty; }
        }

        public ISwFeatureNode GetFirstFeature()
        {
            try
            {
                var feat = _model.FirstFeature() as Feature;
                return feat == null ? null : new SwLiveFeatureNode(feat);
            }
            catch { return null; }
        }

        private sealed class SwLiveFeatureNode : ISwFeatureNode
        {
            private readonly Feature _feat;
            public SwLiveFeatureNode(Feature feat) { _feat = feat; }

            public string GetId()
            {
                try { return _feat.GetID().ToString(); } catch { return string.Empty; }
            }

            public string GetName()
            {
                try { return _feat.Name ?? string.Empty; } catch { return string.Empty; }
            }

            public string GetTypeName()
            {
                try { return _feat.GetTypeName() ?? string.Empty; } catch { return string.Empty; }
            }

            public string GetSuppressionState()
            {
                try
                {
                    var t = _feat.GetType();
                    var m = t.GetMethod("GetSuppression2");
                    if (m != null) { var v = m.Invoke(_feat, null); return v?.ToString() ?? "unknown"; }
                    m = t.GetMethod("GetSuppression");
                    if (m != null) { var v = m.Invoke(_feat, null); return v?.ToString() ?? "unknown"; }
                    return "unknown";
                }
                catch { return "unknown"; }
            }

            public string GetState()
            {
                try
                {
                    var t = _feat.GetType();
                    var m = t.GetMethod("IsSuppressed");
                    if (m != null)
                    {
                        var pars = m.GetParameters();
                        object res = pars.Length == 0 ? m.Invoke(_feat, null) : m.Invoke(_feat, new object[] { false });
                        if (res is bool b) return b ? "suppressed" : "resolved";
                        return res?.ToString() ?? "unknown";
                    }
                    return "unknown";
                }
                catch { return "unknown"; }
            }

            public ISwFeatureNode GetNext()
            {
                try
                {
                    var n = _feat.GetNextFeature() as Feature;
                    return n == null ? null : new SwLiveFeatureNode(n);
                }
                catch { return null; }
            }

            public ISwFeatureNode GetFirstSubFeature()
            {
                try
                {
                    var c = _feat.GetFirstSubFeature() as Feature;
                    return c == null ? null : new SwLiveFeatureNode(c);
                }
                catch { return null; }
            }
        }
    }
}
