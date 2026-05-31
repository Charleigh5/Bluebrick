using System;
using System.IO;
using Newtonsoft.Json;

namespace BlueBrick.Agent
{
    internal class AgentConfig
    {
        internal PdmConfig Pdm { get; set; }
        internal TemplateConfig Templates { get; set; }
        internal AgentSettings Agent { get; set; }
        internal UISettings UI { get; set; }
        internal MemorySettings Memory { get; set; }
        internal ScriptSettings Scripts { get; set; }
        internal BaselineSettings Baselines { get; set; }
        internal VaultSettings Vault { get; set; }
        internal AssistantSettings Assistant { get; set; }
        internal AssistantToolSettings AssistantTools { get; set; }
        internal RelaySettings Relay { get; set; }

        internal static AgentConfig Load()
        {
            var baseDir = Path.GetDirectoryName(typeof(AgentConfig).Assembly.Location)
                ?? AppDomain.CurrentDomain.BaseDirectory;
            var root = FindRepoRoot(baseDir);
            var cfgPath = AppIdentity.ConfigPath(root);
            if (!File.Exists(cfgPath)) return CreateDefault(root);

            var json = File.ReadAllText(cfgPath);
            var config = JsonConvert.DeserializeObject<AgentConfig>(json) ?? CreateDefault(root);
            config.ApplyDefaults(root);
            return config;
        }

        internal static string FindRepoRoot(string startPath)
        {
            var current = new DirectoryInfo(startPath);
            for (var i = 0; i < 8; i++)
            {
                var candidate = Path.Combine(
                    current.FullName,
                    "config",
                    AppIdentity.IsLabBuild ? "appsettings.lab.json" : "appsettings.json");
                if (File.Exists(candidate))
                {
                    return current.FullName;
                }
                current = current.Parent;
                if (current == null) break;
            }
            return startPath;
        }

        private static AgentConfig CreateDefault(string root)
        {
            var config = new AgentConfig();
            config.ApplyDefaults(root);
            return config;
        }

        private void ApplyDefaults(string root)
        {
            Pdm ??= new PdmConfig();
            Templates ??= new TemplateConfig { Defaults = new TemplateDefaults() };
            Agent ??= new AgentSettings();
            UI ??= new UISettings { Fonts = new FontSettings() };
            Memory ??= new MemorySettings();
            Scripts ??= new ScriptSettings();
            Baselines ??= new BaselineSettings();
            Vault ??= new VaultSettings();
            Assistant ??= new AssistantSettings();
            AssistantTools ??= new AssistantToolSettings();
            Relay ??= new RelaySettings();

            Agent.BridgePort = Agent.BridgePort == 0 ? AppIdentity.BridgePort : Agent.BridgePort;
            Agent.OverlayColor = DefaultIfEmpty(Agent.OverlayColor, "#D9FF5A");

            Memory.LocalPath = DefaultIfEmpty(Memory.LocalPath, Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppIdentity.TelemetryFolderName, "memory"));
            Memory.PdmSyncPath = DefaultIfEmpty(Memory.PdmSyncPath, Path.Combine(root, "memory", "pdm-sync"));

            Scripts.ManifestPath = DefaultIfEmpty(Scripts.ManifestPath, Path.Combine(root, "scripts", "manifest.json"));
            Scripts.QaRoot = DefaultIfEmpty(Scripts.QaRoot, Path.Combine(root, "reports", "qa"));
            Scripts.ReportRoot = DefaultIfEmpty(Scripts.ReportRoot, Path.Combine(root, "reports"));

            Baselines.Root = DefaultIfEmpty(Baselines.Root, Path.Combine(root, "baselines"));
            Vault.Root = DefaultIfEmpty(Vault.Root, AppIdentity.LocalVaultRoot);
            Vault.SampleSeedRoot = DefaultIfEmpty(Vault.SampleSeedRoot, Path.Combine(root, "samples"));
            Vault.GeneratedRoot = DefaultIfEmpty(Vault.GeneratedRoot, Path.Combine(Vault.Root, "generated"));
            Vault.ThumbsRoot = DefaultIfEmpty(Vault.ThumbsRoot, Path.Combine(Vault.Root, "thumbs"));
            Vault.MetadataRoot = DefaultIfEmpty(Vault.MetadataRoot, Path.Combine(Vault.Root, "db"));
            Vault.LogRoot = DefaultIfEmpty(Vault.LogRoot, Path.Combine(Vault.Root, "logs"));
            Vault.SourceRoot = DefaultIfEmpty(Vault.SourceRoot, Path.Combine(Vault.Root, "source"));

