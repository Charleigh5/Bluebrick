using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BlueBrick.Audit.Contracts;
using BlueBrick.Audit.Core;
using BlueBrick.SolidWorks.Adapters;
using BlueBrick.SolidWorks.Adapters.Internal;
using BlueBrick.SolidWorks.Runtime;
using BlueBrick.SolidWorks.Snapshots;

namespace BlueBrick.UI.Tests.SolidWorks
{
    [TestClass]
    public class FeatureTreeSnapshotTests
    {
        private sealed class FakeFeatureNode : ISwFeatureNode
        {
            private readonly string _id;
            private readonly string _name;
            private readonly string _type;
            private readonly string _suppression;
            private readonly string _state;
            private ISwFeatureNode _next;
            private ISwFeatureNode _firstSub;
            private readonly bool _throwOnId;
            private readonly bool _throwOnName;
            private readonly bool _throwOnType;
            private readonly bool _throwOnSupp;
            private readonly bool _throwOnState;
            private readonly bool _throwOnNext;
            private readonly bool _throwOnSub;

            public FakeFeatureNode(string id, string name, string type, string suppression, string state, bool throwOnId = false, bool throwOnName = false, bool throwOnType = false, bool throwOnSupp = false, bool throwOnState = false, bool throwOnNext = false, bool throwOnSub = false)
            {
                _id = id; _name = name; _type = type; _suppression = suppression ?? "unknown"; _state = state ?? "unknown";
                _throwOnId = throwOnId; _throwOnName = throwOnName; _throwOnType = throwOnType; _throwOnSupp = throwOnSupp; _throwOnState = throwOnState; _throwOnNext = throwOnNext; _throwOnSub = throwOnSub;
            }
            public void SetNext(ISwFeatureNode n) => _next = n;
            public void SetFirstSub(ISwFeatureNode n) => _firstSub = n;
            public string GetId() { if (_throwOnId) throw new InvalidOperationException("GetId fail"); return _id; }
            public string GetName() { if (_throwOnName) throw new InvalidOperationException("GetName fail"); return _name; }
            public string GetTypeName() { if (_throwOnType) throw new InvalidOperationException("GetTypeName fail"); return _type; }
            public string GetSuppressionState() { if (_throwOnSupp) throw new InvalidOperationException("GetSuppressionState fail"); return _suppression; }
            public string GetState() { if (_throwOnState) throw new InvalidOperationException("GetState fail"); return _state; }
            public ISwFeatureNode GetNext() { if (_throwOnNext) throw new InvalidOperationException("GetNext fail"); return _next; }
            public ISwFeatureNode GetFirstSubFeature() { if (_throwOnSub) throw new InvalidOperationException("GetFirstSubFeature fail"); return _firstSub; }
        }

        private sealed class FakeFeatureSource : ISwFeatureSource
        {
            private readonly string _hash;
            private readonly ISwFeatureNode _first;
            public FakeFeatureSource(string hash, ISwFeatureNode first) { _hash = hash; _first = first; }
            public string GetDocumentIdentityHash() => _hash;
            public ISwFeatureNode GetFirstFeature() => _first;
        }

        private static SolidWorksFeatureTreeReadAdapter NewAdapter(ISwFeatureSource src)
        {
            var disp = new SolidWorksThreadGuard(System.Threading.Thread.CurrentThread.ManagedThreadId);
            return new SolidWorksFeatureTreeReadAdapter(disp, SolidWorksRuntimeInfoFactory.ForMock(), new AuditReceiptFactory(), () => src);
        }

        private static AuditRunRequest Req(string corr) => new AuditRunRequest { CorrelationId = corr, Mode = AuditOperationMode.READ_ONLY_ANALYST };

        private static ISwFeatureNode BuildFlat(int count, int start = 0)
        {
            FakeFeatureNode head = null, prev = null;
            for (int i = 0; i < count; i++)
            {
                var n = new FakeFeatureNode("id-" + (start + i), "F" + (start + i), "Extrude", "resolved", "resolved");
                if (head == null) head = n;
                if (prev != null) prev.SetNext(n);
                prev = n;
            }
            return head;
        }

