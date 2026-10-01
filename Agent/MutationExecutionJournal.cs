using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using BlueBrick.Audit.Core;
using Newtonsoft.Json;

namespace BlueBrick.Agent
{
    // Mutation journal stage vocabulary (Sprint 04 contract C7). Terminal stages clear the
    // admission block; any other latest stage blocks. `reconciled` / `restored` are appended
    // only by the manual Lab-checklist procedures (no code writes them).
    internal static class MutationStage
    {
        internal const string Prepared = "prepared";
        internal const string WriteStarted = "write_started";
        internal const string SaveAttempted = "save_attempted";
        internal const string SaveReturned = "save_returned";
        internal const string SavedVerified = "saved_verified";
        internal const string EvidenceComplete = "evidence_complete";
        internal const string Reconciled = "reconciled";
        internal const string Restored = "restored";

        internal static bool IsTerminal(string stage)
        {
            return string.Equals(stage, EvidenceComplete, StringComparison.Ordinal)
                || string.Equals(stage, Reconciled, StringComparison.Ordinal)
                || string.Equals(stage, Restored, StringComparison.Ordinal);
        }
    }

    internal sealed class MutationJournalRecord
    {
        [JsonProperty("targetHash")]
        public string TargetHash { get; set; }
        [JsonProperty("requestId")]
        public string RequestId { get; set; }
        [JsonProperty("approvalId")]
        public string ApprovalId { get; set; }
        [JsonProperty("stage")]
        public string Stage { get; set; }
        [JsonProperty("baselineHash")]
        public string BaselineHash { get; set; }
        [JsonProperty("checkpointHash")]
        public string CheckpointHash { get; set; }
        [JsonProperty("timestampUtc")]
        public string TimestampUtc { get; set; }
    }

    internal sealed class MutationJournalCorruptException : Exception
    {
        internal MutationJournalCorruptException(string message)
            : base(message)
        {
        }
    }

    // Durable per-transaction execution journal (Sprint 04 contract C7), distinct from the
    // issuer ledger (which is evidence-only and can never clear this block). One JSON object
    // per line, appended through FileStream WriteThrough under a static lock shared with
    // readers (no torn-line reads; flush precedes every irreversible step). Exact paths never
    // persist: matching runs on the SHA256 of the canonical path, and request/approval IDs
    // are redacted+bounded at the single Append choke point. Admission reads the journal
    // through on every mutation call (disk truth — restart-safe with no latch hazard); a
    // static fast-path cache is refreshed by the explicitly named EnsureScanned, called at
    // executor admission start after target ownership is acquired.
    internal sealed class MutationExecutionJournal
    {
        private const int EvidenceIdentifierLimit = 128;

        private static readonly object _writerSync = new object();
        private static readonly Dictionary<string, bool> _blockedCache = new Dictionary<string, bool>(StringComparer.Ordinal);

        private readonly string _journalPath;

        internal MutationExecutionJournal(string journalPath)
        {
            _journalPath = journalPath ?? string.Empty;
        }

        internal string JournalPath
        {
            get { return _journalPath; }
        }

        internal static string DefaultJournalPath(string testFileRoot)
        {
            return Path.Combine(testFileRoot ?? string.Empty, ".execution-journal", "journal.jsonl");
        }

        internal static string DefaultCheckpointPath(string testFileRoot, string targetHash, string requestHash)
        {
            return Path.Combine(testFileRoot ?? string.Empty, ".checkpoints", targetHash + "-" + requestHash + ".sldprt");
        }

        internal static string HashTarget(string canonicalPath)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(canonicalPath ?? string.Empty);
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        internal static string HashRequest(string requestId)
        {
            var full = HashTarget(requestId ?? string.Empty);
            return full.Length <= 16 ? full : full.Substring(0, 16);
        }

