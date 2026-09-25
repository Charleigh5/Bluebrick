using System;
using System.Collections.Generic;
using System.Threading;
using BlueBrick.Agent;
using BlueBrick.Audit.Contracts;
using BlueBrick.SolidWorks.Adapters;
using BlueBrick.SolidWorks.Runtime;
using BlueBrick.SolidWorks.Snapshots;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BlueBrick.UI.Tests.Agent
{
    [TestClass]
    public class CustomPropertyPreviewBuilderTests
    {
        [TestMethod]
        public void NullDependenciesThrow()
        {
            var dispatcher = new FakeDispatcher();
            var adapter = new FakeAdapter();
            Assert.ThrowsException<ArgumentNullException>(() => new CustomPropertyApprovalPreviewBuilder(null, dispatcher));
            Assert.ThrowsException<ArgumentNullException>(() => new CustomPropertyApprovalPreviewBuilder(adapter, null));
        }

        [TestMethod]
        public void NullDescriptorOrRequestThrows()
        {
            var builder = new CustomPropertyApprovalPreviewBuilder(new FakeAdapter(), new FakeDispatcher());
            Assert.ThrowsException<ArgumentNullException>(() => builder.Build(null, Request()));
            Assert.ThrowsException<ArgumentNullException>(() => builder.Build(Descriptor(), null));
        }

        [DataTestMethod]
        [DataRow("file_path", "")]
        [DataRow("file_path", " ")]
        [DataRow("property", "")]
        [DataRow("property", " ")]
        [DataRow("value", "")]
        [DataRow("value", " ")]
        public void BlankParametersThrow(string name, string value)
        {
            var builder = new CustomPropertyApprovalPreviewBuilder(new FakeAdapter(), new FakeDispatcher());
            var request = Request();
            request.Parameters[name] = value;
            Assert.ThrowsException<ArgumentException>(() => builder.Build(Descriptor(), request));
        }

        [TestMethod]
        public void MissingParameterKeyThrows()
        {
            var builder = new CustomPropertyApprovalPreviewBuilder(new FakeAdapter(), new FakeDispatcher());
            var request = Request();
            request.Parameters.Remove("property");
            Assert.ThrowsException<ArgumentException>(() => builder.Build(Descriptor(), request));
            Assert.ThrowsException<ArgumentException>(() => builder.Build(Descriptor(), new AssistantToolRequest { Parameters = null }));
        }

        [TestMethod]
        public void FormatsOldToNewFromConfigurationScope()
        {
            var adapter = new FakeAdapter();
            adapter.Handler = r => Bundle(Scope("Configuration", Prop("Description", "old")), Scope("Document"));
            var builder = new CustomPropertyApprovalPreviewBuilder(adapter, new FakeDispatcher());
            var preview = builder.Build(Descriptor(), Request());
            Assert.AreEqual("old", preview.Before);
            Assert.AreEqual("new", preview.After);
            Assert.IsTrue(preview.Title.Contains("Description"));
            Assert.IsTrue(preview.Title.Contains("part.sldprt"));
            Assert.IsTrue(preview.Summary.Contains("Description"));
            Assert.AreEqual("MUTATION - custom property write", preview.Risk);
        }

        [TestMethod]
        public void ConfigurationScopeWinsOverDocumentScope()
        {
            var adapter = new FakeAdapter();
            adapter.Handler = r => Bundle(Scope("Document", Prop("Description", "doc")), Scope("Configuration", Prop("Description", "cfg")));
            var builder = new CustomPropertyApprovalPreviewBuilder(adapter, new FakeDispatcher());
            Assert.AreEqual("cfg", builder.Build(Descriptor(), Request()).Before);
        }

        [TestMethod]
        public void DocumentScopeUsedWhenNoConfigurationMatch()
        {
            var adapter = new FakeAdapter();
            var stored = Prop("Description", "doc");
            stored.NormalizedName = null;
            adapter.Handler = r => Bundle(Scope("Document", stored));
            var builder = new CustomPropertyApprovalPreviewBuilder(adapter, new FakeDispatcher());
            Assert.AreEqual("doc", builder.Build(Descriptor(), Request()).Before);
        }

        [TestMethod]
        public void NameMatchIgnoresCaseAndWhitespace()
        {
            var adapter = new FakeAdapter();
            adapter.Handler = r => Bundle(Scope("Document", Prop("  DESCRIPTION  ", "old")));
            var builder = new CustomPropertyApprovalPreviewBuilder(adapter, new FakeDispatcher());
            var request = Request();
            request.Parameters["property"] = "  description ";
            Assert.AreEqual("old", builder.Build(Descriptor(), request).Before);
        }

        [TestMethod]
        public void ResolvedNullFallsBackToRaw()
        {
            var adapter = new FakeAdapter();
            adapter.Handler = r => Bundle(Scope("Document", Prop("Description", null, "raw")));
            var builder = new CustomPropertyApprovalPreviewBuilder(adapter, new FakeDispatcher());
            Assert.AreEqual("raw", builder.Build(Descriptor(), Request()).Before);
        }

        [TestMethod]
        public void AbsentPropertyReturnsExactMarker()
        {
            var adapter = new FakeAdapter();
            adapter.Handler = r => Bundle(Scope("Document", Prop("Other", "x")));
            var builder = new CustomPropertyApprovalPreviewBuilder(adapter, new FakeDispatcher());
            var preview = builder.Build(Descriptor(), Request());
            Assert.AreEqual(CustomPropertyApprovalPreviewBuilder.UnavailableMarker, preview.Before);
            Assert.AreEqual("new", preview.After);
        }

        [TestMethod]
        public void AdapterThrowReturnsMarker()
        {
            var adapter = new FakeAdapter();
            adapter.Handler = r => { throw new InvalidOperationException("private adapter failure"); };
            var builder = new CustomPropertyApprovalPreviewBuilder(adapter, new FakeDispatcher());
            Assert.AreEqual(CustomPropertyApprovalPreviewBuilder.UnavailableMarker, builder.Build(Descriptor(), Request()).Before);
        }

        [TestMethod]
        public void MarshalUnavailableReturnsMarkerWithoutCallingAdapter()
        {
            var adapter = new FakeAdapter();
            var builder = new CustomPropertyApprovalPreviewBuilder(adapter, new FakeDispatcher { CanMarshal = false });
            Assert.AreEqual(CustomPropertyApprovalPreviewBuilder.UnavailableMarker, builder.Build(Descriptor(), Request()).Before);
            Assert.AreEqual(0, adapter.Calls);
        }

        [TestMethod]
        public void AuditRequestCarriesReadOnlyModeAndCorrelation()
        {
            var adapter = new FakeAdapter();
            adapter.Handler = r => Bundle();
            var builder = new CustomPropertyApprovalPreviewBuilder(adapter, new FakeDispatcher());
            var request = Request();
            request.RequestId = "correlation-1";
            builder.Build(Descriptor(), request);
            Assert.AreEqual(AuditOperationMode.READ_ONLY_ANALYST, adapter.Last.Mode);
            CollectionAssert.AreEqual(new[] { "Description" }, adapter.Last.RequestedPropertyNames);
            Assert.AreEqual("correlation-1", adapter.Last.CorrelationId);
        }

        [TestMethod]
        public void DisplayPathIsRedactedBasenameBoundedAt80()
        {
            var adapter = new FakeAdapter();
            adapter.Handler = r => Bundle();
            var builder = new CustomPropertyApprovalPreviewBuilder(adapter, new FakeDispatcher());
            var request = Request();
            request.Parameters["file_path"] = "C:\\fab\\secret\\" + new string('p', 200) + ".sldprt";
            var preview = builder.Build(Descriptor(), request);
            Assert.IsFalse(preview.Title.Contains("C:\\fab"), "Raw absolute path must not display.");
            var marker = " in ";
            var display = preview.Title.Substring(preview.Title.IndexOf(marker, StringComparison.Ordinal) + marker.Length);
            Assert.IsTrue(display.Length <= 80, "Display basename must be bounded.");
            Assert.IsTrue(display.EndsWith(".sldprt") || display.Length == 80);
        }

        [TestMethod]
        public void NonexistentPathStillBuildsWithoutOpeningFiles()
        {
            var adapter = new FakeAdapter();
            adapter.Handler = r => Bundle(Scope("Document", Prop("Description", "old")));
            var builder = new CustomPropertyApprovalPreviewBuilder(adapter, new FakeDispatcher());
            var request = Request();
            request.Parameters["file_path"] = "Z:\\no\\such\\dir\\ghost.sldprt";
            var preview = builder.Build(Descriptor(), request);
            Assert.AreEqual("old", preview.Before);
            Assert.IsTrue(preview.Title.Contains("ghost.sldprt"));
        }

        private static AssistantToolDescriptor Descriptor()
        {
            return new AssistantToolDescriptor { Name = "tool", CapabilityId = "capability" };
        }

        private static AssistantToolRequest Request()
        {
            return new AssistantToolRequest
            {
                ToolName = "tool",
                RequestId = "request",
                SessionId = "session",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["file_path"] = "C:\\fab\\part.sldprt",
                    ["property"] = "Description",
                    ["value"] = "new"
                }
            };
        }

        private static CustomPropertySnapshot Prop(string name, string resolved, string raw = null)
        {
            return new CustomPropertySnapshot
            {
                Name = name,
                NormalizedName = (name ?? string.Empty).Trim().ToLowerInvariant(),
                ResolvedValue = resolved,
                RawValue = raw ?? resolved
            };
        }

        private static PropertyScopeSnapshot Scope(string label, params CustomPropertySnapshot[] properties)
        {
            return new PropertyScopeSnapshot
            {
                Scope = label,
                Properties = new List<CustomPropertySnapshot>(properties)
            };
        }

        private static PropertyAuditSnapshot Bundle(params PropertyScopeSnapshot[] scopes)
        {
            return new PropertyAuditSnapshot { Scopes = new List<PropertyScopeSnapshot>(scopes) };
        }

        private sealed class FakeDispatcher : ISolidWorksMainThreadDispatcher
        {
            internal bool CanMarshal = true;

            public FakeDispatcher()
            {
                MainThreadId = Thread.CurrentThread.ManagedThreadId;
            }

            public int MainThreadId { get; set; }

            public bool CheckAccess()
            {
                return Thread.CurrentThread.ManagedThreadId == MainThreadId;
            }

            public void VerifyAccess()
            {
                if (!CheckAccess()) throw new SolidWorksThreadViolationException("test thread violation");
            }

            public void Invoke(Action action)
            {
                if (!TryInvoke(action)) throw new SolidWorksThreadViolationException("test marshal unavailable");
            }

            public bool TryInvoke(Action action)
            {
                if (action == null) throw new ArgumentNullException(nameof(action));
                if (!CanMarshal) return false;
                action();
                return true;
            }
        }

        private sealed class FakeAdapter : ICustomPropertyReadAdapter
        {
            internal Func<AuditRunRequest, PropertyAuditSnapshot> Handler;
            internal AuditRunRequest Last;
            internal int Calls;

            public string AdapterName
            {
                get { return "TestAdapter"; }
            }

            public PropertyAuditSnapshot ReadCustomProperties(AuditRunRequest request, out List<AuditError> errors)
            {
                Calls++;
                Last = request;
                errors = new List<AuditError>();
                return Handler == null ? new PropertyAuditSnapshot() : Handler(request);
            }
        }
    }
}
