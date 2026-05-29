using System;
using System.Collections.Generic;

namespace BlueBrick.Vault
{
    internal sealed class PdmVaultWorkspace : IVaultWorkspace
    {
        public IReadOnlyList<VaultSearchResult> Search(string query, int limit)
        {
            throw new NotSupportedException("PDM workspace search is handled by the production PDM flow.");
        }

        public VaultItem ResolveFile(string idOrPath)
        {
            throw new NotSupportedException("PDM workspace resolution is handled by the production PDM flow.");
        }

        public VaultMetadataRecord GetMetadata(string idOrPath)
        {
            return null;
        }

        public void UpsertMetadata(VaultMetadataRecord record)
        {
        }

        public GeneratedArtifactRecord SaveGeneratedArtifact(GeneratedArtifactRecord artifact)
        {
            return artifact;
        }

        public void ReindexSampleFiles()
        {
        }

        public void Reset()
        {
        }
    }
}
