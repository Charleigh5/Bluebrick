using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlueBrick.Agent;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace BlueBrick.UI.Tests.Agent
{
    [TestClass]
    public class AssistantApprovalServiceTests
    {
        [TestMethod]
        public async Task NormalDebugConstructorCannotIssueEvenWhenEnabled()
        {
            using (var f = new Fixture())
            {
                var ledger = new AssistantApprovalLedger(Path.Combine(f.Root, "ledger.jsonl"));
                var logger = new TelemetryLogger(Path.Combine(f.Root, "logs"), sampleRateSuccess: 0);
                using (var service = new AssistantApprovalService(f.Config, f.Prompt, f.Builder, ledger, logger))
                {
                    Assert.IsFalse(AppIdentity.IsLabBuild);
                    Assert.IsNull(await service.RequestApprovalAsync(Descriptor(), Request(), "trace"));
                    Assert.AreEqual(0, f.Builder.Calls);
                    Assert.AreEqual(0, f.Prompt.Calls);
                    Assert.AreEqual("denied", ledger.Tail(5).Single().Event);
                    Assert.AreEqual("Production", ledger.Tail(5).Single().Environment);
                    Assert.AreEqual("denied", (string)logger.Tail(5).Single()["metadata"]["lifecycleEvent"]);
                }
            }
        }

        [TestMethod]
        public async Task DebugLabOverrideIssuesBoundUnconsumedSingleUseAuthority()
        {
            using (var f = new Fixture())
            {
                var descriptor = Descriptor();
                var request = Request();
                var pending = f.Service.RequestApprovalAsync(descriptor, request, "trace");
                f.Prompt.Complete(ApprovalPromptOutcome.Approved);
                var token = await Finish(pending);
                Assert.IsNotNull(token);
                Assert.IsTrue(token.Granted);
                Assert.IsTrue(token.IsServerIssued);
                Assert.AreEqual("native_dialog", token.ApprovedBy);
                Assert.AreEqual("Lab", token.Environment);
                Assert.AreEqual(f.Prompt.Last.ExpiresUtc, token.ExpiresUtc);
                Assert.IsNull(token.ConsumedUtc);
                Assert.IsTrue(Valid(token, descriptor, request));
                Assert.IsFalse(Valid(token, descriptor, request, requestId: "other"));
                Assert.IsFalse(Valid(token, descriptor, request, sessionId: "other"));
                Assert.IsFalse(Valid(token, descriptor, request, environment: "Production"));
                Assert.IsFalse(Valid(token, new AssistantToolDescriptor { CapabilityId = "other" }, request));
                Assert.IsFalse(Valid(token, null, request));
                Assert.IsFalse(token.IsValidFor(descriptor, request, "request", "session", "Lab", token.ExpiresUtc.Value));
                token.ConsumedUtc = DateTime.UtcNow;
                Assert.IsFalse(Valid(token, descriptor, request));
                f.Evidence.AssertTransitions("requested", "issued");
                CollectionAssert.AreEqual(new[] { false, true }, f.Evidence.Authority.ToArray());
            }
        }

        [DataTestMethod]
        [DataRow("tool")]
        [DataRow("query")]
        [DataRow("limit")]
        [DataRow("scope")]
        [DataRow("value")]
        [DataRow("key")]
        [DataRow("add")]
        [DataRow("remove")]
        public async Task TokenRejectsEveryDigestFieldTamper(string field)
        {
            using (var f = new Fixture())
            {
                var pending = f.Service.RequestApprovalAsync(Descriptor(), Request(), "trace");
                f.Prompt.Complete(ApprovalPromptOutcome.Approved);
                var token = await Finish(pending);
                var changed = Request();
                switch (field)
                {
                    case "tool": changed.ToolName = "other"; break;
                    case "query": changed.Query = "other"; break;
                    case "limit": changed.Limit++; break;
                    case "scope": changed.ScopeId = "other"; break;
                    case "value": changed.Parameters["name"] = "other"; break;
                    case "key": changed.Parameters.Remove("name"); changed.Parameters["NAME"] = "value"; break;
                    case "add": changed.Parameters["extra"] = "value"; break;
                    case "remove": changed.Parameters.Remove("name"); break;
                }
                Assert.IsFalse(Valid(token, Descriptor(), changed), field);
            }
        }

        [DataTestMethod]
        [DataRow("config")]
        [DataRow("assistant")]
        [DataRow("mutations")]
        [DataRow("production")]
        public async Task AdmissionGateNeverBuildsOrPrompts(string gate)
        {
            using (var f = new Fixture(gate != "production"))
            {
                if (gate == "config") f.Config.Assistant.Mutations.Enabled = false;
                if (gate == "assistant") f.Config.Assistant = null;
                if (gate == "mutations") f.Config.Assistant.Mutations = null;
                Assert.IsNull(await f.Service.RequestApprovalAsync(Descriptor(), Request(), "trace"));
                Assert.AreEqual(0, f.Builder.Calls);
                Assert.AreEqual(0, f.Prompt.Calls);
                f.Evidence.AssertTransitions("denied");
                Assert.AreEqual("mutations_disabled", f.Evidence.Ledger.Single().Outcome);
            }
        }

        [DataTestMethod]
        [DataRow("descriptor")]
        [DataRow("request")]
        [DataRow("requestId")]
        [DataRow("capability")]
        public async Task MalformedRequestsFailClosed(string malformed)
        {
            using (var f = new Fixture())
            {
                var descriptor = Descriptor();
                var request = Request();
                var trace = "trace";
                if (malformed == "descriptor") descriptor = null;
                if (malformed == "request") request = null;
                if (malformed == "requestId") { request.RequestId = " "; trace = " "; }
                if (malformed == "capability") { descriptor.CapabilityId = " "; descriptor.Name = null; }
                Assert.IsNull(await f.Service.RequestApprovalAsync(descriptor, request, trace));
                Assert.AreEqual(0, f.Builder.Calls);
                Assert.AreEqual(0, f.Prompt.Calls);
                f.Evidence.AssertTransitions("denied");
                Assert.AreEqual("invalid_request", f.Evidence.Ledger.Single().Outcome);
            }
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow(" ")]
        [DataRow("\t")]
        public async Task NonNullBlankCapabilityIsMalformedEvenWithAValidName(string capability)
        {
            using (var f = new Fixture())
            {
                var descriptor = Descriptor();
                descriptor.CapabilityId = capability;
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Approved);
                Assert.IsNull(await Finish(f.Service.RequestApprovalAsync(descriptor, Request(), "trace")));
                Assert.AreEqual(0, f.Builder.Calls);
                Assert.AreEqual(0, f.Prompt.Calls);
                f.Evidence.AssertTransitions("denied");
                Assert.AreEqual("invalid_request", f.Evidence.Ledger.Single().Outcome);
            }
        }

        [TestMethod]
        public void AllConstructorDependenciesAreRequired()
        {
            using (var f = new Fixture())
            {
                Assert.ThrowsException<ArgumentNullException>(() => new AssistantApprovalService(null, f.Prompt, f.Builder, f.Evidence, f.Evidence, f.Time, true));
                Assert.ThrowsException<ArgumentNullException>(() => new AssistantApprovalService(f.Config, null, f.Builder, f.Evidence, f.Evidence, f.Time, true));
                Assert.ThrowsException<ArgumentNullException>(() => new AssistantApprovalService(f.Config, f.Prompt, null, f.Evidence, f.Evidence, f.Time, true));
                Assert.ThrowsException<ArgumentNullException>(() => new AssistantApprovalService(f.Config, f.Prompt, f.Builder, null, f.Evidence, f.Time, true));
                Assert.ThrowsException<ArgumentNullException>(() => new AssistantApprovalService(f.Config, f.Prompt, f.Builder, f.Evidence, null, f.Time, true));
                Assert.ThrowsException<ArgumentNullException>(() => new AssistantApprovalService(f.Config, f.Prompt, f.Builder, f.Evidence, f.Evidence, null, true));
                var ledger = new AssistantApprovalLedger(Path.Combine(f.Root, "ledger.jsonl"));
                var logger = new TelemetryLogger(Path.Combine(f.Root, "logs"));
                Assert.ThrowsException<ArgumentNullException>(() => new AssistantApprovalService(null, f.Prompt, f.Builder, ledger, logger));
                Assert.ThrowsException<ArgumentNullException>(() => new AssistantApprovalService(f.Config, null, f.Builder, ledger, logger));
                Assert.ThrowsException<ArgumentNullException>(() => new AssistantApprovalService(f.Config, f.Prompt, null, ledger, logger));
                Assert.ThrowsException<ArgumentNullException>(() => new AssistantApprovalService(f.Config, f.Prompt, f.Builder, null, logger));
                Assert.ThrowsException<ArgumentNullException>(() => new AssistantApprovalService(f.Config, f.Prompt, f.Builder, ledger, null));
            }
        }

        [TestMethod]
        public async Task MissingIdentifiersUseTraceAndDescriptorNameFallbacks()
        {
            using (var f = new Fixture())
            {
                var descriptor = Descriptor();
                descriptor.CapabilityId = null;
                var request = Request();
                request.RequestId = null;
                request.Parameters = null;
                request.SessionId = null;
                var pending = f.Service.RequestApprovalAsync(descriptor, request, "fallback");
                Assert.AreEqual("fallback", f.Prompt.Last.RequestId);
                Assert.AreEqual(descriptor.Name, f.Prompt.Last.CapabilityId);
                f.Prompt.Complete(ApprovalPromptOutcome.Approved);
                var token = await Finish(pending);
                Assert.IsTrue(token.IsValidFor(descriptor, request, "fallback", null, "Lab", DateTime.UtcNow));
            }
        }

        [TestMethod]
        public async Task CallerMutationWhilePendingCannotChangePrivateAuthority()
        {
            using (var f = new Fixture())
            {
                var descriptor = Descriptor();
                var request = Request();
                var originalAuthorization = request.Authorization;
                var pending = f.Service.RequestApprovalAsync(descriptor, request, "trace");
                Assert.AreNotSame(originalAuthorization, f.Prompt.Last.Request.Authorization);
                Assert.IsFalse(f.Prompt.Last.Request.Authorization.Granted);
                descriptor.Name = "changed";
                descriptor.CapabilityId = "changed";
                descriptor.AllowedEnvironments[0] = "changed";
                descriptor.AllowedModes[0] = "changed";
                request.RequestId = "changed";
                request.SessionId = "changed";
                request.Environment = "changed";
                request.ToolName = "changed";
                request.Query = "changed";
                request.Limit = 999;
                request.ScopeId = "changed";
                request.Parameters["name"] = "changed";
                request.Parameters = new Dictionary<string, string>();
                originalAuthorization.Granted = true;
                f.Prompt.Complete(ApprovalPromptOutcome.Approved);
                var token = await Finish(pending);
                Assert.IsTrue(Valid(token, Descriptor(), Request()));
                Assert.IsFalse(Valid(token, descriptor, request));
                Assert.AreEqual("Lab", f.Prompt.Last.Descriptor.AllowedEnvironments[0]);
                Assert.AreEqual("READ_ONLY_ANALYST", f.Prompt.Last.Descriptor.AllowedModes[0]);
            }
        }

        [TestMethod]
        public async Task BuilderAndPromptMutationsCannotChangeAuthorityOrEachOther()
        {
            using (var f = new Fixture())
            {
                AssistantToolRequest builderRequest = null;
                f.Builder.Handler = (descriptor, request) =>
                {
                    builderRequest = request;
                    Assert.IsFalse(request.Authorization.Granted);
                    Assert.AreSame(StringComparer.Ordinal, request.Parameters.Comparer);
                    descriptor.CapabilityId = "builder";
                    descriptor.AllowedEnvironments[0] = "builder";
                    descriptor.AllowedModes[0] = "builder";
                    request.Parameters["name"] = "builder";
                    request.RequestId = "builder";
                    request.SessionId = "builder";
                    request.Authorization.Granted = true;
                    return f.Builder.Preview;
                };
                f.Prompt.Handler = prompt =>
                {
                    Assert.AreNotSame(builderRequest, prompt.Request);
                    Assert.AreNotSame(builderRequest.Parameters, prompt.Request.Parameters);
                    Assert.AreNotSame(builderRequest.Authorization, prompt.Request.Authorization);
                    Assert.IsFalse(prompt.Request.Authorization.Granted);
                    Assert.AreEqual("value", prompt.Request.Parameters["name"]);
                    Assert.AreEqual("CaseValue", prompt.Request.Parameters["Name"]);
                    Assert.AreEqual("Lab", prompt.Descriptor.AllowedEnvironments[0]);
                    Assert.AreEqual("READ_ONLY_ANALYST", prompt.Descriptor.AllowedModes[0]);
                    prompt.Descriptor.CapabilityId = "prompt";
                    prompt.Request.Parameters["name"] = "prompt";
                    prompt.Request.SessionId = "prompt";
                    prompt.Request.RequestId = "prompt";
                    prompt.Request.Environment = "prompt";
                    prompt.Request.Authorization.Granted = true;
                    return Task.FromResult(ApprovalPromptOutcome.Approved);
                };
                var token = await Finish(f.Service.RequestApprovalAsync(Descriptor(), Request(), "trace"));
                Assert.IsTrue(Valid(token, Descriptor(), Request()));
            }
        }

        [DataTestMethod]
        [DataRow(1, "denied")]
        [DataRow(2, "expired")]
        public async Task ExplicitNonApprovalOutcomesNeverIssue(int outcomeValue, string terminal)
        {
            using (var f = new Fixture())
            {
                var outcome = (ApprovalPromptOutcome)outcomeValue;
                var pending = f.Service.RequestApprovalAsync(Descriptor(), Request(), "trace");
                f.Prompt.Complete(outcome);
                Assert.IsNull(await Finish(pending));
                f.Evidence.AssertTransitions("requested", terminal);
                Assert.IsTrue(f.Time.DelayToken.IsCancellationRequested);
                if (outcome == ApprovalPromptOutcome.Timeout) Assert.IsTrue(f.Prompt.Last.CancellationToken.IsCancellationRequested);
            }
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ControlledTimeoutCancelsBeforeReadmissionAndIgnoresLateActivity(bool fault)
        {
            using (var f = new Fixture())
            {
                var pending = f.Service.RequestApprovalAsync(Descriptor(), Request(), "trace");
                var prompt = f.Prompt.Last;
                var callbackRan = false;
                prompt.CancellationToken.Register(() => callbackRan = true);
                f.Time.CompleteDelay();
                Assert.IsNull(await Finish(pending));
                Assert.IsTrue(prompt.CancellationToken.IsCancellationRequested);
                Assert.IsTrue(callbackRan, "Cancellation callbacks must run before request completion and slot release.");
                f.Evidence.AssertTransitions("requested", "expired");
                if (fault) f.Prompt.Fail(new InvalidOperationException("late private error"));
                else f.Prompt.Complete(ApprovalPromptOutcome.Approved);
                Assert.IsTrue(f.Prompt.Completion.Task.IsCompleted);
                f.Evidence.AssertTransitions("requested", "expired");
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Denied);
                f.Time.ResetDelay();
                Assert.IsNull(await Finish(f.Service.RequestApprovalAsync(Descriptor(), Request(), "next")));
                Assert.AreEqual(2, f.Prompt.Calls);
                f.Evidence.AssertTransitions("requested", "expired", "requested", "denied");
            }
        }

        [TestMethod]
        public async Task ConcurrentRequestIsImmediatelyDeniedWithoutSecondPreviewOrPrompt()
        {
            using (var f = new Fixture())
            {
                var first = f.Service.RequestApprovalAsync(Descriptor(), Request(), "first");
                var second = f.Service.RequestApprovalAsync(Descriptor(), Request(), "second");
                Assert.IsTrue(second.IsCompleted);
                Assert.IsNull(await second);
                Assert.AreEqual(1, f.Builder.Calls);
                Assert.AreEqual(1, f.Prompt.Calls);
                Assert.AreEqual("approval_in_progress", f.Evidence.Ledger[1].Outcome);
                Assert.IsFalse(first.IsCompleted);
                f.Prompt.Complete(ApprovalPromptOutcome.Approved);
                Assert.IsNotNull(await Finish(first));
                f.Evidence.AssertTransitions("requested", "denied", "issued");
            }
        }

        [TestMethod]
        public async Task RevokedFlagIsRecheckedAfterApproval()
        {
            using (var f = new Fixture())
            {
                var pending = f.Service.RequestApprovalAsync(Descriptor(), Request(), "trace");
                f.Config.Assistant.Mutations.Enabled = false;
                f.Prompt.Complete(ApprovalPromptOutcome.Approved);
                Assert.IsNull(await Finish(pending));
                f.Evidence.AssertTransitions("requested", "denied");
                Assert.AreEqual("mutations_disabled", f.Evidence.Ledger.Last().Outcome);
            }
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ApprovedAtOrAfterExpiryFailsClosedAndCancelsPrompt(bool past)
        {
            using (var f = new Fixture())
            {
                var pending = f.Service.RequestApprovalAsync(Descriptor(), Request(), "trace");
                f.Time.Now = f.Prompt.Last.ExpiresUtc.AddTicks(past ? 1 : 0);
                f.Prompt.Complete(ApprovalPromptOutcome.Approved);
                Assert.IsNull(await Finish(pending));
                Assert.IsTrue(f.Prompt.Last.CancellationToken.IsCancellationRequested);
                f.Evidence.AssertTransitions("requested", "expired");
            }
        }

        [TestMethod]
        public async Task RealFactoryWallClockExpiryGuardIsPreserved()
        {
            using (var f = new Fixture())
            {
                f.Time.Now = DateTime.UtcNow.AddHours(-1);
                var pending = f.Service.RequestApprovalAsync(Descriptor(), Request(), "trace");
                f.Prompt.Complete(ApprovalPromptOutcome.Approved);
                Assert.IsNull(await Finish(pending));
                Assert.IsTrue(f.Prompt.Last.CancellationToken.IsCancellationRequested);
                f.Evidence.AssertTransitions("requested", "expired");
            }
        }

        [TestMethod]
        public async Task ShutdownWinsBeforePromptCompletionAndNewCallsAreRejected()
        {
            using (var f = new Fixture())
            {
                var pending = f.Service.RequestApprovalAsync(Descriptor(), Request(), "trace");
                f.Service.Dispose();
                f.Service.Dispose();
                Assert.IsTrue(f.Prompt.Last.CancellationToken.IsCancellationRequested);
                Assert.IsNull(await Finish(pending));
                f.Evidence.AssertTransitions("requested", "orphaned");
                f.Prompt.Complete(ApprovalPromptOutcome.Approved);
                f.Evidence.AssertTransitions("requested", "orphaned");
                Assert.IsNull(await f.Service.RequestApprovalAsync(Descriptor(), Request(), "new"));
                Assert.AreEqual(1, f.Builder.Calls);
                Assert.AreEqual(1, f.Prompt.Calls);
                f.Evidence.AssertTransitions("requested", "orphaned", "denied");
                Assert.AreEqual("service_disposed", f.Evidence.Ledger.Last().Outcome);
            }
        }

        [TestMethod]
        public async Task IssuanceWinsStateLockBeforeConcurrentShutdown()
        {
            using (var f = new Fixture())
            using (var issuing = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            using (var disposing = new ManualResetEventSlim())
            {
                f.Evidence.BeforeCall = (sink, entry) =>
                {
                    if (sink == "ledger" && entry.Event == "issued")
                    {
                        issuing.Set();
                        if (!release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("test release watchdog");
                    }
                };
                var pending = f.Service.RequestApprovalAsync(Descriptor(), Request(), "trace");
                f.Prompt.Complete(ApprovalPromptOutcome.Approved);
                Assert.IsTrue(issuing.Wait(TimeSpan.FromSeconds(5)), "Issuer must reach required evidence inside its state lock.");
                var shutdown = Task.Run(() => { disposing.Set(); f.Service.Dispose(); });
                try
                {
                    Assert.IsTrue(disposing.Wait(TimeSpan.FromSeconds(5)));
                    Assert.IsFalse(pending.IsCompleted, "Authority cannot return before required evidence completes.");
                    Assert.IsFalse(shutdown.IsCompleted, "Shutdown cannot overtake the issuance lock.");
                }
                finally { release.Set(); }
                var token = await Finish(pending);
                await Finish(shutdown);
                Assert.IsTrue(Valid(token, Descriptor(), Request()));
                f.Evidence.AssertTransitions("requested", "issued");
            }
        }

        [TestMethod]
        public async Task ShutdownCancellationCallbacksCanReenterAndFaultOutsideStateLock()
        {
            using (var f = new Fixture())
            {
                var pending = f.Service.RequestApprovalAsync(Descriptor(), Request(), "trace");
                var reentered = false;
                var secondCallback = false;
                Task reentry = null;
                f.Prompt.Last.CancellationToken.Register(() => secondCallback = true);
                f.Prompt.Last.CancellationToken.Register(() =>
                {
                    reentry = Task.Run(() => f.Service.Dispose());
                    reentered = reentry.Wait(TimeSpan.FromSeconds(5));
                    throw new InvalidOperationException("callback private error");
                });
                f.Service.Dispose();
                Assert.IsTrue(reentered, "Another thread must reenter Dispose while the callback runs; no state lock may be held.");
                Assert.IsTrue(secondCallback, "Cancel(false) must run the other callbacks despite a fault.");
                await Finish(reentry);
                Assert.IsNull(await Finish(pending));
                f.Prompt.Complete(ApprovalPromptOutcome.Approved);
                f.Evidence.AssertTransitions("requested", "orphaned");
            }
        }

        [TestMethod]
        public async Task TimeoutCancellationCallbackFaultCannotPreventExpiryOrSlotRelease()
        {
            using (var f = new Fixture())
            {
                var pending = f.Service.RequestApprovalAsync(Descriptor(), Request(), "trace");
                f.Prompt.Last.CancellationToken.Register(() => { throw new InvalidOperationException("callback private error"); });
                f.Time.CompleteDelay();
                Assert.IsNull(await Finish(pending));
                Assert.IsTrue(f.Prompt.Last.CancellationToken.IsCancellationRequested);
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Denied);
                f.Time.ResetDelay();
                Assert.IsNull(await Finish(f.Service.RequestApprovalAsync(Descriptor(), Request(), "next")));
                f.Evidence.AssertTransitions("requested", "expired", "requested", "denied");
            }
        }

        [DataTestMethod]
        [DataRow("preview_null", "preview_unavailable", false)]
        [DataRow("preview_throw", "preview_failed", false)]
        [DataRow("prompt_null", "prompt_unavailable", true)]
        [DataRow("prompt_throw", "prompt_failed", true)]
        [DataRow("prompt_fault", "prompt_failed", true)]
        [DataRow("outcome", "invalid_prompt_outcome", true)]
        public async Task InvalidDependenciesAndOutcomesFailClosed(string failure, string outcome, bool requested)
        {
            using (var f = new Fixture())
            {
                if (failure == "preview_null") f.Builder.Handler = (d, r) => null;
                if (failure == "preview_throw") f.Builder.Handler = (d, r) => { throw new InvalidOperationException("private error"); };
                if (failure == "prompt_null") f.Prompt.Handler = p => null;
                if (failure == "prompt_throw") f.Prompt.Handler = p => { throw new InvalidOperationException("private error"); };
                if (failure == "prompt_fault") f.Prompt.Handler = p => Task.FromException<ApprovalPromptOutcome>(new InvalidOperationException("private error"));
                if (failure == "outcome") f.Prompt.Handler = p => Task.FromResult((ApprovalPromptOutcome)999);
                Assert.IsNull(await Finish(f.Service.RequestApprovalAsync(Descriptor(), Request(), "trace")));
                f.Evidence.AssertTransitions(requested ? new[] { "requested", "denied" } : new[] { "denied" });
                Assert.AreEqual(requested ? 1 : 0, f.Prompt.Calls);
                Assert.AreEqual(outcome, f.Evidence.Ledger.Last().Outcome);
                Assert.IsFalse(JsonConvert.SerializeObject(f.Evidence.Ledger).Contains("private error"));
            }
        }

        [DataTestMethod]
        [DataRow("prompt_throw")]
        [DataRow("prompt_null")]
        [DataRow("delay_throw")]
        [DataRow("delay_null")]
        public async Task ShutdownTakesPrecedenceOverSynchronousPromptOrDelayFailure(string failure)
        {
            using (var f = new Fixture())
            {
                if (failure.StartsWith("prompt", StringComparison.Ordinal))
                {
                    f.Prompt.Handler = p =>
                    {
                        f.Service.Dispose();
                        if (failure == "prompt_throw") throw new InvalidOperationException("private prompt failure");
                        return null;
                    };
                }
                else
                {
                    f.Time.Handler = (delay, token) =>
                    {
                        f.Service.Dispose();
                        if (failure == "delay_throw") throw new InvalidOperationException("private delay failure");
                        return null;
                    };
                }
                Assert.IsNull(await Finish(f.Service.RequestApprovalAsync(Descriptor(), Request(), "trace")));
                Assert.IsTrue(f.Prompt.Last.CancellationToken.IsCancellationRequested);
                f.Evidence.AssertTransitions("requested", "orphaned");
                Assert.AreEqual("shutdown", f.Evidence.Ledger.Last().Outcome);
                f.Prompt.Complete(ApprovalPromptOutcome.Approved);
                f.Evidence.AssertTransitions("requested", "orphaned");
            }
        }

        [TestMethod]
        public async Task PreviewExhaustingLifetimeExpiresBeforeShowingPrompt()
        {
            using (var f = new Fixture())
            {
                f.Builder.Handler = (d, r) =>
                {
                    f.Time.Now = f.Time.Now.AddSeconds(60);
                    return f.Builder.Preview;
                };
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Approved);
                Assert.IsNull(await Finish(f.Service.RequestApprovalAsync(Descriptor(), Request(), "trace")));
                Assert.AreEqual(0, f.Prompt.Calls);
                f.Evidence.AssertTransitions("requested", "expired");
            }
        }

        [DataTestMethod]
        [DataRow("ledger", "requested")]
        [DataRow("telemetry", "requested")]
        [DataRow("ledger", "issued")]
        [DataRow("telemetry", "issued")]
        public async Task RequiredSinkFailuresNeverReturnAuthorityAndDoNotRetry(string failedSink, string failedEvent)
        {
            using (var f = new Fixture())
            {
                f.Evidence.BeforeCall = (sink, entry) =>
                {
                    if (sink == failedSink && entry.Event == failedEvent) throw new IOException("private sink failure");
                };
                f.Prompt.Handler = p => Task.FromResult(ApprovalPromptOutcome.Approved);
                Assert.IsNull(await Finish(f.Service.RequestApprovalAsync(Descriptor(), Request(), "trace")));
                var expected = new List<string> { "ledger:requested" };
                if (!(failedSink == "ledger" && failedEvent == "requested")) expected.Add("telemetry:requested");
                if (failedEvent == "issued")
                {
                    expected.Add("ledger:issued");
                    if (failedSink == "telemetry") expected.Add("telemetry:issued");
                }
                CollectionAssert.AreEqual(expected, f.Evidence.Attempts);
                Assert.AreEqual(failedEvent == "requested" ? 0 : 1, f.Prompt.Calls);
                var ledgerSuccesses = failedEvent == "requested" ? (failedSink == "ledger" ? 0 : 1) : (failedSink == "ledger" ? 1 : 2);
                Assert.AreEqual(ledgerSuccesses, f.Evidence.Ledger.Count);
                Assert.AreEqual(failedEvent == "requested" ? 0 : 1, f.Evidence.Telemetry.Count);
            }
        }

        [DataTestMethod]
        [DataRow(0, 60)]
        [DataRow(-1, 60)]
        [DataRow(17, 17)]
        public async Task PromptCarriesCopiedPreviewAndAdmissionBasedExpiry(int configured, int expected)
        {
            using (var f = new Fixture())
            {
                f.Config.Assistant.Mutations.ApprovalTimeoutSeconds = configured;
                var admitted = f.Time.Now;
                f.Builder.Handler = (d, r) =>
                {
                    Assert.AreEqual(0, f.Evidence.Attempts.Count, "Preview must precede requested evidence.");
                    f.Time.Now = f.Time.Now.AddSeconds(3);
                    return f.Builder.Preview;
                };
                f.Prompt.Handler = p =>
                {
                    f.Evidence.AssertTransitions("requested");
                    return f.Prompt.Completion.Task;
                };
                var pending = f.Service.RequestApprovalAsync(Descriptor(), Request(), "trace");
                Assert.AreEqual(admitted.AddSeconds(expected), f.Prompt.Last.ExpiresUtc);
                Assert.AreEqual(TimeSpan.FromSeconds(expected - 3), f.Time.LastDelay);
                Assert.AreNotSame(f.Builder.Preview, f.Prompt.Last.Preview);
                Assert.AreEqual("title", f.Prompt.Last.Preview.Title);
                Assert.AreEqual("summary", f.Prompt.Last.Preview.Summary);
                Assert.AreEqual("before", f.Prompt.Last.Preview.Before);
                Assert.AreEqual("after", f.Prompt.Last.Preview.After);
                Assert.AreEqual("risk", f.Prompt.Last.Preview.Risk);
                Assert.AreEqual("request", f.Prompt.Last.RequestId);
                Assert.AreEqual("capability", f.Prompt.Last.CapabilityId);
                Assert.AreEqual("trace", f.Prompt.Last.TraceId);
                Assert.AreEqual("session", f.Prompt.Last.SessionId);
                Assert.AreEqual("Lab", f.Prompt.Last.Environment);
                Assert.AreEqual("Lab", f.Prompt.Last.Request.Environment);
                Assert.IsTrue(f.Prompt.Last.CancellationToken.CanBeCanceled);
                Assert.IsFalse(f.Prompt.Last.CancellationToken.IsCancellationRequested);
                f.Prompt.Complete(ApprovalPromptOutcome.Denied);
                await Finish(pending);
            }
        }

        [TestMethod]
        public async Task PersistedEvidenceIsBoundedRedactedAndNeverContainsPayloadOrPreview()
        {
            using (var f = new Fixture())
            {
                var ledgerPath = Path.Combine(f.Root, "ledger.jsonl");
                var ledger = new AssistantApprovalLedger(ledgerPath);
                var logger = new TelemetryLogger(Path.Combine(f.Root, "logs"), sampleRateSuccess: 0);
                var descriptor = Descriptor();
                var request = Request();
                const string marker = "SYNTHETIC_ISSUER_SECRET";
                var sensitiveId = "api_key=" + marker + " " + new string('x', 200);
                descriptor.CapabilityId = sensitiveId;
                request.RequestId = sensitiveId;
                request.SessionId = sensitiveId;
                request.Parameters["private"] = "PRIVATE_ARGUMENT_PAYLOAD";
                f.Builder.Preview = new ApprovalPreview("PRIVATE_PREVIEW", marker, marker, marker, marker);
                using (var service = new AssistantApprovalService(f.Config, f.Prompt, f.Builder, ledger, new ApprovalLifecycleTelemetrySink(logger), f.Time, true))
                {
                    var pending = service.RequestApprovalAsync(descriptor, request, sensitiveId);
                    f.Prompt.Complete(ApprovalPromptOutcome.Approved);
                    var token = await Finish(pending);
                    Assert.IsTrue(token.IsValidFor(descriptor, request, sensitiveId, sensitiveId, "Lab", DateTime.UtcNow));
                    Assert.AreEqual(sensitiveId, token.RequestId, "Exact private bindings must not be redacted or bounded.");
                    Assert.AreEqual(sensitiveId, token.CapabilityId);
                    Assert.AreEqual(sensitiveId, token.SessionId);
                }
                var persisted = File.ReadAllText(ledgerPath) + logger.Tail(10).ToString();
                Assert.IsFalse(persisted.Contains(marker));
                Assert.IsFalse(persisted.Contains("PRIVATE_ARGUMENT_PAYLOAD"));
                Assert.IsFalse(persisted.Contains("PRIVATE_PREVIEW"));
                StringAssert.Contains(persisted, "REDACTED");
                Assert.AreEqual(2, File.ReadAllLines(ledgerPath).Length);
                Assert.AreEqual(2, logger.Tail(10).Count);
                foreach (var entry in ledger.Tail(10))
                {
                    Assert.IsTrue(entry.TraceId.Length <= 128);
                    Assert.IsTrue(entry.SessionId.Length <= 128);
                    Assert.IsTrue(entry.CapabilityId.Length <= 128);
                    Assert.AreEqual(64, entry.ArgumentDigest.Length);
                    Assert.AreEqual(AssistantToolAuthorization.ComputeArgumentDigest(request), entry.ArgumentDigest);
                }
                foreach (var entry in logger.Tail(10))
                {
                    Assert.IsTrue(((string)entry["metadata"]["traceId"]).Length <= 128);
                    Assert.IsTrue(((string)entry["metadata"]["sessionId"]).Length <= 128);
                    Assert.IsTrue(((string)entry["metadata"]["capabilityId"]).Length <= 128);
                }
            }
        }

        [TestMethod]
        public void RequiredSuccessfulLifecycleBypassesSamplingAndOrdinaryBehaviorIsUnchanged()
        {
            using (var f = new Fixture())
            {
                var logger = new TelemetryLogger(Path.Combine(f.Root, "logs"), sampleRateSuccess: 0);
                var sink = new ApprovalLifecycleTelemetrySink(logger);
                sink.Record(new AssistantApprovalLedgerEntry { Event = "issued", Outcome = "issued", TraceId = "required" }, true);
                logger.LogEvent("ORDINARY_SUCCESS", "test", true, 1, new { traceId = "sampled" });
                logger.LogEvent("ORDINARY_FAILURE", "test", false, 1, new { traceId = "failure" });
                var entries = logger.Tail(10);
                Assert.AreEqual(2, entries.Count);
                Assert.AreEqual("APPROVAL_LIFECYCLE", (string)entries[0]["type"]);
                Assert.AreEqual(true, (bool)entries[0]["success"]);
                Assert.AreEqual("issued", (string)entries[0]["metadata"]["lifecycleEvent"]);
                Assert.AreEqual("ORDINARY_FAILURE", (string)entries[1]["type"]);
                Assert.AreEqual(false, (bool)entries[1]["success"]);
                Assert.AreEqual(3L, logger.Summary()["totalRequests"]);
            }
        }

        private static AssistantToolDescriptor Descriptor()
        {
            return new AssistantToolDescriptor { Name = "tool", CapabilityId = "capability", Mutating = true, Enabled = true };
        }

        private static AssistantToolRequest Request()
        {
            return new AssistantToolRequest
            {
                ToolName = "tool", Query = "query", Limit = 7, ScopeId = "scope",
                RequestId = "request", SessionId = "session", Environment = "CALLER_CANNOT_SET_THIS",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal) { ["name"] = "value", ["Name"] = "CaseValue" }
            };
        }

        private static bool Valid(AssistantToolAuthorization token, AssistantToolDescriptor descriptor, AssistantToolRequest request,
            string requestId = "request", string sessionId = "session", string environment = "Lab")
        {
            Assert.IsNotNull(token);
            return token.IsValidFor(descriptor, request, requestId, sessionId, environment, DateTime.UtcNow);
        }

        // These delays are deadlock watchdogs only. Every race is selected by controlled tasks/events.
        private static async Task<T> Finish<T>(Task<T> task)
        {
            Assert.AreSame(task, await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10))), "Request did not settle.");
            return await task;
        }

        private static async Task Finish(Task task)
        {
            Assert.IsNotNull(task);
            Assert.AreSame(task, await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10))), "Operation did not settle.");
            await task;
        }

        private sealed class Fixture : IDisposable
        {
            internal readonly string Root = Path.Combine(Path.GetTempPath(), "bb-issuer-tests-" + Guid.NewGuid().ToString("N"));
            internal readonly AgentConfig Config = new AgentConfig { Assistant = new AssistantSettings { Mutations = new AssistantMutationSettings { Enabled = true, ApprovalTimeoutSeconds = 60 } } };
            internal readonly FakePrompt Prompt = new FakePrompt();
            internal readonly FakeBuilder Builder = new FakeBuilder();
            internal readonly FakeTime Time = new FakeTime();
            internal readonly EvidenceProbe Evidence = new EvidenceProbe();
            internal readonly AssistantApprovalService Service;

            internal Fixture(bool lab = true)
            {
                Service = new AssistantApprovalService(Config, Prompt, Builder, Evidence, Evidence, Time, lab);
            }

            public void Dispose()
            {
                Service.Dispose();
                if (Directory.Exists(Root)) Directory.Delete(Root, true);
            }
        }

        private sealed class FakePrompt : IApprovalPrompt
        {
            internal readonly TaskCompletionSource<ApprovalPromptOutcome> Completion = new TaskCompletionSource<ApprovalPromptOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
            internal Func<ApprovalPrompt, Task<ApprovalPromptOutcome>> Handler;
            internal ApprovalPrompt Last;
            internal int Calls;

            public Task<ApprovalPromptOutcome> ShowAsync(ApprovalPrompt prompt)
            {
                Calls++;
                Last = prompt;
                return Handler == null ? Completion.Task : Handler(prompt);
            }

            internal void Complete(ApprovalPromptOutcome outcome) { Completion.TrySetResult(outcome); }
            internal void Fail(Exception exception) { Completion.TrySetException(exception); }
        }

        private sealed class FakeBuilder : IPreviewBuilder
        {
            internal ApprovalPreview Preview = new ApprovalPreview("title", "summary", "before", "after", "risk");
            internal Func<AssistantToolDescriptor, AssistantToolRequest, ApprovalPreview> Handler;
            internal int Calls;

            public ApprovalPreview Build(AssistantToolDescriptor descriptor, AssistantToolRequest request)
            {
                Calls++;
                return Handler == null ? Preview : Handler(descriptor, request);
            }
        }

        private sealed class FakeTime : IApprovalTimeProvider
        {
            internal DateTime Now = DateTime.UtcNow.AddHours(1);
            internal TimeSpan LastDelay;
            internal CancellationToken DelayToken;
            internal Func<TimeSpan, CancellationToken, Task> Handler;
            private TaskCompletionSource<bool> _delay = NewDelay();
            public DateTime UtcNow => Now;

            public Task Delay(TimeSpan delay, CancellationToken cancellationToken)
            {
                LastDelay = delay;
                DelayToken = cancellationToken;
                if (Handler != null) return Handler(delay, cancellationToken);
                var completion = _delay;
                cancellationToken.Register(() => completion.TrySetCanceled());
                return completion.Task;
            }

            internal void CompleteDelay() { _delay.TrySetResult(true); }
            internal void ResetDelay() { _delay = NewDelay(); }
            private static TaskCompletionSource<bool> NewDelay() { return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); }
        }

        private sealed class EvidenceProbe : IApprovalLedgerSink, IApprovalLifecycleTelemetrySink
        {
            internal readonly List<AssistantApprovalLedgerEntry> Ledger = new List<AssistantApprovalLedgerEntry>();
            internal readonly List<AssistantApprovalLedgerEntry> Telemetry = new List<AssistantApprovalLedgerEntry>();
            internal readonly List<bool> Authority = new List<bool>();
            internal readonly List<string> Attempts = new List<string>();
            internal Action<string, AssistantApprovalLedgerEntry> BeforeCall;
            private readonly object _sync = new object();

            public void Append(AssistantApprovalLedgerEntry entry)
            {
                lock (_sync) Attempts.Add("ledger:" + entry.Event);
                BeforeCall?.Invoke("ledger", entry);
                lock (_sync) Ledger.Add(entry);
            }

            public void Record(AssistantApprovalLedgerEntry entry, bool authorityIssued)
            {
                lock (_sync) Attempts.Add("telemetry:" + entry.Event);
                BeforeCall?.Invoke("telemetry", entry);
                lock (_sync) { Telemetry.Add(entry); Authority.Add(authorityIssued); }
            }

            internal void AssertTransitions(params string[] events)
            {
                lock (_sync)
                {
                    CollectionAssert.AreEqual(events, Ledger.Select(e => e.Event).ToArray());
                    CollectionAssert.AreEqual(events, Telemetry.Select(e => e.Event).ToArray());
                    CollectionAssert.AreEqual(events.SelectMany(e => new[] { "ledger:" + e, "telemetry:" + e }).ToArray(), Attempts.ToArray());
                    CollectionAssert.AreEqual(events.Select(e => e == "issued").ToArray(), Authority.ToArray());
                }
            }
        }
    }
}