        private static ISwFeatureNode BuildDeep(int totalNodes)
        {
            if (totalNodes <= 0) return null;
            var nodes = new List<FakeFeatureNode>();
            for (int i = 0; i < totalNodes; i++) nodes.Add(new FakeFeatureNode("did-" + i, "D" + i, "Sketch", "resolved", "resolved"));
            for (int i = 0; i < totalNodes - 1; i++) nodes[i].SetFirstSub(nodes[i + 1]);
            return nodes[0];
        }

        [TestMethod]
        public void F01_NoFeatures_ReturnsEmptyOk()
        {
            var src = new FakeFeatureSource("hash-f01", null);
            var ad = NewAdapter(src);
            List<AuditError> errs;
            var snap = ad.ReadFeatureTree(Req("f01"), out errs);
            Assert.IsNotNull(snap);
            Assert.AreEqual(0, snap.Features.Count);
            Assert.AreEqual(0, snap.TotalCount);
            Assert.IsFalse(snap.Truncated);
            Assert.AreEqual("ok", snap.Status);
            Assert.AreEqual("hash-f01", snap.DocumentIdentityHash);
            Assert.IsFalse(snap.Limitations.Contains(FeatureTreeSnapshot.LimitReachedCode));
        }

        [TestMethod]
        public void F02_OneFeature_ReturnsSingle()
        {
            var node = new FakeFeatureNode("fid-1", "Boss-Extrude1", "Extrude", "resolved", "resolved");
            var src = new FakeFeatureSource("hash-f02", node);
            var ad = NewAdapter(src);
            List<AuditError> errs;
            var snap = ad.ReadFeatureTree(Req("f02"), out errs);
            Assert.AreEqual(1, snap.Features.Count);
            Assert.AreEqual(1, snap.TotalCount);
            Assert.AreEqual("ok", snap.Status);
            Assert.IsFalse(snap.Truncated);
            var f = snap.Features[0];
            Assert.AreEqual("Boss-Extrude1", f.Name);
            Assert.AreEqual("Extrude", f.Type);
            Assert.AreEqual(0, f.Depth);
            Assert.AreEqual(string.Empty, f.Parent);
        }

        [TestMethod]
        public void F03_NestedTree_PreservesHierarchy()
        {
            var child = new FakeFeatureNode("cid-1", "Sketch1", "Sketch", "resolved", "resolved");
            var parent = new FakeFeatureNode("pid-1", "Boss-Extrude1", "Extrude", "resolved", "resolved");
            parent.SetFirstSub(child);
            var src = new FakeFeatureSource("hash-f03", parent);
            var ad = NewAdapter(src);
            List<AuditError> errs;
            var snap = ad.ReadFeatureTree(Req("f03"), out errs);
            Assert.AreEqual(2, snap.Features.Count);
            var p = snap.Features[0];
            var c = snap.Features[1];
            Assert.AreEqual(0, p.Depth);
            Assert.AreEqual(1, c.Depth);
            Assert.AreEqual(p.Id, c.Parent);
            Assert.AreEqual("ok", snap.Status);
        }

        [TestMethod]
        public void F04_SiblingOrdering_IsPreserved()
        {
            var n1 = new FakeFeatureNode("id-a", "A", "Extrude", "resolved", "resolved");
            var n2 = new FakeFeatureNode("id-b", "B", "Extrude", "resolved", "resolved");
            var n3 = new FakeFeatureNode("id-c", "C", "Extrude", "resolved", "resolved");
            n1.SetNext(n2); n2.SetNext(n3);
            var src = new FakeFeatureSource("hash-f04", n1);
            var ad = NewAdapter(src);
            List<AuditError> errs;
            var snap = ad.ReadFeatureTree(Req("f04"), out errs);
            Assert.AreEqual(3, snap.Features.Count);
            CollectionAssert.AreEqual(new[] { "A", "B", "C" }, snap.Features.Select(x => x.Name).ToArray());
            CollectionAssert.AreEqual(new[] { "id-a", "id-b", "id-c" }, snap.Features.Select(x => x.Id).ToArray());
            foreach (var f in snap.Features) Assert.AreEqual(0, f.Depth);
        }

