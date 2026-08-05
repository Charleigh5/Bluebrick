using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BlueBrick.Agent
{
    internal class TelemetryLogger
    {
        private readonly object _lock = new object();
        private readonly string _logDir;
        private readonly string _prefix;
        private readonly double _sampleRateSuccess;
        private readonly int _retentionDays;
        private readonly int _maxMetadataBytes;
        private readonly int _maxErrorLength;
        private DateTime _lastCleanupUtc = DateTime.MinValue;
        private long _totalRequests;
        private long _totalErrors;
        private double _totalLatencyMs;
        private string _lastError;
        private static readonly Random _rng = new Random();

        internal TelemetryLogger(
            string logDir,
            string prefix = "events",
            double sampleRateSuccess = 0.1,
            int retentionDays = 7,
            int maxMetadataBytes = 2048,
            int maxErrorLength = 500)
        {
            _logDir = logDir;
            _prefix = prefix;
            _sampleRateSuccess = Math.Max(0.0, Math.Min(1.0, sampleRateSuccess));
            _retentionDays = Math.Max(1, retentionDays);
            _maxMetadataBytes = Math.Max(256, maxMetadataBytes);
            _maxErrorLength = Math.Max(100, maxErrorLength);
            Directory.CreateDirectory(_logDir);
        }

        private string CurrentLogPath()
        {
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            return Path.Combine(_logDir, $"{_prefix}-{stamp}.jsonl");
        }

        private void CleanupOldFiles()
        {
            var now = DateTime.UtcNow;
            if ((now - _lastCleanupUtc).TotalSeconds < 60) return;
            _lastCleanupUtc = now;
            var cutoff = now.AddDays(-_retentionDays);
            var files = Directory.GetFiles(_logDir, $"{_prefix}-*.jsonl");
            foreach (var file in files)
            {
                try
                {
                    var info = new FileInfo(file);
                    if (info.LastWriteTimeUtc < cutoff)
                    {
                        info.Delete();
                    }
                }
                catch
                {
                    // ignore cleanup errors
                }
            }
        }

        private JToken SanitizeMetadata(object metadata)
        {
            if (metadata == null) return new JObject();
            string raw;
            try
            {
                raw = JsonConvert.SerializeObject(metadata);
            }
            catch
            {
                raw = metadata.ToString();
            }
            if (raw.Length <= _maxMetadataBytes)
            {
                try
                {
                    return JToken.FromObject(metadata);
                }
                catch
                {
                    return new JObject { ["raw"] = raw };
                }
            }
            return new JObject
            {
                ["truncated"] = true,
                ["raw"] = raw.Substring(0, _maxMetadataBytes)
            };
        }

        internal void LogEvent(string eventType, string operation, bool success, double durationMs, object metadata)
        {
            lock (_lock)
            {
                _totalRequests += 1;
                _totalLatencyMs += durationMs;
                if (!success)
                {
                    _totalErrors += 1;
                    _lastError = (metadata ?? string.Empty).ToString();
                    if (!string.IsNullOrEmpty(_lastError) && _lastError.Length > _maxErrorLength)
                    {
                        _lastError = _lastError.Substring(0, _maxErrorLength);
                    }
                }
            }

            if (success)
            {
                lock (_rng)
                {
                    if (_rng.NextDouble() > _sampleRateSuccess)
                    {
                        return;
                    }
                }
            }

            var payload = new JObject
            {
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ["type"] = eventType,
                ["operation"] = operation,
                ["duration_ms"] = durationMs,
                ["success"] = success,
                ["metadata"] = SanitizeMetadata(metadata)
            };

            CleanupOldFiles();

            lock (_lock)
            {
                File.AppendAllText(CurrentLogPath(), payload.ToString(Formatting.None) + Environment.NewLine, Encoding.UTF8);
            }
        }

        internal IDictionary<string, object> Summary()
        {
            var uptime = Math.Max((DateTime.UtcNow - ProcessStartUtc).TotalSeconds, 1.0);
            var avgLatency = _totalLatencyMs / Math.Max(_totalRequests, 1);
            var errorRate = _totalErrors / (double)Math.Max(_totalRequests, 1);
            return new Dictionary<string, object>
            {
                ["totalRequests"] = _totalRequests,
                ["errorRate"] = Math.Round(errorRate, 4),
                ["averageLatencyMs"] = Math.Round(avgLatency, 2),
                ["uptimeSeconds"] = Math.Round(uptime, 2),
                ["lastError"] = _lastError,
                ["sampleRateSuccess"] = _sampleRateSuccess,
                ["retentionDays"] = _retentionDays,
                ["logDir"] = _logDir
            };
        }

        internal JArray Tail(int limit)
        {
            var files = Directory.GetFiles(_logDir, $"{_prefix}-*.jsonl").OrderBy(f => f).ToList();
            if (files.Count == 0) return new JArray();
            var latest = files[files.Count - 1];
            var lines = File.ReadAllLines(latest);
            var tail = lines.Skip(Math.Max(0, lines.Length - limit));
            var arr = new JArray();
            foreach (var line in tail)
            {
                try
                {
                    arr.Add(JObject.Parse(line));
                }
                catch
                {
                    // ignore
                }
            }
            return arr;
        }

        internal JArray FindByTraceId(string traceId, int limit)
        {
            if (string.IsNullOrEmpty(traceId)) return new JArray();
            var files = Directory.GetFiles(_logDir, $"{_prefix}-*.jsonl").OrderBy(f => f).ToList();
            if (files.Count == 0) return new JArray();
            var arr = new JArray();
            foreach (var file in files.Skip(Math.Max(0, files.Count - 2)))
            {
                var lines = File.ReadAllLines(file);
                for (var idx = lines.Length - 1; idx >= 0; idx--)
                {
                    try
                    {
                        var obj = JObject.Parse(lines[idx]);
                        var meta = obj["metadata"] as JObject;
                        var metaTrace = meta?["traceId"]?.ToString();
                        if (metaTrace == traceId)
                        {
                            arr.Add(obj);
                            if (arr.Count >= limit)
                            {
                                return new JArray(arr.Reverse());
                            }
                        }
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }
            return new JArray(arr.Reverse());
        }

        private static readonly DateTime ProcessStartUtc = DateTime.UtcNow;
    }
}
