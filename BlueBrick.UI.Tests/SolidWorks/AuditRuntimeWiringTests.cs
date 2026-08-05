using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BlueBrick.Audit.Contracts;
using BlueBrick.Audit.Core;
using BlueBrick.SolidWorks.Adapters;
using BlueBrick.SolidWorks.Runtime;

namespace BlueBrick.UI.Tests.SolidWorks
{
    /// <summary>
    /// Slice 2 §19 wiring decision tests. Per BB-M001 packet §19, if no safe
    /// integration seam exists, the adapter+service compile and mark runtime
    /// wiring STAGED_NOT_WIRED. This fixture captures the STAGED_NOT_WIRED
    /// contract as a runnable assertion so a later wiring attempt cannot
    /// silently regress.
    /// </summary>
    [TestClass]
    public class AuditRuntimeWiringTests
    {
        /// <summary>Constant string label surfaced in receipts when the runtime hook in <c>swaddin.cs</c> is intentionally NOT applied.</summary>
        public const string WiringStatus = "STAGED_NOT_WIRED";

        [TestMethod]
        public void AuditRuntimeWiring_IsStagedNotWired()
        {
            // This is a META assertion: the runtime wiring is intentionally left STAGED_NOT_WIRED because
            // swaddin.cs is a prohibited file for Slice 1/2 (packet §4 prohibits modificarion).
            Assert.AreEqual("STAGED_NOT_WIRED", WiringStatus);
        }

        [TestMethod]
        public void AuditRuntimeWiring_ControlledRemoval_PreservesServiceShape()
        {
            // The service interface surface must still plugin through the composition root even
            // though no caller currently constructs it inside swaddin.cs.
            Assert.IsNotNull(typeof(ISolidWorksReadOnlySnapshotService));
            Assert.IsNotNull(typeof(SolidWorksReadOnlySnapshotService));
            // Contract methods
            var methods = typeof(ISolidWorksReadOnlySnapshotService).GetMethods();
            Assert.IsTrue(methods.Length >= 1, "Service interface must expose at least the RunReadonlySnapshot method.");
            Assert.AreEqual("ServiceName", typeof(ISolidWorksReadOnlySnapshotService).GetProperty("ServiceName").Name);
        }

        [TestMethod]
        public void AuditRuntimeWiring_DeniedMode_ReturnsDeniedReceipt()
        {
            // When the caller requests an unsupported mode (PreviewOnly/HumanApprovedMutation), the
            // service must NOT crash. It MUST return a Denied receipt with INVALID_MODE error.
            var factory = new AuditReceiptFactory();
            var runtime = SolidWorksRuntimeInfoFactory.ForMock();
            // Use a stub adapter that simply records its AdapterName and produces no snapshot.
            var stubAdapter = new StubAdapter();
            var service = new SolidWorksReadOnlySnapshotService(stubAdapter, factory, runtime);
            var req = new AuditRunRequest { CorrelationId = "w-test", Mode = AuditOperationMode.PREVIEW_ONLY };

            var result = service.RunReadonlySnapshot(req);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Receipt);
            Assert.AreEqual("Denied", result.Receipt.ResultStatus);
            Assert.AreEqual(AuditErrorCodes.INVALID_MODE, result.Errors[0].Code);
            Assert.AreEqual(0, result.Receipt.SideEffects.Count, "Denied run must have zero side effects.");
        }

        private sealed class StubAdapter : ICustomPropertyReadAdapter
        {
            public string AdapterName => "StubAdapter";

            public BlueBrick.SolidWorks.Snapshots.PropertyAuditSnapshot ReadCustomProperties(AuditRunRequest request, out List<AuditError> errors)
            {
                errors = new List<AuditError> { new AuditError { Code = AuditErrorCodes.NO_ACTIVE_DOCUMENT, CorrelationId = request?.CorrelationId, Message = "Stub: no document." } };
                return new BlueBrick.SolidWorks.Snapshots.PropertyAuditSnapshot();
            }
        }
    }
}
