using System;
using System.IO;

namespace BlueBrick
{
    internal static class AppIdentity
    {
#if LAB_BUILD
        internal const bool IsLabBuild = true;
        internal const string ProductName = "BlueBrick Lab";
        internal const string AddinDescription = "ViraInsight SolidWorks lab add-in";
        internal const string RegistryRoot = @"HKEY_CURRENT_USER\SOFTWARE\ViraInsight\BlueBrickLab\Settings";
        internal const string TelemetryFolderName = "VIRA-Digital-Engineer-Lab";
        internal const int BridgePort = 17179;
#else
        internal const bool IsLabBuild = false;
        internal const string ProductName = "BlueBrick";
        internal const string AddinDescription = "ViraInsight SolidWorks add-in";
        internal const string RegistryRoot = @"HKEY_CURRENT_USER\SOFTWARE\ViraInsight\BlueBrick\Settings";
        internal const string TelemetryFolderName = "VIRA-Digital-Engineer";
        internal const int BridgePort = 17178;
#endif

        internal static string DefaultWorkingFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                ProductName, "Working");

        internal static string LocalVaultRoot =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                ProductName + " Vault");

        internal static string AssistantHistoryRoot =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                TelemetryFolderName, "assistant");

        internal static string WebViewUserDataRoot =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                TelemetryFolderName, "webview2");

        internal static string ConfigPath(string baseDirectory)
        {
            return Path.Combine(baseDirectory, "config", IsLabBuild ? "appsettings.lab.json" : "appsettings.json");
        }
    }
}
