using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlueBrick.Agent
{
    internal sealed class ProtectedRouteIngressGate
    {
        private readonly IProtectedRouteDecisionReceiptStore _receipts;
        internal ProtectedRouteIngressGate(IProtectedRouteDecisionReceiptStore receipts) { _receipts = receipts; }

        internal async Task<ProtectedRouteIngressInvocationResult> InvokeAsync(ProtectedRouteIngressRequest request, Func<ProtectedRouteIngressNormalizedRequest, Task> continuation)
        {
            var decision = Evaluate(request);
            if (!decision.Allowed) return new ProtectedRouteIngressInvocationResult(decision, false);
            if (continuation == null) return new ProtectedRouteIngressInvocationResult(ProtectedRouteIngressDecision.DenyUnprotected(decision.NormalizedRoute, decision.HttpMethod, "continuation_missing", "Request continuation missing."), false);
            await continuation(decision.NormalizedRequest).ConfigureAwait(false);
            return new ProtectedRouteIngressInvocationResult(decision, true);
        }

        private ProtectedRouteIngressDecision Evaluate(ProtectedRouteIngressRequest request)
        {
            request = request ?? new ProtectedRouteIngressRequest();
            var normalized = Normalize(request.Route, request.Method);
            var isProtected = normalized.IsAmbiguous || IsProtectedNamespace(normalized.NormalizedRoute, normalized.HttpMethod);
            if (!isProtected)
            {
                return request.IsAuthenticated ? ProtectedRouteIngressDecision.AllowUnprotected(normalized) : ProtectedRouteIngressDecision.DenyUnprotected(normalized.NormalizedRoute, normalized.HttpMethod, "authentication_failed", "Invalid or missing authentication token.");
            }

            var pre = new ProtectedRouteDecisionReceiptSnapshot(Guid.NewGuid().ToString("N"), "pre_action", DateTime.UtcNow, normalized.NormalizedRoute, normalized.HttpMethod, request.IsAuthenticated ? "x_agent_auth" : "unauthenticated", null, null, 0);
            if (!TryRecordPreAction(pre)) return ProtectedRouteIngressDecision.DenyProtected(normalized, "receipt_unavailable", "Protected request denied because its pre-action receipt could not be created.", null, null);
            if (normalized.IsAmbiguous) return DenyAndRecordFinal(normalized, pre, "ambiguous_route", "Ambiguous protected route denied.");
            if (!request.IsAuthenticated) return DenyAndRecordFinal(normalized, pre, "authentication_failed", "Invalid or missing authentication token.");
            if (RequiresOriginCheck(normalized.NormalizedRoute) && !IsAllowedOrigin(request.Origin)) return DenyAndRecordFinal(normalized, pre, "origin_not_allowed", "Origin not allowed.");
            if (request.ClientAuthorization != null && request.ClientAuthorization.Granted) return DenyAndRecordFinal(normalized, pre, "untrusted_client_authorization", "Client-supplied authorization is not a trusted approval.");
            return DenyAndRecordFinal(normalized, pre, "trusted_approval_lifecycle_unavailable", "Protected routes remain denied until a trusted approval lifecycle is established.");
        }

        private ProtectedRouteIngressDecision DenyAndRecordFinal(ProtectedRouteIngressNormalizedRequest normalized, ProtectedRouteDecisionReceiptSnapshot pre, string errorCode, string message)
        {
            var final = new ProtectedRouteDecisionReceiptSnapshot(pre.ReceiptId, "final", DateTime.UtcNow, pre.NormalizedRoute, pre.HttpMethod, pre.ActorSource, errorCode, "denied", 0);
            if (!TryRecordFinal(final)) return ProtectedRouteIngressDecision.DenyProtected(normalized, "receipt_unavailable", "Protected request denied because its final receipt could not be created.", pre, null);
            return ProtectedRouteIngressDecision.DenyProtected(normalized, errorCode, message, pre, final);
        }

        private bool TryRecordPreAction(ProtectedRouteDecisionReceiptSnapshot receipt) { try { return _receipts != null && _receipts.TryRecordPreAction(receipt); } catch { return false; } }
        private bool TryRecordFinal(ProtectedRouteDecisionReceiptSnapshot receipt) { try { return _receipts != null && _receipts.TryRecordFinal(receipt); } catch { return false; } }

        internal static ProtectedRouteIngressNormalizedRequest Normalize(string rawRoute, string rawMethod)
        {
            var raw = (rawRoute ?? string.Empty).Trim();
            var malformedPercent = HasMalformedPercent(raw);
            var canonical = CanonicalizeSeparators(raw);
            var decoded = canonical;
            if (!malformedPercent)
            {
                try { decoded = Uri.UnescapeDataString(canonical); } catch { malformedPercent = true; }
            }
            decoded = CanonicalizeSeparators(decoded);
            var residualPercent = decoded.IndexOf('%') >= 0;
            if (string.IsNullOrEmpty(decoded)) decoded = "/";
            if (!decoded.StartsWith("/", StringComparison.Ordinal)) decoded = "/" + decoded;
            if (decoded.Length > 1) decoded = decoded.TrimEnd('/');
            return new ProtectedRouteIngressNormalizedRequest(decoded.ToLowerInvariant(), NormalizeMethod(rawMethod), malformedPercent || residualPercent);
        }

        private static string CanonicalizeSeparators(string value)
        {
            var result = (value ?? string.Empty).Replace('\\', '/');
            while (result.Contains("//")) result = result.Replace("//", "/");
            return result;
        }

        private static bool HasMalformedPercent(string value)
        {
            for (var i = 0; i < (value ?? string.Empty).Length; i++)
            {
                if (value[i] != '%') continue;
                if (i + 2 >= value.Length || !IsHex(value[i + 1]) || !IsHex(value[i + 2])) return true;
                i += 2;
            }
            return false;
        }

        private static bool IsHex(char value) { return (value >= '0' && value <= '9') || (value >= 'a' && value <= 'f') || (value >= 'A' && value <= 'F'); }
        private static string NormalizeMethod(string method) { return string.IsNullOrWhiteSpace(method) ? string.Empty : method.Trim().ToUpperInvariant(); }
        private static bool IsProtectedNamespace(string route, string method) { return route == "/sw" || route.StartsWith("/sw/", StringComparison.Ordinal) || route == "/pdm" || route.StartsWith("/pdm/", StringComparison.Ordinal) || ((route == "/lab/vault" || route.StartsWith("/lab/vault/", StringComparison.Ordinal)) && !(route == "/lab/vault/status" && method == "GET")); }
        private static bool RequiresOriginCheck(string route) { return route == "/sw" || route.StartsWith("/sw/", StringComparison.Ordinal) || route == "/pdm" || route.StartsWith("/pdm/", StringComparison.Ordinal); }
        private static bool IsAllowedOrigin(string origin)
        {
            if (origin == null || origin.Length == 0) return true;
            if (!string.Equals(origin, origin.Trim(), StringComparison.Ordinal) || !HasAuthorityOnlyRawSuffix(origin)) return false;
            Uri uri;
            if (!Uri.TryCreate(origin, UriKind.Absolute, out uri)) return false;
            if (!(uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) || !string.IsNullOrEmpty(uri.UserInfo)) return false;
            if (!(string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) || string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase))) return false;
            return uri.AbsolutePath == "/" && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);
        }

        private static bool HasAuthorityOnlyRawSuffix(string origin)
        {
            var schemeEnd = origin.IndexOf("://", StringComparison.Ordinal);
            if (schemeEnd <= 0) return false;
            var suffixStart = origin.IndexOfAny(new[] { '/', '?', '#' }, schemeEnd + 3);
            return suffixStart < 0 || string.Equals(origin.Substring(suffixStart), "/", StringComparison.Ordinal);
        }
    }

    internal sealed class ProtectedRouteIngressRequest { internal string Route { get; set; } internal string Method { get; set; } internal bool IsAuthenticated { get; set; } internal string Origin { get; set; } internal AssistantToolAuthorization ClientAuthorization { get; set; } }
    internal sealed class ProtectedRouteIngressNormalizedRequest { internal ProtectedRouteIngressNormalizedRequest(string route, string method, bool ambiguous) { NormalizedRoute = route; HttpMethod = method; IsAmbiguous = ambiguous; } internal string NormalizedRoute { get; private set; } internal string HttpMethod { get; private set; } internal bool IsAmbiguous { get; private set; } }
    internal sealed class ProtectedRouteIngressInvocationResult { internal ProtectedRouteIngressInvocationResult(ProtectedRouteIngressDecision decision, bool invoked) { Decision = decision; ContinuationInvoked = invoked; } internal ProtectedRouteIngressDecision Decision { get; private set; } internal bool ContinuationInvoked { get; private set; } }
    internal sealed class ProtectedRouteIngressDecision
    {
        internal bool IsProtectedRoute { get; private set; } internal bool Allowed { get; private set; } internal int StatusCode { get; private set; } internal string NormalizedRoute { get; private set; } internal string HttpMethod { get; private set; } internal string ErrorCode { get; private set; } internal string Message { get; private set; } internal ProtectedRouteIngressNormalizedRequest NormalizedRequest { get; private set; } internal ProtectedRouteDecisionReceiptSnapshot PreActionReceipt { get; private set; } internal ProtectedRouteDecisionReceiptSnapshot FinalReceipt { get; private set; }
        internal static ProtectedRouteIngressDecision AllowUnprotected(ProtectedRouteIngressNormalizedRequest request) { return new ProtectedRouteIngressDecision { Allowed = true, StatusCode = 200, NormalizedRoute = request.NormalizedRoute, HttpMethod = request.HttpMethod, NormalizedRequest = request }; }
        internal static ProtectedRouteIngressDecision DenyUnprotected(string route, string method, string code, string message) { return new ProtectedRouteIngressDecision { Allowed = false, StatusCode = 403, NormalizedRoute = route, HttpMethod = method, ErrorCode = code, Message = message, NormalizedRequest = new ProtectedRouteIngressNormalizedRequest(route, method, false) }; }
        internal static ProtectedRouteIngressDecision DenyProtected(ProtectedRouteIngressNormalizedRequest request, string code, string message, ProtectedRouteDecisionReceiptSnapshot pre, ProtectedRouteDecisionReceiptSnapshot final) { return new ProtectedRouteIngressDecision { IsProtectedRoute = true, Allowed = false, StatusCode = 403, NormalizedRoute = request.NormalizedRoute, HttpMethod = request.HttpMethod, ErrorCode = code, Message = message, NormalizedRequest = request, PreActionReceipt = pre, FinalReceipt = final }; }
    }
    internal sealed class ProtectedRouteDecisionReceiptSnapshot
    {
        internal ProtectedRouteDecisionReceiptSnapshot(string id, string stage, DateTime timestamp, string route, string method, string actor, string policy, string outcome, int mutations) { ReceiptId = id; Stage = stage; TimestampUtc = timestamp; NormalizedRoute = route; HttpMethod = method; ActorSource = actor; PolicyCode = policy; Outcome = outcome; MutationCount = mutations; }
        internal string ReceiptId { get; private set; } internal string Stage { get; private set; } internal DateTime TimestampUtc { get; private set; } internal string NormalizedRoute { get; private set; } internal string HttpMethod { get; private set; } internal string ActorSource { get; private set; } internal string PolicyCode { get; private set; } internal string Outcome { get; private set; } internal int MutationCount { get; private set; }
        internal ProtectedRouteDecisionReceiptSnapshot Copy() { return new ProtectedRouteDecisionReceiptSnapshot(ReceiptId, Stage, TimestampUtc, NormalizedRoute, HttpMethod, ActorSource, PolicyCode, Outcome, MutationCount); }
    }
    internal interface IProtectedRouteDecisionReceiptStore { bool TryRecordPreAction(ProtectedRouteDecisionReceiptSnapshot receipt); bool TryRecordFinal(ProtectedRouteDecisionReceiptSnapshot receipt); }
    internal sealed class InMemoryProtectedRouteDecisionReceiptStore : IProtectedRouteDecisionReceiptStore
    {
        private sealed class Pair { internal ProtectedRouteDecisionReceiptSnapshot Pre; internal ProtectedRouteDecisionReceiptSnapshot Final; }
        private readonly int _capacity; private readonly Dictionary<string, Pair> _pairs = new Dictionary<string, Pair>(); private readonly LinkedList<string> _order = new LinkedList<string>(); private readonly object _sync = new object();
        internal InMemoryProtectedRouteDecisionReceiptStore(int capacity = 256) { if (capacity <= 0) throw new ArgumentOutOfRangeException("capacity"); _capacity = capacity; }
        internal int Capacity { get { return _capacity; } }
        internal IReadOnlyList<ProtectedRouteDecisionReceiptSnapshot> PreActionSnapshots { get { lock (_sync) { var list = new List<ProtectedRouteDecisionReceiptSnapshot>(); foreach (var id in _order) list.Add(_pairs[id].Pre.Copy()); return list.AsReadOnly(); } } }
        internal IReadOnlyList<ProtectedRouteDecisionReceiptSnapshot> FinalSnapshots { get { lock (_sync) { var list = new List<ProtectedRouteDecisionReceiptSnapshot>(); foreach (var id in _order) if (_pairs[id].Final != null) list.Add(_pairs[id].Final.Copy()); return list.AsReadOnly(); } } }
        public bool TryRecordPreAction(ProtectedRouteDecisionReceiptSnapshot receipt) { if (receipt == null) return false; lock (_sync) { if (_pairs.ContainsKey(receipt.ReceiptId)) return false; while (_pairs.Count >= _capacity) { var id = _order.First.Value; _order.RemoveFirst(); _pairs.Remove(id); } _pairs.Add(receipt.ReceiptId, new Pair { Pre = receipt.Copy() }); _order.AddLast(receipt.ReceiptId); return true; } }
        public bool TryRecordFinal(ProtectedRouteDecisionReceiptSnapshot receipt) { if (receipt == null) return false; lock (_sync) { Pair pair; if (!_pairs.TryGetValue(receipt.ReceiptId, out pair) || pair.Final != null) return false; pair.Final = receipt.Copy(); return true; } }
    }
}