        internal void Append(string targetId, string requestId, string approvalId, string stage, string baselineHash, string checkpointHash)
        {
            if (string.IsNullOrWhiteSpace(_journalPath))
                throw new InvalidOperationException("Mutation journal path is not configured.");
            if (string.IsNullOrWhiteSpace(targetId))
                throw new ArgumentException("Target identity is required.", "targetId");
            if (string.IsNullOrWhiteSpace(stage))
                throw new ArgumentException("Stage is required.", "stage");
            var record = new MutationJournalRecord
            {
                TargetHash = HashTarget(targetId),
                RequestId = Evidence(requestId),
                ApprovalId = Evidence(approvalId),
                Stage = stage,
                BaselineHash = baselineHash ?? string.Empty,
                CheckpointHash = checkpointHash ?? string.Empty,
                TimestampUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
            };
            var directory = Path.GetDirectoryName(_journalPath);
            lock (_writerSync)
            {
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                using (var stream = new FileStream(_journalPath, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))
                using (var writer = new StreamWriter(stream, Encoding.UTF8))
                {
                    writer.WriteLine(JsonConvert.SerializeObject(record, Formatting.None));
                    writer.Flush();
                    stream.Flush(true);
                }
            }
        }

        internal IReadOnlyList<MutationJournalRecord> ReadAll()
        {
            var records = new List<MutationJournalRecord>();
            if (string.IsNullOrWhiteSpace(_journalPath))
            {
                return records;
            }
            string[] lines;
            lock (_writerSync)
            {
                if (!File.Exists(_journalPath))
                {
                    return records;
                }
                try
                {
                    lines = File.ReadAllLines(_journalPath, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    throw new MutationJournalCorruptException("Mutation journal unreadable: " + ex.Message);
                }
            }
            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                MutationJournalRecord record = null;
                try
                {
                    record = JsonConvert.DeserializeObject<MutationJournalRecord>(lines[i]);
                }
                catch
                {
                    record = null;
                }
                if (record == null || string.IsNullOrWhiteSpace(record.TargetHash) || string.IsNullOrWhiteSpace(record.Stage))
                {
                    throw new MutationJournalCorruptException("Mutation journal line " + (i + 1) + " is corrupt.");
                }
                records.Add(record);
            }
            return records;
        }

        // Read-through admission check: the latest line per target hash decides. Throws corrupt
        // (mapped by the caller to a global block) on unreadable/corrupt journals.
        internal bool IsBlocked(string targetId)
        {
            var wanted = HashTarget(targetId);
            MutationJournalRecord latest = null;
            foreach (var record in ReadAll())
            {
                if (string.Equals(record.TargetHash, wanted, StringComparison.Ordinal)) latest = record;
            }
            return latest != null && !MutationStage.IsTerminal(latest.Stage);
        }

        // Named startup-scan entry: refreshes the fast-path cache for one journal file.
        // Called at executor admission start after target ownership is acquired (never relied
        // upon for correctness — the read-through check above is always authoritative).
        internal static void EnsureScanned(string journalPath)
        {
            var records = new MutationExecutionJournal(journalPath).ReadAll();
            var latestByTarget = new Dictionary<string, MutationJournalRecord>(StringComparer.Ordinal);
            foreach (var record in records)
            {
                latestByTarget[record.TargetHash] = record;
            }
            lock (_writerSync)
            {
                var stale = new List<string>();
                foreach (var key in _blockedCache.Keys)
                {
                    string path;
                    string dummy;
                    SplitKey(key, out path, out dummy);
                    if (string.Equals(path, journalPath ?? string.Empty, StringComparison.Ordinal)) stale.Add(key);
                }
                foreach (var key in stale)
                {
                    _blockedCache.Remove(key);
                }
                foreach (var pair in latestByTarget)
                {
                    _blockedCache[MakeKey(journalPath, pair.Key)] = !MutationStage.IsTerminal(pair.Value.Stage);
                }
            }
        }

        internal static bool IsBlockedCached(string journalPath, string targetId)
        {
            lock (_writerSync)
            {
                bool blocked;
                return _blockedCache.TryGetValue(MakeKey(journalPath, HashTarget(targetId)), out blocked) && blocked;
            }
        }

        private static string Evidence(string value)
        {
            var redacted = AuditRedactionService.RedactSecrets(value ?? string.Empty);
            return redacted.Length <= EvidenceIdentifierLimit ? redacted : redacted.Substring(0, EvidenceIdentifierLimit);
        }

        private static string MakeKey(string journalPath, string targetHash)
        {
            return (journalPath ?? string.Empty) + "\0" + (targetHash ?? string.Empty);
        }

        private static void SplitKey(string key, out string journalPath, out string targetHash)
        {
            var at = (key ?? string.Empty).IndexOf('\0');
            if (at < 0)
            {
                journalPath = key;
                targetHash = string.Empty;
                return;
            }
            journalPath = key.Substring(0, at);
            targetHash = key.Substring(at + 1);
        }
    }
}
