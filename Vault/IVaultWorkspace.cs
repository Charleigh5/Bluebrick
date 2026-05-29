using System.Collections.Generic;

namespace BlueBrick.Vault
{
    internal interface IVaultWorkspace
    {
        IReadOnlyList<VaultSearchResult> Search(string query, int limit);
        VaultItem ResolveFile(string idOrPath);
        VaultMetadataRecord GetMetadata(string idOrPath);
        void UpsertMetadata(VaultMetadataRecord record);
        GeneratedArtifactRecord SaveGeneratedArtifact(GeneratedArtifactRecord artifact);
        void ReindexSampleFiles();
        void Reset();
    }
}
