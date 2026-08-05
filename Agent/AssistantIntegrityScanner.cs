using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace BlueBrick.Agent
{
    public sealed class AssistantIntegrityScanResult
    {
        public string FilePath { get; set; } = string.Empty;
        public string Sha256Hash { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public bool Tampered { get; set; }
        public List<string> Findings { get; set; } = new List<string>();
    }

    public sealed class AssistantSecretScanFinding
    {
        public string FilePath { get; set; } = string.Empty;
        public int LineNumber { get; set; }
        public string PatternName { get; set; } = string.Empty;
        public string RedactedLine { get; set; } = string.Empty;
        public string Severity { get; set; } = "high";
    }

    public static class AssistantIntegrityScanner
    {
        private static readonly Regex SecretPattern = new Regex(
            @"(?:(?:api[_-]?key|password|secret|token|connectionstring|authorization|access[_-]?token|client[_-]?secret)\s*[""']?\s*[:=]\s*[""']?(?<value>[^\s""',;}\]]+)|(?:\bbearer\s+)(?<value>[A-Za-z0-9._~+/=-]+))",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static string ComputeSha256(string filePath)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = sha.ComputeHash(stream);
            var sb = new StringBuilder(64);
            for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        public static string ComputeSha256Bytes(byte[] bytes)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(bytes);
            var sb = new StringBuilder(64);
            for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        public static string ComputeSha256String(string input)
        {
            var bytes = Encoding.UTF8.GetBytes(input ?? string.Empty);
            return ComputeSha256Bytes(bytes);
        }

        public static AssistantIntegrityScanResult ScanFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return new AssistantIntegrityScanResult { FilePath = filePath, Tampered = true, Findings = new List<string> { "file does not exist" } };
            }

            var info = new FileInfo(filePath);
            var hash = ComputeSha256(filePath);

            return new AssistantIntegrityScanResult
            {
                FilePath = filePath,
                Sha256Hash = hash,
                SizeBytes = info.Length,
                Tampered = false,
                Findings = new List<string>()
            };
        }

        public static IReadOnlyList<AssistantSecretScanFinding> ScanForSecrets(string filePath)
        {
            if (!File.Exists(filePath)) return Array.Empty<AssistantSecretScanFinding>();

            var findings = new List<AssistantSecretScanFinding>();
            var lines = File.ReadAllLines(filePath);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var matches = SecretPattern.Matches(line);
                foreach (Match m in matches)
                {
                    var value = m.Groups["value"]?.Value ?? string.Empty;
                    findings.Add(new AssistantSecretScanFinding
                    {
                        FilePath = filePath,
                        LineNumber = i + 1,
                        PatternName = value.Length > 0 ? "secret_value" : "secret_key",
                        RedactedLine = Truncate(RedactFindings(line), 200),
                        Severity = "high"
                    });
                }
            }
            return findings;
        }

        public static IReadOnlyList<AssistantSecretScanFinding> ScanDirectory(string directoryPath)
        {
            if (!Directory.Exists(directoryPath)) return Array.Empty<AssistantSecretScanFinding>();

            var allFindings = new List<AssistantSecretScanFinding>();
            var extensions = new[] { ".cs", ".json", ".xml", ".config", ".md", ".txt", ".yml", ".yaml", ".props", ".targets", ".bat", ".ps1", ".env", ".toml" };

            foreach (var file in Directory.EnumerateFiles(directoryPath, "*.*", SearchOption.TopDirectoryOnly))
            {
                var ext = Path.GetExtension(file);
                if (extensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                {
                    allFindings.AddRange(ScanForSecrets(file));
                }
            }
            return allFindings;
        }

        public static bool HasSecrets(IReadOnlyList<AssistantSecretScanFinding> findings)
        {
            return findings != null && findings.Count > 0;
        }

        public static string RedactFindings(string input)
        {
            if (string.IsNullOrEmpty(input)) return input ?? string.Empty;
            return SecretPattern.Replace(input, m =>
            {
                var valueGroup = m.Groups["value"];
                if (!valueGroup.Success || valueGroup.Index < m.Index || valueGroup.Length == 0)
                {
                    return m.Value;
                }

                var prefix = m.Value.Substring(0, valueGroup.Index - m.Index);
                return prefix + "REDACTED";
            });
        }

        private static string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength) return text ?? string.Empty;
            return text.Substring(0, maxLength) + "...";
        }
    }
}
