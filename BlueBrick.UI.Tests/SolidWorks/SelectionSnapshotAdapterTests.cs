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
    public class SelectionSnapshotAdapterTests
    {
        private sealed class FakeSelectionSource : ISwSelectionSource
        {
            private readonly int _count;
            private readonly List<int> _types;
            private readonly List<string> _names;
            private readonly List<int> _marks;
            private readonly string _docHash;
            private readonly bool _throwOnCount;
            private readonly HashSet<int> _throwOnType;
            private readonly HashSet<int> _throwOnName;
            public FakeSelectionSource(int count, List<int> types, List<string> names, List<int> marks, string docHash, bool throwOnCount, HashSet<int> throwOnType, HashSet<int> throwOnName)
            {
                _count = count; _types = types ?? new List<int>(); _names = names ?? new List<string>(); _marks = marks ?? new List<int>(); _docHash = docHash ?? string.Empty; _throwOnCount = throwOnCount; _throwOnType = throwOnType ?? new HashSet<int>(); _throwOnName = throwOnName ?? new HashSet<int>();
            }
            public string GetDocumentIdentityHash() => _docHash;
            public int GetSelectedObjectCount2(int mark) { if (_throwOnCount) throw new InvalidOperationException("COM failure count"); return _count; }
            public int GetSelectedObjectType3(int index, int mark) { if (_throwOnType.Contains(index)) throw new InvalidOperationException("COM type fail " + index); return index <= _types.Count ? _types[index-1] : 0; }
            public string GetSafeNameForIndex(int index, int mark) { if (_throwOnName.Contains(index)) throw new InvalidOperationException("COM name fail " + index); return index <= _names.Count ? _names[index-1] : string.Empty; }
            public int GetSelectionMark(int index, int mark) => index <= _marks.Count ? _marks[index-1] : mark;
        }

        private static SolidWorksSelectionReadAdapter NewAdapter(ISwSelectionSource src)
        {
            var disp = new SolidWorksThreadGuard(System.Threading.Thread.CurrentThread.ManagedThreadId);
            return new SolidWorksSelectionReadAdapter(disp, SolidWorksRuntimeInfoFactory.ForMock(), new AuditReceiptFactory(), () => src);
        }

        [TestMethod]
        public void Selection_Zero_ReturnsEmpty()
        {
            var src = new FakeSelectionSource(0, new List<int>(), new List<string>(), new List<int>(), "hash123", false, null, null);
            var ad = NewAdapter(src);
            List<AuditError> errs;
            var snap = ad.ReadSelection(new AuditRunRequest { CorrelationId = "c0", Mode = AuditOperationMode.READ_ONLY_ANALYST }, out errs);
            Assert.AreEqual(0, snap.Count);
            Assert.AreEqual(0, snap.Items.Count);
            Assert.AreEqual("empty", snap.Status);
            Assert.AreEqual("hash123", snap.DocumentIdentityHash);
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(snap);
            Assert.IsFalse(json.Contains("System.__ComObject"));
        }

        [TestMethod]
        public void Selection_One_ReturnsSingle()
        {
            var src = new FakeSelectionSource(1, new List<int>{ 1 }, new List<string>{ "Face1" }, new List<int>{ 5 }, "h1", false, null, null);
            var ad = NewAdapter(src);
            List<AuditError> errs;
            var snap = ad.ReadSelection(new AuditRunRequest { CorrelationId = "c1", Mode = AuditOperationMode.READ_ONLY_ANALYST }, out errs);
            Assert.AreEqual(1, snap.Count);
            Assert.AreEqual(1, snap.Items.Count);
            Assert.AreEqual("Face1", snap.SafeName);
            Assert.AreEqual(5, snap.SelectionMark);
            Assert.AreEqual("ok", snap.Status);
            Assert.IsFalse(snap.Limitations.Contains(SelectionSnapshot.LimitReachedCode));
        }

        [TestMethod]
        public void Selection_Many_ReturnsAll()
        {
            var types = Enumerable.Repeat(1, 5).ToList();
            var names = new List<string>{ "A","B","C","D","E" };
            var marks = new List<int>{ 0,0,0,0,0 };
            var src = new FakeSelectionSource(5, types, names, marks, "h2", false, null, null);
            var ad = NewAdapter(src);
            List<AuditError> errs;
            var snap = ad.ReadSelection(new AuditRunRequest { CorrelationId = "cm", Mode = AuditOperationMode.READ_ONLY_ANALYST }, out errs);
            Assert.AreEqual(5, snap.Count);
            Assert.AreEqual(5, snap.Items.Count);
            Assert.AreEqual("ok", snap.Status);
        }

        [TestMethod]
        public void Selection_Truncated_LimitsTo100()
        {
            int count = 250;
            var types = Enumerable.Repeat(2, count).ToList();
            var names = Enumerable.Range(0, count).Select(i => "N" + i).ToList();
            var marks = Enumerable.Repeat(0, count).ToList();
            var src = new FakeSelectionSource(count, types, names, marks, "h3", false, null, null);
            var ad = NewAdapter(src);
            List<AuditError> errs;
            var snap = ad.ReadSelection(new AuditRunRequest { CorrelationId = "ct", Mode = AuditOperationMode.READ_ONLY_ANALYST }, out errs);
            Assert.AreEqual(count, snap.Count);
            Assert.AreEqual(SelectionSnapshot.MaxSelectionCount, snap.Items.Count);
            Assert.IsTrue(snap.Limitations.Contains(SelectionSnapshot.LimitReachedCode));
            Assert.AreEqual("partial", snap.Status);
            Assert.IsTrue(errs.Any(e => e.Code == SelectionSnapshot.LimitReachedCode));
        }

        [TestMethod]
        public void Selection_Unsupported_Type_MarksLimitation()
        {
            var src = new FakeSelectionSource(2, new List<int>{ 0, 9999 }, new List<string>{ "X","Y" }, new List<int>{ 0,0 }, "h4", false, null, null);
            var ad = NewAdapter(src);
            List<AuditError> errs;
            var snap = ad.ReadSelection(new AuditRunRequest { CorrelationId = "cu", Mode = AuditOperationMode.READ_ONLY_ANALYST }, out errs);
            Assert.AreEqual(2, snap.Count);
            Assert.IsTrue(snap.Limitations.Contains("UNSUPPORTED_SELECTION_TYPE"));
            Assert.AreEqual("partial", snap.Status);
        }

        [TestMethod]
        public void Selection_ComFailure_ReturnsPartial()
        {
            var src = new FakeSelectionSource(3, new List<int>{1,1,1}, new List<string>{ "A","B","C"}, new List<int>{0,0,0}, "h5", false, new HashSet<int>{2}, new HashSet<int>{3});
            var ad = NewAdapter(src);
            List<AuditError> errs;
            var snap = ad.ReadSelection(new AuditRunRequest { CorrelationId = "cf", Mode = AuditOperationMode.READ_ONLY_ANALYST }, out errs);
            Assert.IsTrue(errs.Any(e => e.Code == AuditErrorCodes.READ_FAILURE));
            Assert.AreEqual("partial", snap.Status);
            Assert.IsTrue(snap.Items.Count < 3);
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(snap);
            Assert.IsFalse(json.Contains("System.__ComObject"));
        }

        [TestMethod]
        public void Selection_ComCountFailure_ReturnsPartial()
        {
            var src = new FakeSelectionSource(0, null, null, null, "h6", true, null, null);
            var ad = NewAdapter(src);
            List<AuditError> errs;
            var snap = ad.ReadSelection(new AuditRunRequest { CorrelationId = "cc", Mode = AuditOperationMode.READ_ONLY_ANALYST }, out errs);
            Assert.IsTrue(errs.Any(e => e.Code == AuditErrorCodes.READ_FAILURE));
            Assert.AreEqual("partial", snap.Status);
        }

        [TestMethod]
        public void Selection_NoActiveDocument_ReturnsEmptyTyped()
        {
            var ad = new SolidWorksSelectionReadAdapter(new SolidWorksThreadGuard(System.Threading.Thread.CurrentThread.ManagedThreadId), SolidWorksRuntimeInfoFactory.ForMock(), new AuditReceiptFactory(), () => null);
            List<AuditError> errs;
            var snap = ad.ReadSelection(new AuditRunRequest { CorrelationId = "cn", Mode = AuditOperationMode.READ_ONLY_ANALYST }, out errs);
            Assert.IsTrue(errs.Any(e => e.Code == AuditErrorCodes.NO_ACTIVE_DOCUMENT));
            Assert.AreEqual("empty", snap.Status);
        }

        [TestMethod]
        public void Selection_Serializable_NoComEscape()
        {
            var src = new FakeSelectionSource(1, new List<int>{1}, new List<string>{"SafeName"}, new List<int>{-1}, "h7", false, null, null);
            var ad = NewAdapter(src);
            List<AuditError> errs;
            var snap = ad.ReadSelection(new AuditRunRequest { CorrelationId = "cs", Mode = AuditOperationMode.READ_ONLY_ANALYST }, out errs);
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(snap);
            Assert.IsFalse(string.IsNullOrEmpty(json));
            Assert.IsFalse(json.Contains("MarshalByRefObject"));
            Assert.IsFalse(json.Contains("RCW"));
        }
    }
}
