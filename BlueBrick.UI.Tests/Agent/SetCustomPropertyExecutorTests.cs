using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BlueBrick.Agent;
using BlueBrick.Audit.Contracts;
using BlueBrick.SolidWorks.Adapters;
using BlueBrick.SolidWorks.Runtime;
using BlueBrick.SolidWorks.Snapshots;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BlueBrick.UI.Tests.Agent
{
    [TestClass]
    public class SetCustomPropertyExecutorTests
    {
        [TestMethod]
        public void GateOffDeniesWithoutCadContact()
        {
            using (var f = new Fixture())
            {
                f.Config.Assistant.Mutations.Enabled = false;
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("disabled", result.Status, "Gate-off preserves the frozen pre-activation terminal.");
                Assert.AreEqual(0, f.Adapter.Calls);
                Assert.AreEqual(0, f.Prompt.Calls);
                Assert.AreEqual(0, f.Session.ComCalls.Count);
                Assert.AreEqual("HUMAN_APPROVED_MUTATION", result.Receipt.Mode);
            }
        }

        [TestMethod]
        public void NonLabIdentityDenies()
        {
            using (var f = new Fixture(false))
            {
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("disabled", result.Status);
                Assert.AreEqual(0, f.Prompt.Calls);
            }
        }

        [TestMethod]
        public void ToolDisabledDenies()
        {
            using (var f = new Fixture())
            {
                f.Descriptor.Enabled = false;
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("disabled", result.Status, "Tool-disabled preserves the frozen pre-activation terminal.");
                Assert.AreEqual(0, f.Prompt.Calls);
            }
        }

        [TestMethod]
        public void NullCompositionDeniesBeforePrompt()
        {
            using (var f = new Fixture())
            {
                var executor = f.ExecutorWithNulls();
                var result = FinishSync(executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("denied", result.Status);
                Assert.AreEqual(0, f.Prompt.Calls);
            }
        }

        [TestMethod]
        public void MalformedParamsDenyBeforePrompt()
        {
            using (var f = new Fixture())
            {
                var request = Request(f.TargetPath);
                request.Parameters.Remove("value");
                var result = FinishSync(f.Executor.ExecuteAsync(request, "trace"));
                Assert.AreEqual("denied", result.Status);
                Assert.AreEqual(0, f.Prompt.Calls);
                Assert.AreEqual(0, f.Session.ComCalls.Count);
            }
        }

        [TestMethod]
        public void RequestIdTraceFallbackApplies()
        {
            using (var f = new Fixture())
            {
                var request = Request(f.TargetPath);
                request.RequestId = null;
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Approved);
                var result = FinishSync(f.Executor.ExecuteAsync(request, "fallback"));
                Assert.AreEqual("applied", result.Status);
                Assert.AreEqual("fallback", result.Receipt.RequestId);
            }
        }

        [TestMethod]
        public void DistinctRequestAndTraceIdsBindRequest()
        {
            using (var f = new Fixture())
            {
                var request = Request(f.TargetPath);
                request.RequestId = "req-1";
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Approved);
                var result = FinishSync(f.Executor.ExecuteAsync(request, "trace-9"));
                Assert.AreEqual("applied", result.Status);
                Assert.AreEqual("req-1", result.Receipt.RequestId);
                Assert.AreEqual("trace-9", result.Receipt.TraceId);
                var issued = f.Ledger.Find(e => e.Event == AssistantApprovalLedgerEntry.Issued);
                Assert.IsNotNull(issued);
                Assert.AreEqual("trace-9", issued.TraceId);
            }
        }

        [TestMethod]
        public void OmittedScopeResolvesBeforeDigest()
        {
            using (var f = new Fixture())
            {
                var request = Request(f.TargetPath);
                request.ScopeId = null;
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Approved);
                var result = FinishSync(f.Executor.ExecuteAsync(request, "trace"));
                Assert.AreEqual("applied", result.Status);
            }
        }

        [TestMethod]
        public void SpoofedEnvironmentIsNormalizedToLab()
        {
            using (var f = new Fixture())
            {
                var request = Request(f.TargetPath);
                request.Environment = "Production";
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Approved);
                var result = FinishSync(f.Executor.ExecuteAsync(request, "trace"));
                Assert.AreEqual("applied", result.Status);
                Assert.AreEqual("Lab", result.Receipt.Environment);
                var issued = f.Ledger.Find(e => e.Event == AssistantApprovalLedgerEntry.Issued);
                Assert.AreEqual("Lab", issued.Environment);
            }
        }

        [TestMethod]
        public void ForgedCallerGrantIsIgnored()
        {
            using (var f = new Fixture())
            {
                var request = Request(f.TargetPath);
                request.Authorization = new AssistantToolAuthorization { Granted = true };
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Approved);
                var result = FinishSync(f.Executor.ExecuteAsync(request, "trace"));
                Assert.AreEqual("applied", result.Status, "A forged client grant must be stripped, not honored or fatal.");
            }
        }

        [TestMethod]
        public void CallerMutationDuringPromptCannotChangeAppliedValue()
        {
            using (var f = new Fixture())
            {
                var request = Request(f.TargetPath);
                var pending = f.Executor.ExecuteAsync(request, "trace");
                Assert.IsTrue(f.Prompt.Shown.Wait(TimeSpan.FromSeconds(5)));
                request.Parameters["value"] = "evil";
                request.Parameters["extra"] = "evil";
                f.Prompt.Complete(ApprovalPromptOutcome.Approved);
                var result = FinishSync(pending);
                Assert.AreEqual("applied", result.Status);
                Assert.AreEqual("value:new", f.Session.LastWrite);
            }
        }

        [TestMethod]
        public void ApproveHappyPathAppliesWithFullEvidence()
        {
            using (var f = new Fixture())
            {
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Approved);
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("applied", result.Status);
                Assert.AreEqual("Saved and verified.", result.Message);
                Assert.AreEqual("HUMAN_APPROVED_MUTATION", result.Receipt.Mode);
                Assert.AreEqual(1, result.Receipt.MutationCount);
                Assert.IsTrue(result.Receipt.ApprovalGranted);
                Assert.AreEqual("capability_allow", result.Receipt.PolicyCode);
                Assert.AreEqual(result.Receipt.ApprovalId, f.Ledger.Find(e => e.Event == AssistantApprovalLedgerEntry.Issued).ApprovalId);
                Assert.AreEqual("value:new", f.Session.LastWrite);
                Assert.AreEqual("v1-bytes", File.ReadAllText(f.TargetPath), "Fake CAD never touches disk; the applied value is proven via the session write log.");
                var stages = f.JournalStages();
                CollectionAssert.AreEqual(
                    new[] { "prepared", "write_started", "save_attempted", "saved_verified", "evidence_complete" },
                    stages);
                Assert.IsFalse(new MutationExecutionJournal(f.JournalPath).IsBlocked(f.TargetPath));
                Assert.AreEqual(1, f.MutationTelemetry.Count);
                var persisted = new List<AssistantToolExecutionReceipt>(f.AuditLog.TailPersisted(25));
                Assert.IsTrue(persisted.Exists(r => r.ReceiptId == result.Receipt.ReceiptId), "Returned receipt must be the acked one.");
            }
        }

        [TestMethod]
        public void DenyLeavesCadAndDiskUnchanged()
        {
            using (var f = new Fixture())
            {
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Denied);
                var before = File.ReadAllBytes(f.TargetPath);
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("denied", result.Status);
                AssertNoWrites(f.Session);
                CollectionAssert.AreEqual(before, File.ReadAllBytes(f.TargetPath));
                Assert.AreEqual(0, f.JournalLineCount());
            }
        }

        [TestMethod]
        public void CheckpointLocationFailureFailsBeforeWrite()
        {
            using (var f = new Fixture())
            {
                var pending = f.Executor.ExecuteAsync(Request(f.TargetPath), "trace");
                Assert.IsTrue(f.Prompt.Shown.Wait(TimeSpan.FromSeconds(5)));
                // The pre-prompt destinations probe created .checkpoints as a directory;
                // replace it with a file so the post-approval checkpoint copy fails.
                Directory.Delete(Path.Combine(f.FilesDir, ".checkpoints"), true);
                File.WriteAllText(Path.Combine(f.FilesDir, ".checkpoints"), "blocker");
                f.Prompt.Complete(ApprovalPromptOutcome.Approved);
                try
                {
                    var result = FinishSync(pending);
                    Assert.AreEqual("failed_before_write", result.Status);
                    AssertNoWrites(f.Session);
                }
                finally
                {
                    File.Delete(Path.Combine(f.FilesDir, ".checkpoints"));
                }
            }
        }

        [TestMethod]
        public void ShutdownOrphanLeavesUnchanged()
        {
            using (var f = new Fixture())
            {
                var pending = f.Executor.ExecuteAsync(Request(f.TargetPath), "trace");
                Assert.IsTrue(f.Prompt.Shown.Wait(TimeSpan.FromSeconds(5)));
                f.CapturedIssuer.Dispose();
                var result = FinishSync(pending);
                Assert.AreEqual("denied", result.Status);
                AssertNoWrites(f.Session);
                Assert.AreEqual("orphaned", f.Ledger.Find(e => e.Event == AssistantApprovalLedgerEntry.Orphaned).Event);
            }
        }

        [TestMethod]
        public void RawResolvedDivergenceDeniesBeforePrompt()
        {
            using (var f = new Fixture())
            {
                f.Snapshot = Snap("old", raw: "different-raw");
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("denied", result.Status);
                Assert.AreEqual(0, f.Prompt.Calls);
            }
        }

        [TestMethod]
        public void UnresolvedEmptyDenies()
        {
            using (var f = new Fixture())
            {
                f.Snapshot = Snap("old", wasResolved: false);
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("denied", result.Status);
                Assert.AreEqual(0, f.Prompt.Calls);
            }
        }

        [TestMethod]
        public void ExpressionValueDenies()
        {
            using (var f = new Fixture())
            {
                f.Snapshot = Snap("old", linked: "Expression");
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("denied", result.Status);
                Assert.AreEqual(0, f.Prompt.Calls);
            }
        }

        [TestMethod]
        public void UnknownEditabilityDenies()
        {
            using (var f = new Fixture())
            {
                f.Snapshot = Snap("old", editable: "Unknown");
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("denied", result.Status);
                Assert.AreEqual(0, f.Prompt.Calls);
            }
        }

        [TestMethod]
        public void PartialAdapterErrorsDeny()
        {
            using (var f = new Fixture())
            {
                f.Adapter.ExtraErrors.Add(new AuditError { Code = "READ_FAILURE", Message = "private" });
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("denied", result.Status);
                Assert.AreEqual(0, f.Prompt.Calls);
            }
        }

        [TestMethod]
        public void NonActiveConfigOverrideDenies()
        {
            using (var f = new Fixture())
            {
                f.Snapshot = Bundle(
                    DocScope(Prop("Description", "old")),
                    CfgScope("Default", Prop("Description", "override")));
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("denied", result.Status);
                Assert.AreEqual(0, f.Prompt.Calls);
            }
        }

        [TestMethod]
        public void LimitationsDeny()
        {
            using (var f = new Fixture())
            {
                f.SnapshotLimitations.Add("CONFIG_LIMIT_REACHED");
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("denied", result.Status);
                Assert.AreEqual(0, f.Prompt.Calls);
            }
        }

        [TestMethod]
        public void AbsentPropertyDenies()
        {
            using (var f = new Fixture())
            {
                f.Snapshot = Bundle(DocScope(Prop("Other", "x")), CfgScope("Default"));
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("denied", result.Status);
                Assert.AreEqual(0, f.Prompt.Calls);
            }
        }

        [TestMethod]
        public void ActiveDocumentMismatchDenies()
        {
            using (var f = new Fixture())
            {
                f.Session.ActivePath = Path.Combine(f.FilesDir, "other.sldprt");
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("denied", result.Status);
                Assert.AreEqual(0, f.Prompt.Calls);
            }
        }

        [TestMethod]
        public void PathEscapeDenies()
        {
            using (var f = new Fixture())
            {
                foreach (var evil in new[] { "..\\evil.sldprt", "C:\\Windows\\evil.sldprt", Path.Combine(f.FilesDir, "part.txt") })
                {
                    var request = Request(f.TargetPath);
                    request.Parameters["file_path"] = evil;
                    var result = FinishSync(f.Executor.ExecuteAsync(request, "trace"));
                    Assert.AreEqual("denied", result.Status, evil);
                }
                var blank = Request(f.TargetPath);
                blank.Parameters["file_path"] = " ";
                Assert.AreEqual("denied", FinishSync(f.Executor.ExecuteAsync(blank, "trace")).Status);
                Assert.AreEqual(0, f.Prompt.Calls);
            }
        }

        [TestMethod]
        public void DirtyTargetDenies()
        {
            using (var f = new Fixture())
            {
                f.Snapshot.State.DirtyBefore = true;
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("denied", result.Status);
                Assert.AreEqual(0, f.Prompt.Calls);
            }
        }

        [TestMethod]
        public void ReadOnlyFileDenies()
        {
            using (var f = new Fixture())
            {
                File.SetAttributes(f.TargetPath, FileAttributes.ReadOnly);
                try
                {
                    var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                    Assert.AreEqual("denied", result.Status);
                    Assert.AreEqual(0, f.Prompt.Calls);
                }
                finally
                {
                    File.SetAttributes(f.TargetPath, FileAttributes.Normal);
                }
            }
        }

        [TestMethod]
        public void LockedTargetDenies()
        {
            using (var f = new Fixture())
            using (var exclusive = new FileStream(f.TargetPath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("denied", result.Status);
                Assert.AreEqual(0, f.Prompt.Calls);
            }
        }

        [TestMethod]
        public void StateChangeDuringDialogAbortsWithoutWrite()
        {
            using (var f = new Fixture())
            {
                var pending = f.Executor.ExecuteAsync(Request(f.TargetPath), "trace");
                Assert.IsTrue(f.Prompt.Shown.Wait(TimeSpan.FromSeconds(5)));
                File.WriteAllBytes(f.TargetPath, Encoding.UTF8.GetBytes("changed-during-dialog"));
                f.Prompt.Complete(ApprovalPromptOutcome.Approved);
                var result = FinishSync(pending);
                Assert.AreEqual("denied", result.Status);
                Assert.IsTrue(result.Message.Contains("fresh approval required"));
                Assert.IsFalse(f.Session.ComCalls.Exists(c => c.StartsWith("write:", StringComparison.Ordinal)));
            }
        }

        [TestMethod]
        public void RecheckWriteBarrierCatchesDivergenceWithZeroCom()
        {
            using (var f = new Fixture())
            {
                var diverged = Bundle(DocScope(Prop("Description", "diverged")), CfgScope("Default"));
                f.Adapter.Handler = (r, n) => n >= 4 ? diverged : f.Snapshot;
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Approved);
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("failed_before_write", result.Status);
                AssertNoWrites(f.Session);
            }
        }

        [TestMethod]
        public void ReentrantReplacementDuringWriteYieldsUncertain()
        {
            using (var f = new Fixture())
            {
                f.Session.WriteHandler = (name, value) =>
                {
                    File.WriteAllBytes(f.TargetPath, Encoding.UTF8.GetBytes("tampered-during-write"));
                    return null;
                };
                var tampered = Bundle(DocScope(Prop("Description", "tampered-during-write")), CfgScope("Default"));
                f.Adapter.Handler = (r, n) => n >= 5 ? tampered : f.Snapshot;
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Approved);
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("save_uncertain", result.Status);
                Assert.AreEqual(1, result.Receipt.MutationCount);
                Assert.IsTrue(File.Exists(f.CheckpointFor("request")), "Checkpoint must be preserved.");
            }
        }

        [TestMethod]
        public void WriteFailureYieldsUncertain()
        {
            using (var f = new Fixture())
            {
                f.Session.WriteHandler = (name, value) => "private com failure";
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Approved);
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("save_uncertain", result.Status);
                Assert.AreEqual(1, result.Receipt.MutationCount);
            }
        }

        [TestMethod]
        public void SaveFailureYieldsUncertain()
        {
            using (var f = new Fixture())
            {
                f.Session.SaveHandler = () => "private save failure";
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Approved);
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("save_uncertain", result.Status);
            }
        }

        [TestMethod]
        public void ReopenFailureYieldsUncertain()
        {
            using (var f = new Fixture())
            {
                f.Session.ReopenHandler = () => "private reopen failure";
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Approved);
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("save_uncertain", result.Status);
            }
        }

        [TestMethod]
        public void VerifyMismatchYieldsUncertain()
        {
            using (var f = new Fixture())
            {
                f.Adapter.Handler = (r, n) => f.Snapshot;
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Approved);
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("save_uncertain", result.Status);
            }
        }

        [TestMethod]
        public void ReceiptFailureAfterSaveYieldsEvidenceIncomplete()
        {
            using (var f = new Fixture())
            {
                var auditDir = Path.GetDirectoryName(f.AuditLog.CurrentLogPath());
                f.Session.SaveHandler = () =>
                {
                    Directory.Delete(auditDir, true);
                    File.WriteAllText(auditDir, "blocker");
                    return null;
                };
                try
                {
                    f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Approved);
                    var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                    Assert.AreEqual("evidence_incomplete", result.Status);
                    Assert.AreEqual(1, result.Receipt.MutationCount);
                    Assert.IsTrue(result.Receipt.ApprovalGranted);
                }
                finally
                {
                    if (File.Exists(auditDir)) File.Delete(auditDir);
                }
            }
        }

        [TestMethod]
        public void TelemetryFailureAfterSaveYieldsEvidenceIncomplete()
        {
            using (var f = new Fixture())
            {
                f.MutationTelemetry.ThrowOnRecord = true;
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Approved);
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("evidence_incomplete", result.Status);
            }
        }

        [TestMethod]
        public void ConsumptionEvidenceFailureDeniesBeforeWrite()
        {
            using (var f = new Fixture())
            {
                f.Ledger.ThrowOn = e => e.Event == AssistantApprovalLedgerEntry.Consumed
                    ? new IOException("private sink failure")
                    : null;
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Approved);
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("failed_before_write", result.Status);
                AssertNoWrites(f.Session);
            }
        }

        [TestMethod]
        public void RestartAfterPreparedBlocksAdmission()
        {
            using (var f = new Fixture())
            {
                var journal = new MutationExecutionJournal(f.JournalPath);
                journal.Append(new MutationJournalRecord
                {
                    TargetId = f.TargetPath,
                    RequestId = "old",
                    ApprovalId = "old",
                    Stage = MutationStage.Prepared,
                    BaselineHash = "b",
                    CheckpointHash = "c"
                });
                var restarted = f.RestartedExecutor();
                var result = FinishSync(restarted.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("denied", result.Status);
                Assert.IsTrue(result.Message.Contains("blocked"));
                Assert.AreEqual(0, f.Prompt.Calls);
            }
        }

        [TestMethod]
        public void RestartAfterEvidenceCompleteAdmits()
        {
            using (var f = new Fixture())
            {
                var journal = new MutationExecutionJournal(f.JournalPath);
                journal.Append(new MutationJournalRecord
                {
                    TargetId = f.TargetPath,
                    RequestId = "old",
                    ApprovalId = "old",
                    Stage = MutationStage.EvidenceComplete,
                    BaselineHash = "b",
                    CheckpointHash = "c"
                });
                var restarted = f.RestartedExecutor();
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Denied);
                var result = FinishSync(restarted.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("denied", result.Status);
                Assert.AreEqual(1, f.Prompt.Calls, "Clean journal must admit to prompt.");
            }
        }

        [TestMethod]
        public void CorruptJournalBlocksGlobally()
        {
            using (var f = new Fixture())
            {
                Directory.CreateDirectory(Path.GetDirectoryName(f.JournalPath));
                File.WriteAllText(f.JournalPath, "not-json}\n");
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("denied", result.Status);
                Assert.IsTrue(result.Message.Contains("journal unreadable"));
                Assert.AreEqual(0, f.Prompt.Calls);
            }
        }

        [TestMethod]
        public void NoAutomaticRestoreOnMissingCheckpoint()
        {
            using (var f = new Fixture())
            {
                var journal = new MutationExecutionJournal(f.JournalPath);
                journal.Append(new MutationJournalRecord
                {
                    TargetId = f.TargetPath,
                    RequestId = "old",
                    ApprovalId = "old",
                    Stage = MutationStage.SaveAttempted,
                    BaselineHash = "b",
                    CheckpointHash = "c"
                });
                var restarted = f.RestartedExecutor();
                var result = FinishSync(restarted.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("denied", result.Status, "Unfinished stage blocks; nothing restores automatically.");
            }
        }

        [TestMethod]
        public void ConcurrentSameTargetDeniesSecond()
        {
            using (var f = new Fixture())
            {
                var first = f.Executor.ExecuteAsync(Request(f.TargetPath), "first");
                Assert.IsTrue(f.Prompt.Shown.Wait(TimeSpan.FromSeconds(5)));
                var second = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "second"));
                Assert.AreEqual("denied", second.Status);
                Assert.IsTrue(second.Message.Contains("approval_in_progress"));
                f.Prompt.Complete(ApprovalPromptOutcome.Approved);
                Assert.AreEqual("applied", FinishSync(first).Status);
            }
        }

        [TestMethod]
        public void ConsumedTokenCannotReplay()
        {
            using (var f = new Fixture())
            {
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Approved);
                Assert.AreEqual("applied", FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace")).Status);
                var firstApproval = f.Ledger.Find(e => e.Event == AssistantApprovalLedgerEntry.Issued).ApprovalId;
                Assert.IsNotNull(f.Ledger.Find(e => e.Event == AssistantApprovalLedgerEntry.Consumed));
                Assert.AreEqual("applied", FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace")).Status);
                var approvals = f.Ledger.FindAll(e => e.Event == AssistantApprovalLedgerEntry.Issued);
                Assert.AreEqual(2, approvals.Count);
                Assert.AreNotEqual(firstApproval, approvals[1].ApprovalId, "Each run needs a fresh approval; no stored token reuse.");
                Assert.AreEqual(2, f.Prompt.Calls);
            }
        }

        [TestMethod]
        public void DisabledAuditRootDeniesBeforePrompt()
        {
            using (var f = new Fixture())
            {
                var executor = f.ExecutorWithAuditRoot(null);
                var result = FinishSync(executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("denied", result.Status);
                Assert.AreEqual(0, f.Prompt.Calls);
            }
        }

        [TestMethod]
        public void ServiceBranchRoutesMutationTool()
        {
            var config = new AgentConfig { Assistant = new AssistantSettings { Mutations = new AssistantMutationSettings { Enabled = true, ApprovalTimeoutSeconds = 60 } } };
            var service = new AssistantToolService(config);
            var result = FinishSync(service.ExecuteAsync(Request("C:\\unused\\part.sldprt"), "trace"));
            Assert.AreEqual("disabled", result.Status, "Debug is non-Lab: gate denies inside the executor branch.");
            Assert.AreEqual("HUMAN_APPROVED_MUTATION", result.Receipt.Mode);
        }

        [TestMethod]
        public void ServiceBranchDisabledDenies()
        {
            var config = new AgentConfig { Assistant = new AssistantSettings { Mutations = new AssistantMutationSettings { Enabled = false } } };
            var service = new AssistantToolService(config);
            var result = FinishSync(service.ExecuteAsync(Request("C:\\unused\\part.sldprt"), "trace"));
            Assert.AreEqual("disabled", result.Status);
        }

        private static AssistantToolRequest Request(string targetPath)
        {
            return new AssistantToolRequest
            {
                ToolName = "solidworks.set_custom_property",
                RequestId = "request",
                SessionId = "session",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["file_path"] = targetPath,
                    ["property"] = "Description",
                    ["value"] = "new"
                }
            };
        }

        private static T FinishSync<T>(Task<T> task)
        {
            Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(15)), "Executor did not settle.");
            return task.GetAwaiter().GetResult();
        }

        private static void AssertNoWrites(FakeSession session)
        {
            Assert.IsFalse(session.ComCalls.Exists(c => c.StartsWith("write:", StringComparison.Ordinal)), "No COM write expected.");
            Assert.IsFalse(session.ComCalls.Contains("save"), "No COM save expected.");
            Assert.IsFalse(session.ComCalls.Contains("reopen"), "No COM reopen expected.");
        }

        private static PropertyAuditSnapshot Snap(
            string literal, string raw = null, bool? wasResolved = null, string linked = null, string editable = null)
        {
            return Bundle(DocScope(Prop("Description", literal, raw, wasResolved, linked, editable)), CfgScope("Default"));
        }

        private static CustomPropertySnapshot Prop(string name, string resolved, string raw = null, bool? wasResolved = null, string linked = null, string editable = null)
        {
            return new CustomPropertySnapshot
            {
                Name = name,
                NormalizedName = (name ?? string.Empty).Trim().ToLowerInvariant(),
                Scope = "Document",
                ResolvedValue = resolved,
                RawValue = raw ?? resolved,
                WasResolved = wasResolved ?? true,
                LinkedOrExpressionStatus = linked ?? "None",
                EditableStatusWhenAvailable = editable ?? "Editable"
            };
        }

        private static PropertyScopeSnapshot DocScope(params CustomPropertySnapshot[] properties)
        {
            return new PropertyScopeSnapshot { Scope = "Document", Properties = new List<CustomPropertySnapshot>(properties) };
        }

        private static PropertyScopeSnapshot CfgScope(string configuration, params CustomPropertySnapshot[] properties)
        {
            foreach (var property in properties)
            {
                property.Scope = "Configuration";
                property.Configuration = configuration;
            }
            return new PropertyScopeSnapshot { Scope = "Configuration", Configuration = configuration, Properties = new List<CustomPropertySnapshot>(properties) };
        }

        private static PropertyAuditSnapshot Bundle(params PropertyScopeSnapshot[] scopes)
        {
            return new PropertyAuditSnapshot
            {
                Scopes = new List<PropertyScopeSnapshot>(scopes),
                State = new DocumentStateSnapshot
                {
                    DirtyBefore = false,
                    DirtyAfter = false,
                    IsReadOnly = false,
                    AvailableConfigurations = new List<string> { "Default" }
                },
                Limitations = new List<string>()
            };
        }

        private sealed class Fixture : IDisposable
        {
            internal readonly string Root;
            internal readonly string FilesDir;
            internal readonly string TargetPath;
            internal readonly AgentConfig Config;
            internal readonly AssistantToolDescriptor Descriptor;
            internal readonly FakePrompt Prompt = new FakePrompt();
            internal readonly FakeDispatcher Dispatcher = new FakeDispatcher();
            internal readonly FakeAdapter Adapter = new FakeAdapter();
            internal readonly FakeSession Session = new FakeSession();
            internal readonly LedgerProbe Ledger = new LedgerProbe();
            internal readonly IssuerTelemetryProbe IssuerTelemetry = new IssuerTelemetryProbe();
            internal readonly MutationTelemetryProbe MutationTelemetry = new MutationTelemetryProbe();
            internal readonly FakeTime Time = new FakeTime();
            internal readonly AssistantToolAuditLog AuditLog;
            internal readonly SetCustomPropertyExecutor Executor;
            internal AssistantApprovalService CapturedIssuer;
            internal PropertyAuditSnapshot Snapshot;
            internal PropertyAuditSnapshot SnapshotNew;
            internal readonly List<string> SnapshotLimitations = new List<string>();

            internal string JournalPath
            {
                get { return MutationExecutionJournal.DefaultJournalPath(Config.Assistant.Mutations.TestFileRoot); }
            }

            internal Fixture()
                : this(true)
            {
            }

            internal Fixture(bool lab)
            {
                Root = Path.Combine(Path.GetTempPath(), "bb-exec-" + Guid.NewGuid().ToString("N"));
                FilesDir = Path.Combine(Root, "files");
                Directory.CreateDirectory(FilesDir);
                TargetPath = Path.Combine(FilesDir, "part.sldprt");
                File.WriteAllText(TargetPath, "v1-bytes");
                Config = new AgentConfig
                {
                    Assistant = new AssistantSettings
                    {
                        Mutations = new AssistantMutationSettings
                        {
                            Enabled = true,
                            ApprovalTimeoutSeconds = 60,
                            TestFileRoot = FilesDir
                        }
                    }
                };
                Descriptor = new AssistantToolDescriptor
                {
                    Name = "solidworks.set_custom_property",
                    CapabilityId = "solidworks.set_custom_property",
                    Mutating = true,
                    MutatesCad = true,
                    Enabled = true
                };
                AuditLog = new AssistantToolAuditLog(Path.Combine(Root, "audit"));
                Snapshot = Bundle(DocScope(Prop("Description", "old")), CfgScope("Default"));
                SnapshotNew = Bundle(DocScope(Prop("Description", "new")), CfgScope("Default"));
                Session.ActivePath = TargetPath;
                Adapter.Handler = (request, calls) =>
                {
                    var snapshot = calls >= 5 ? SnapshotNew : Snapshot;
                    snapshot.Limitations = new List<string>(SnapshotLimitations);
                    return snapshot;
                };
                var catalog = new[] { Descriptor };
                var fixture = this;
                Executor = new SetCustomPropertyExecutor(
                    Config,
                    new AssistantToolPolicy(),
                    AuditLog,
                    MutationTelemetry,
                    Ledger,
                    IssuerTelemetry,
                    Dispatcher,
                    Adapter,
                    catalog,
                    () => Prompt,
                    (prompt, builder, ledgerSink, telemetrySink) =>
                    {
                        var issuer = new AssistantApprovalService(Config, prompt, builder, ledgerSink, telemetrySink, Time, lab);
                        fixture.CapturedIssuer = issuer;
                        return issuer;
                    },
                    () => Session,
                    lab);
            }

            internal SetCustomPropertyExecutor RestartedExecutor()
            {
                var fixture = this;
                return new SetCustomPropertyExecutor(
                    Config,
                    new AssistantToolPolicy(),
                    AuditLog,
                    MutationTelemetry,
                    Ledger,
                    IssuerTelemetry,
                    Dispatcher,
                    Adapter,
                    new[] { Descriptor },
                    () => Prompt,
                    (prompt, builder, ledgerSink, telemetrySink) =>
                    {
                        var issuer = new AssistantApprovalService(Config, prompt, builder, ledgerSink, telemetrySink, Time, true);
                        fixture.CapturedIssuer = issuer;
                        return issuer;
                    },
                    () => Session,
                    true);
            }

            internal SetCustomPropertyExecutor ExecutorWithAuditRoot(string root)
            {
                var fixture = this;
                return new SetCustomPropertyExecutor(
                    Config,
                    new AssistantToolPolicy(),
                    new AssistantToolAuditLog(root),
                    MutationTelemetry,
                    Ledger,
                    IssuerTelemetry,
                    Dispatcher,
                    Adapter,
                    new[] { Descriptor },
                    () => Prompt,
                    (prompt, builder, ledgerSink, telemetrySink) =>
                    {
                        var issuer = new AssistantApprovalService(Config, prompt, builder, ledgerSink, telemetrySink, Time, true);
                        fixture.CapturedIssuer = issuer;
                        return issuer;
                    },
                    () => Session,
                    true);
            }

            internal SetCustomPropertyExecutor ExecutorWithNulls()
            {
                return new SetCustomPropertyExecutor(
                    Config,
                    new AssistantToolPolicy(),
                    AuditLog,
                    MutationTelemetry,
                    Ledger,
                    IssuerTelemetry,
                    null,
                    null,
                    new[] { Descriptor },
                    () => Prompt,
                    (prompt, builder, ledgerSink, telemetrySink) => new AssistantApprovalService(Config, prompt, builder, ledgerSink, telemetrySink, Time, true),
                    () => Session,
                    true);
            }

            internal List<string> JournalStages()
            {
                var stages = new List<string>();
                var journal = new MutationExecutionJournal(JournalPath);
                foreach (var record in journal.ReadAll())
                {
                    stages.Add(record.Stage);
                }
                return stages;
            }

            internal int JournalLineCount()
            {
                if (!File.Exists(JournalPath)) return 0;
                return File.ReadAllLines(JournalPath).Length;
            }

            internal string CheckpointFor(string requestId)
            {
                var directory = Path.Combine(FilesDir, ".checkpoints");
                if (!Directory.Exists(directory)) return null;
                foreach (var file in Directory.GetFiles(directory))
                {
                    if (file.Contains(requestId)) return file;
                }
                return null;
            }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(Root)) Directory.Delete(Root, true);
                }
                catch
                {
                }
            }
        }

        private sealed class FakePrompt : IApprovalPrompt
        {
            internal readonly TaskCompletionSource<ApprovalPromptOutcome> Completion = new TaskCompletionSource<ApprovalPromptOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
            internal Func<ApprovalPrompt, Task<ApprovalPromptOutcome>> Handler;
            internal readonly ManualResetEventSlim Shown = new ManualResetEventSlim();
            internal ApprovalPrompt Last;
            internal int Calls;

            public Task<ApprovalPromptOutcome> ShowAsync(ApprovalPrompt prompt)
            {
                Calls++;
                Last = prompt;
                Shown.Set();
                return Handler == null ? Completion.Task : Handler(prompt);
            }

            internal void Complete(ApprovalPromptOutcome outcome)
            {
                Completion.TrySetResult(outcome);
            }
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
            internal Func<AuditRunRequest, int, PropertyAuditSnapshot> Handler;
            internal readonly List<AuditError> ExtraErrors = new List<AuditError>();
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
                errors = new List<AuditError>(ExtraErrors);
                return Handler == null ? new PropertyAuditSnapshot() : Handler(request, Calls);
            }
        }

        private sealed class FakeSession : ICustomPropertyMutationSession
        {
            internal string ActivePath;
            internal Func<string, string, string> WriteHandler;
            internal Func<string> SaveHandler;
            internal Func<string> ReopenHandler;
            internal readonly List<string> ComCalls = new List<string>();
            internal string LastWrite;

            public string GetActiveDocumentPath()
            {
                ComCalls.Add("path");
                return ActivePath;
            }

            public bool TryWriteProperty(string name, string value, out string error)
            {
                ComCalls.Add("write:" + name + "=" + value);
                LastWrite = "value:" + value;
                error = null;
                if (WriteHandler != null)
                {
                    error = WriteHandler(name, value);
                    return error == null;
                }
                return true;
            }

            public bool TrySave(out string error)
            {
                ComCalls.Add("save");
                error = null;
                if (SaveHandler != null)
                {
                    error = SaveHandler();
                    return error == null;
                }
                return true;
            }

            public bool TryReleaseAndReopen(out string error)
            {
                ComCalls.Add("reopen");
                error = null;
                if (ReopenHandler != null)
                {
                    error = ReopenHandler();
                    return error == null;
                }
                return true;
            }
        }

        private sealed class LedgerProbe : IApprovalLedgerSink
        {
            internal readonly List<AssistantApprovalLedgerEntry> Entries = new List<AssistantApprovalLedgerEntry>();
            internal Func<AssistantApprovalLedgerEntry, Exception> ThrowOn;

            public void Append(AssistantApprovalLedgerEntry entry)
            {
                if (ThrowOn != null)
                {
                    var failure = ThrowOn(entry);
                    if (failure != null) throw failure;
                }
                Entries.Add(entry);
            }

            internal AssistantApprovalLedgerEntry Find(Predicate<AssistantApprovalLedgerEntry> match)
            {
                return Entries.Find(match);
            }

            internal List<AssistantApprovalLedgerEntry> FindAll(Predicate<AssistantApprovalLedgerEntry> match)
            {
                return Entries.FindAll(match);
            }
        }

        private sealed class IssuerTelemetryProbe : IApprovalLifecycleTelemetrySink
        {
            internal readonly List<AssistantApprovalLedgerEntry> Entries = new List<AssistantApprovalLedgerEntry>();

            public void Record(AssistantApprovalLedgerEntry entry, bool authorityIssued)
            {
                Entries.Add(entry);
            }
        }

        private sealed class MutationTelemetryProbe : IMutationTelemetrySink
        {
            internal readonly List<string> Terminals = new List<string>();
            internal bool ThrowOnRecord;
            internal int Count
            {
                get { return Terminals.Count; }
            }

            public void Record(string terminal, bool success, double durationMs, object metadata)
            {
                if (ThrowOnRecord) throw new InvalidOperationException("private telemetry failure");
                Terminals.Add(terminal);
            }
        }

        private sealed class FakeTime : IApprovalTimeProvider
        {
            internal DateTime Now = DateTime.UtcNow.AddHours(1);
            internal TimeSpan LastDelay;
            internal CancellationToken DelayToken;
            private TaskCompletionSource<bool> _delay = NewDelay();
            public DateTime UtcNow
            {
                get { return Now; }
            }

            public Task Delay(TimeSpan delay, CancellationToken cancellationToken)
            {
                LastDelay = delay;
                DelayToken = cancellationToken;
                var completion = _delay;
                cancellationToken.Register(() => completion.TrySetCanceled());
                return completion.Task;
            }

            private static TaskCompletionSource<bool> NewDelay()
            {
                return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
    }

    [TestClass]
    public class MutationPendingCardTests
    {
        [TestMethod]
        public void MutationToolPendingMessageIsExactAndPathless()
        {
            Assert.AreEqual(
                "Awaiting native approval — edit and save this test file once.",
                AssistantPanel.AssistantPanelMutationPending.PendingMessage("solidworks.set_custom_property"));
            Assert.IsTrue(AssistantPanel.AssistantPanelMutationPending.IsMutationApprovalTool("SOLIDWORKS.SET_CUSTOM_PROPERTY"));
            Assert.IsNull(AssistantPanel.AssistantPanelMutationPending.PendingMessage("search_local_vault"));
            Assert.IsNull(AssistantPanel.AssistantPanelMutationPending.PendingMessage(null));
        }
    }
}
