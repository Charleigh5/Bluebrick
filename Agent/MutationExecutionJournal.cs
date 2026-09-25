using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
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
        [JsonProperty("targetId")]
        public string TargetId { get; set; }
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
    // per line; stages flushed before the corresponding irreversible step. Admission reads the
    // journal through on every mutation call (disk truth — restart-safe with no latch hazard);
    // a static fast-path cache is refreshed by the explicitly named EnsureScanned, called at
    // executor admission start.
    internal sealed class MutationExecutionJournal
    {
        private static readonly object _cacheSync = new object();
        private static readonly Dictionary<string, bool> _blockedCache = new Dictionary<string, bool>(StringComparer.Ordinal);

        private readonly string _journalPath;

        internal string JournalPath
        {
            get { return _journalPath; }
        }

        internal MutationExecutionJournal(string journalPath)
        {
            _journalPath = journalPath ?? string.Empty;
        }

        internal static string DefaultJournalPath(string testFileRoot)
        {
            return Path.Combine(testFileRoot ?? string.Empty, ".execution-journal", "journal.jsonl");
        }

        internal static string DefaultCheckpointPath(string testFileRoot, string targetHash, string requestId)
        {
            return Path.Combine(testFileRoot ?? string.Empty, ".checkpoints", targetHash + "-" + requestId + ".sldprt");
        }

        internal void Append(MutationJournalRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (string.IsNullOrWhiteSpace(_journalPath))
                throw new InvalidOperationException("Mutation journal path is not configured.");
            var directory = Path.GetDirectoryName(_journalPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
            record.TimestampUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            File.AppendAllText(_journalPath, JsonConvert.SerializeObject(record, Formatting.None) + Environment.NewLine, Encoding.UTF8);
        }

        internal IReadOnlyList<MutationJournalRecord> ReadAll()
        {
            var records = new List<MutationJournalRecord>();
            if (string.IsNullOrWhiteSpace(_journalPath) || !File.Exists(_journalPath))
            {
                return records;
            }
            string[] lines;
            try
            {
                lines = File.ReadAllLines(_journalPath, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                throw new MutationJournalCorruptException("Mutation journal unreadable: " + ex.Message);
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
                if (record == null || string.IsNullOrWhiteSpace(record.TargetId) || string.IsNullOrWhiteSpace(record.Stage))
                {
                    throw new MutationJournalCorruptException("Mutation journal line " + (i + 1) + " is corrupt.");
                }
                records.Add(record);
            }
            return records;
        }

        // Read-through admission check: the latest line per target decides. Throws corrupt
        // (mapped by the caller to a global block) on unreadable/corrupt journals.
        internal bool IsBlocked(string targetId)
        {
            MutationJournalRecord latest = null;
            foreach (var record in ReadAll())
            {
                if (string.Equals(record.TargetId, targetId, StringComparison.OrdinalIgnoreCase))
                {
                    latest = record;
                }
            }
            return latest != null && !MutationStage.IsTerminal(latest.Stage);
        }

        // Named startup-scan entry: refreshes the fast-path cache for one journal file.
        // Called at executor admission start (never relied upon for correctness — the
        // read-through check above is always authoritative).
        internal static void EnsureScanned(string journalPath)
        {
            var records = new MutationExecutionJournal(journalPath).ReadAll();
            var latestByTarget = new Dictionary<string, MutationJournalRecord>(StringComparer.OrdinalIgnoreCase);
            foreach (var record in records)
            {
                latestByTarget[record.TargetId] = record;
            }
            lock (_cacheSync)
            {
                var stale = new List<string>();
                foreach (var key in _blockedCache.Keys)
                {
                    string path;
                    string dummy;
                    SplitKey(key, out path, out dummy);
                    if (string.Equals(path, journalPath ?? string.Empty, StringComparison.Ordinal))
                    {
                        stale.Add(key);
                    }
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
            lock (_cacheSync)
            {
                bool blocked;
                return _blockedCache.TryGetValue(MakeKey(journalPath, targetId), out blocked) && blocked;
            }
        }

        private static string MakeKey(string journalPath, string targetId)
        {
            return (journalPath ?? string.Empty) + "\0" + (targetId ?? string.Empty).ToUpperInvariant();
        }

        private static void SplitKey(string key, out string journalPath, out string targetId)
        {
            var at = (key ?? string.Empty).IndexOf('\0');
            if (at < 0)
            {
                journalPath = key;
                targetId = string.Empty;
                return;
            }
            journalPath = key.Substring(0, at);
            targetId = key.Substring(at + 1);
        }
    }
}
