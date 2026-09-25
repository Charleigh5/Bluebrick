using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using BlueBrick.Agent;
using BlueBrick.SolidWorks.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BlueBrick.UI.Tests.Agent
{
    [TestClass]
    public class ApprovalDialogTests
    {
        [TestMethod]
        public void NullDispatcherThrows()
        {
            Assert.ThrowsException<ArgumentNullException>(() => new ApprovalDialog(null));
            Assert.ThrowsException<ArgumentNullException>(() => new ApprovalDialog(null, d => { }));
        }

        [TestMethod]
        public void NullPromptThrows()
        {
            using (var dialog = new ApprovalDialog(new FakeDispatcher()))
            {
                try
                {
                    dialog.ShowAsync(null);
                    Assert.Fail("Expected ArgumentNullException.");
                }
                catch (ArgumentNullException)
                {
                }
            }
        }

        [TestMethod]
        public void ShowAsyncReturnsPromptlyBeforeAnyOutcome()
        {
            var recorder = new RecorderShow();
            using (var cts = new CancellationTokenSource())
            using (var dialog = new ApprovalDialog(new FakeDispatcher(), recorder.Invoke))
            {
                var pending = dialog.ShowAsync(Prompt(cts));
                Assert.AreEqual(1, recorder.Calls);
                Assert.IsFalse(pending.IsCompleted, "ShowAsync must return the Task promptly (design ruling).");
                dialog.OnDenyClicked();
                Assert.AreEqual(ApprovalPromptOutcome.Denied, FinishSync(pending));
            }
        }

        [TestMethod]
        public void AdmissionCreatesWindowHandleForBeginInvoke()
        {
            var recorder = new RecorderShow();
            using (var cts = new CancellationTokenSource())
            using (var dialog = new ApprovalDialog(new FakeDispatcher(), recorder.Invoke))
            {
                var pending = dialog.ShowAsync(Prompt(cts));
                Assert.IsTrue(dialog.IsHandleCreated, "Handle must exist or production BeginInvoke throws (round-01 F1).");
                dialog.OnDenyClicked();
                Assert.AreEqual(ApprovalPromptOutcome.Denied, FinishSync(pending));
            }
        }

        [TestMethod]
        public void CancelBeforeShowReturnsTimeoutWithoutShowing()
        {
            var recorder = new RecorderShow();
            using (var cts = new CancellationTokenSource())
            using (var dialog = new ApprovalDialog(new FakeDispatcher(), recorder.Invoke))
            {
                cts.Cancel();
                Assert.AreEqual(ApprovalPromptOutcome.Timeout, FinishSync(dialog.ShowAsync(Prompt(cts))));
                Assert.AreEqual(0, recorder.Calls);
                Assert.IsTrue(dialog.IsDisposed);
            }
        }

        [TestMethod]
        public void MarshalUnavailableDeniesWithoutShowing()
        {
            var recorder = new RecorderShow();
            using (var cts = new CancellationTokenSource())
            using (var dialog = new ApprovalDialog(new FakeDispatcher { CanMarshal = false }, recorder.Invoke))
            {
                Assert.AreEqual(ApprovalPromptOutcome.Denied, FinishSync(dialog.ShowAsync(Prompt(cts))));
                Assert.AreEqual(0, recorder.Calls);
                Assert.IsTrue(dialog.IsDisposed);
            }
        }

        [TestMethod]
        public void ApproveClickReturnsApprovedAndDisposes()
        {
            var recorder = new RecorderShow();
            using (var cts = new CancellationTokenSource())
            using (var dialog = new ApprovalDialog(new FakeDispatcher(), recorder.Invoke))
            {
                var pending = dialog.ShowAsync(Prompt(cts));
                dialog.OnApproveClicked();
                Assert.AreEqual(ApprovalPromptOutcome.Approved, FinishSync(pending));
                Assert.AreEqual(ApprovalPromptOutcome.Approved, dialog.CurrentOutcome);
                Assert.IsTrue(dialog.IsDisposed);
            }
        }

        [TestMethod]
        public void DenyClickReturnsDenied()
        {
            var recorder = new RecorderShow();
            using (var cts = new CancellationTokenSource())
            using (var dialog = new ApprovalDialog(new FakeDispatcher(), recorder.Invoke))
            {
                var pending = dialog.ShowAsync(Prompt(cts));
                dialog.OnDenyClicked();
                Assert.AreEqual(ApprovalPromptOutcome.Denied, FinishSync(pending));
                Assert.AreEqual(ApprovalPromptOutcome.Denied, dialog.CurrentOutcome);
                Assert.IsTrue(dialog.IsDisposed);
            }
        }

        [TestMethod]
        public void CloseWithoutDecisionDenies()
        {
            var recorder = new RecorderShow();
            using (var cts = new CancellationTokenSource())
            using (var dialog = new ApprovalDialog(new FakeDispatcher(), recorder.Invoke))
            {
                var pending = dialog.ShowAsync(Prompt(cts));
                dialog.MapModalResult(DialogResult.Cancel);
                Assert.AreEqual(ApprovalPromptOutcome.Denied, FinishSync(pending));
            }
        }

        [TestMethod]
        public void BareOkWithoutApproveClickDenies()
        {
            var recorder = new RecorderShow();
            using (var cts = new CancellationTokenSource())
            using (var dialog = new ApprovalDialog(new FakeDispatcher(), recorder.Invoke))
            {
                var pending = dialog.ShowAsync(Prompt(cts));
                dialog.MapModalResult(DialogResult.OK);
                Assert.AreEqual(ApprovalPromptOutcome.Denied, FinishSync(pending));
            }
        }

        [TestMethod]
        public void TimerExpiryReturnsTimeout()
        {
            var recorder = new RecorderShow();
            using (var cts = new CancellationTokenSource())
            using (var dialog = new ApprovalDialog(new FakeDispatcher(), recorder.Invoke))
            {
                var pending = dialog.ShowAsync(Prompt(cts));
                dialog.OnTimerExpired();
                Assert.AreEqual(ApprovalPromptOutcome.Timeout, FinishSync(pending));
                Assert.AreEqual(ApprovalPromptOutcome.Timeout, dialog.CurrentOutcome);
            }
        }

        [TestMethod]
        public void CancelWhileShownReturnsTimeout()
        {
            var recorder = new RecorderShow();
            using (var cts = new CancellationTokenSource())
            using (var dialog = new ApprovalDialog(new FakeDispatcher(), recorder.Invoke))
            {
                var pending = dialog.ShowAsync(Prompt(cts));
                Assert.IsFalse(pending.IsCompleted);
                cts.Cancel();
                Assert.AreEqual(ApprovalPromptOutcome.Timeout, FinishSync(pending));
                Assert.AreEqual(ApprovalPromptOutcome.Timeout, dialog.CurrentOutcome);
                Assert.IsTrue(dialog.IsDisposed);
            }
        }

        [TestMethod]
        public void ApproveAfterExpiryLosesTheRace()
        {
            var recorder = new RecorderShow();
            using (var cts = new CancellationTokenSource())
            using (var dialog = new ApprovalDialog(new FakeDispatcher(), recorder.Invoke))
            {
                var pending = dialog.ShowAsync(Prompt(cts));
                dialog.OnTimerExpired();
                dialog.OnApproveClicked();
                Assert.AreEqual(ApprovalPromptOutcome.Timeout, FinishSync(pending));
                Assert.AreEqual(ApprovalPromptOutcome.Timeout, dialog.CurrentOutcome);
            }
        }

        [TestMethod]
        public void ShowThrowDeniesAndDisposes()
        {
            using (var cts = new CancellationTokenSource())
            using (var dialog = new ApprovalDialog(new FakeDispatcher(), d => { throw new InvalidOperationException("private show failure"); }))
            {
                Assert.AreEqual(ApprovalPromptOutcome.Denied, FinishSync(dialog.ShowAsync(Prompt(cts))));
                Assert.IsTrue(dialog.IsDisposed);
            }
        }

        [TestMethod]
        public void SecondShowAfterDisposeDenies()
        {
            var recorder = new RecorderShow();
            using (var cts = new CancellationTokenSource())
            using (var dialog = new ApprovalDialog(new FakeDispatcher(), recorder.Invoke))
            {
                var first = dialog.ShowAsync(Prompt(cts));
                dialog.OnDenyClicked();
                Assert.AreEqual(ApprovalPromptOutcome.Denied, FinishSync(first));
                Assert.AreEqual(ApprovalPromptOutcome.Denied, FinishSync(dialog.ShowAsync(Prompt(cts))));
                Assert.AreEqual(1, recorder.Calls);
            }
        }

        [TestMethod]
        public void ConstructAndDisposeCarriesNoAuthority()
        {
            var dialog = new ApprovalDialog(new FakeDispatcher(), d => { });
            Assert.IsNotNull(dialog);
            Assert.IsFalse(dialog.IsDisposed);
            dialog.Dispose();
            Assert.IsTrue(dialog.IsDisposed);
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

        private static ApprovalPrompt Prompt(CancellationTokenSource cts)
        {
            return new ApprovalPrompt(
                "request",
                "capability",
                "trace",
                "session",
                "Lab",
                DateTime.UtcNow.AddSeconds(60),
                Descriptor(),
                Request(),
                new ApprovalPreview("title", "summary", "before", "after", "risk"),
                cts.Token);
        }

        // Host constraint (proven 2026-09-23): an async test that genuinely suspends on an
        // incomplete task never resumes in this testhost, so dialog tests never await.
        // Everything here runs synchronously on the test thread through the fake inline
        // dispatcher, so waits below return immediately and exist only as watchdogs.
        private static T FinishSync<T>(Task<T> task)
        {
            Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(10)), "Dialog did not settle.");
            return task.GetAwaiter().GetResult();
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

        private sealed class RecorderShow
        {
            internal int Calls;

            internal void Invoke(ApprovalDialog dialog)
            {
                Calls++;
            }
        }
    }
}