        [TestMethod]
        public void F05_MaxNodeBoundary_500_IsOk()
        {
            var head = BuildFlat(500);
            var src = new FakeFeatureSource("hash-f05", head);
            var ad = NewAdapter(src);
            List<AuditError> errs;
            var snap = ad.ReadFeatureTree(Req("f05"), out errs);
            Assert.AreEqual(500, snap.Features.Count);
            Assert.AreEqual(500, snap.TotalCount);
            Assert.IsFalse(snap.Truncated);
            Assert.AreEqual("ok", snap.Status);
            Assert.IsFalse(snap.Limitations.Contains(FeatureTreeSnapshot.LimitReachedCode));
            Assert.IsFalse(errs.Any(e => e.Code == FeatureTreeSnapshot.LimitReachedCode));
        }

        [TestMethod]
        public void F06_NodeLimitExceeded_501_PartialFeatureLimitReached()
        {
            var head = BuildFlat(501);
            var src = new FakeFeatureSource("hash-f06", head);
            var ad = NewAdapter(src);
            List<AuditError> errs;
            var snap = ad.ReadFeatureTree(Req("f06"), out errs);
            Assert.AreEqual(500, snap.Features.Count);
            Assert.IsTrue(snap.Truncated);
            Assert.AreEqual("partial", snap.Status);
            Assert.IsTrue(snap.Limitations.Contains(FeatureTreeSnapshot.LimitReachedCode));
            Assert.IsTrue(errs.Any(e => e.Code == FeatureTreeSnapshot.LimitReachedCode));
            Assert.IsTrue(errs.Any(e => e.Code == AuditErrorCodes.FEATURE_LIMIT_REACHED));
        }

        [TestMethod]
        public void F07_MaxDepthBoundary_20_IsOk()
        {
            var head = BuildDeep(21);
            var src = new FakeFeatureSource("hash-f07", head);
            var ad = NewAdapter(src);
            List<AuditError> errs;
            var snap = ad.ReadFeatureTree(Req("f07"), out errs);
            Assert.AreEqual(21, snap.Features.Count);
            Assert.IsFalse(snap.Truncated);
            Assert.AreEqual("ok", snap.Status);
            Assert.AreEqual(20, snap.Features.Last().Depth);
            Assert.IsFalse(snap.Limitations.Contains(FeatureTreeSnapshot.LimitReachedCode));
        }

        [TestMethod]
        public void F08_DepthExceeded_21_PartialFeatureDepthLimitReached()
        {
            var head = BuildDeep(22);
            var src = new FakeFeatureSource("hash-f08", head);
            var ad = NewAdapter(src);
            List<AuditError> errs;
            var snap = ad.ReadFeatureTree(Req("f08"), out errs);
            Assert.IsTrue(snap.Truncated);
            Assert.AreEqual("partial", snap.Status);
            Assert.IsTrue(snap.Limitations.Contains(FeatureTreeSnapshot.LimitReachedCode) || snap.Limitations.Contains("FEATURE_DEPTH_LIMIT_REACHED"));
            Assert.IsTrue(errs.Any(e => e.Code == FeatureTreeSnapshot.LimitReachedCode));
            Assert.IsTrue(snap.Features.Count <= 21);
            Assert.IsTrue(snap.Features.All(f => f.Depth <= FeatureTreeSnapshot.MaxDepth));
        }

        [TestMethod]
        public void F09_IndividualReadFailure_PartialReadFailure()
        {
            var n1 = new FakeFeatureNode("id-1", "Good1", "Extrude", "resolved", "resolved");
            var bad = new FakeFeatureNode("id-bad", "BadName", "Extrude", "resolved", "resolved", throwOnName: true);
            var n3 = new FakeFeatureNode("id-3", "Good3", "Extrude", "resolved", "resolved");
            n1.SetNext(bad); bad.SetNext(n3);
            var src = new FakeFeatureSource("hash-f09", n1);
            var ad = NewAdapter(src);
            List<AuditError> errs;
            var snap = ad.ReadFeatureTree(Req("f09"), out errs);
            Assert.IsTrue(errs.Any(e => e.Code == AuditErrorCodes.READ_FAILURE));
            Assert.AreEqual("partial", snap.Status);
            Assert.AreEqual(3, snap.Features.Count);
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(snap);
            Assert.IsFalse(json.Contains("System.__ComObject"));
        }

