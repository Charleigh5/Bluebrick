using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using BlueBrick.SolidWorks.Adapters.Internal;

namespace BlueBrick.SolidWorks.Adapters
{
    internal sealed class SwLiveDocumentSource : ISwDocumentSource
    {
        private readonly IModelDoc2 _model;
        private readonly ISldWorks _app;
        public SwLiveDocumentSource(IModelDoc2 model, ISldWorks app) { _model = model; _app = app; }
        public ISwCustomPropertySource GetDocumentLevelSource()
        {
            try
            {
                var mgr = _model.Extension.CustomPropertyManager[""];
                return mgr == null ? null : new SwLivePropertySource(mgr, string.Empty, "Document");
            }
            catch { return null; }
        }
        public ISwCustomPropertySource GetConfigurationSource(string configurationName)
        {
            try
            {
                var mgr = _model.Extension.CustomPropertyManager[configurationName ?? string.Empty];
                return mgr == null ? null : new SwLivePropertySource(mgr, configurationName ?? string.Empty, "Configuration");
            }
            catch { return null; }
        }
        public string GetActiveConfigurationName()
        {
            try { return (_model.GetActiveConfiguration() as IConfiguration)?.Name ?? string.Empty; } catch { return string.Empty; }
        }
        public IReadOnlyList<string> GetConfigurationNames()
        {
            try { return (_model.GetConfigurationNames() as string[])?.ToList(); } catch { return null; }
        }
        public bool GetDirty() { try { return _model.GetSaveFlag(); } catch { return false; } }
        public bool GetIsReadOnly() { try { return _model.IsOpenedReadOnly(); } catch { return false; } }
        public string GetDocumentType()
        {
            try
            {
                var t = (swDocumentTypes_e)_model.GetType();
                if (t == swDocumentTypes_e.swDocPART) return "Part";
                if (t == swDocumentTypes_e.swDocASSEMBLY) return "Assembly";
                if (t == swDocumentTypes_e.swDocDRAWING) return "Drawing";
                return "Unknown";
            }
            catch { return "Unknown"; }
        }
        public string GetPath() { try { return _model.GetPathName() ?? string.Empty; } catch { return string.Empty; } }

        private sealed class SwLivePropertySource : ISwCustomPropertySource
        {
            private readonly CustomPropertyManager _mgr;
            public SwLivePropertySource(CustomPropertyManager mgr, string cfg, string scope) { _mgr = mgr; ConfigurationName = cfg; Scope = scope; }
            public string ConfigurationName { get; }
            public string Scope { get; }
            public IReadOnlyList<string> GetPropertyNames()
            {
                try
                {
                    var names = _mgr.GetNames() as string[];
                    return names;
                }
                catch { return null; }
            }
            public bool TryGet(string name, out string rawValue, out string resolvedValue, out bool wasResolved, out string linkedOrExpressionStatus, out string editableStatusWhenAvailable, out string apiStatus, out List<string> limitations)
            {
                rawValue = null; resolvedValue = null; wasResolved = false; linkedOrExpressionStatus = "Unknown"; editableStatusWhenAvailable = "Unknown"; apiStatus = "Get2"; limitations = new List<string>();
                try
                {
                    string v = null, r = null;
                    _mgr.Get2(name, out v, out r);
                    rawValue = v; resolvedValue = r; wasResolved = !string.IsNullOrEmpty(r) && r != v;
                    if (string.IsNullOrEmpty(v) && string.IsNullOrEmpty(r)) return false;
                    return true;
                }
                catch (Exception ex) { limitations.Add("interop_Get2_exception:" + ex.GetType().Name); return false; }
            }
        }
    }
}
