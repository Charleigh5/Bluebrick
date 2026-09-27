using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
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
        [TestMethod]
        public void W12_PreviewLifetime_CloseCancelsAndCompletes()
        {
            var lifetime = new AssistantPreviewLifetime();
            var stream = lifetime.BeginStream();
            var token = stream.Token;

            Assert.IsFalse(lifetime.PageReady.IsCompleted);
            Assert.IsTrue(lifetime.TryBeginClose());
            Assert.IsFalse(lifetime.TryBeginClose());
            Assert.IsTrue(lifetime.IsClosing);
            Assert.IsTrue(lifetime.PageReady.IsCompleted);
            Assert.IsTrue(token.IsCancellationRequested);

            lifetime.EndStream(stream);
            lifetime.Dispose();
        }

        [TestMethod]
        public void W13_PreviewLifetime_OlderStreamCannotDisposeNewerStream()
        {
            var lifetime = new AssistantPreviewLifetime();
            var first = lifetime.BeginStream();
            var firstToken = first.Token;
            var second = lifetime.BeginStream();
            var secondToken = second.Token;

            Assert.IsTrue(firstToken.IsCancellationRequested);
            Assert.IsFalse(secondToken.IsCancellationRequested);
            lifetime.EndStream(first);
            Assert.IsFalse(secondToken.IsCancellationRequested);
            lifetime.EndStream(second);
            lifetime.Dispose();
        }

        [TestMethod]
        public void W14_DisconnectCleanupContinuesAfterFailure()
        {
            var events = new List<string>();

            SwAddin.RunCleanupStep("first", () => throw new InvalidOperationException("test"));
            SwAddin.RunCleanupStep("second", () => events.Add("second"));

            CollectionAssert.AreEqual(new[] { "second" }, events);
        }

        [TestMethod]
        public void W15_DisconnectClosesLabPreviewBeforeComRelease()
        {
            var source = File.ReadAllText(FindRepositoryFile("swaddin.cs"));
            var preview = source.IndexOf("RunCleanupStep(\"assistant preview\"", System.StringComparison.Ordinal);
            var comRelease = source.IndexOf("RunCleanupStep(\"COM release\"", System.StringComparison.Ordinal);

            Assert.IsTrue(preview >= 0);
            Assert.IsTrue(comRelease > preview);
            StringAssert.Contains(source, "assistantWindow?.CloseAssistantPreview();");
        }

        [TestMethod]
        public void W16_DisconnectStopsServerAfterPreviewAndBeforeComRelease()
        {
            var source = File.ReadAllText(FindRepositoryFile("swaddin.cs"));
            var preview = source.IndexOf("RunCleanupStep(\"assistant preview\"", StringComparison.Ordinal);
            var server = source.IndexOf("RunCleanupStep(\"agent server\"", StringComparison.Ordinal);
            var comRelease = source.IndexOf("RunCleanupStep(\"COM release\"", StringComparison.Ordinal);

            Assert.IsTrue(preview >= 0);
            Assert.IsTrue(server > preview);
            Assert.IsTrue(comRelease > server);
            StringAssert.Contains(source, "_agentServer?.Stop();");
        }

        [TestMethod]
        public void W17_StatusAndStreamFinalizerKeepBestEffortErrorContainment()
        {
            var source = File.ReadAllText(FindRepositoryFile("FrmAssistantWindow.cs"));
            var refreshStart = source.IndexOf("private async Task RefreshStatusAsync()", StringComparison.Ordinal);
            var refreshEnd = source.IndexOf("private async Task CaptureAsync()", refreshStart, StringComparison.Ordinal);
            var refresh = source.Substring(refreshStart, refreshEnd - refreshStart);
            var modelScript = refresh.IndexOf("await ExecuteScriptIfActiveAsync", StringComparison.Ordinal);
            var beforeModelScript = refresh.Substring(0, modelScript);
            var afterModelScript = refresh.Substring(modelScript);

            Assert.IsTrue(beforeModelScript.LastIndexOf("try", StringComparison.Ordinal) > beforeModelScript.LastIndexOf("}", StringComparison.Ordinal));
            Assert.IsTrue(afterModelScript.IndexOf("catch", StringComparison.Ordinal) >= 0);

            var sendStart = source.IndexOf("private async Task SendAsync()", StringComparison.Ordinal);
            var sendEnd = source.IndexOf("private async Task TestConnectionAsync()", sendStart, StringComparison.Ordinal);
            var send = source.Substring(sendStart, sendEnd - sendStart);
            var finallyIndex = send.IndexOf("finally", StringComparison.Ordinal);
            var refreshCall = send.IndexOf("await RefreshStatusAsync()", finallyIndex, StringComparison.Ordinal);
            var finalizer = send.Substring(finallyIndex, send.Length - finallyIndex);

            Assert.IsTrue(refreshCall > finallyIndex);
            Assert.IsTrue(finalizer.Substring(0, refreshCall - finallyIndex).LastIndexOf("try", StringComparison.Ordinal) >= 0);
            Assert.IsTrue(finalizer.IndexOf("catch", refreshCall - finallyIndex, StringComparison.Ordinal) >= 0);
        }

        private static string FindRepositoryFile(string relativePath)
        {
            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, relativePath);
                if (File.Exists(candidate)) return candidate;
                directory = directory.Parent;
            }
            throw new FileNotFoundException("Repository file not found.", relativePath);
        }
    }
}