            Assistant.Model = DefaultIfEmpty(Assistant.Model, "meta/llama-3.1-70b-instruct");
            Assistant.ApiBaseUrl = DefaultIfEmpty(Assistant.ApiBaseUrl, "https://integrate.api.nvidia.com/v1");
            Assistant.ModelProfiles = EnsureModelProfiles(Assistant);
            Assistant.Mode = DefaultIfEmpty(Assistant.Mode, string.Empty);
            Assistant.SystemPrompt = DefaultIfEmpty(Assistant.SystemPrompt,
                "You are the BlueBrick Lab assistant. Help users troubleshoot SolidWorks workflows, drawings, generated outputs, and the BlueBrick interface using text and screenshots. Be concise, practical, and grounded in what is visible.");
            Assistant.Detail = DefaultIfEmpty(Assistant.Detail, "low");
            Assistant.ConnectionTestPrompt = DefaultIfEmpty(Assistant.ConnectionTestPrompt,
                "Reply with the word READY and one short sentence confirming the BlueBrick Lab assistant connection is working.");
            Assistant.MaxImageDimension = Assistant.MaxImageDimension <= 0 ? 1600 : Assistant.MaxImageDimension;
            Assistant.JpegQuality = Assistant.JpegQuality <= 0 ? 75 : Assistant.JpegQuality;
            if (AppIdentity.IsLabBuild && !Assistant.EnableUploads)
            {
                Assistant.EnableUploads = true;
            }
            if (AppIdentity.IsLabBuild)
            {
                Assistant.RequireExplicitUploadConsent = true;
            }
            Assistant.MaxHistory = Assistant.MaxHistory <= 0 ? 20 : Assistant.MaxHistory;
        Assistant.MaxTotalAttachmentBytes = Assistant.MaxTotalAttachmentBytes <= 0 ? 10 * 1024 * 1024 : Assistant.MaxTotalAttachmentBytes;

            AssistantTools.PdmMaxResults = AssistantTools.PdmMaxResults <= 0 ? 25 : Math.Min(AssistantTools.PdmMaxResults, 50);
            AssistantTools.EpicorMaxResults = AssistantTools.EpicorMaxResults <= 0 ? 25 : Math.Min(AssistantTools.EpicorMaxResults, 50);
            AssistantTools.EpicorConnectionStringEnvironmentVariable = DefaultIfEmpty(
                AssistantTools.EpicorConnectionStringEnvironmentVariable,
                "BLUEBRICK_EPICOR_CONNECTION_STRING");

            Relay.Enabled = AppIdentity.IsLabBuild || Relay.Enabled;
            Relay.BaseUrl = DefaultIfEmpty(Relay.BaseUrl, string.Empty);
            Relay.ChatWorkspaceUrl = DefaultIfEmpty(Relay.ChatWorkspaceUrl, "https://chatgpt.com/");
            Relay.DeviceId = DefaultIfEmpty(Relay.DeviceId, Environment.MachineName + "-bluebrick-lab");
            Relay.DeviceName = DefaultIfEmpty(Relay.DeviceName, AppIdentity.ProductName + " on " + Environment.MachineName);
            Relay.HandoffPath = DefaultIfEmpty(Relay.HandoffPath, "chatgpt/handoff");
            Relay.RegistrationToken = DefaultIfEmpty(Relay.RegistrationToken, string.Empty);
            Relay.HeartbeatIntervalSeconds = Relay.HeartbeatIntervalSeconds <= 0 ? 30 : Relay.HeartbeatIntervalSeconds;
        }

        private static string DefaultIfEmpty(string current, string fallback)
        {
            return string.IsNullOrWhiteSpace(current) ? fallback : current;
        }

