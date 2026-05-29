using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace BlueBrick.Agent
{
    internal class AssistantToolAuditLog
    {
        private readonly object _sync = new object();
        private readonly List<AssistantToolExecutionReceipt> _receipts = new List<AssistantToolExecutionReceipt>();
        private readonly string _logRoot;

        internal AssistantToolAuditLog()
        {
        }

        internal AssistantToolAuditLog(string logRoot)
        {
            _logRoot = logRoot;
            if (!string.IsNullOrWhiteSpace(_logRoot))
            {
                Directory.CreateDirectory(_logRoot);
            }
        }

        internal string CurrentLogPath()
        {
            if (string.IsNullOrWhiteSpace(_logRoot)) return string.Empty;
            var stamp = System.DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            return Path.Combine(_logRoot, "assistant-tool-receipts-" + stamp + ".jsonl");
        }

        internal void Record(AssistantToolExecutionReceipt receipt)
        {
            if (receipt == null) return;
            lock (_sync)
            {
                _receipts.Add(receipt);
                Persist(receipt);
            }
        }

        internal IReadOnlyList<AssistantToolExecutionReceipt> Tail(int limit)
        {
            limit = limit <= 0 ? 25 : limit;
            lock (_sync)
            {
                return _receipts.Skip(System.Math.Max(0, _receipts.Count - limit)).ToArray();
            }
        }

        internal IReadOnlyList<AssistantToolExecutionReceipt> TailPersisted(int limit)
        {
            limit = limit <= 0 ? 25 : limit;
            if (string.IsNullOrWhiteSpace(_logRoot) || !Directory.Exists(_logRoot))
            {
                return new AssistantToolExecutionReceipt[0];
            }

            var files = Directory.GetFiles(_logRoot, "assistant-tool-receipts-*.jsonl").OrderBy(f => f).ToList();
            if (files.Count == 0) return new AssistantToolExecutionReceipt[0];

            var selected = new List<string>();
            for (var i = files.Count - 1; i >= 0 && selected.Count < limit; i--)
            {
                var lines = File.ReadAllLines(files[i]);
                for (var j = lines.Length - 1; j >= 0 && selected.Count < limit; j--)
                {
                    selected.Add(lines[j]);
                }
            }

            selected.Reverse();
            var receipts = new List<AssistantToolExecutionReceipt>();
            foreach (var line in selected)
            {
                try
                {
                    var receipt = JsonConvert.DeserializeObject<AssistantToolExecutionReceipt>(line);
                    if (receipt != null) receipts.Add(receipt);
                }
                catch
                {
                    // Ignore malformed audit lines instead of breaking the panel.
                }
            }

            return receipts;
        }

        private void Persist(AssistantToolExecutionReceipt receipt)
        {
            var path = CurrentLogPath();
            if (string.IsNullOrWhiteSpace(path)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var json = JsonConvert.SerializeObject(receipt, Formatting.None);
            File.AppendAllText(path, json + System.Environment.NewLine, Encoding.UTF8);
        }
    }
}
