using System;

namespace BlueBrick.Vault
{
    internal class VaultItem
    {
        public string Id { get; set; }
        public string FileName { get; set; }
        public string FullPath { get; set; }
        public string DirectoryPath { get; set; }
        public string Extension { get; set; }
        public string PartNumber { get; set; }
        public string DocumentNumber { get; set; }
        public string Description { get; set; }
        public string Customer { get; set; }
        public string ThumbnailPath { get; set; }
    }

    internal class VaultSearchResult : VaultItem
    {
        public int Score { get; set; }
    }

    internal class VaultMetadataRecord : VaultItem
    {
        public string DrawingPath { get; set; }
        public string ModelPath { get; set; }
        public DateTime UpdatedUtc { get; set; }
    }

    internal class GeneratedArtifactRecord
    {
        public string SourcePath { get; set; }
        public string OutputPath { get; set; }
        public string RelativeOutputPath { get; set; }
        public string ArtifactType { get; set; }
        public string PartNumber { get; set; }
        public string DocumentNumber { get; set; }
        public string Description { get; set; }
        public string Customer { get; set; }
        public DateTime CreatedUtc { get; set; }
    }
}
