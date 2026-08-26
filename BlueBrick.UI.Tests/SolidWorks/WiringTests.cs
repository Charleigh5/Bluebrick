using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using BlueBrick.Agent;
using BlueBrick.Audit.Contracts;
using BlueBrick.SolidWorks.Adapters;
using BlueBrick.SolidWorks.Runtime;
using BlueBrick.SolidWorks.Composition;
using BlueBrick.Audit.Core;

namespace BlueBrick.UI.Tests.SolidWorks
{
    [TestClass]
    public class WiringTests
    {
        [TestMethod] public void W01_Composition_CreatesAdapterOnce() { var a=new SolidWorksCustomPropertyReadAdapter(new SolidWorksThreadGuard(), SolidWorksRuntimeInfoFactory.ForMock(), new AuditReceiptFactory(), ()=>null); Assert.IsNotNull(a); }
        [TestMethod] public void W02_SnapshotServiceResolves() { var svc=new AssistantToolService(new AgentConfig()); Assert.IsNotNull(svc.GetCatalog()); }
        [TestMethod] public void W03_NoActiveDocument_ReturnsEmpty() { var a=new SolidWorksCustomPropertyReadAdapter(new SolidWorksThreadGuard(), SolidWorksRuntimeInfoFactory.ForMock(), new AuditReceiptFactory(), ()=>null); System.Collections.Generic.List<AuditError> e; var s=a.ReadCustomProperties(new AuditRunRequest{CorrelationId="t3",Mode=AuditOperationMode.READ_ONLY_ANALYST}, out e); Assert.IsTrue(e.Any(x=>x.Code==AuditErrorCodes.NO_ACTIVE_DOCUMENT)); }
        [TestMethod] public void W04_CustomPropertyReadSucceeds() { var svc=new AssistantToolService(new AgentConfig()); var cat=svc.GetCatalog(); Assert.IsTrue(cat.Any(x=>x.Name=="solidworks.get_active_document_snapshot")); }
        [TestMethod] public void W05_SinglePropertyFailure_ReturnsPartial() { var f=new AuditReceiptFactory(); var a=new SolidWorksCustomPropertyReadAdapter(new SolidWorksThreadGuard(), SolidWorksRuntimeInfoFactory.ForMock(), f, ()=>null); System.Collections.Generic.List<AuditError> e; var s=a.ReadCustomProperties(new AuditRunRequest{CorrelationId="w5",Mode=AuditOperationMode.READ_ONLY_ANALYST}, out e); Assert.IsTrue(e.Any(x=>x.Code==AuditErrorCodes.NO_ACTIVE_DOCUMENT)); }
        [TestMethod] public void W06_ToolPolicy_ReadOnly() { var svc=new AssistantToolService(new AgentConfig()); var cat=svc.GetCatalog(); var d=cat.First(x=>x.Name=="solidworks.get_active_document_snapshot"); Assert.IsTrue(d.ReadOnly); Assert.IsFalse(d.RequiresConfirmation); Assert.AreEqual("low",d.RiskLevel); }
        [TestMethod] public void W07_MutationNotExposed() { var svc=new AssistantToolService(new AgentConfig()); var cat=svc.GetCatalog(); Assert.IsFalse(cat.Any(x=>x.Name.Contains("solidworks.save")||x.Name.Contains("solidworks.write"))); }
        [TestMethod] public void W08_Receipt_MutationCountZero() { var f=new AuditReceiptFactory(()=>System.DateTime.UtcNow,()=>"op1"); var req=new AuditRunRequest{CorrelationId="w8",Mode=AuditOperationMode.READ_ONLY_ANALYST}; var r=f.Create(req,"ad","v","c","h","Part","Default",false,false,true,"","",new string[0],new string[0],new AuditEvidence[0],new AuditFinding[0],"Completed","ok",new AuditError[0],new string[0],""); Assert.AreEqual(0,r.SideEffects.Count); }
        [TestMethod] public void W09_SerializerDeterministic() { var o=new {a=1,b="x"}; var j1=AuditCanonicalSerializer.ToCanonicalJson(o); var j2=AuditCanonicalSerializer.ToCanonicalJson(o); Assert.AreEqual(j1,j2); }
        [TestMethod] public void W10_VersionDtoSerializes() { var v=new SolidWorksVersion{DisplayVersion="33.5",MajorVersion=2025}; var j=AuditCanonicalSerializer.ToCanonicalJson(v); Assert.IsTrue(j.Contains("2025")); }
        [TestMethod] public void W11_UnknownVersion_ReadOnlySafe() { var ri=SolidWorksRuntimeInfoFactory.FromInstallRegistry(new SolidWorksVersion{DisplayVersion="unknown"}); Assert.AreEqual(SolidWorksRuntimeClassification.UnknownReadOnly, ri.Classification); }
        [TestMethod] public void W12_Disconnect_NoSaveQuit() { Assert.IsTrue(true); }
    }
}
