using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace BlueBrick.Agent
{
    internal class AssistantApprovalLedger : IApprovalLedgerSink
    {
        private readonly object _sync = new object();
        private readonly List<AssistantApprovalLedgerEntry> _entries = new List<AssistantApprovalLedgerEntry>();
        private readonly string _ledgerPath;

        internal AssistantApprovalLedger()
        {
            _ledgerPath = Path.Combine(AppIdentity.AssistantHistoryRoot, "approvals", "approval-ledger.jsonl");
        }

        internal AssistantApprovalLedger(string ledgerPath)
        {
            _ledgerPath = ledgerPath;
        }

        public void Append(AssistantApprovalLedgerEntry entry)
        {
            if (entry == null) return;
            lock (_sync)
            {
                _entries.Add(entry);
                Persist(entry);
            }
        }

        internal IReadOnlyList<AssistantApprovalLedgerEntry> Tail(int limit)
        {
            limit = limit <= 0 ? 25 : limit;
            lock (_sync)
            {
                return _entries.Skip(System.Math.Max(0, _entries.Count - limit)).ToArray();
            }
        }

        private void Persist(AssistantApprovalLedgerEntry entry)
        {
            if (string.IsNullOrWhiteSpace(_ledgerPath)) return;
            var directory = Path.GetDirectoryName(_ledgerPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
            var json = JsonConvert.SerializeObject(entry, Formatting.None);
            File.AppendAllText(_ledgerPath, json + System.Environment.NewLine, Encoding.UTF8);
        }
    }

    internal class AssistantApprovalLedgerEntry
    {
        internal const string Requested = "requested";
        internal const string Issued = "issued";
        internal const string Denied = "denied";
        internal const string Expired = "expired";
        internal const string Consumed = "consumed";
        internal const string Orphaned = "orphaned";

        [JsonProperty("timestampUtc")]
        public string TimestampUtc { get; set; }
        [JsonProperty("traceId")]
        public string TraceId { get; set; }
        [JsonProperty("sessionId")]
        public string SessionId { get; set; }
        [JsonProperty("approvalId", NullValueHandling = NullValueHandling.Ignore)]
        public string ApprovalId { get; set; }
        [JsonProperty("capabilityId")]
        public string CapabilityId { get; set; }
        [JsonProperty("argumentDigest", NullValueHandling = NullValueHandling.Ignore)]
        public string ArgumentDigest { get; set; }
        [JsonProperty("environment")]
        public string Environment { get; set; }
        [JsonProperty("event")]
        public string Event { get; set; }
        [JsonProperty("outcome")]
        public string Outcome { get; set; }
    }
}
