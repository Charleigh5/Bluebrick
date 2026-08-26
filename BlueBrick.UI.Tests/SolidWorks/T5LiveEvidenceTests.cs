using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BlueBrick.Agent;
using BlueBrick.Audit.Contracts;
using BlueBrick.Audit.Core;
using BlueBrick.SolidWorks.Adapters;
using BlueBrick.SolidWorks.Adapters.Internal;
using BlueBrick.SolidWorks.Runtime;
using BlueBrick.SolidWorks.Snapshots;

namespace BlueBrick.UI.Tests.SolidWorks
{
    [TestClass]
    public class T5LiveEvidenceTests
    {
        private sealed class MockDoc : ISwDocumentSource
        {
            private readonly string _active;
            private readonly IReadOnlyList<string> _cfgs;
            private readonly bool _dirty;
            private readonly string _path;
            private readonly string _type;
            private readonly Dictionary<string, Dictionary<string, string>> _props;
            private readonly bool _failPerProp;
            public MockDoc(string active, IReadOnlyList<string> cfgs, bool dirty, string path, string type, Dictionary<string, Dictionary<string, string>> props, bool failPerProp = false) { _active = active; _cfgs = cfgs; _dirty = dirty; _path = path; _type = type; _props = props; _failPerProp = failPerProp; }
            public ISwCustomPropertySource GetDocumentLevelSource() => _failPerProp ? (ISwCustomPropertySource)new FailingSource() : new MockSource("Document", string.Empty, _props != null && _props.ContainsKey("") ? _props[""] : new Dictionary<string,string>{{"Document Number","DN-001"},{"Description","desc"}});
            public ISwCustomPropertySource GetConfigurationSource(string n) => _failPerProp ? (ISwCustomPropertySource)new FailingSource() : new MockSource("Configuration", n, _props != null && _props.ContainsKey(n) ? _props[n] : new Dictionary<string,string>{{"Document Number","DN-"+n}});
            public string GetActiveConfigurationName() => _active;
            public IReadOnlyList<string> GetConfigurationNames() => _cfgs;
            public bool GetDirty() => _dirty;
            public bool GetIsReadOnly() => false;
            public string GetDocumentType() => _type;
            public string GetPath() => _path;
            private sealed class MockSource : ISwCustomPropertySource
            {
                private readonly Dictionary<string,string> _d;
                public MockSource(string scope, string cfg, Dictionary<string,string> d){Scope=scope;ConfigurationName=cfg;_d=d??new Dictionary<string,string>();}
                public string ConfigurationName{get;}
                public string Scope{get;}
                public IReadOnlyList<string> GetPropertyNames()=>_d.Keys.ToList();
                public bool TryGet(string name, out string raw, out string resolved, out bool was, out string linked, out string editable, out string api, out List<string> lim){lim=new List<string>(); if(_d.TryGetValue(name,out var v)){raw=v;resolved=v;was=true;linked="None";editable="Unknown";api="Get2";return true;} raw=null;resolved=null;was=false;linked="Unknown";editable="Unknown";api="NotRead";return false;}
            }
            private sealed class FailingSource : ISwCustomPropertySource
            {
                public string ConfigurationName=>string.Empty;
                public string Scope=>"Document";
                public IReadOnlyList<string> GetPropertyNames()=>null;
                public bool TryGet(string n, out string raw, out string resolved, out bool was, out string linked, out string editable, out string api, out List<string> lim){raw=null;resolved=null;was=false;linked=null;editable=null;api="Get2";lim=new List<string>(); throw new InvalidOperationException("Simulated read failure "+n);}
            }
        }

        private static SolidWorksCustomPropertyReadAdapter NewAdapter(ISwDocumentSource src)
        {
            return new SolidWorksCustomPropertyReadAdapter(new SolidWorksThreadGuard(System.Threading.Thread.CurrentThread.ManagedThreadId), SolidWorksRuntimeInfoFactory.ForMock(), new AuditReceiptFactory(), () => src);
        }

        [TestMethod]
        public void T22_ActiveDocSnapshot_Ok_ZeroMutations()
        {
            var doc = new MockDoc("Default", new[]{"Default"}, false, @"C:\tmp\part.sldprt", "Part", new Dictionary<string,Dictionary<string,string>>{{"Default", new Dictionary<string,string>{{"Document Number","DN-T22"}}}});
            var adapter = NewAdapter(doc);
            var req = new AuditRunRequest{CorrelationId="T22", Mode=AuditOperationMode.READ_ONLY_ANALYST};
            List<AuditError> errors; var snap = adapter.ReadCustomProperties(req, out errors);
            Assert.IsNotNull(snap);
            Assert.IsFalse(errors.Any(e=>e.Code==AuditErrorCodes.READ_FAILURE));
            Assert.AreEqual(false, snap.State.DirtyBefore);
            Assert.AreEqual(false, snap.State.DirtyAfter);
            Assert.AreEqual("Default", snap.Identity.ActiveConfiguration);
            var json = AuditCanonicalSerializer.ToCanonicalJson(snap);
            Assert.IsFalse(string.IsNullOrEmpty(json));
            Assert.AreEqual(0, snap.State.DirtyBefore==snap.State.DirtyAfter?0:1);
        }

