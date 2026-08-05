namespace BlueBrick.Vault
{
    internal static class VaultWorkspaceFactory
    {
        private static IVaultWorkspace _current;

        internal static IVaultWorkspace Current =>
            _current ?? (_current = AppIdentity.IsLabBuild
                ? (IVaultWorkspace)new LocalVaultWorkspace()
                : new PdmVaultWorkspace());

        internal static void Reset()
        {
            _current = null;
        }
    }
}
