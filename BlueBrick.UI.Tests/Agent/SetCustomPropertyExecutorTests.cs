using System;
using System.Collections.Concurrent;
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
using Newtonsoft.Json;
using SolidWorks.Interop.swconst;

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
                Assert.AreEqual("disabled", result.Status);
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
                Assert.AreEqual("disabled", result.Status);
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
        public void NewValueRuleRejectsExpressionBlankAndOversize()
        {
            using (var f = new Fixture())
            {
                foreach (var bad in new[] { "", " ", "$PRP:Description", "x$prp:y", "a\0b", "a\nb", new string('v', 2049) })
                {
                    var request = Request(f.TargetPath);
                    request.Parameters["value"] = bad;
                    var result = FinishSync(f.Executor.ExecuteAsync(request, "trace"));
                    Assert.AreEqual("denied", result.Status, "value length=" + (bad ?? "null").Length);
                }
                Assert.AreEqual(0, f.Prompt.Calls);
                var ok = Request(f.TargetPath);
                ok.Parameters["value"] = new string('v', 2048);
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Approved);
                f.Adapter.PreviewBefore = new string('v', 2048);
                f.Reader.Handler = (query, calls) => ReaderResult.ProofFor(new string('v', 2048), query.TargetFullPath);
                Assert.AreEqual("applied", FinishSync(f.Executor.ExecuteAsync(ok, "trace")).Status);
            }
        }

        [TestMethod]
        public void SupportedNewValueHelperPinsRules()
        {
            string error;
            Assert.IsFalse(SetCustomPropertyExecutor.IsSupportedNewValue(null, out error));
            Assert.IsFalse(SetCustomPropertyExecutor.IsSupportedNewValue("  ", out error));
            Assert.IsFalse(SetCustomPropertyExecutor.IsSupportedNewValue("$PRP:x", out error));
            Assert.IsFalse(SetCustomPropertyExecutor.IsSupportedNewValue("a\0", out error));
            Assert.IsFalse(SetCustomPropertyExecutor.IsSupportedNewValue(new string('x', 2049), out error));
            Assert.IsTrue(SetCustomPropertyExecutor.IsSupportedNewValue("Fixture bracket", out error));
            Assert.IsTrue(SetCustomPropertyExecutor.IsSupportedNewValue(new string('x', 2048), out error));
        }

        [TestMethod]
        public void AncestorReparseDeniedByHelper()
        {
            string error;
            Assert.IsTrue(SetCustomPropertyExecutor.CheckNoReparseAncestors(
                Path.Combine(Path.GetTempPath(), "plain", "part.sldprt"),
                p => FileAttributes.Normal, out error));
            Assert.IsFalse(SetCustomPropertyExecutor.CheckNoReparseAncestors(
                Path.Combine(Path.GetTempPath(), "plain", "part.sldprt"),
                p => p.EndsWith("plain", StringComparison.OrdinalIgnoreCase)
                    ? FileAttributes.Directory | FileAttributes.ReparsePoint
                    : FileAttributes.Normal,
                out error));
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
                    new[] { "prepared", "write_started", "save_attempted", "save_returned", "saved_verified", "evidence_complete" },
                    stages);
                Assert.IsFalse(new MutationExecutionJournal(f.JournalPath).IsBlocked(f.TargetPath));
                Assert.AreEqual(1, f.MutationTelemetry.Count);
                var persisted = new List<AssistantToolExecutionReceipt>(f.AuditLog.TailPersisted(25));
                Assert.IsTrue(persisted.Exists(r => r.ReceiptId == result.Receipt.ReceiptId), "Returned receipt must be the acked one.");
                StringAssert.Contains(result.Items[0].Metadata["recovery"], MutationExecutionJournal.HashTarget(f.TargetPath));
                Assert.IsFalse(result.Items[0].Metadata["recovery"].Contains(f.Root));
            }
        }

        [TestMethod]
        public void PromptSummaryCarriesSaveConsequence()
        {
            using (var f = new Fixture())
            {
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Denied);
                FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.IsNotNull(f.Prompt.Last);
                Assert.AreEqual("edit and save this test file once.", f.Prompt.Last.Preview.Summary);
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
        public void TimeoutLeavesUnchanged()
        {
            using (var f = new Fixture())
            {
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Timeout);
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("denied", result.Status);
                AssertNoWrites(f.Session);
            }
        }

        [TestMethod]
        public void ShutdownOrphanLeavesUnchanged()
        {
            using (var f = new Fixture())
            {
                var pending = f.Executor.ExecuteAsync(Request(f.TargetPath), "trace");
                Assert.IsTrue(f.Prompt.Shown.Wait(TimeSpan.FromSeconds(5)));
                f.Ownership.NotifyShutdown();
                var result = FinishSync(pending);
                Assert.AreEqual("denied", result.Status);
                AssertNoWrites(f.Session);
                Assert.AreEqual("orphaned", f.Ledger.Find(e => e.Event == AssistantApprovalLedgerEntry.Orphaned).Event);
            }
        }

        [TestMethod]
        public void PostStopAdmissionDeniedPermanently()
        {
            using (var f = new Fixture())
            {
                f.Ownership.NotifyShutdown();
                f.Ownership.NotifyShutdown();
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("denied", result.Status);
                Assert.AreEqual(0, f.Prompt.Calls);
            }
        }

        [TestMethod]
        public void ConfigRevokedAfterApprovalDenies()
        {
            using (var f = new Fixture())
            {
                f.Reader.Handler = (query, calls) =>
                {
                    if (calls == 3) f.Config.Assistant.Mutations.Enabled = false;
                    return ReaderResult.ProofFor("old", query.TargetFullPath);
                };
                var pending = f.Executor.ExecuteAsync(Request(f.TargetPath), "trace");
                Assert.IsTrue(f.Prompt.Shown.Wait(TimeSpan.FromSeconds(5)));
                f.Prompt.Complete(ApprovalPromptOutcome.Approved);
                var result = FinishSync(pending);
                Assert.AreEqual("denied", result.Status);
                AssertNoWrites(f.Session);
            }
        }

        [TestMethod]
        public void ExpiredAuthorityAfterApprovalDenies()
        {
            using (var f = new Fixture())
            {
                f.Reader.Handler = (query, calls) =>
                {
                    if (calls == 3) f.Now = f.Now.AddHours(3);
                    return ReaderResult.ProofFor("old", query.TargetFullPath);
                };
                var pending = f.Executor.ExecuteAsync(Request(f.TargetPath), "trace");
                Assert.IsTrue(f.Prompt.Shown.Wait(TimeSpan.FromSeconds(5)));
                f.Prompt.Complete(ApprovalPromptOutcome.Approved);
                var result = FinishSync(pending);
                Assert.AreEqual("denied", result.Status);
                AssertNoWrites(f.Session);
            }
        }

        [TestMethod]
        public void ShutdownDuringCheckpointDenies()
        {
            using (var f = new Fixture())
            {
                f.Reader.Handler = (query, calls) =>
                {
                    if (calls == 3) f.Ownership.NotifyShutdown();
                    return ReaderResult.ProofFor("old", query.TargetFullPath);
                };
                var pending = f.Executor.ExecuteAsync(Request(f.TargetPath), "trace");
                Assert.IsTrue(f.Prompt.Shown.Wait(TimeSpan.FromSeconds(5)));
                f.Prompt.Complete(ApprovalPromptOutcome.Approved);
                var result = FinishSync(pending);
                Assert.AreEqual("denied", result.Status);
                AssertNoWrites(f.Session);
            }
        }

        [TestMethod]
        public void RawResolvedDivergenceDeniesBeforePrompt()
        {
            using (var f = new Fixture())
            {
                f.Reader.Handler = (query, calls) => ReaderResult.ProofFor("old", query.TargetFullPath, raw: "different-raw");
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
                f.Reader.Handler = (query, calls) => ReaderResult.ProofFor("old", query.TargetFullPath, wasResolved: false);
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
                f.Reader.Handler = (query, calls) => ReaderResult.ProofFor("old", query.TargetFullPath, linked: true);
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
                f.Reader.Handler = (query, calls) => ReaderResult.ProofFor("old", query.TargetFullPath, editable: false);
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("denied", result.Status);
                Assert.AreEqual(0, f.Prompt.Calls);
            }
        }

        [TestMethod]
        public void NonTextNativeKindDenies()
        {
            using (var f = new Fixture())
            {
                f.Reader.Handler = (query, calls) => ReaderResult.ProofFor("old", query.TargetFullPath, kind: MutationNativeKind.Unsupported);
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("denied", result.Status);
                Assert.AreEqual(0, f.Prompt.Calls);
            }
        }

        [TestMethod]
        public void ReaderFailureDenies()
        {
            using (var f = new Fixture())
            {
                f.Reader.Handler = (query, calls) => ReaderResult.Failure("read failure");
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
                f.Reader.Handler = (query, calls) => ReaderResult.Failure("same-name override in configuration scope");
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("denied", result.Status);
                Assert.AreEqual(0, f.Prompt.Calls);
            }
        }

        [TestMethod]
        public void EnumerationUnavailableDenies()
        {
            using (var f = new Fixture())
            {
                f.Reader.Handler = (query, calls) => ReaderResult.Failure("enumeration unavailable");
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
                f.Reader.Handler = (query, calls) => ReaderResult.Absent();
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("denied", result.Status);
                Assert.AreEqual(0, f.Prompt.Calls);
            }
        }

        [TestMethod]
        public void BuilderSkewDeniesBeforePrompt()
        {
            using (var f = new Fixture())
            {
                f.Adapter.PreviewBefore = "evil-skew";
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Approved);
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("denied", result.Status);
                Assert.AreEqual(0, f.Prompt.Calls);
            }
        }

        [TestMethod]
        public void BuilderThrowDeniesBeforePrompt()
        {
            using (var f = new Fixture())
            {
                f.Adapter.ThrowOnBuild = true;
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Approved);
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("denied", result.Status);
                Assert.AreEqual(0, f.Prompt.Calls);
            }
        }

        [TestMethod]
        public void DirtyAtBuilderRevalidationDeniesWithZeroPrompts()
        {
            using (var f = new Fixture())
            {
                var calls = 0;
                f.Reader.Handler = (query, callsIn) =>
                {
                    calls++;
                    if (calls == 2) return ReaderResult.ProofFor("old", query.TargetFullPath, clean: false);
                    return ReaderResult.ProofFor("old", query.TargetFullPath);
                };
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Approved);
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("denied", result.Status);
                Assert.AreEqual(0, f.Prompt.Calls, "Dirty at wrapper revalidation must deny with preview_unavailable and zero prompt calls.");
            }
        }

        [TestMethod]
        public void ActiveDocumentMismatchDenies()
        {
            using (var f = new Fixture())
            {
                var other = Path.Combine(f.FilesDir, "other.sldprt");
                f.Reader.Handler = (query, calls) => ReaderResult.ProofFor("old", other);
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
        public void TraversalRequestIdStaysContained()
        {
            using (var f = new Fixture())
            {
                var request = Request(f.TargetPath);
                request.RequestId = "x\\..\\..\\..\\escaped";
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Approved);
                var result = FinishSync(f.Executor.ExecuteAsync(request, "trace"));
                Assert.AreEqual("applied", result.Status);
                var escaped = Path.GetFullPath(Path.Combine(f.Root, "escaped.sldprt"));
                Assert.IsFalse(File.Exists(escaped), "Checkpoint names must be server-derived hex, never caller text.");
            }
        }

        [TestMethod]
        public void DirtyTargetDenies()
        {
            using (var f = new Fixture())
            {
                f.Reader.Handler = (query, calls) => ReaderResult.ProofFor("old", query.TargetFullPath, clean: false);
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
                f.Reader.Handler = (query, calls) => ReaderResult.ProofFor(calls >= 4 ? "diverged" : "old", query.TargetFullPath);
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
                f.Reader.Handler = (query, calls) => ReaderResult.ProofFor(calls >= 5 ? "tampered-during-write" : "old", query.TargetFullPath);
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Approved);
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("save_uncertain", result.Status);
                Assert.AreEqual(1, result.Receipt.MutationCount);
                Assert.IsTrue(File.Exists(f.CheckpointForRequest("request")), "Checkpoint must be preserved.");
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
        public void PreSaveTamperYieldsUncertainWithoutSave()
        {
            using (var f = new Fixture())
            {
                f.Session.WriteHandler = (name, value) =>
                {
                    File.WriteAllBytes(f.TargetPath, Encoding.UTF8.GetBytes("tampered-before-save"));
                    return null;
                };
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Approved);
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("save_uncertain", result.Status);
                Assert.IsFalse(f.Session.ComCalls.Contains("save"), "Pre-save hash gate must refuse before any save call.");
            }
        }

        [TestMethod]
        public void PreSaveIdentitySwitchYieldsUncertainWithoutSave()
        {
            using (var f = new Fixture())
            {
                f.Session.WriteHandler = (name, value) =>
                {
                    f.Session.IsCurrent = false;
                    return null;
                };
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Approved);
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("save_uncertain", result.Status);
                Assert.IsFalse(f.Session.ComCalls.Contains("save"), "Identity switch with unchanged bytes must still refuse.");
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
                f.Reader.Handler = (query, calls) => ReaderResult.ProofFor("old", query.TargetFullPath);
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Approved);
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("save_uncertain", result.Status);
            }
        }

        [TestMethod]
        public void DirtyAfterSaveLeavesOpenWithSaveReturned()
        {
            using (var f = new Fixture())
            {
                f.Reader.Handler = (query, calls) => ReaderResult.ProofFor(calls >= 5 ? "new" : "old", query.TargetFullPath, clean: calls < 5);
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Approved);
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("save_returned", result.Status);
                Assert.IsFalse(f.Session.ComCalls.Contains("reopen"), "Dirty after save must leave the document open.");
                Assert.IsTrue(File.Exists(f.CheckpointForRequest("request")), "Checkpoint must be preserved.");
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
        public void TelemetryFailureOnDenialKeepsDeniedWithIncompleteEvidence()
        {
            using (var f = new Fixture())
            {
                f.MutationTelemetry.ThrowOnRecord = true;
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Denied);
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("denied", result.Status);
                Assert.AreEqual("incomplete", result.Items[0].Metadata["evidence"]);
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
        public void SecretsAbsentFromAllPersistedEvidence()
        {
            using (var f = new Fixture())
            {
                const string marker = "EVALUATOR_SYNTHETIC_MARKER_7Q2";
                var request = Request(f.TargetPath);
                request.SessionId = "api_key=" + marker;
                request.RequestId = "api_key=" + marker;
                var telemetryDir = Path.Combine(f.Root, "telemetry");
                var telemetry = new MutationTelemetryLoggerSink(new TelemetryLogger(telemetryDir, sampleRateSuccess: 0));
                var executor = f.ExecutorWithTelemetry(telemetry);
                    f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Approved);
                    var result = FinishSync(executor.ExecuteAsync(request, "trace"));
                    Assert.AreEqual("applied", result.Status);
                    var auditText = File.ReadAllText(f.AuditLog.CurrentLogPath());
                Assert.IsFalse(auditText.Contains(marker), "Audit receipt must not carry the marker.");
                var telemetryText = new StringBuilder();
                foreach (var file in Directory.GetFiles(telemetryDir, "*", SearchOption.AllDirectories))
                {
                    telemetryText.Append(File.ReadAllText(file));
                }
                Assert.IsFalse(telemetryText.ToString().Contains(marker), "Telemetry must not carry the marker.");
                var journalText = File.ReadAllText(f.JournalPath);
                Assert.IsFalse(journalText.Contains(marker), "Journal must not carry the marker.");
                var rendered = JsonConvert.SerializeObject(result.Receipt) + JsonConvert.SerializeObject(result.Items);
                Assert.IsFalse(rendered.Contains(marker), "Result metadata must not carry the marker.");
                Assert.IsFalse(rendered.Contains(f.Root), "Result metadata must not carry absolute paths.");
            }
        }

        [TestMethod]
        public void LongIdentifiersAreBoundedInPersistedEvidence()
        {
            using (var f = new Fixture())
            {
                var request = Request(f.TargetPath);
                request.SessionId = new string('s', 200);
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Approved);
                var result = FinishSync(f.Executor.ExecuteAsync(request, "trace"));
                Assert.AreEqual("applied", result.Status);
                var auditText = File.ReadAllText(f.AuditLog.CurrentLogPath());
                Assert.IsFalse(auditText.Contains(new string('s', 200)), "200-char identifiers must be bounded.");
                Assert.IsTrue(auditText.Contains(new string('s', 128)), "Bounded prefix must persist.");
            }
        }

        [TestMethod]
        public void RestartMatrixSeedsBlockOrAdmit()
        {
            var cases = new[]
            {
                new { Stage = MutationStage.Prepared, Blocked = true },
                new { Stage = MutationStage.WriteStarted, Blocked = true },
                new { Stage = MutationStage.SaveAttempted, Blocked = true },
                new { Stage = MutationStage.SaveReturned, Blocked = true },
                new { Stage = MutationStage.SavedVerified, Blocked = true },
                new { Stage = MutationStage.EvidenceComplete, Blocked = false },
                new { Stage = MutationStage.Reconciled, Blocked = false },
                new { Stage = MutationStage.Restored, Blocked = false }
            };
            foreach (var c in cases)
            {
                using (var f = new Fixture())
                {
                    var journal = new MutationExecutionJournal(f.JournalPath);
                    journal.Append(f.TargetPath, "old", "old", c.Stage, "b", "c");
                    var restarted = f.RestartedExecutor();
                    if (c.Blocked)
                    {
                        var result = FinishSync(restarted.ExecuteAsync(Request(f.TargetPath), "trace"));
                        Assert.AreEqual("denied", result.Status, c.Stage);
                        Assert.AreEqual(0, f.Prompt.Calls, c.Stage);
                    }
                    else
                    {
                        f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Denied);
                        var result = FinishSync(restarted.ExecuteAsync(Request(f.TargetPath), "trace"));
                        Assert.AreEqual("denied", result.Status, c.Stage);
                        Assert.AreEqual(1, f.Prompt.Calls, "Clean journal must admit to prompt: " + c.Stage);
                    }
                }
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
        public void UnreadableJournalPathBlocksGlobally()
        {
            using (var f = new Fixture())
            {
                var directory = Path.GetDirectoryName(f.JournalPath);
                Directory.CreateDirectory(directory);
                File.WriteAllText(f.JournalPath, "x");
                File.Delete(f.JournalPath);
                Directory.Delete(directory);
                File.WriteAllText(directory, "blocker-file");
                try
                {
                    var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                    Assert.AreEqual("denied", result.Status);
                }
                finally
                {
                    File.Delete(directory);
                }
            }
        }

        [TestMethod]
        public void NoAutomaticRestoreOnMissingCheckpoint()
        {
            using (var f = new Fixture())
            {
                var journal = new MutationExecutionJournal(f.JournalPath);
                journal.Append(f.TargetPath, "old", "old", MutationStage.SaveAttempted, "b", "c");
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
        public void SameIdDifferentTargetsApplyIndependently()
        {
            using (var f = new Fixture())
            {
                var otherPath = Path.Combine(f.FilesDir, "other.sldprt");
                File.WriteAllText(otherPath, "v1-bytes");
                f.Reader.Handler = (query, calls) => ReaderResult.ProofFor(
                    query.TargetFullPath == otherPath ? "other-new" : (calls >= 5 ? "new" : "old"),
                    query.TargetFullPath);
                f.Adapter.PreviewBefore = "old";
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Approved);
                var firstRequest = Request(f.TargetPath);
                firstRequest.RequestId = "same-id";
                var secondRequest = Request(otherPath);
                secondRequest.RequestId = "same-id";
                secondRequest.Parameters["value"] = "other-new";
                Assert.AreEqual("applied", FinishSync(f.Executor.ExecuteAsync(firstRequest, "trace")).Status);
                f.Adapter.PreviewBefore = "other-new";
                f.Session.ActivePath = otherPath;
                Assert.AreEqual("applied", FinishSync(f.Executor.ExecuteAsync(secondRequest, "trace")).Status);
                Assert.AreEqual("value:new", f.Session.WritesFor(f.TargetPath));
                Assert.AreEqual("value:other-new", f.Session.WritesFor(otherPath));
                Assert.AreEqual(2, f.Prompt.Calls);
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
                f.Adapter.PreviewBefore = "new";
                Assert.AreEqual("applied", FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace")).Status);
                var approvals = f.Ledger.FindAll(e => e.Event == AssistantApprovalLedgerEntry.Issued);
                Assert.AreEqual(2, approvals.Count);
                Assert.AreNotEqual(firstApproval, approvals[1].ApprovalId, "Each run needs a fresh approval; no stored token reuse.");
                Assert.AreEqual(2, f.Prompt.Calls);
            }
        }

        [TestMethod]
        public void MultiAdmissionSameIssuerAppliesTwice()
        {
            using (var f = new Fixture())
            {
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Approved);
                Assert.AreEqual("applied", FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace")).Status);
                var secondRequest = Request(f.TargetPath);
                secondRequest.RequestId = "second";
                secondRequest.Parameters["value"] = "second-new";
                f.Adapter.PreviewBefore = "second-new";
                f.Reader.Handler = (query, calls) => ReaderResult.ProofFor("second-new", query.TargetFullPath);
                Assert.AreEqual("applied", FinishSync(f.Executor.ExecuteAsync(secondRequest, "trace")).Status);
                Assert.AreEqual(2, f.Prompt.Calls);
                Assert.AreEqual("value:second-new", f.Session.LastWrite);
            }
        }

        [TestMethod]
        public void DenyThenNewRequestApplies()
        {
            using (var f = new Fixture())
            {
                int calls = 0;
                f.Prompt.Handler = p => Task.FromResult(++calls == 1 ? ApprovalPromptOutcome.Denied : ApprovalPromptOutcome.Approved);
                var first = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("denied", first.Status);
                AssertNoWrites(f.Session);
                var secondRequest = Request(f.TargetPath);
                secondRequest.RequestId = "second";
                secondRequest.Parameters["value"] = "second-new";
                f.Adapter.PreviewBefore = "second-new";
                f.Reader.Handler = (query, queryCalls) => ReaderResult.ProofFor("second-new", query.TargetFullPath);
                var second = FinishSync(f.Executor.ExecuteAsync(secondRequest, "trace"));
                Assert.AreEqual("applied", second.Status);
                Assert.AreEqual(2, f.Prompt.Calls);
                Assert.AreEqual("value:second-new", f.Session.LastWrite);
            }
        }

        [TestMethod]
        public void TimeoutThenNewRequestApplies()
        {
            using (var f = new Fixture())
            {
                int calls = 0;
                f.Prompt.Handler = p => Task.FromResult(++calls == 1 ? ApprovalPromptOutcome.Timeout : ApprovalPromptOutcome.Approved);
                var first = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("denied", first.Status);
                AssertNoWrites(f.Session);
                var secondRequest = Request(f.TargetPath);
                secondRequest.RequestId = "second";
                secondRequest.Parameters["value"] = "second-new";
                f.Adapter.PreviewBefore = "second-new";
                f.Reader.Handler = (query, queryCalls) => ReaderResult.ProofFor("second-new", query.TargetFullPath);
                var second = FinishSync(f.Executor.ExecuteAsync(secondRequest, "trace"));
                Assert.AreEqual("applied", second.Status);
                Assert.AreEqual(2, f.Prompt.Calls);
                Assert.AreEqual("value:second-new", f.Session.LastWrite);
            }
        }

        [TestMethod]
        public void ConfigFlipBetweenConsumptionAndEntryDenies()
        {
            using (var f = new Fixture())
            {
                var dispatcher = new FlipArmingDispatcher(f.Dispatcher, f.Config);
                var ownership = new MutationExecutionOwnership();
                var router = new RoutingPreviewBuilder();
                ownership.EnsureInitialized(() => new AssistantApprovalService(f.Config, f.Prompt, router, f.Ledger, f.IssuerTelemetry, f.Time, true), router);
                var executor = new SetCustomPropertyExecutor(
                    f.Config,
                    new AssistantToolPolicy(),
                    f.AuditLog,
                    f.MutationTelemetry,
                    f.Ledger,
                    dispatcher,
                    f.Adapter,
                    new[] { f.Descriptor },
                    ownership,
                    () => f.Session,
                    () => f.Reader,
                    () => f.Now,
                    true);
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Approved);
                f.Ledger.ThrowOn = entry =>
                {
                    if (entry != null && entry.Event == AssistantApprovalLedgerEntry.Consumed) dispatcher.Armed = true;
                    return null;
                };
                var result = FinishSync(executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("denied", result.Status);
                AssertNoWrites(f.Session);
                Assert.IsNotNull(f.Ledger.Find(e => e.Event == AssistantApprovalLedgerEntry.Consumed), "Consumption must have succeeded before the flip.");
                CollectionAssert.AreEqual(new[] { "prepared", "write_started" }, f.JournalStages());
            }
        }

        [TestMethod]
        public void StrictTwoThreadFullAdmissionAppliesThroughRealReader()
        {
            using (var f = new Fixture())
            using (var strict = new StrictMainThreadDispatcher())
            {
                var document = new StrictComSource();
                document.Path = f.TargetPath;
                document.Values["Description"] = "old";
                var emptyScope = new StrictComSource();
                emptyScope.Path = f.TargetPath;
                var probe = new SwComAdmissionReader(strict, cfg => string.IsNullOrEmpty(cfg) ? (ISwMutationComSource)document : (ISwMutationComSource)emptyScope);
                MutationAdmissionProof refused;
                string refusedError;
                Assert.IsFalse(probe.TryProveTarget(new MutationAdmissionQuery
                {
                    TargetFullPath = f.TargetPath,
                    PropertyName = "Description",
                    CorrelationId = "trace",
                    ConfigurationReadLimit = 64
                }, out refused, out refusedError));
                StringAssert.StartsWith(refusedError, "thread affinity", "Non-vacuity: the production guard must be live in this configuration.");

                var session = new FakeSession();
                session.ActivePath = f.TargetPath;
                session.WriteHandler = (name, value) =>
                {
                    document.Values[name] = value;
                    return null;
                };
                var ownership = new MutationExecutionOwnership();
                var router = new RoutingPreviewBuilder();
                ownership.EnsureInitialized(() => new AssistantApprovalService(f.Config, f.Prompt, router, f.Ledger, f.IssuerTelemetry, f.Time, true), router);
                var executor = new SetCustomPropertyExecutor(
                    f.Config,
                    new AssistantToolPolicy(),
                    f.AuditLog,
                    f.MutationTelemetry,
                    f.Ledger,
                    strict,
                    f.Adapter,
                    new[] { f.Descriptor },
                    ownership,
                    () => session,
                    () => new SwComAdmissionReader(strict, cfg => string.IsNullOrEmpty(cfg) ? (ISwMutationComSource)document : (ISwMutationComSource)emptyScope),
                    () => f.Now,
                    true);
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Approved);
                var result = FinishSync(executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("applied", result.Status);
                Assert.AreEqual(1, f.Prompt.Calls, "The prompt must be reached: R1 denied with zero prompts.");
                Assert.AreEqual("value:new", session.LastWrite);
                CollectionAssert.AreEqual(
                    new[] { "prepared", "write_started", "save_attempted", "save_returned", "saved_verified", "evidence_complete" },
                    f.JournalStages());
            }
        }

        [TestMethod]
        public void StrictComSourceNullNamesShapeReturnsAbsent()
        {
            var source = new StrictComSource();
            source.Values["Description"] = "old";
            source.NamesOverride = scope => null;
            Assert.IsFalse(source.ContainsProperty("Description"));
        }

        [TestMethod]
        public void StrictComSourceEmptyNamesShapeReturnsAbsent()
        {
            var source = new StrictComSource();
            source.Values["Description"] = "old";
            source.NamesOverride = scope => new string[0];
            Assert.IsFalse(source.ContainsProperty("Description"));
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
        public void TimeoutRecordsExpiredLedgerEntry()
        {
            using (var f = new Fixture())
            {
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Timeout);
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("denied", result.Status);
                AssertNoWrites(f.Session);
                Assert.IsNotNull(f.Ledger.Find(e => e.Event == AssistantApprovalLedgerEntry.Expired), "Timeout must record an expired ledger entry.");
                Assert.AreEqual("approval_required", result.Receipt.PolicyCode);
            }
        }

        [TestMethod]
        public void NullValuedProofDeniesBeforePrompt()
        {
            using (var f = new Fixture())
            {
                f.Reader.Handler = (query, calls) => ReaderResult.ProofFor(null, query.TargetFullPath);
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("denied", result.Status);
                Assert.AreEqual(0, f.Prompt.Calls);
            }
        }

        [TestMethod]
        public void NonActiveDocumentFailsBeforeWrite()
        {
            using (var f = new Fixture())
            {
                var otherPath = Path.Combine(f.FilesDir, "other.sldprt");
                File.WriteAllText(otherPath, "v1-bytes");
                f.Session.ActivePath = otherPath;
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Approved);
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("failed_before_write", result.Status);
                AssertNoWrites(f.Session);
            }
        }

        [TestMethod]
        public void TargetDeletedDuringPromptDenies()
        {
            using (var f = new Fixture())
            {
                var pending = f.Executor.ExecuteAsync(Request(f.TargetPath), "trace");
                Assert.IsTrue(f.Prompt.Shown.Wait(TimeSpan.FromSeconds(5)));
                File.Delete(f.TargetPath);
                f.Prompt.Complete(ApprovalPromptOutcome.Approved);
                var result = FinishSync(pending);
                Assert.AreEqual("denied", result.Status);
                AssertNoWrites(f.Session);
            }
        }

        [TestMethod]
        public void ForeignProcessLockDeniesTargetLocked()
        {
            using (var f = new Fixture())
            {
                var ownership = new MutationExecutionOwnership();
                var router = new RoutingPreviewBuilder();
                ownership.EnsureInitialized(() => new AssistantApprovalService(f.Config, f.Prompt, router, f.Ledger, f.IssuerTelemetry, f.Time, true), router);
                var executor = new SetCustomPropertyExecutor(
                    f.Config,
                    new AssistantToolPolicy(),
                    f.AuditLog,
                    f.MutationTelemetry,
                    f.Ledger,
                    f.Dispatcher,
                    f.Adapter,
                    new[] { f.Descriptor },
                    ownership,
                    () => f.Session,
                    () => f.Reader,
                    () => f.Now,
                    true);
                executor.LockerListProvider = path => new[] { -1 };
                var result = FinishSync(executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("denied", result.Status);
                StringAssert.Contains(result.Message, "target locked");
                Assert.AreEqual(0, f.Prompt.Calls);
                AssertNoWrites(f.Session);
            }
        }

        [TestMethod]
        public void OwnedProcessLockAdmitsToPrompt()
        {
            using (var f = new Fixture())
            {
                int owned = System.Diagnostics.Process.GetCurrentProcess().Id;
                var ownership = new MutationExecutionOwnership();
                var router = new RoutingPreviewBuilder();
                ownership.EnsureInitialized(() => new AssistantApprovalService(f.Config, f.Prompt, router, f.Ledger, f.IssuerTelemetry, f.Time, true), router);
                var executor = new SetCustomPropertyExecutor(
                    f.Config,
                    new AssistantToolPolicy(),
                    f.AuditLog,
                    f.MutationTelemetry,
                    f.Ledger,
                    f.Dispatcher,
                    f.Adapter,
                    new[] { f.Descriptor },
                    ownership,
                    () => f.Session,
                    () => f.Reader,
                    () => f.Now,
                    true);
                executor.LockerListProvider = path => new[] { owned };
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Denied);
                var result = FinishSync(executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("denied", result.Status);
                Assert.AreEqual(1, f.Prompt.Calls, "Own-process lock must pass validation to the prompt boundary.");
                Assert.IsFalse(result.Message.Contains("target locked"), "Denial must come from the Deny outcome, not the lock probe.");
                AssertNoWrites(f.Session);
            }
        }

        [TestMethod]
        public void LockerEnumerationUnavailableDenies()
        {
            using (var f = new Fixture())
            {
                var ownership = new MutationExecutionOwnership();
                var router = new RoutingPreviewBuilder();
                ownership.EnsureInitialized(() => new AssistantApprovalService(f.Config, f.Prompt, router, f.Ledger, f.IssuerTelemetry, f.Time, true), router);
                var executor = new SetCustomPropertyExecutor(
                    f.Config,
                    new AssistantToolPolicy(),
                    f.AuditLog,
                    f.MutationTelemetry,
                    f.Ledger,
                    f.Dispatcher,
                    f.Adapter,
                    new[] { f.Descriptor },
                    ownership,
                    () => f.Session,
                    () => f.Reader,
                    () => f.Now,
                    true);
                executor.LockerListProvider = path => { throw new InvalidOperationException("synthetic enumeration failure"); };
                var result = FinishSync(executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("denied", result.Status);
                StringAssert.Contains(result.Message, "target lock state unknown");
                Assert.AreEqual(0, f.Prompt.Calls);
                AssertNoWrites(f.Session);
            }
        }

        [TestMethod]
        public void ReopenTimeReplacementYieldsUncertain()
        {
            using (var f = new Fixture())
            {
                f.Session.ReopenHandler = () =>
                {
                    File.WriteAllText(f.TargetPath, "tampered-bytes");
                    return null;
                };
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Approved);
                try
                {
                    var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                    Assert.AreEqual("save_uncertain", result.Status);
                    Assert.AreEqual(1, result.Receipt.MutationCount);
                    Assert.IsTrue(File.Exists(f.CheckpointForRequest("request")), "Checkpoint must be preserved.");
                }
                finally
                {
                    File.WriteAllText(f.TargetPath, "v1-bytes");
                }
            }
        }

        [TestMethod]
        public void PostSaveReadFailureLeavesOpenWithSaveReturned()
        {
            using (var f = new Fixture())
            {
                bool failReads = false;
                f.Session.SaveHandler = () =>
                {
                    failReads = true;
                    return null;
                };
                f.Reader.Handler = (query, calls) =>
                {
                    if (failReads) throw new InvalidOperationException("synthetic late read failure");
                    return ReaderResult.ProofFor(calls >= 5 ? "new" : "old", query.TargetFullPath);
                };
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Approved);
                var result = FinishSync(f.Executor.ExecuteAsync(Request(f.TargetPath), "trace"));
                Assert.AreEqual("save_returned", result.Status);
                Assert.IsFalse(f.Session.ComCalls.Contains("reopen"), "Failed verification must leave the document open.");
                Assert.IsTrue(File.Exists(f.CheckpointForRequest("request")), "Checkpoint must be preserved.");
            }
        }

        [TestMethod]
        public void CheckpointLocationFailureFailsBeforeWrite()
        {
            using (var f = new Fixture())
            {
                var pending = f.Executor.ExecuteAsync(Request(f.TargetPath), "trace");
                Assert.IsTrue(f.Prompt.Shown.Wait(TimeSpan.FromSeconds(5)));
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
        public void ServiceBranchRoutesMutationTool()
        {
            var config = new AgentConfig { Assistant = new AssistantSettings { Mutations = new AssistantMutationSettings { Enabled = true, ApprovalTimeoutSeconds = 60 } } };
            var service = new AssistantToolService(config);
            var result = FinishSync(service.ExecuteAsync(Request("C:\\unused\\part.sldprt"), "trace"));
            Assert.AreEqual("disabled", result.Status);
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

        private sealed class ReaderResult
        {
            internal MutationAdmissionProof Proof;
            internal string Error;

            internal static ReaderResult ProofFor(
                string literal,
                string targetPath,
                string raw = null,
                bool? wasResolved = null,
                bool? linked = null,
                bool? editable = null,
                MutationNativeKind? kind = null,
                bool? clean = null)
            {
                return new ReaderResult
                {
                    Proof = new MutationAdmissionProof
                    {
                        Found = true,
                        Literal = literal,
                        RawValue = raw ?? literal,
                        ResolvedValue = literal,
                        WasResolved = wasResolved ?? true,
                        NativeKind = kind ?? MutationNativeKind.Text,
                        IsLinkedOrExpression = linked ?? false,
                        IsEditable = editable ?? true,
                        Configurations = new List<string> { "Default" },
                        ActiveConfiguration = "Default",
                        IsClean = clean ?? true,
                        IsReadOnly = false,
                        DocumentPath = targetPath
                    }
                };
            }

            internal static ReaderResult Failure(string error)
            {
                return new ReaderResult { Error = error };
            }

            internal static ReaderResult Absent()
            {
                return new ReaderResult { Error = "property absent" };
            }
        }

        private static CustomPropertySnapshot Prop(string name, string value)
        {
            return new CustomPropertySnapshot
            {
                Name = name,
                NormalizedName = (name ?? string.Empty).Trim().ToLowerInvariant(),
                Scope = "Document",
                ResolvedValue = value,
                RawValue = value,
                WasResolved = true,
                LinkedOrExpressionStatus = "None",
                EditableStatusWhenAvailable = "Editable"
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
            internal readonly FakeReader Reader = new FakeReader();
            internal readonly LedgerProbe Ledger = new LedgerProbe();
            internal readonly IssuerTelemetryProbe IssuerTelemetry = new IssuerTelemetryProbe();
            internal readonly MutationTelemetryProbe MutationTelemetry = new MutationTelemetryProbe();
            internal readonly FakeTime Time = new FakeTime();
            internal readonly AssistantToolAuditLog AuditLog;
            internal readonly MutationExecutionOwnership Ownership = new MutationExecutionOwnership();
            internal readonly RoutingPreviewBuilder Router = new RoutingPreviewBuilder();
            internal readonly SetCustomPropertyExecutor Executor;
            internal AssistantApprovalService CapturedIssuer;
            internal DateTime Now = DateTime.UtcNow;

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
                Session.ActivePath = TargetPath;
                Adapter.PreviewBefore = "old";
                Reader.Handler = (query, calls) =>
                {
                    var literal = calls >= 5 ? "new" : "old";
                    return ReaderResult.ProofFor(literal, query.TargetFullPath);
                };
                var catalog = new[] { Descriptor };
                var fixture = this;
                Ownership.EnsureInitialized(() =>
                {
                    var issuer = new AssistantApprovalService(Config, Prompt, Router, Ledger, IssuerTelemetry, Time, lab);
                    fixture.CapturedIssuer = issuer;
                    return issuer;
                }, Router);
                Executor = new SetCustomPropertyExecutor(
                    Config,
                    new AssistantToolPolicy(),
                    AuditLog,
                    MutationTelemetry,
                    Ledger,
                    Dispatcher,
                    Adapter,
                    catalog,
                    Ownership,
                    () => Session,
                    () => Reader,
                    () => Now,
                    lab);
            }

            internal SetCustomPropertyExecutor RestartedExecutor()
            {
                var ownership = new MutationExecutionOwnership();
                var fixture = this;
                ownership.EnsureInitialized(() =>
                {
                    var issuer = new AssistantApprovalService(Config, Prompt, Router, Ledger, IssuerTelemetry, Time, true);
                    fixture.CapturedIssuer = issuer;
                    return issuer;
                }, Router);
                return new SetCustomPropertyExecutor(
                    Config,
                    new AssistantToolPolicy(),
                    AuditLog,
                    MutationTelemetry,
                    Ledger,
                    Dispatcher,
                    Adapter,
                    new[] { Descriptor },
                    ownership,
                    () => Session,
                    () => Reader,
                    () => Now,
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
                    Dispatcher,
                    Adapter,
                    new[] { Descriptor },
                    fixture.Ownership,
                    () => Session,
                    () => Reader,
                    () => Now,
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
                    null,
                    null,
                    new[] { Descriptor },
                    new MutationExecutionOwnership(),
                    () => Session,
                    () => Reader,
                    () => Now,
                    true);
            }

            internal SetCustomPropertyExecutor ExecutorWithTelemetry(IMutationTelemetrySink sink)
            {
                var fixture = this;
                return new SetCustomPropertyExecutor(
                    Config,
                    new AssistantToolPolicy(),
                    AuditLog,
                    sink,
                    Ledger,
                    Dispatcher,
                    Adapter,
                    new[] { Descriptor },
                    fixture.Ownership,
                    () => Session,
                    () => Reader,
                    () => Now,
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

            internal string CheckpointForRequest(string requestId)
            {
                var directory = Path.Combine(FilesDir, ".checkpoints");
                if (!Directory.Exists(directory)) return null;
                var expected = MutationExecutionJournal.HashTarget(TargetPath) + "-" + MutationExecutionJournal.HashRequest(requestId) + ".sldprt";
                var candidate = Path.Combine(directory, expected);
                return File.Exists(candidate) ? candidate : null;
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

        private sealed class FlipArmingDispatcher : ISolidWorksMainThreadDispatcher
        {
            private readonly FakeDispatcher _inner;
            private readonly AgentConfig _config;
            internal bool Armed;

            internal FlipArmingDispatcher(FakeDispatcher inner, AgentConfig config)
            {
                _inner = inner;
                _config = config;
            }

            public int MainThreadId
            {
                get { return _inner.MainThreadId; }
            }

            public bool CheckAccess()
            {
                return _inner.CheckAccess();
            }

            public void VerifyAccess()
            {
                _inner.VerifyAccess();
            }

            public void Invoke(Action action)
            {
                _inner.Invoke(action);
            }

            public bool TryInvoke(Action action)
            {
                if (Armed)
                {
                    Armed = false;
                    _config.Assistant.Mutations.Enabled = false;
                }
                return _inner.TryInvoke(action);
            }
        }

        private sealed class StrictMainThreadDispatcher : ISolidWorksMainThreadDispatcher, IDisposable
        {
            private readonly BlockingCollection<Action> _queue = new BlockingCollection<Action>();
            private readonly Thread _mainThread;
            private readonly int _mainThreadId;
            private bool _disposed;

            internal StrictMainThreadDispatcher()
            {
                int captured = 0;
                using (var ready = new ManualResetEventSlim())
                {
                    _mainThread = new Thread(() =>
                    {
                        captured = Thread.CurrentThread.ManagedThreadId;
                        ready.Set();
                        foreach (var work in _queue.GetConsumingEnumerable())
                        {
                            work();
                        }
                    });
                    _mainThread.IsBackground = true;
                    _mainThread.Start();
                    Assert.IsTrue(ready.Wait(TimeSpan.FromSeconds(15)), "Strict main thread did not start.");
                    _mainThreadId = captured;
                }
            }

            public int MainThreadId
            {
                get { return _mainThreadId; }
            }

            public bool CheckAccess()
            {
                return Thread.CurrentThread.ManagedThreadId == _mainThreadId;
            }

            public void VerifyAccess()
            {
                if (!CheckAccess()) throw new SolidWorksThreadViolationException("strict thread violation");
            }

            public void Invoke(Action action)
            {
                if (!TryInvoke(action)) throw new SolidWorksThreadViolationException("strict marshal unavailable");
            }

            public bool TryInvoke(Action action)
            {
                if (action == null) throw new ArgumentNullException("action");
                if (CheckAccess())
                {
                    action();
                    return true;
                }
                Exception fault = null;
                using (var done = new ManualResetEventSlim())
                {
                    try
                    {
                        _queue.Add(() =>
                        {
                            try
                            {
                                action();
                            }
                            catch (Exception ex)
                            {
                                fault = ex;
                            }
                            finally
                            {
                                done.Set();
                            }
                        });
                    }
                    catch (InvalidOperationException)
                    {
                        return false;
                    }
                    if (!done.Wait(TimeSpan.FromSeconds(15))) return false;
                }
                if (fault != null) throw fault;
                return true;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _queue.CompleteAdding();
                _mainThread.Join(TimeSpan.FromSeconds(15));
                _queue.Dispose();
            }
        }

        private sealed class StrictComSource : ISwMutationComSource
        {
            internal readonly Dictionary<string, string> Values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            internal string Path;
            internal bool Dirty;
            internal Func<string, string[]> NamesOverride;

            public string Scope
            {
                get { return "Document"; }
            }

            public bool ContainsProperty(string name)
            {
                string[] names = NamesOverride != null ? NamesOverride("Document") : new List<string>(Values.Keys).ToArray();
                if (names == null) return false;
                foreach (var candidate in names)
                {
                    if (string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase)) return true;
                }
                return false;
            }

            public bool TryGetText(string name, out string rawValue, out string resolvedValue, out bool wasResolved, out int nativeType, out bool linkedOrExpression, out string error)
            {
                rawValue = null;
                resolvedValue = null;
                wasResolved = false;
                nativeType = 0;
                linkedOrExpression = false;
                error = null;
                string found;
                if (!Values.TryGetValue(name ?? string.Empty, out found)) return false;
                rawValue = found;
                resolvedValue = found;
                wasResolved = true;
                nativeType = (int)swCustomInfoType_e.swCustomInfoText;
                linkedOrExpression = false;
                return true;
            }

            public bool TryGetEditable(string name, out bool editable, out string error)
            {
                editable = false;
                error = null;
                if (!Values.ContainsKey(name ?? string.Empty))
                {
                    error = "absent";
                    return false;
                }
                editable = true;
                return true;
            }

            public IReadOnlyList<string> GetConfigurationNames()
            {
                return new List<string> { "Default" };
            }

            public string GetActiveConfigurationName()
            {
                return "Default";
            }

            public bool GetDirtyFlag()
            {
                return Dirty;
            }

            public bool GetReadOnlyFlag()
            {
                return false;
            }

            public string GetDocumentPath()
            {
                return Path;
            }
        }

        private sealed class FakeAdapter : ICustomPropertyReadAdapter
        {
            internal string PreviewBefore = "old";
            internal bool ThrowOnBuild;
            internal int Calls;

            public string AdapterName
            {
                get { return "TestAdapter"; }
            }

            public PropertyAuditSnapshot ReadCustomProperties(AuditRunRequest request, out List<AuditError> errors)
            {
                Calls++;
                errors = new List<AuditError>();
                if (ThrowOnBuild) throw new InvalidOperationException("private builder failure");
                string name = "Description";
                if (request != null && request.RequestedPropertyNames != null && request.RequestedPropertyNames.Count > 0)
                {
                    name = request.RequestedPropertyNames[0];
                }
                return Bundle(DocScope(Prop(name, PreviewBefore)), CfgScope("Default"));
            }
        }

        private sealed class FakeReader : IMutationAdmissionReader
        {
            internal Func<MutationAdmissionQuery, int, ReaderResult> Handler;
            internal MutationAdmissionQuery Last;
            internal int Calls;

            public bool TryProveTarget(MutationAdmissionQuery query, out MutationAdmissionProof proof, out string error)
            {
                Calls++;
                Last = query;
                var outcome = Handler == null ? null : Handler(query, Calls);
                if (outcome == null)
                {
                    proof = null;
                    error = null;
                    return false;
                }
                proof = outcome.Proof;
                error = outcome.Error;
                return proof != null && error == null;
            }
        }

        private sealed class FakeSession : ICustomPropertyMutationSession
        {
            internal string ActivePath;
            internal bool IsCurrent = true;
            internal string IdentityError;
            internal Func<string, string, string> WriteHandler;
            internal Func<string> SaveHandler;
            internal Func<string> ReopenHandler;
            internal readonly List<string> ComCalls = new List<string>();
            internal readonly Dictionary<string, string> WritesByTarget = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            internal string LastWrite;

            public bool IsCurrentTarget(out string activePath, out string error)
            {
                ComCalls.Add("identity");
                activePath = ActivePath;
                error = IdentityError;
                if (IdentityError != null) return false;
                return IsCurrent;
            }

            public bool TryWriteProperty(string name, string value, out string error)
            {
                ComCalls.Add("write:" + name + "=" + value);
                LastWrite = "value:" + value;
                WritesByTarget[ActivePath ?? string.Empty] = "value:" + value;
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

            internal string WritesFor(string target)
            {
                string value;
                return WritesByTarget.TryGetValue(target, out value) ? value : LastWrite;
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

    [TestClass]
    public class MutationOwnershipTests
    {
        [TestMethod]
        public void EnsureIsIdempotentAndExposesIssuer()
        {
            var ownership = new MutationExecutionOwnership();
            Assert.IsNull(ownership.GetIssuer());
            Assert.IsNull(ownership.GetRouter());
            Assert.AreEqual(0, ownership.Generation);
            Assert.IsFalse(ownership.IsShutdown);
            var router = new RoutingPreviewBuilder();
            var ledger = new LedgerProbe();
            var telemetry = new IssuerTelemetryProbe();
            var time = new FakeTime();
            var config = new AgentConfig { Assistant = new AssistantSettings { Mutations = new AssistantMutationSettings { Enabled = true } } };
            var prompt = new FakePrompt();
            var calls = 0;
            ownership.EnsureInitialized(() =>
            {
                calls++;
                return new AssistantApprovalService(config, prompt, router, ledger, telemetry, time, true);
            }, router);
            var first = ownership.GetIssuer();
            Assert.IsNotNull(first);
            Assert.AreSame(router, ownership.GetRouter());
            ownership.EnsureInitialized(() => { throw new InvalidOperationException("must not rebuild"); }, router);
            Assert.AreSame(first, ownership.GetIssuer());
            Assert.AreEqual(1, calls);
        }

        [TestMethod]
        public void NotifyShutdownCommitsThenDisposesOutsideLock()
        {
            var ownership = new MutationExecutionOwnership();
            var prompt = new ReentrantPrompt(ownership);
            var ledger = new LedgerProbe();
            var telemetry = new IssuerTelemetryProbe();
            var time = new FakeTime();
            var config = new AgentConfig { Assistant = new AssistantSettings { Mutations = new AssistantMutationSettings { Enabled = true } } };
            var router = new RoutingPreviewBuilder();
            var stubPreview = new ApprovalPreview("t", "s", "b", "a", "r");
            AssistantApprovalService issuer = null;
            ownership.EnsureInitialized(() =>
            {
                issuer = new AssistantApprovalService(config, prompt, new StubPreviewBuilder(stubPreview), ledger, telemetry, time, true);
                return issuer;
            }, router);
            Assert.AreSame(issuer, ownership.GetIssuer());
            Assert.AreSame(router, ownership.GetRouter());
            var pending = issuer.RequestApprovalAsync(
                new AssistantToolDescriptor { Name = "tool", CapabilityId = "tool" },
                new AssistantToolRequest { RequestId = "r", Parameters = new Dictionary<string, string>() },
                "trace");
            Assert.IsTrue(prompt.Shown.Wait(TimeSpan.FromSeconds(5)));
            var shutdown = Task.Run(() => ownership.NotifyShutdown());
            Assert.IsTrue(shutdown.Wait(TimeSpan.FromSeconds(10)), "NotifyShutdown must not deadlock on reentrant callbacks.");
            Assert.IsTrue(ownership.IsShutdown);
            Assert.AreEqual(1, ownership.Generation);
            Assert.IsNull(ownership.GetIssuer());
            Assert.IsNull(ownership.GetRouter());
            Assert.IsNull(FinishSync(pending));
            ownership.NotifyShutdown();
            Assert.AreEqual(1, ownership.Generation, "Shutdown is idempotent.");
            Assert.IsTrue(prompt.Reentered);
            Assert.IsTrue(prompt.SecondCallbackRan);
        }

        private static T FinishSync<T>(Task<T> task)
        {
            Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(15)), "Operation did not settle.");
            return task.GetAwaiter().GetResult();
        }

        private sealed class LedgerProbe : IApprovalLedgerSink
        {
            internal readonly List<AssistantApprovalLedgerEntry> Entries = new List<AssistantApprovalLedgerEntry>();

            public void Append(AssistantApprovalLedgerEntry entry)
            {
                Entries.Add(entry);
            }
        }

        private sealed class IssuerTelemetryProbe : IApprovalLifecycleTelemetrySink
        {
            public void Record(AssistantApprovalLedgerEntry entry, bool authorityIssued)
            {
            }
        }

        private sealed class FakeTime : IApprovalTimeProvider
        {
            internal DateTime Now = DateTime.UtcNow.AddHours(1);
            public DateTime UtcNow
            {
                get { return Now; }
            }

            public Task Delay(TimeSpan delay, CancellationToken cancellationToken)
            {
                var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                cancellationToken.Register(() => completion.TrySetCanceled());
                return completion.Task;
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

        private sealed class StubPreviewBuilder : IPreviewBuilder
        {
            private readonly ApprovalPreview _preview;

            internal StubPreviewBuilder(ApprovalPreview preview)
            {
                _preview = preview;
            }

            public ApprovalPreview Build(AssistantToolDescriptor descriptor, AssistantToolRequest request)
            {
                return _preview;
            }
        }

        private sealed class ReentrantPrompt : IApprovalPrompt
        {
            private readonly MutationExecutionOwnership _ownership;
            internal readonly ManualResetEventSlim Shown = new ManualResetEventSlim();
            internal bool Reentered;
            internal bool SecondCallbackRan;

            internal ReentrantPrompt(MutationExecutionOwnership ownership)
            {
                _ownership = ownership;
            }

            public Task<ApprovalPromptOutcome> ShowAsync(ApprovalPrompt prompt)
            {
                Shown.Set();
                prompt.CancellationToken.Register(() => SecondCallbackRan = true);
                prompt.CancellationToken.Register(() =>
                {
                    Reentered = _ownership.IsShutdown;
                    throw new InvalidOperationException("callback private error");
                });
                return new TaskCompletionSource<ApprovalPromptOutcome>(TaskCreationOptions.RunContinuationsAsynchronously).Task;
            }
        }
    }

    [TestClass]
    public class RoutingPreviewBuilderTests
    {
        [TestMethod]
        public void MissingBindingDenyNullAndOwnerOnlyRemoval()
        {
            var routing = new RoutingPreviewBuilder();
            var descriptor = new AssistantToolDescriptor { Name = "tool", CapabilityId = "tool" };
            Assert.IsNull(routing.Build(descriptor, TunedRequest("req", "C:\\x\\p.sldprt")));
            var winner = new StubBuilder("before");
            Assert.IsTrue(routing.Register("req", TargetHashFor("C:\\x\\p.sldprt"), winner));
            Assert.IsFalse(routing.Register("req", TargetHashFor("C:\\x\\p.sldprt"), new StubBuilder("other")), "Duplicates deny without replacing the winner.");
            routing.Unregister("req", TargetHashFor("C:\\x\\p.sldprt"), new StubBuilder("before"));
            Assert.IsNotNull(routing.Build(descriptor, TunedRequest("req", "C:\\x\\p.sldprt")));
            routing.Unregister("req", TargetHashFor("C:\\x\\p.sldprt"), winner);
            Assert.IsNull(routing.Build(descriptor, TunedRequest("req", "C:\\x\\p.sldprt")));
        }

        [TestMethod]
        public void SameIdDifferentTargetKeysStaySeparate()
        {
            var routing = new RoutingPreviewBuilder();
            var descriptor = new AssistantToolDescriptor { Name = "tool", CapabilityId = "tool" };
            var first = new StubBuilder("first");
            var second = new StubBuilder("second");
            Assert.IsTrue(routing.Register("same-id", TargetHashFor("C:\\a\\p.sldprt"), first));
            Assert.IsTrue(routing.Register("same-id", TargetHashFor("C:\\b\\q.sldprt"), second));
            Assert.AreEqual("first", routing.Build(descriptor, TunedRequest("same-id", "C:\\a\\p.sldprt")).Before);
            Assert.AreEqual("second", routing.Build(descriptor, TunedRequest("same-id", "C:\\b\\q.sldprt")).Before);
            routing.Unregister("same-id", TargetHashFor("C:\\b\\q.sldprt"), second);
            Assert.AreEqual("first", routing.Build(descriptor, TunedRequest("same-id", "C:\\a\\p.sldprt")).Before);
        }

        private static string TargetHashFor(string path)
        {
            return MutationExecutionJournal.HashTarget(Path.GetFullPath(path));
        }

        private static AssistantToolRequest TunedRequest(string requestId, string path)
        {
            return new AssistantToolRequest
            {
                RequestId = requestId,
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal) { ["file_path"] = path }
            };
        }

        private sealed class StubBuilder : IPreviewBuilder
        {
            private readonly string _before;

            internal StubBuilder(string before)
            {
                _before = before;
            }

            public ApprovalPreview Build(AssistantToolDescriptor descriptor, AssistantToolRequest request)
            {
                return new ApprovalPreview("t", "s", _before, "a", "r");
            }
        }
    }

    [TestClass]
    public class AdmissionReaderMappingTests
    {
        [TestMethod]
        public void EqualLiteralPasses()
        {
            var reader = ReaderWith(("Description", "old", "old", true, (int)global::SolidWorks.Interop.swconst.swCustomInfoType_e.swCustomInfoText, false, true));
            MutationAdmissionProof proof;
            string error;
            Assert.IsTrue(reader.TryProveTarget(Query(), out proof, out error), error);
            Assert.AreEqual("old", proof.Literal);
            Assert.IsTrue(proof.IsClean);
        }

        [TestMethod]
        public void EmptyLiteralPassesPositive()
        {
            var reader = ReaderWith(("Description", "", "", true, (int)global::SolidWorks.Interop.swconst.swCustomInfoType_e.swCustomInfoText, false, true));
            MutationAdmissionProof proof;
            string error;
            Assert.IsTrue(reader.TryProveTarget(Query(), out proof, out error), error);
            Assert.AreEqual("", proof.Literal);
        }

        [TestMethod]
        public void AbsentPropertyFails()
        {
            var reader = ReaderWith();
            MutationAdmissionProof proof;
            string error;
            Assert.IsFalse(reader.TryProveTarget(Query(), out proof, out error));
            Assert.AreEqual("property absent", error);
        }

        [TestMethod]
        public void NonTextNativeKindFails()
        {
            var reader = ReaderWith(("Description", "5", "5", true, 999, false, true));
            MutationAdmissionProof proof;
            string error;
            Assert.IsFalse(reader.TryProveTarget(Query(), out proof, out error));
            Assert.AreEqual("unsupported native type", error);
        }

        [TestMethod]
        public void LinkedValueFails()
        {
            var reader = ReaderWith(("Description", "old", "old", true, (int)global::SolidWorks.Interop.swconst.swCustomInfoType_e.swCustomInfoText, true, true));
            MutationAdmissionProof proof;
            string error;
            Assert.IsFalse(reader.TryProveTarget(Query(), out proof, out error));
            Assert.AreEqual("linked or expression value", error);
        }

        [TestMethod]
        public void UneditableValueFails()
        {
            var reader = ReaderWith(("Description", "old", "old", true, (int)global::SolidWorks.Interop.swconst.swCustomInfoType_e.swCustomInfoText, false, false));
            MutationAdmissionProof proof;
            string error;
            Assert.IsFalse(reader.TryProveTarget(Query(), out proof, out error));
            Assert.AreEqual("not editable", error);
        }

        [TestMethod]
        public void FailedStatusReadsFail()
        {
            var reader = ReaderWithNoProperty(true, false, false);
            MutationAdmissionProof proof;
            string error;
            Assert.IsFalse(reader.TryProveTarget(Query(), out proof, out error));
            Assert.IsFalse(string.IsNullOrEmpty(error));
        }

        [TestMethod]
        public void FailedEnumerationFails()
        {
            var reader = ReaderWithNoProperty(false, true, false);
                MutationAdmissionProof proof;
                string error;
                Assert.IsFalse(reader.TryProveTarget(Query(), out proof, out error));
                StringAssert.StartsWith(error, "enumeration unavailable");
        }

        [TestMethod]
        public void DirtyStateSurfacesInProof()
        {
            var reader = ReaderWith(("Description", "old", "old", true, (int)global::SolidWorks.Interop.swconst.swCustomInfoType_e.swCustomInfoText, false, true), dirty: true);
            MutationAdmissionProof proof;
            string error;
            Assert.IsTrue(reader.TryProveTarget(Query(), out proof, out error), error);
            Assert.IsFalse(proof.IsClean);
        }

        [TestMethod]
        public void NativeTypeCodeTextIsThirty()
        {
            Assert.AreEqual(30, (int)global::SolidWorks.Interop.swconst.swCustomInfoType_e.swCustomInfoText);
        }

        [TestMethod]
        public void NullNamesShapeReturnsAbsent()
        {
            var reader = ReaderWith(
                ("Description", "old", "old", true, (int)global::SolidWorks.Interop.swconst.swCustomInfoType_e.swCustomInfoText, false, true),
                configure: source => { source.NamesOverride = scope => null; });
            MutationAdmissionProof proof;
            string error;
            Assert.IsFalse(reader.TryProveTarget(Query(), out proof, out error));
            Assert.AreEqual("property absent", error);
        }

        [TestMethod]
        public void EmptyNamesShapeReturnsAbsent()
        {
            var reader = ReaderWith(
                ("Description", "old", "old", true, (int)global::SolidWorks.Interop.swconst.swCustomInfoType_e.swCustomInfoText, false, true),
                configure: source => { source.NamesOverride = scope => new string[0]; });
            MutationAdmissionProof proof;
            string error;
            Assert.IsFalse(reader.TryProveTarget(Query(), out proof, out error));
            Assert.AreEqual("property absent", error);
        }

        [TestMethod]
        public void FaultNamesShapeThrowsEnumerationFailure()
        {
            var source = new FakeComSource();
            source.NamesOverride = scope => { throw new InvalidOperationException("synthetic names failure"); };
            try
            {
                source.ContainsProperty("Description");
                Assert.Fail("Faulted names enumeration must throw.");
            }
            catch (InvalidOperationException)
            {
            }
        }

        [TestMethod]
        public void NullConfigurationListDeniesUnavailable()
        {
            var reader = ReaderWithNoProperty(false, false, false, source => { source.NullConfigurations = true; });
            MutationAdmissionProof proof;
            string error;
            Assert.IsFalse(reader.TryProveTarget(Query(), out proof, out error));
            StringAssert.StartsWith(error, "enumeration unavailable");
        }

        [TestMethod]
        public void AddResultCodesArePinned()
        {
            Assert.AreEqual(0, (int)global::SolidWorks.Interop.swconst.swCustomInfoAddResult_e.swCustomInfoAddResult_AddedOrChanged);
            Assert.AreEqual(1, (int)global::SolidWorks.Interop.swconst.swCustomInfoAddResult_e.swCustomInfoAddResult_GenericFail);
            Assert.AreEqual(2, (int)global::SolidWorks.Interop.swconst.swCustomInfoAddResult_e.swCustomInfoAddResult_MismatchAgainstExistingType);
            Assert.AreEqual(3, (int)global::SolidWorks.Interop.swconst.swCustomInfoAddResult_e.swCustomInfoAddResult_MismatchAgainstSpecifiedType);
            Assert.AreEqual(4, (int)global::SolidWorks.Interop.swconst.swCustomInfoAddResult_e.swCustomInfoAddResult_MismatchAgainstLegacyTypes);
        }

        [TestMethod]
        public void GetResultCodesArePinned()
        {
            Assert.AreEqual(0, (int)global::SolidWorks.Interop.swconst.swCustomInfoGetResult_e.swCustomInfoGetResult_CachedValue);
            Assert.AreEqual(1, (int)global::SolidWorks.Interop.swconst.swCustomInfoGetResult_e.swCustomInfoGetResult_NotPresent);
            Assert.AreEqual(2, (int)global::SolidWorks.Interop.swconst.swCustomInfoGetResult_e.swCustomInfoGetResult_ResolvedValue);
        }

        [TestMethod]
        public void GetStatusMappingAcceptsCachedAndResolved()
        {
            bool absent;
            string error;
            Assert.IsTrue(SwComSource.MapCustomInfoGetStatus(0, out absent, out error));
            Assert.IsFalse(absent);
            Assert.IsNull(error);
            Assert.IsTrue(SwComSource.MapCustomInfoGetStatus(2, out absent, out error));
            Assert.IsFalse(absent);
            Assert.IsNull(error);
            Assert.IsFalse(SwComSource.MapCustomInfoGetStatus(1, out absent, out error));
            Assert.IsTrue(absent);
            Assert.AreEqual("property absent", error);
            Assert.IsFalse(SwComSource.MapCustomInfoGetStatus(99, out absent, out error));
            Assert.IsFalse(absent);
            Assert.AreEqual("read status 99", error);
        }

        [TestMethod]
        public void MutationPathOptionCodesArePinned()
        {
            Assert.AreEqual(0, (int)global::SolidWorks.Interop.swconst.swCustomPropertyAddOption_e.swCustomPropertyOnlyIfNew);
            Assert.AreEqual(1, (int)global::SolidWorks.Interop.swconst.swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
            Assert.AreEqual(2, (int)global::SolidWorks.Interop.swconst.swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
            Assert.AreEqual(1, (int)global::SolidWorks.Interop.swconst.swDocumentTypes_e.swDocPART);
            Assert.AreEqual(1, (int)global::SolidWorks.Interop.swconst.swOpenDocOptions_e.swOpenDocOptions_Silent);
            Assert.AreEqual(1, (int)global::SolidWorks.Interop.swconst.swSaveAsOptions_e.swSaveAsOptions_Silent);
        }

        [TestMethod]
        public void SaveLoadErrorCodesAreNonzero()
        {
            Assert.AreNotEqual(0, (int)global::SolidWorks.Interop.swconst.swFileSaveError_e.swGenericSaveError);
            Assert.AreNotEqual(0, (int)global::SolidWorks.Interop.swconst.swFileSaveError_e.swReadOnlySaveError);
            Assert.AreNotEqual(0, (int)global::SolidWorks.Interop.swconst.swFileLoadError_e.swFileNotFoundError);
        }

        private static MutationAdmissionQuery Query()
        {
            return new MutationAdmissionQuery
            {
                TargetFullPath = "C:\\t\\part.sldprt",
                PropertyName = "Description",
                CorrelationId = "trace",
                ConfigurationReadLimit = 64
            };
        }

        private static SwComAdmissionReader ReaderWith()
        {
            var source = new FakeComSource();
            var dispatcher = new FakeDispatcher();
            return new SwComAdmissionReader(dispatcher, cfg => source);
        }

        private static SwComAdmissionReader ReaderWith(
            ValueTuple<string, string, string, bool, int, bool, bool> property,
            bool failRead = false,
            bool failEnumerate = false,
            bool dirty = false,
            string path = "C:\\t\\part.sldprt",
            Action<FakeComSource> configure = null)
        {
            var source = new FakeComSource();
            source.Path = path;
            source.Dirty = dirty;
            source.Properties[property.Item1] = new ComProp
            {
                Raw = property.Item2,
                Resolved = property.Item3,
                WasResolved = property.Item4,
                NativeType = property.Item5,
                Linked = property.Item6,
                Editable = property.Item7
            };
            source.FailRead = failRead;
            source.FailEnumerate = failEnumerate;
            if (configure != null) configure(source);
            var dispatcher = new FakeDispatcher();
            var emptyScope = new FakeComSource();
            emptyScope.Path = path;
            emptyScope.Dirty = dirty;
            return new SwComAdmissionReader(dispatcher, cfg => string.IsNullOrEmpty(cfg) ? source : emptyScope);
        }

        private static SwComAdmissionReader ReaderWithNoProperty(bool failRead, bool failEnumerate, bool dirty, Action<FakeComSource> configure = null)
        {
            var source = new FakeComSource();
            source.FailRead = failRead;
            source.FailEnumerate = failEnumerate;
            source.Dirty = dirty;
            if (configure != null) configure(source);
            var dispatcher = new FakeDispatcher();
            return new SwComAdmissionReader(dispatcher, cfg => source);
        }

        private sealed class ComProp
        {
            internal string Raw;
            internal string Resolved;
            internal bool WasResolved;
            internal int NativeType;
            internal bool Linked;
            internal bool Editable;
        }

        private sealed class FakeDispatcher : ISolidWorksMainThreadDispatcher
        {
            public int MainThreadId
            {
                get { return Thread.CurrentThread.ManagedThreadId; }
            }

            public bool CheckAccess()
            {
                return true;
            }

            public void VerifyAccess()
            {
            }

            public void Invoke(Action action)
            {
                action();
            }

            public bool TryInvoke(Action action)
            {
                action();
                return true;
            }
        }

        private sealed class FakeComSource : ISwMutationComSource
        {
            internal readonly Dictionary<string, ComProp> Properties = new Dictionary<string, ComProp>(StringComparer.OrdinalIgnoreCase);
            internal bool FailRead;
            internal bool FailEnumerate;
            internal bool Dirty;
            internal string Path = "C:\\t\\part.sldprt";
            internal Func<string, string[]> NamesOverride;
            internal bool NullConfigurations;

            public string Scope
            {
                get { return "Document"; }
            }

            public bool ContainsProperty(string name)
            {
                if (FailRead) throw new InvalidOperationException("synthetic enumeration failure");
                string[] names = NamesOverride != null ? NamesOverride("Document") : new List<string>(Properties.Keys).ToArray();
                if (names == null) return false;
                foreach (var candidate in names)
                {
                    if (string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase)) return true;
                }
                return false;
            }

            public bool TryGetText(string name, out string rawValue, out string resolvedValue, out bool wasResolved, out int nativeType, out bool linkedOrExpression, out string error)
            {
                rawValue = null;
                resolvedValue = null;
                wasResolved = false;
                nativeType = 0;
                linkedOrExpression = false;
                error = null;
                if (FailRead) throw new InvalidOperationException("synthetic read failure");
                if (!ContainsProperty(name)) return false;
                ComProp found;
                if (!Properties.TryGetValue(name ?? string.Empty, out found)) return false;
                rawValue = found.Raw;
                resolvedValue = found.Resolved;
                wasResolved = found.WasResolved;
                nativeType = found.NativeType;
                linkedOrExpression = found.Linked;
                return true;
            }

            public bool TryGetEditable(string name, out bool editable, out string error)
            {
                editable = false;
                error = null;
                if (FailRead) throw new InvalidOperationException("synthetic read failure");
                ComProp found;
                if (!Properties.TryGetValue(name ?? string.Empty, out found))
                {
                    error = "absent";
                    return false;
                }
                editable = found.Editable;
                return true;
            }

            public IReadOnlyList<string> GetConfigurationNames()
            {
                if (FailEnumerate) throw new InvalidOperationException("synthetic enumeration failure");
                if (NullConfigurations) throw new InvalidOperationException("synthetic configuration enumeration failure");
                return new List<string> { "Default" };
            }

            public string GetActiveConfigurationName()
            {
                return "Default";
            }

            public bool GetDirtyFlag()
            {
                return Dirty;
            }

            public bool GetReadOnlyFlag()
            {
                return false;
            }

            public string GetDocumentPath()
            {
                return Path;
            }
        }
    }

    [TestClass]
    public class ApprovalPromptHostTests
    {
        [TestMethod]
        public void HostCreatesFreshDialogPerCall()
        {
            var dispatcher = new FakeDispatcher();
            var shown = new List<ApprovalDialog>();
            var host = new ApprovalPromptHost(dispatcher, d => shown.Add(d));
            var first = host.ShowAsync(Prompt("one"));
            Assert.AreEqual(1, shown.Count);
            shown[0].OnApproveClicked();
            Assert.AreEqual(ApprovalPromptOutcome.Approved, FinishSync(first));
            var second = host.ShowAsync(Prompt("two"));
            Assert.AreEqual(2, shown.Count);
            Assert.AreNotSame(shown[0], shown[1]);
            shown[1].OnDenyClicked();
            Assert.AreEqual(ApprovalPromptOutcome.Denied, FinishSync(second));
            Assert.IsTrue(shown[0].IsDisposed);
            Assert.IsTrue(shown[1].IsDisposed);
        }

        private static ApprovalPrompt Prompt(string requestId)
        {
            return new ApprovalPrompt(
                requestId,
                "capability",
                "trace",
                "session",
                "Lab",
                DateTime.UtcNow.AddSeconds(60),
                new AssistantToolDescriptor { Name = "tool", CapabilityId = "capability" },
                new AssistantToolRequest { RequestId = requestId },
                new ApprovalPreview("title", "summary", "before", "after", "risk"),
                CancellationToken.None);
        }

        private static T FinishSync<T>(Task<T> task)
        {
            Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(15)), "Prompt did not settle.");
            return task.GetAwaiter().GetResult();
        }

        private sealed class FakeDispatcher : ISolidWorksMainThreadDispatcher
        {
            public int MainThreadId
            {
                get { return Thread.CurrentThread.ManagedThreadId; }
            }

            public bool CheckAccess()
            {
                return true;
            }

            public void VerifyAccess()
            {
            }

            public void Invoke(Action action)
            {
                action();
            }

            public bool TryInvoke(Action action)
            {
                if (action == null) throw new ArgumentNullException(nameof(action));
                action();
                return true;
            }
        }
    }
}
