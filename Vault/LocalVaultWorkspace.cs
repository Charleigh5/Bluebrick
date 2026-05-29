using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace BlueBrick.Vault
{
    internal sealed class LocalVaultWorkspace : IVaultWorkspace
    {
        private const string IndexFileName = "index.json";
        private readonly Agent.AgentConfig _config;
        private readonly string _indexPath;
        private readonly object _sync = new object();

        internal LocalVaultWorkspace()
        {
            _config = Agent.AgentConfig.Load();
            Directory.CreateDirectory(_config.Vault.Root);
            Directory.CreateDirectory(_config.Vault.SourceRoot);
            Directory.CreateDirectory(_config.Vault.GeneratedRoot);
            Directory.CreateDirectory(_config.Vault.ThumbsRoot);
            Directory.CreateDirectory(_config.Vault.MetadataRoot);
            Directory.CreateDirectory(_config.Vault.LogRoot);
            _indexPath = Path.Combine(_config.Vault.MetadataRoot, IndexFileName);
        }

        public IReadOnlyList<VaultSearchResult> Search(string query, int limit)
        {
            var normalized = (query ?? string.Empty).Trim();
            if (normalized.Length == 0) return Array.Empty<VaultSearchResult>();

            var results = LoadIndex()
                .Select(record => new VaultSearchResult
                {
                    Id = record.Id,
                    FileName = record.FileName,
                    FullPath = record.FullPath,
                    DirectoryPath = record.DirectoryPath,
                    Extension = record.Extension,
                    PartNumber = record.PartNumber,
                    DocumentNumber = record.DocumentNumber,
                    Description = record.Description,
                    Customer = record.Customer,
                    ThumbnailPath = record.ThumbnailPath,
                    Score = Score(record, normalized)
                })
                .Where(result => result.Score > 0)
                .OrderByDescending(result => result.Score)
                .ThenBy(result => result.FileName)
                .Take(Math.Max(1, limit))
                .ToList();
            return results;
        }

        public VaultItem ResolveFile(string idOrPath)
        {
            return LoadIndex().FirstOrDefault(x =>
                string.Equals(x.Id, idOrPath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.FullPath, idOrPath, StringComparison.OrdinalIgnoreCase));
        }

        public VaultMetadataRecord GetMetadata(string idOrPath)
        {
            return LoadIndex().FirstOrDefault(x =>
                string.Equals(x.Id, idOrPath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.FullPath, idOrPath, StringComparison.OrdinalIgnoreCase));
        }

        public void UpsertMetadata(VaultMetadataRecord record)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.FullPath)) return;

            lock (_sync)
            {
                var index = LoadIndexInternal();
                var existing = index.FindIndex(x =>
                    string.Equals(x.Id, record.Id, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(x.FullPath, record.FullPath, StringComparison.OrdinalIgnoreCase));
                if (string.IsNullOrWhiteSpace(record.Id))
                {
                    record.Id = BuildId(record.FullPath);
                }
                record.UpdatedUtc = record.UpdatedUtc == default ? DateTime.UtcNow : record.UpdatedUtc;

                if (existing >= 0) index[existing] = record;
                else index.Add(record);
                WriteIndexInternal(index);
            }
        }

        public GeneratedArtifactRecord SaveGeneratedArtifact(GeneratedArtifactRecord artifact)
        {
            if (artifact == null || string.IsNullOrWhiteSpace(artifact.OutputPath) || !File.Exists(artifact.OutputPath))
                return artifact;

            var fileName = Path.GetFileName(artifact.OutputPath);
            var destDir = Path.Combine(_config.Vault.GeneratedRoot, artifact.ArtifactType ?? "misc");
            Directory.CreateDirectory(destDir);
            var destPath = Path.Combine(destDir, fileName);
            if (!string.Equals(artifact.OutputPath, destPath, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(artifact.OutputPath, destPath, true);
            }

            artifact.RelativeOutputPath = GetRelativePath(_config.Vault.Root, destPath);
            artifact.OutputPath = destPath;
            artifact.CreatedUtc = artifact.CreatedUtc == default ? DateTime.UtcNow : artifact.CreatedUtc;

            UpsertMetadata(new VaultMetadataRecord
            {
                Id = BuildId(destPath),
                FileName = Path.GetFileName(destPath),
                FullPath = destPath,
                DirectoryPath = Path.GetDirectoryName(destPath),
                Extension = Path.GetExtension(destPath),
                PartNumber = artifact.PartNumber,
                DocumentNumber = artifact.DocumentNumber,
                Description = artifact.Description,
                Customer = artifact.Customer,
                UpdatedUtc = artifact.CreatedUtc
            });

            return artifact;
        }

        public void ReindexSampleFiles()
        {
            var sampleRoot = _config.Vault.SampleSeedRoot;
            if (!Directory.Exists(sampleRoot))
            {
                Directory.CreateDirectory(sampleRoot);
                WriteIndexInternal(new List<VaultMetadataRecord>());
                return;
            }

            var supported = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".sldprt", ".sldasm", ".slddrw", ".pdf", ".dxf", ".step", ".stp", ".png", ".igs", ".iges"
            };

            var records = Directory.GetFiles(sampleRoot, "*.*", SearchOption.AllDirectories)
                .Where(path => supported.Contains(Path.GetExtension(path)))
                .Select(BuildRecord)
                .OrderBy(record => record.FileName)
                .ToList();

            WriteIndexInternal(records);
        }

        public void Reset()
        {
            if (Directory.Exists(_config.Vault.GeneratedRoot))
                Directory.Delete(_config.Vault.GeneratedRoot, true);
            if (Directory.Exists(_config.Vault.ThumbsRoot))
                Directory.Delete(_config.Vault.ThumbsRoot, true);
            Directory.CreateDirectory(_config.Vault.GeneratedRoot);
            Directory.CreateDirectory(_config.Vault.ThumbsRoot);
            ReindexSampleFiles();
        }

        private List<VaultMetadataRecord> LoadIndex()
        {
            lock (_sync)
            {
                return LoadIndexInternal();
            }
        }

        private List<VaultMetadataRecord> LoadIndexInternal()
        {
            if (!File.Exists(_indexPath)) return new List<VaultMetadataRecord>();
            var json = File.ReadAllText(_indexPath);
            return JsonConvert.DeserializeObject<List<VaultMetadataRecord>>(json) ?? new List<VaultMetadataRecord>();
        }

        private void WriteIndexInternal(List<VaultMetadataRecord> records)
        {
            var json = JsonConvert.SerializeObject(records, Formatting.Indented);
            File.WriteAllText(_indexPath, json);
        }

        private static VaultMetadataRecord BuildRecord(string path)
        {
            var fileName = Path.GetFileName(path);
            var stem = Path.GetFileNameWithoutExtension(path);
            var parts = stem.Split(new[] { '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            var partNumber = parts.Length > 0 ? parts[0].ToUpperInvariant() : stem.ToUpperInvariant();
            var documentNumber = parts.Length > 1 ? parts[1].ToUpperInvariant() : partNumber;
            var description = parts.Length > 2 ? string.Join(" ", parts.Skip(2)).ToUpperInvariant() : stem.ToUpperInvariant();

            return new VaultMetadataRecord
            {
                Id = BuildId(path),
                FileName = fileName,
                FullPath = path,
                DirectoryPath = Path.GetDirectoryName(path),
                Extension = Path.GetExtension(path),
                PartNumber = partNumber,
                DocumentNumber = documentNumber,
                Description = description,
                Customer = "LAB",
                UpdatedUtc = File.GetLastWriteTimeUtc(path),
                DrawingPath = Path.GetExtension(path).Equals(".slddrw", StringComparison.OrdinalIgnoreCase) ? path : null,
                ModelPath = Path.GetExtension(path).Equals(".slddrw", StringComparison.OrdinalIgnoreCase) ? null : path
            };
        }

        private static int Score(VaultMetadataRecord record, string query)
        {
            var score = 0;
            score += MatchScore(record.FileName, query, 40);
            score += MatchScore(record.PartNumber, query, 100);
            score += MatchScore(record.DocumentNumber, query, 80);
            score += MatchScore(record.Description, query, 60);
            score += MatchScore(record.Customer, query, 20);
            return score;
        }

        private static int MatchScore(string source, string query, int baseScore)
        {
            if (string.IsNullOrWhiteSpace(source)) return 0;
            if (source.Equals(query, StringComparison.OrdinalIgnoreCase)) return baseScore * 2;
            return source.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ? baseScore : 0;
        }

        private static string BuildId(string path)
        {
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(path))
                .Replace("=", string.Empty)
                .Replace("/", "_")
                .Replace("+", "-");
        }

        private static string GetRelativePath(string root, string fullPath)
        {
            var rootUri = new Uri(AppendSeparator(root));
            var fileUri = new Uri(fullPath);
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(fileUri).ToString().Replace('/', Path.DirectorySeparatorChar));
        }

        private static string AppendSeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString()) ? path : path + Path.DirectorySeparatorChar;
        }
    }
}
