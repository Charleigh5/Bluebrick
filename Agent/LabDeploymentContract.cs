using System;
using System.IO;

namespace BlueBrick.Agent
{
    internal sealed class LabDeploymentTarget
    {
        internal string RepositoryRoot { get; private set; }
        internal string LabRuntimeRoot { get; private set; }
        internal string ProductionRuntimeRoot { get; private set; }
        internal string LabDllSource { get; private set; }
        internal string LabDllTarget { get; private set; }
        internal string LabConfigSource { get; private set; }
        internal string LabConfigTarget { get; private set; }
        internal string LabRegistryRoot { get; private set; }
        internal string ProductionRegistryRoot { get; private set; }
        internal string LabWebViewUserDataRoot { get; private set; }
        internal string ProductionWebViewUserDataRoot { get; private set; }
        internal string LabTelemetryRoot { get; private set; }
        internal string ProductionTelemetryRoot { get; private set; }
        internal int LabBridgePort { get; private set; }
        internal int ProductionBridgePort { get; private set; }

        internal static LabDeploymentTarget Create(string repositoryRoot, string labRuntimeRoot, string productionRuntimeRoot)
        {
            if (string.IsNullOrWhiteSpace(repositoryRoot)) throw new ArgumentException("repositoryRoot required", nameof(repositoryRoot));
            if (string.IsNullOrWhiteSpace(labRuntimeRoot)) throw new ArgumentException("labRuntimeRoot required", nameof(labRuntimeRoot));
            if (string.IsNullOrWhiteSpace(productionRuntimeRoot)) throw new ArgumentException("productionRuntimeRoot required", nameof(productionRuntimeRoot));

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return new LabDeploymentTarget
            {
                RepositoryRoot = Normalize(repositoryRoot),
                LabRuntimeRoot = Normalize(labRuntimeRoot),
                ProductionRuntimeRoot = Normalize(productionRuntimeRoot),
                LabDllSource = Normalize(Path.Combine(repositoryRoot, "bin", "Lab", "BlueBrick.Lab.dll")),
                LabDllTarget = Normalize(Path.Combine(labRuntimeRoot, "BlueBrick.Lab.dll")),
                LabConfigSource = Normalize(Path.Combine(repositoryRoot, "config", "appsettings.lab.json")),
                LabConfigTarget = Normalize(Path.Combine(labRuntimeRoot, "config", "appsettings.lab.json")),
                LabRegistryRoot = @"HKCU\SOFTWARE\ViraInsight\BlueBrickLab\Settings",
                ProductionRegistryRoot = @"HKCU\SOFTWARE\ViraInsight\BlueBrick\Settings",
                LabWebViewUserDataRoot = Normalize(Path.Combine(localAppData, "VIRA-Digital-Engineer-Lab", "webview2")),
                ProductionWebViewUserDataRoot = Normalize(Path.Combine(localAppData, "VIRA-Digital-Engineer", "webview2")),
                LabTelemetryRoot = Normalize(Path.Combine(localAppData, "VIRA-Digital-Engineer-Lab")),
                ProductionTelemetryRoot = Normalize(Path.Combine(localAppData, "VIRA-Digital-Engineer")),
                LabBridgePort = 17179,
                ProductionBridgePort = 17178
            };
        }

        internal bool IsIsolatedFromProduction()
        {
            return !SamePath(LabRuntimeRoot, ProductionRuntimeRoot) &&
                   !SamePath(LabDllTarget, Path.Combine(ProductionRuntimeRoot, "BlueBrick.dll")) &&
                   !SamePath(LabConfigTarget, Path.Combine(ProductionRuntimeRoot, "config", "appsettings.json")) &&
                   !string.Equals(LabRegistryRoot, ProductionRegistryRoot, StringComparison.OrdinalIgnoreCase) &&
                   LabBridgePort != ProductionBridgePort &&
                   !SamePath(LabWebViewUserDataRoot, ProductionWebViewUserDataRoot) &&
                   !SamePath(LabTelemetryRoot, ProductionTelemetryRoot);
        }

        internal LabRollbackPlan BuildRollbackPlan(string backupRoot)
        {
            if (string.IsNullOrWhiteSpace(backupRoot)) throw new ArgumentException("backupRoot required", nameof(backupRoot));
            return new LabRollbackPlan
            {
                BackupRoot = Normalize(backupRoot),
                LabDllTarget = LabDllTarget,
                LabDllBackup = Normalize(Path.Combine(backupRoot, "BlueBrick.Lab.dll.orig")),
                LabConfigTarget = LabConfigTarget,
                LabConfigBackup = Normalize(Path.Combine(backupRoot, "appsettings.lab.json.orig")),
                LabRegistryBackupRoot = Normalize(Path.Combine(backupRoot, "registry")),
                ProductionRuntimeRoot = ProductionRuntimeRoot,
                ProductionRegistryRoot = ProductionRegistryRoot,
                NeverTouchesProduction = true
            };
        }

        private static string Normalize(string path)
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static bool SamePath(string left, string right)
        {
            return string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);
        }
    }

    internal sealed class LabRollbackPlan
    {
        internal string BackupRoot { get; set; }
        internal string LabDllTarget { get; set; }
        internal string LabDllBackup { get; set; }
        internal string LabConfigTarget { get; set; }
        internal string LabConfigBackup { get; set; }
        internal string LabRegistryBackupRoot { get; set; }
        internal string ProductionRuntimeRoot { get; set; }
        internal string ProductionRegistryRoot { get; set; }
        internal bool NeverTouchesProduction { get; set; }
    }
}