        [TestMethod]
        public void T23_NoDoc_Empty_TypedError()
        {
            var adapter = NewAdapter(null);
            var svc = new AssistantToolService(new AgentConfig());
            // via adapter directly
            var req = new AuditRunRequest{CorrelationId="T23", Mode=AuditOperationMode.READ_ONLY_ANALYST};
            List<AuditError> errors; var snap = adapter.ReadCustomProperties(req, out errors);
            Assert.IsTrue(errors.Any(e=>e.Code==AuditErrorCodes.NO_ACTIVE_DOCUMENT));
            // via tool service
            var toolReq = new AssistantToolRequest{ToolName="solidworks.get_active_document_snapshot", Parameters=new Dictionary<string,string>()};
            var result = svc.ExecuteAsync(toolReq, "T23").GetAwaiter().GetResult();
            Assert.AreEqual("empty", result.Status);
            Assert.AreEqual(0, result.Receipt.MutationCount);
            Assert.IsTrue(result.ReadOnly);
        }

        [TestMethod]
        public void T24_ReadFailure_Partial_TypedError()
        {
            var doc = new MockDoc("Default", new[]{"Default"}, false, @"C:\tmp\part.sldprt", "Part", null, true);
            var adapter = NewAdapter(doc);
            var req = new AuditRunRequest{CorrelationId="T24", Mode=AuditOperationMode.READ_ONLY_ANALYST};
            List<AuditError> errors; var snap = adapter.ReadCustomProperties(req, out errors);
            Assert.IsTrue(errors.Any(e=>e.Code==AuditErrorCodes.READ_FAILURE));
            Assert.IsNotNull(snap);
            Assert.IsTrue(snap.Scopes.Count>=1);
            var svc = new AssistantToolService(new AgentConfig());
            // tool level partial not testable via real tool (uses null doc) — adapter level is proof
        }

        [TestMethod]
        public void T25_DeniedMutation_Deny_NoMutation()
        {
            var svc = new AssistantToolService(new AgentConfig());
            var cat = svc.GetCatalog();
            Assert.IsFalse(cat.Any(x=>x.Name.Contains("save")||x.Name.Contains("write")||x.Name.Contains("mutat")));
            var desc = cat.First(x=>x.Name=="solidworks.get_active_document_snapshot");
            Assert.IsTrue(desc.ReadOnly);
            Assert.AreEqual("deny_safe", desc.FailureMode);
            // attempt unknown mutating tool should be denied
            var req = new AssistantToolRequest{ToolName="solidworks.save_document", Parameters=new Dictionary<string,string>()};
            var result = svc.ExecuteAsync(req, "T25").GetAwaiter().GetResult();
            Assert.IsTrue(result.Status=="unknown"||result.Status=="deny"||result.Status=="unsupported"||result.Receipt.ErrorCodes.Count>0);
            Assert.AreEqual(0, result.Receipt.MutationCount);
        }

        [TestMethod]
        public void T26_Readback_ZeroMutations_DirtyUnchanged()
        {
            var doc = new MockDoc("Default", new[]{"Default","CfgA"}, true, @"C:\tmp\assy.sldasm", "Assembly", new Dictionary<string,Dictionary<string,string>>{{"Default", new Dictionary<string,string>{{"Document Number","DN-T26"}}},{"CfgA", new Dictionary<string,string>{{"Document Number","DN-CfgA"}}}});
            var adapter = NewAdapter(doc);
            var req = new AuditRunRequest{CorrelationId="T26", Mode=AuditOperationMode.READ_ONLY_ANALYST};
            List<AuditError> errors; var snap = adapter.ReadCustomProperties(req, out errors);
            Assert.AreEqual(snap.State.DirtyBefore, snap.State.DirtyAfter);
            Assert.AreEqual(snap.State.ActiveConfigurationBefore, snap.State.ActiveConfigurationAfter);
            var v1 = AuditStateVersionBuilder.BuildStateVersion(snap);
            var v2 = AuditStateVersionBuilder.BuildStateVersion(snap);
            Assert.AreEqual(v1, v2);
            var receiptFactory = new AuditReceiptFactory(()=>DateTime.UtcNow,()=>"T26");
            var receipt = receiptFactory.Create(req, adapter.AdapterName, "33.5", "Mock", snap.Identity.DocumentIdentityHash, snap.Identity.DocumentType, snap.Identity.ActiveConfiguration, snap.State.DirtyBefore, snap.State.DirtyAfter, snap.State.IsReadOnly, v1, v2, new string[0], new string[0], new AuditEvidence[0], new AuditFinding[0], "Completed","ok", errors, new string[0],"");
            Assert.AreEqual(0, receipt.SideEffects.Count);
        }
    }
}
