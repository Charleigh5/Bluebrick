using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlueBrick.Agent;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BlueBrick.UI.Tests.Agent
{
    [TestClass]
    public class ProtectedRouteIngressTests
    {
        [TestMethod]
        public async Task ProtectedRouteIngress_PersistsImmutableCorrelatedPair_AndPassesGateNormalizedRequestToContinuation()
        {
            var receipts = new InMemoryProtectedRouteDecisionReceiptStore(4);
            var gate = new ProtectedRouteIngressGate(receipts);
            var continuation = new CountingContinuation();

            var denied = await gate.InvokeAsync(Protected("/sw/open", "POST"), continuation.InvokeAsync);
            Assert.IsFalse(denied.ContinuationInvoked);
            Assert.AreEqual(0, continuation.InvocationCount, "Protected denial must not invoke the exact supplied continuation.");
            Assert.AreEqual(1, receipts.PreActionSnapshots.Count);
            Assert.AreEqual(1, receipts.FinalSnapshots.Count);
            Assert.AreEqual(receipts.PreActionSnapshots[0].ReceiptId, receipts.FinalSnapshots[0].ReceiptId);
            Assert.AreEqual("pre_action", receipts.PreActionSnapshots[0].Stage);
            Assert.AreEqual("final", receipts.FinalSnapshots[0].Stage);
            Assert.IsNull(receipts.PreActionSnapshots[0].Outcome);
            Assert.AreEqual("denied", receipts.FinalSnapshots[0].Outcome);
            var allowed = await gate.InvokeAsync(Protected("/lab/vault/status/", " get "), continuation.InvokeAsync);
            Assert.IsTrue(allowed.ContinuationInvoked);
            Assert.AreEqual(1, continuation.InvocationCount);
            Assert.AreEqual("/lab/vault/status", continuation.LastRequest.NormalizedRoute);
            Assert.AreEqual("GET", continuation.LastRequest.HttpMethod);
        }

        [TestMethod]
        public async Task ProtectedRouteIngress_DeniesAllClientApprovalClaims_BeforeContinuation()
        {
            var claims = new[]
            {
                new AssistantToolAuthorization { Granted = true, ApprovalId = "forged" },
                new AssistantToolAuthorization { Granted = true, ApprovedRoute = "/sw/create_drawing" },
                new AssistantToolAuthorization { Granted = true, ApprovedMethod = "GET" },
                new AssistantToolAuthorization { Granted = true, ExpiresUtc = DateTime.UtcNow.AddMinutes(-1) },
                new AssistantToolAuthorization { Granted = true, ConsumedUtc = DateTime.UtcNow.AddMinutes(-1) }
            };
            foreach (var claim in claims)
            {
                var continuation = new CountingContinuation();
                var result = await new ProtectedRouteIngressGate(new InMemoryProtectedRouteDecisionReceiptStore(4)).InvokeAsync(Protected("/sw/open", "POST", claim), continuation.InvokeAsync);
                Assert.IsFalse(result.ContinuationInvoked);
                Assert.AreEqual(0, continuation.InvocationCount);
                Assert.AreEqual("untrusted_client_authorization", result.Decision.ErrorCode);
            }
        }

        [TestMethod]
        public async Task ProtectedRouteIngress_NormalizesOneDecodeSeparatorVariants_AndFailsClosedForAmbiguousPaths()
        {
            var resolvable = new[]
            {
                new { Route = "/sw", Expected = "/sw" }, new { Route = "/pdm", Expected = "/pdm" }, new { Route = "/lab/vault", Expected = "/lab/vault" },
                new { Route = "/SW//OPEN/", Expected = "/sw/open" }, new { Route = "/sw\\open", Expected = "/sw/open" },
                new { Route = "/pdm%2funknown", Expected = "/pdm/unknown" }, new { Route = "/lab%2fvault%5creindex", Expected = "/lab/vault/reindex" }
            };
            foreach (var item in resolvable)
            {
                var continuation = new CountingContinuation();
                var result = await new ProtectedRouteIngressGate(new InMemoryProtectedRouteDecisionReceiptStore(16)).InvokeAsync(Protected(item.Route, "POST"), continuation.InvokeAsync);
                Assert.IsFalse(result.ContinuationInvoked, item.Route);
                Assert.AreEqual(0, continuation.InvocationCount, item.Route);
                Assert.AreEqual(item.Expected, result.Decision.NormalizedRoute, item.Route);
            }
            foreach (var route in new[] { "/sw%252fopen", "/pdm%255csearch", "/%2573%2577/open", "/sw%", "/lab/vault%2" })
            {
                var receipts = new InMemoryProtectedRouteDecisionReceiptStore(4);
                var continuation = new CountingContinuation();
                var result = await new ProtectedRouteIngressGate(receipts).InvokeAsync(Protected(route, "POST"), continuation.InvokeAsync);
                Assert.IsTrue(result.Decision.IsProtectedRoute, route);
                Assert.IsFalse(result.ContinuationInvoked, route);
                Assert.AreEqual(0, continuation.InvocationCount, route);
                Assert.AreEqual(1, receipts.PreActionSnapshots.Count, route);
                Assert.AreEqual(1, receipts.FinalSnapshots.Count, route);
            }
        }

        [TestMethod]
        public async Task ProtectedRouteIngress_DeniesUnknownSolidWorksAndVaultSubroutes_BeforeContinuation()
        {
            foreach (var route in new[] { "/sw/unrecognized-operation", "/lab/vault/unrecognized-operation" })
            {
                var continuation = new CountingContinuation();
                var result = await new ProtectedRouteIngressGate(new InMemoryProtectedRouteDecisionReceiptStore(4)).InvokeAsync(Protected(route, "PATCH"), continuation.InvokeAsync);
                Assert.IsFalse(result.ContinuationInvoked, route);
                Assert.AreEqual(0, continuation.InvocationCount, route);
            }
        }

        [TestMethod]
        public async Task ProtectedRouteIngress_PreservesOnlyAuthenticatedGetVaultStatus()
        {
            var allowedContinuation = new CountingContinuation();
            var allowed = await new ProtectedRouteIngressGate(new InMemoryProtectedRouteDecisionReceiptStore(4)).InvokeAsync(Protected("/lab/vault/status/", "GET"), allowedContinuation.InvokeAsync);
            var deniedContinuation = new CountingContinuation();
            var denied = await new ProtectedRouteIngressGate(new InMemoryProtectedRouteDecisionReceiptStore(4)).InvokeAsync(Protected("/lab/vault/status", "POST"), deniedContinuation.InvokeAsync);
            Assert.IsTrue(allowed.ContinuationInvoked);
            Assert.AreEqual(1, allowedContinuation.InvocationCount);
            Assert.IsFalse(denied.ContinuationInvoked);
            Assert.AreEqual(0, deniedContinuation.InvocationCount);
        }

        [TestMethod]
        public async Task ProtectedRouteIngress_AllowsOnlyStrictLocalHttpHttpsOrigins()
        {
            foreach (var origin in new[] { null, "http://localhost", "https://localhost:9443/", "http://127.0.0.1:35001/" })
            {
                var continuation = new CountingContinuation();
                var result = await new ProtectedRouteIngressGate(new InMemoryProtectedRouteDecisionReceiptStore(4)).InvokeAsync(ProtectedWithOrigin(origin), continuation.InvokeAsync);
                Assert.IsFalse(result.ContinuationInvoked, origin ?? "missing origin");
                Assert.AreEqual(0, continuation.InvocationCount);
                Assert.AreNotEqual("origin_not_allowed", result.Decision.ErrorCode, origin ?? "missing origin");
            }
            foreach (var origin in new[] { "file://localhost", "ftp://localhost", "http://localhost/path", "http://localhost/./", "http://localhost/a/..", "http://localhost/%2e/", "http://user@localhost/", "http://localhost/?q=1", "https://localhost/#x", "http://example.invalid", "not-a-uri", "   ", " http://localhost", "http://localhost/ " })
            {
                var continuation = new CountingContinuation();
                var result = await new ProtectedRouteIngressGate(new InMemoryProtectedRouteDecisionReceiptStore(4)).InvokeAsync(ProtectedWithOrigin(origin), continuation.InvokeAsync);
                Assert.AreEqual("origin_not_allowed", result.Decision.ErrorCode, origin);
                Assert.AreEqual(0, continuation.InvocationCount, origin);
            }
        }

        [TestMethod]
        public async Task ProtectedRouteIngress_DeniesWhenEitherReceiptOperationFailsOrThrows_BeforeContinuation()
        {
            foreach (var store in new IProtectedRouteDecisionReceiptStore[] { new FailingReceiptStore(true, false, false), new FailingReceiptStore(false, true, false), new FailingReceiptStore(false, false, false), new FailingReceiptStore(false, false, true) })
            {
                var continuation = new CountingContinuation();
                var result = await new ProtectedRouteIngressGate(store).InvokeAsync(Protected("/pdm/search", "POST"), continuation.InvokeAsync);
                Assert.AreEqual("receipt_unavailable", result.Decision.ErrorCode);
                Assert.IsFalse(result.ContinuationInvoked);
                Assert.AreEqual(0, continuation.InvocationCount);
            }
        }

        [TestMethod]
        public async Task ProtectedRouteIngress_BoundsCorrelatedReceiptPairsUnderSustainedDenial()
        {
            var receipts = new InMemoryProtectedRouteDecisionReceiptStore(2);
            var gate = new ProtectedRouteIngressGate(receipts);
            for (var i = 0; i < 8; i++) await gate.InvokeAsync(Protected("/sw/reject-" + i, "POST"), new CountingContinuation().InvokeAsync);

            Assert.AreEqual(2, receipts.Capacity);
            Assert.AreEqual(2, receipts.PreActionSnapshots.Count);
            Assert.AreEqual(2, receipts.FinalSnapshots.Count);
            CollectionAssert.AreEquivalent(receipts.PreActionSnapshots.Select(x => x.ReceiptId).ToArray(), receipts.FinalSnapshots.Select(x => x.ReceiptId).ToArray());
        }

        [TestMethod]
        public void ProtectedRouteIngress_PreservesExistingAssistantToolProtectedRouteDenial()
        {
            var decision = new AssistantToolPolicy().EvaluateRoute("/sw/open", "POST", AssistantToolInvocationSource.AssistantTool);
            Assert.IsFalse(decision.Allowed);
            Assert.AreEqual("blocked_cad_route", decision.Code);
        }

        private static ProtectedRouteIngressRequest Protected(string route, string method, AssistantToolAuthorization authorization = null) { return new ProtectedRouteIngressRequest { Route = route, Method = method, IsAuthenticated = true, ClientAuthorization = authorization }; }
        private static ProtectedRouteIngressRequest ProtectedWithOrigin(string origin) { return new ProtectedRouteIngressRequest { Route = "/sw/open", Method = "POST", IsAuthenticated = true, Origin = origin }; }

        private sealed class CountingContinuation
        {
            internal int InvocationCount { get; private set; }
            internal ProtectedRouteIngressNormalizedRequest LastRequest { get; private set; }
            internal Task InvokeAsync(ProtectedRouteIngressNormalizedRequest request) { InvocationCount++; LastRequest = request; return Task.FromResult(0); }
        }

        private sealed class FailingReceiptStore : IProtectedRouteDecisionReceiptStore
        {
            private readonly bool _failPre; private readonly bool _throwPre; private readonly bool _throwFinal;
            internal FailingReceiptStore(bool failPre, bool throwPre, bool throwFinal) { _failPre = failPre; _throwPre = throwPre; _throwFinal = throwFinal; }
            public bool TryRecordPreAction(ProtectedRouteDecisionReceiptSnapshot receipt) { if (_throwPre) throw new InvalidOperationException(); return !_failPre; }
            public bool TryRecordFinal(ProtectedRouteDecisionReceiptSnapshot receipt) { if (_throwFinal) throw new InvalidOperationException(); return false; }
        }
    }
}
