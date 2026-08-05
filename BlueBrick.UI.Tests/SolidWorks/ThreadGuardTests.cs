using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BlueBrick.Audit.Contracts;
using BlueBrick.SolidWorks.Runtime;

namespace BlueBrick.UI.Tests.SolidWorks
{
    [TestClass]
    public class ThreadGuardTests
    {
        // ---------------------------------------------------------------------------
        // Packet §18 + §2-T11 test names:
        //
        //   ThreadGuard_WrongThread_ThrowsTypedViolation
        //   Runtime_UnknownVersion_ReturnsReadOnlyLimited
        // ---------------------------------------------------------------------------

        [TestMethod]
        [ExpectedException(typeof(SolidWorksThreadViolationException))]
        public void ThreadGuard_WrongThread_ThrowsTypedViolation()
        {
            // Construct a guard claiming a *different* thread id than the current one.
            int remoteThreadId = Thread.CurrentThread.ManagedThreadId + 1;
            var guard = new SolidWorksThreadGuard(remoteThreadId);
            // VerifyAccess MUST throw a typed SolidWorksThreadViolationException.
            guard.VerifyAccess();
        }

        [TestMethod]
        public void ThreadGuard_RightThread_DoesNotThrow()
        {
            var guard = new SolidWorksThreadGuard(Thread.CurrentThread.ManagedThreadId);
            Assert.IsTrue(guard.CheckAccess());
            guard.VerifyAccess();
            Assert.AreEqual(Thread.CurrentThread.ManagedThreadId, guard.MainThreadId);
        }

        [TestMethod]
        public void ThreadGuard_ViolationException_CarriesCOM_THREAD_VIOLATIONCode()
        {
            int remoteThreadId = Thread.CurrentThread.ManagedThreadId + 1;
            var guard = new SolidWorksThreadGuard(remoteThreadId);
            try
            {
                guard.VerifyAccess();
                Assert.Fail("Expected a SolidWorksThreadViolationException.");
            }
            catch (SolidWorksThreadViolationException ex)
            {
                Assert.AreEqual(AuditErrorCodes.COM_THREAD_VIOLATION, ex.ErrorCode,
                    "The violation exception must surface the literal COM_THREAD_VIOLATION code so receipts can record it directly.");
            }
        }

        [TestMethod]
        public void Runtime_UnknownVersion_ReturnsReadOnlyLimited()
        {
            // Install-registry-derived runtime info MUST classify as UnknownReadOnly per packet §16.
            var info = SolidWorksRuntimeInfoFactory.FromInstallRegistry(
                new SolidWorksVersion { DisplayVersion = "33.5.0.53", MajorVersion = 2025, ServicePack = "SP05" });
            Assert.AreEqual(SolidWorksRuntimeClassification.UnknownReadOnly, info.Classification,
                "Install-registry capture must NOT raise classification above UnknownReadOnly — packet rule: \"Do not claim a service pack that cannot be proven.\"");
            Assert.AreEqual(RuntimeInfoCaptureSource.FromInstallRegistry, info.CaptureSource);
            Assert.AreEqual(2025, info.Version.MajorVersion, "Major version metadata is recorded for diagnostics even when classification groups as UnknownReadOnly.");
        }

        [TestMethod]
        public void Runtime_LiveRevisionNumber_2025MapsToSw2025Target()
        {
            var info = SolidWorksRuntimeInfoFactory.FromLiveRevisionNumber("SolidWorks 2025 SP5.0");
            Assert.AreEqual(SolidWorksRuntimeClassification.Sw2025Target, info.Classification);
            Assert.AreEqual(RuntimeInfoCaptureSource.FromLiveInstance, info.CaptureSource);
            Assert.AreEqual("SolidWorks 2025 SP5.0", info.Version.RawRevisionString);
        }

        [TestMethod]
        public void Runtime_LiveRevisionNumber_2026MapsToForwardUnverified()
        {
            var info = SolidWorksRuntimeInfoFactory.FromLiveRevisionNumber("SolidWorks 2026 SP0.0");
            Assert.AreEqual(SolidWorksRuntimeClassification.Sw2026ForwardUnverified, info.Classification);
        }

        [TestMethod]
        public void Runtime_LiveRevisionNumber_2024MapsToSp5Regression()
        {
            var info = SolidWorksRuntimeInfoFactory.FromLiveRevisionNumber("SolidWorks 2024 SP5.0");
            Assert.AreEqual(SolidWorksRuntimeClassification.Sw2024Sp5Regression, info.Classification);
        }

        [TestMethod]
        public void Runtime_LiveRevisionNumber_EmptyMapsToUnknownReadOnly()
        {
            var info = SolidWorksRuntimeInfoFactory.FromLiveRevisionNumber(string.Empty);
            Assert.AreEqual(SolidWorksRuntimeClassification.UnknownReadOnly, info.Classification);
        }
    }
}