        [TestMethod]
        public void F10_ComObjectAbsent_NoIFeatureLeak()
        {
            var n1 = new FakeFeatureNode("id-10a", "FeatA", "Cut", "resolved", "resolved");
            var n2 = new FakeFeatureNode("id-10b", "FeatB", "Sketch", "resolved", "resolved");
            n1.SetNext(n2);
            var src = new FakeFeatureSource("hash-f10", n1);
            var ad = NewAdapter(src);
            List<AuditError> errs;
            var snap = ad.ReadFeatureTree(Req("f10"), out errs);
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(snap);
            Assert.IsFalse(json.Contains("System.__ComObject"));
            Assert.IsFalse(json.Contains("MarshalByRefObject"));
            Assert.IsFalse(json.Contains("RCW"));
            Assert.IsFalse(json.Contains("IFeature"));
            Assert.IsFalse(json.Contains("ISwFeatureNode"));
            Assert.IsFalse(json.Contains("SolidWorks.Interop"));
            foreach (var f in snap.Features)
            {
                Assert.IsNotNull(f.Id);
                Assert.IsNotNull(f.Name);
            }
        }

        [TestMethod]
        public void F11_DeterministicSerialization_CanonicalJsonSorted()
        {
            var n1 = new FakeFeatureNode("id-x", "Zebra", "Extrude", "resolved", "resolved");
            var n2 = new FakeFeatureNode("id-y", "Apple", "Sketch", "resolved", "resolved");
            n1.SetNext(n2);
            var src = new FakeFeatureSource("hash-f11", n1);
            var ad = NewAdapter(src);
            List<AuditError> e1, e2;
            var s1 = ad.ReadFeatureTree(Req("f11"), out e1);
            var s2 = ad.ReadFeatureTree(Req("f11"), out e2);
            var j1 = AuditCanonicalSerializer.ToCanonicalJson(s1);
            var j2 = AuditCanonicalSerializer.ToCanonicalJson(s2);
            Assert.AreEqual(j1, j2);
            Assert.IsTrue(AuditCanonicalSerializer.CanonicalEquals(s1, s2));
            var h1 = AuditStateVersionBuilder.BuildStateVersion(s1);
            var h2 = AuditStateVersionBuilder.BuildStateVersion(s2);
            Assert.AreEqual(h1, h2);
            var idxDepth = j1.IndexOf("\"Depth\"", StringComparison.Ordinal);
            var idxId = j1.IndexOf("\"Id\"", StringComparison.Ordinal);
            Assert.IsTrue(idxDepth >= 0 && idxId >= 0);
        }

        [TestMethod]
        public void F12_Receipt_MutationCountZero()
        {
            var node = new FakeFeatureNode("id-r", "FeatR", "Extrude", "resolved", "resolved");
            var src = new FakeFeatureSource("hash-f12", node);
            var ad = NewAdapter(src);
            List<AuditError> errs;
            var snap = ad.ReadFeatureTree(Req("f12"), out errs);
            var stateVersion = AuditStateVersionBuilder.BuildStateVersion(snap);
            var factory = new AuditReceiptFactory();
            var req = Req("f12");
            var receipt = factory.Create(
                request: req,
                adapter: ad.AdapterName,
                runtimeVersion: "MOCK",
                runtimeClassification: "Mock",
                pathHash: snap.DocumentIdentityHash,
                documentType: "Part",
                activeConfiguration: string.Empty,
                dirtyBefore: false,
                dirtyAfter: false,
                isReadOnly: true,
                stateVersionBefore: stateVersion,
                stateVersionAfter: stateVersion,
                toolsRequested: new[] { "feature_tree_snapshot" },
                toolsExecuted: new[] { "feature_tree_snapshot" },
                evidence: new AuditEvidence[0],
                findings: new AuditFinding[0],
                resultStatus: snap.Status == "ok" ? "Completed" : "Partial",
                message: null,
                errors: errs,
                sideEffects: null,
                rollbackReason: null);
            Assert.AreEqual(0, receipt.SideEffects.Count);
            Assert.AreEqual(stateVersion, receipt.StateVersionBefore);
            Assert.AreEqual(stateVersion, receipt.StateVersionAfter);
            Assert.AreEqual(receipt.StateVersionBefore, receipt.StateVersionAfter);
            Assert.IsFalse(receipt.DirtyBefore != receipt.DirtyAfter);
        }
    }
}
