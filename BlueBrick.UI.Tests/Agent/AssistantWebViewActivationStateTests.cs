using System;
using System.Threading.Tasks;
using BlueBrick.Agent;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BlueBrick.UI.Tests.Agent
{
    [TestClass]
    public class AssistantWebViewActivationStateTests
    {
        [TestMethod]
        public void DisabledReact_RequiresFallbackWithTypedReasonAndInactiveState()
        {
            var state = new AssistantWebViewActivationState();

            Assert.IsFalse(state.BeginReactLoad(false, true, true, true));

            Assert.IsTrue(state.FallbackRequired);
            Assert.IsFalse(state.LoadedReactShell);
            Assert.AreEqual("Assistant React WebView disabled by configuration.", state.LastLoadError);
        }

        [TestMethod]
        public void MissingDistMember_RequiresFallbackWithTypedReasonAndInactiveState()
        {
            var state = new AssistantWebViewActivationState();

            Assert.IsFalse(state.BeginReactLoad(true, true, false, true));

            Assert.IsTrue(state.FallbackRequired);
            Assert.IsFalse(state.LoadedReactShell);
            StringAssert.Contains(state.LastLoadError, "assistant-index.css");
        }

        [TestMethod]
        public void NavigationFailure_RequiresFallbackAndDoesNotClaimReactActive()
        {
            var state = BeginCandidate();

            state.RecordNavigationFailure("ConnectionAborted");

            Assert.IsTrue(state.FallbackRequired);
            Assert.IsFalse(state.LoadedReactShell);
            Assert.AreEqual("Assistant React WebView navigation failed: ConnectionAborted.", state.LastLoadError);
        }

        [TestMethod]
        public void NavigationTimeout_RequiresFallbackAndDoesNotClaimReactActive()
        {
            var state = BeginCandidate();

            state.RecordNavigationTimeout();

            Assert.IsTrue(state.FallbackRequired);
            Assert.IsFalse(state.LoadedReactShell);
            Assert.AreEqual("Assistant React WebView navigation timed out.", state.LastLoadError);
        }

        [TestMethod]
        public void BootstrapFailure_RequiresFallbackAndDoesNotClaimReactActive()
        {
            var state = BeginCandidate();
            state.RecordNavigationSuccess();

            state.RecordBootstrapFailure("17 callback bridge was not mounted.");

            Assert.IsTrue(state.FallbackRequired);
            Assert.IsFalse(state.LoadedReactShell);
            Assert.AreEqual("Assistant React WebView bootstrap failed: 17 callback bridge was not mounted.", state.LastLoadError);
        }

        [TestMethod]
        public void FallbackShown_AfterEveryReactFailure_PreservesTypedReasonAndKeepsWebViewUsable()
        {
            var cases = new[]
            {
                CreateFailureCase("missing asset", state => { state.BeginReactLoad(true, false, true, true); }),
                CreateFailureCase("navigation failure", state => { BeginCandidate(state); state.RecordNavigationFailure("ConnectionAborted"); }),
                CreateFailureCase("navigation timeout", state => { BeginCandidate(state); state.RecordNavigationTimeout(); }),
                CreateFailureCase("bootstrap failure", state => { BeginCandidate(state); state.RecordBootstrapFailure("bridge not ready"); }),
            };

            foreach (var candidate in cases)
            {
                var state = new AssistantWebViewActivationState();
                candidate.Apply(state);
                var typedReason = state.LastLoadError;

                state.RecordFallbackShown();

                Assert.IsTrue(state.FallbackRequired, candidate.Name);
                Assert.IsFalse(state.LoadedReactShell, candidate.Name);
                Assert.IsFalse(state.WebViewUsable, candidate.Name);
                Assert.AreEqual(typedReason, state.LastLoadError, candidate.Name);

                state.RecordFallbackNavigationSuccess();

                Assert.IsTrue(state.WebViewUsable, candidate.Name);
                Assert.AreEqual(typedReason, state.LastLoadError, candidate.Name);
            }
        }

        [TestMethod]
        public void HostFailure_WithoutFallbackNavigation_RemainsUnusableWithTypedReason()
        {
            var state = BeginCandidate();

            state.RecordHostFailure("WebView2 initialization failed.");

            Assert.IsFalse(state.LoadedReactShell);
            Assert.IsFalse(state.WebViewUsable);
            Assert.IsTrue(state.FallbackRequired);
            Assert.AreEqual(
                "Assistant React WebView host initialization failed: WebView2 initialization failed.",
                state.LastLoadError);
        }

        [TestMethod]
        public void NavigationAndBootstrapSuccess_MarkReactActiveOnlyAfterBothProofs()
        {
            var state = BeginCandidate();

            Assert.IsFalse(state.LoadedReactShell, "Navigation dispatch must not claim React active.");
            state.RecordNavigationSuccess();
            Assert.IsFalse(state.LoadedReactShell, "Navigation success alone must not claim React active.");
            state.RecordBootstrapSuccess();

            Assert.IsFalse(state.FallbackRequired);
            Assert.IsTrue(state.LoadedReactShell);
            Assert.IsTrue(string.IsNullOrWhiteSpace(state.LastLoadError));
        }

        [TestMethod]
        public async Task FallbackNavigation_CannotMakeWebViewUsableUntilItsOwnNavigationCompletes()
        {
            var coordinator = new AssistantWebViewFallbackNavigationCoordinator();
            var state = BeginCandidate();
            state.RecordNavigationTimeout();

            Assert.IsFalse(coordinator.Completion.IsCompleted);
            Assert.IsFalse(state.WebViewUsable);

            coordinator.RecordCompleted(true, "Success");
            var outcome = await coordinator.Completion;
            state.RecordFallbackNavigationSuccess();

            Assert.AreEqual(AssistantWebViewFallbackNavigationOutcome.Success, outcome);
            Assert.IsTrue(state.WebViewUsable);
            StringAssert.Contains(state.LastLoadError, "React WebView navigation timed out");
        }

        [TestMethod]
        public async Task FallbackNavigation_FailureAndTimeoutRemainHardFailuresAndPreserveReactReason()
        {
            var failure = new AssistantWebViewFallbackNavigationCoordinator();
            failure.RecordCompleted(false, "ConnectionAborted");
            Assert.AreEqual(
                AssistantWebViewFallbackNavigationOutcome.Failure,
                await failure.Completion);

            var timeout = new AssistantWebViewFallbackNavigationCoordinator();
            timeout.RecordTimeout();
            Assert.AreEqual(
                AssistantWebViewFallbackNavigationOutcome.Timeout,
                await timeout.Completion);

            var state = BeginCandidate();
            state.RecordBootstrapFailure("bridge missing.");
            state.RecordFallbackNavigationFailure("ConnectionAborted");
            Assert.IsFalse(state.WebViewUsable);
            StringAssert.Contains(state.LastLoadError, "Assistant React WebView bootstrap failed: bridge missing.");
            StringAssert.Contains(state.LastLoadError, "Assistant WebView fallback navigation failed: ConnectionAborted.");

            var timeoutState = BeginCandidate();
            timeoutState.RecordNavigationTimeout();
            timeoutState.RecordFallbackShown();
            timeoutState.RecordFallbackNavigationTimeout();
            Assert.IsFalse(timeoutState.WebViewUsable);
            StringAssert.Contains(timeoutState.LastLoadError, "Assistant WebView fallback navigation failed: timed out.");
            Assert.AreNotEqual(state.LastLoadError, timeoutState.LastLoadError);
        }

        [TestMethod]
        public void ActiveFallbackDiagnostic_SanitizesBoundsAndSurvivesOrdinaryStatusUpdates()
        {
            var diagnostic = new AssistantFallbackDiagnostic();
            diagnostic.Activate("bad\r\nvalue \u0001 " + new string('x', 500));

            Assert.IsTrue(diagnostic.IsActive);
            Assert.IsTrue(diagnostic.DisplayText.StartsWith("React fallback active: bad value "));
            Assert.IsTrue(diagnostic.DisplayText.Length <= AssistantFallbackDiagnostic.MaximumDisplayLength);

            string restoredText;
            bool restoredVisible;
            Assert.IsTrue(diagnostic.TryRestore("Session ready", false, out restoredText, out restoredVisible));
            Assert.AreEqual(diagnostic.DisplayText, restoredText);
            Assert.IsTrue(restoredVisible);
        }

        [TestMethod]
        public async Task ConcurrentInitialization_UsesOneInFlightAttempt()
        {
            var gate = new AssistantInitializationGate();
            var completion = new TaskCompletionSource<bool>();
            var attempts = 0;
            Func<Task> initialize = () =>
            {
                attempts++;
                return completion.Task;
            };

            var first = gate.GetOrStart(initialize);
            var second = gate.GetOrStart(initialize);

            Assert.AreSame(first, second);
            Assert.AreEqual(1, attempts);

            completion.SetResult(true);
            await Task.WhenAll(first, second);
        }

        private static AssistantWebViewActivationState BeginCandidate()
        {
            var state = new AssistantWebViewActivationState();
            Assert.IsTrue(state.BeginReactLoad(true, true, true, true));
            return state;
        }

        private static void BeginCandidate(AssistantWebViewActivationState state)
        {
            Assert.IsTrue(state.BeginReactLoad(true, true, true, true));
        }

        private static FailureCase CreateFailureCase(string name, Action<AssistantWebViewActivationState> apply)
        {
            return new FailureCase { Name = name, Apply = apply };
        }

        private sealed class FailureCase
        {
            internal string Name { get; set; }
            internal Action<AssistantWebViewActivationState> Apply { get; set; }
        }
    }
}