        private static AssistantModelProfile[] EnsureModelProfiles(AssistantSettings assistant)
        {
            if (assistant.ModelProfiles != null && assistant.ModelProfiles.Length > 0)
            {
                return assistant.ModelProfiles;
            }

            return new[]
            {
                new AssistantModelProfile
                {
                    Id = "nvidia-llama-3-1-70b",
                    Name = "NVIDIA Llama 3.1 70B",
                    Provider = "NVIDIA",
                    ApiBaseUrl = "https://integrate.api.nvidia.com/v1",
                    Model = "meta/llama-3.1-70b-instruct",
                    KeyEnvironmentVariable = "NVIDIA_API_KEY",
                    IsDefault = true,
                    ProviderKind = "nvidia",
                    BaseUrlAlias = "NVIDIA",
                    SupportsVision = false,
                    SupportsStreaming = false,
                    SupportsTools = false,
                    SupportsJsonMode = false,
                    SecretRef = "runtime-only",
                    Enabled = true,
                    Source = "bluebrick"
                },
                new AssistantModelProfile
                {
                    Id = "openai-gpt-4-1-mini",
                    Name = "OpenAI GPT-4.1 Mini",
                    Provider = "OpenAI",
                    ApiBaseUrl = "https://api.openai.com/v1",
                    Model = "gpt-4.1-mini",
                    KeyEnvironmentVariable = "OPENAI_API_KEY",
                    ProviderKind = "openai",
                    BaseUrlAlias = "OPENAI",
                SupportsVision = true,
                SupportsStreaming = true,
                SupportsTools = true,
                    SupportsJsonMode = true,
                    SecretRef = "runtime-only",
                    Enabled = true,
                    Source = "bluebrick"
                },
                new AssistantModelProfile
                {
                    Id = "aionui-default",
                    Name = "AionUI Default",
                    Provider = "AionUI",
                    ApiBaseUrl = assistant.ApiBaseUrl,
                    Model = assistant.Model,
                    KeyEnvironmentVariable = "OPENAI_API_KEY",
                    ProviderKind = "aionui_broker",
                    BaseUrlAlias = "AIONUI_BROKER",
                    SupportsVision = false,
                    SupportsStreaming = false,
                    SupportsTools = false,
                    SupportsJsonMode = false,
                    SecretRef = "runtime-only",
                    Enabled = true,
                    Source = "bluebrick"
                }
            };
        }
    }

    internal class PdmConfig
    {
        internal string VaultRoot { get; set; }
        internal string VaultName { get; set; }
        internal string EngineeringDbRoot { get; set; }
        internal string[] ProjectFolders { get; set; }
    }

    internal class TemplateConfig
    {
        internal string Root { get; set; }
        internal TemplateDefaults Defaults { get; set; }
        internal string[] MaterialFolders { get; set; }
    }

    internal class TemplateDefaults
    {
        internal string Assembly { get; set; }
        internal string Drawing { get; set; }
        internal string SheetFormat { get; set; }
        internal string Part { get; set; }
    }

    internal class AgentSettings
    {
        internal int BridgePort { get; set; }
        internal string OverlayColor { get; set; }
    }

    internal class MemorySettings
    {
        internal string LocalPath { get; set; }
        internal string PdmSyncPath { get; set; }
    }

    internal class ScriptSettings
    {
        internal string ManifestPath { get; set; }
        internal string QaRoot { get; set; }
        internal string ReportRoot { get; set; }
    }

    internal class BaselineSettings
    {
        internal string Root { get; set; }
    }

    internal class UISettings
    {
        internal FontSettings Fonts { get; set; }
    }

    internal class FontSettings
    {
        internal string SpaceGroteskPath { get; set; }
        internal string IbmPlexSansPath { get; set; }
        internal string FontsPath { get; set; }
    }

    internal class VaultSettings
    {
        internal string Root { get; set; }
        internal string SourceRoot { get; set; }
        internal string GeneratedRoot { get; set; }
        internal string ThumbsRoot { get; set; }
        internal string MetadataRoot { get; set; }
        internal string LogRoot { get; set; }
        internal string SampleSeedRoot { get; set; }
    }

    internal class AssistantSettings
    {
        internal string ApiBaseUrl { get; set; }
        internal string Model { get; set; }
        internal string Mode { get; set; }
        internal string SystemPrompt { get; set; }
        internal string Detail { get; set; }
        internal bool EnableUploads { get; set; }
        internal int MaxImageDimension { get; set; }
        internal int JpegQuality { get; set; }
        internal string ConnectionTestPrompt { get; set; }
        internal bool RequireExplicitUploadConsent { get; set; }
        internal int MaxHistory { get; set; }
        internal long MaxTotalAttachmentBytes { get; set; }
        internal AssistantModelProfile[] ModelProfiles { get; set; }
    }

    internal class AssistantToolSettings
    {
        internal bool EnablePdmSearch { get; set; }
        internal bool EnableEpicorSearch { get; set; }
        internal int PdmMaxResults { get; set; }
        internal int EpicorMaxResults { get; set; }
        internal string EpicorConnectionStringEnvironmentVariable { get; set; }
    }

    internal class RelaySettings
    {
        internal bool Enabled { get; set; }
        internal string BaseUrl { get; set; }
        internal string ChatWorkspaceUrl { get; set; }
        internal string DeviceId { get; set; }
        internal string DeviceName { get; set; }
        internal string RegistrationToken { get; set; }
        internal string HandoffPath { get; set; }
        internal int HeartbeatIntervalSeconds { get; set; }
    }
}
