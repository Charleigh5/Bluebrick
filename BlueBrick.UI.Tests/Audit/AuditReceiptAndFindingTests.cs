using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BlueBrick.Audit.Contracts;
using BlueBrick.Audit.Core;

namespace BlueBrick.UI.Tests.Audit
{
    [TestClass]
    public class AuditReceiptAndFindingTests
    {
        // ---------------------------------------------------------------------------
        // Packet §13 test names:
        //
        //   Receipt_ReadOnlyRun_HasNoSideEffects
        //   Receipt_DeniedRun_IsStillRecorded
        //   EvidenceAndFinding_RoundTrip_PreservesLinkage
        //
        // Tests must not require SOLIDWORKS.
        // ---------------------------------------------------------------------------

        [TestMethod]
        public void Receipt_ReadOnlyRun_HasNoSideEffects()
        {
            var factory = new AuditReceiptFactory();
            var req = new AuditRunRequest
            {
                CorrelationId = "test-corr"
              , Mode = AuditOperationMode.READ_ONLY_ANALYST
              , Target = new AuditTarget { DocumentIdentityHash = "H", DocumentType = "Part" }
            };

            var receipt = factory.Create(
                request: req,
                adapter: "SolidWorksCustomPropertyReadAdapter",
                runtimeVersion: "(MOCK)",
                runtimeClassification: "Mock",
                pathHash: "PH",
                documentType: "Part",
                activeConfiguration: "Default",
                dirtyBefore: false,
                dirtyAfter: false,
                isReadOnly: true,
                stateVersionBefore: "V1",
                stateVersionAfter: "V1",
                toolsRequested: new[] { "custom_property_snapshot" },
                toolsExecuted: new[] { "custom_property_snapshot" },
                evidence: new AuditEvidence[0],
                findings: new AuditFinding[0],
                resultStatus: "Completed",
                message: null,
                errors: null,
                sideEffects: null,
                rollbackReason: null);

            Assert.IsNotNull(receipt);
            Assert.AreEqual("Completed", receipt.ResultStatus);
            Assert.AreEqual(0, receipt.SideEffects.Count, "READ_ONLY_ANALYST receipt must have zero side effects.");
            Assert.AreEqual(receipt.DirtyBefore, receipt.DirtyAfter, "Dirty state must be unchanged.");
            Assert.AreEqual(receipt.StateVersionBefore, receipt.StateVersionAfter, "State version must be unchanged for a read-only run.");
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void Receipt_ReadOnlyRun_WithClaimedSideEffect_Throws()
        {
            var factory = new AuditReceiptFactory();
            var req = new AuditRunRequest
            {
                CorrelationId = "test-corr"
              , Mode = AuditOperationMode.MOCK
              , Target = new AuditTarget { DocumentIdentityHash = "H", DocumentType = "Part" }
            };

            // Caller claims a side effect but mode is MOCK. Factory must reject hard.
            factory.Create(
                request: req,
                adapter: "TestAdapter",
                runtimeVersion: "(MOCK)",
                runtimeClassification: "Mock",
                pathHash: "PH",
                documentType: "Part",
                activeConfiguration: "Default",
                dirtyBefore: false,
                dirtyAfter: false,
                isReadOnly: true,
                stateVersionBefore: "V1",
                stateVersionAfter: "V2",
                toolsRequested: new[] { "x" },
                toolsExecuted: new[] { "x" },
                evidence: new AuditEvidence[0],
                findings: new AuditFinding[0],
                resultStatus: "Completed",
                message: null,
                errors: null,
                sideEffects: new[] { "FORBIDDEN_WRITE" },
                rollbackReason: null);
        }

        [TestMethod]
        public void Receipt_DeniedRun_IsStillRecorded()
        {
            var factory = new AuditReceiptFactory();
            var req = new AuditRunRequest
            {
                CorrelationId = "deny-corr"
              , Mode = AuditOperationMode.READ_ONLY_ANALYST
              , Target = new AuditTarget { DocumentIdentityHash = "H", DocumentType = "Part" }
            };

            var deniedErr = new AuditError { Code = AuditErrorCodes.NO_ACTIVE_DOCUMENT, CorrelationId = "deny-corr", Message = "No active document." };
            var receipt = factory.CreateDenied(req, "SolidWorksCustomPropertyReadAdapter", "(MOCK)", "Mock", "No active document in session.", deniedErr);

            Assert.IsNotNull(receipt);
            Assert.AreEqual("Denied", receipt.ResultStatus, "Denied run must be recorded as such, not silently swallowed.");
            Assert.AreEqual(1, receipt.Errors.Count, "Denied run must record the typed denial error.");
            Assert.AreEqual(AuditErrorCodes.NO_ACTIVE_DOCUMENT, receipt.Errors[0].Code);
            Assert.IsTrue(receipt.ToolsExecuted.Count == 0, "Denied run must reflect zero tools executed.");
            Assert.AreEqual(0, receipt.SideEffects.Count, "Denied run must have zero side effects.");
        }

        [TestMethod]
        public void EvidenceAndFinding_RoundTrip_PreservesLinkage()
        {
            var factory = new AuditReceiptFactory();

            // Evidence has stable IDs; finding references them by EvidenceId list (the linkage contract).
            var evidence = new[]
            {
                new AuditEvidence { EvidenceId = "E1", EvidenceType = "CustomProperty", Source = "Document", Location = new AuditEvidenceLocation { SourceLabel = "CustomPropertyManager" }, RawValue = "DN-0001", ResolvedValue = "DN-0001", Confidence = 1.0 },
                new AuditEvidence { EvidenceId = "E2", EvidenceType = "CustomProperty", Source = "Document", Location = new AuditEvidenceLocation { SourceLabel = "CustomPropertyManager" }, RawValue = "V1", ResolvedValue = "V1", Confidence = 1.0 }
            };
            var findings = new[]
            {
                new AuditFinding { FindingId = "F1", RuleId = "VR-1", Severity = "Warning", Status = "Open", EvidenceIds = new List<string> { "E1", "E2" }, Confidence = 0.9 }
            };

            var req = new AuditRunRequest
            {
                CorrelationId = "round-trip"
              , Mode = AuditOperationMode.READ_ONLY_ANALYST
              , Target = new AuditTarget { DocumentIdentityHash = "H", DocumentType = "Part" }
            };
            var receipt = factory.Create(
                request: req,
                adapter: "SolidWorksCustomPropertyReadAdapter",
                runtimeVersion: "(MOCK)",
                runtimeClassification: "Mock",
                pathHash: "PH",
                documentType: "Part",
                activeConfiguration: "Default",
                dirtyBefore: false,
                dirtyAfter: false,
                isReadOnly: true,
                stateVersionBefore: "V1",
                stateVersionAfter: "V1",
                toolsRequested: new[] { "custom_property_snapshot" },
                toolsExecuted: new[] { "custom_property_snapshot" },
                evidence: evidence,
                findings: findings,
                resultStatus: "Completed",
                message: null,
                errors: null,
                sideEffects: null,
                rollbackReason: null);

            Assert.AreEqual(2, receipt.EvidenceCount, "Evidence count must round-trip.");
            Assert.AreEqual(1, receipt.FindingCount, "Finding count must round-trip.");
            Assert.IsFalse(findings[0].AutomaticCorrectionAllowed, "Findings must NEVER have automatic correction allowed.");
            Assert.AreEqual("Open", findings[0].Status, "Finding status must survive the audit run.");
            CollectionAssert.AreEquivalent(new[] { "E1", "E2" }, findings[0].EvidenceIds.ToList(), "Evidence linkage by stable ID must round-trip.");
        }
    }
}
