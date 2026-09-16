using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BlueBrick.Agent
{
    internal class AgentConfig
    {
        internal const string SupportedSchemaVersion = "1";

        [JsonProperty("ConfigSchemaVersion")]
        internal string ConfigSchemaVersion { get; set; }
        [JsonProperty("Pdm")]
        internal PdmConfig Pdm { get; set; }
        [JsonProperty("Templates")]
        internal TemplateConfig Templates { get; set; }
        [JsonProperty("Agent")]
        internal AgentSettings Agent { get; set; }
        [JsonProperty("UI")]
        internal UISettings UI { get; set; }
        [JsonProperty("Memory")]
        internal MemorySettings Memory { get; set; }
        [JsonProperty("Scripts")]
        internal ScriptSettings Scripts { get; set; }
        [JsonProperty("Baselines")]
        internal BaselineSettings Baselines { get; set; }
        [JsonProperty("Vault")]
        internal VaultSettings Vault { get; set; }
        [JsonProperty("Assistant")]
        internal AssistantSettings Assistant { get; set; }
        [JsonProperty("AssistantTools")]
        internal AssistantToolSettings AssistantTools { get; set; }
        [JsonProperty("Relay")]
        internal RelaySettings Relay { get; set; }

        [JsonIgnore]
        internal AgentConfigurationDiagnostics ConfigurationDiagnostics { get; private set; }

        [JsonIgnore]
        internal SharedAI.Catalog SharedAiCatalog { get; private set; }

        internal static AgentConfig Load()
        {
            var baseDir = Path.GetDirectoryName(typeof(AgentConfig).Assembly.Location)
                ?? AppDomain.CurrentDomain.BaseDirectory;
            var root = FindRepoRoot(baseDir);
            return LoadFrom(root);
        }

        internal static AgentConfig LoadFrom(string root)
        {
            var cfgPath = AppIdentity.ConfigPath(root);
            if (!File.Exists(cfgPath))
            {
                var missing = CreateDefault(root);
                missing.ConfigurationDiagnostics = AgentConfigurationDiagnostics.Missing(cfgPath, missing);
                return missing;
            }

            var json = File.ReadAllText(cfgPath);
            var hash = ComputeSha256(json);
            JObject source;
            try
            {
                source = JObject.Parse(json);
            }
            catch (JsonException ex)
            {
                throw new AgentConfigurationException("CONFIG_PRESENT_INVALID", cfgPath, ex);
            }

            var schemaToken = source["ConfigSchemaVersion"];
            var schemaVersion = schemaToken == null ? SupportedSchemaVersion : (string)schemaToken;
            if (!string.Equals(schemaVersion, SupportedSchemaVersion, StringComparison.Ordinal))
            {
                throw new AgentConfigurationException(
                    "CONFIG_SCHEMA_UNSUPPORTED",
                    cfgPath,
                    new InvalidDataException("Unsupported ConfigSchemaVersion '" + (schemaVersion ?? "null") + "'."));
            }

            ValidateCriticalTypes(source, cfgPath);

            AgentConfig config;
            try
            {
                config = JsonConvert.DeserializeObject<AgentConfig>(json);
            }
            catch (JsonException ex)
            {
                throw new AgentConfigurationException("CONFIG_PRESENT_INVALID", cfgPath, ex);
            }
            if (config == null)
                throw new AgentConfigurationException("CONFIG_PRESENT_INVALID", cfgPath, new InvalidDataException("Configuration root was null."));

            config.ConfigSchemaVersion = schemaVersion;
            config.ApplyDefaults(root);
            config.ConfigurationDiagnostics = AgentConfigurationDiagnostics.Present(
                cfgPath,
                hash,
                config,
                source["Assistant"] as JObject,
                schemaToken == null);
            return config;
        }

        private static void ValidateCriticalTypes(JObject source, string cfgPath)
        {
            var assistant = source["Assistant"];
            if (assistant != null && assistant.Type != JTokenType.Object)
                throw new AgentConfigurationException("CONFIG_PRESENT_INVALID", cfgPath, new InvalidDataException("Assistant must be an object."));

            var assistantObject = assistant as JObject;
            if (assistantObject == null) return;
            foreach (var propertyName in new[] { "UseReactWebView", "EnableUploads", "RequireExplicitUploadConsent" })
            {
                var value = assistantObject[propertyName];
                if (value != null && value.Type != JTokenType.Boolean)
                    throw new AgentConfigurationException("CONFIG_PRESENT_INVALID", cfgPath, new InvalidDataException("Assistant." + propertyName + " must be boolean."));
            }
        }

        private static string ComputeSha256(string value)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
                return BitConverter.ToString(bytes).Replace("-", string.Empty);
            }
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

        internal static AgentConfig CreateInvalidFallback(string configPath, AgentConfigurationException ex)
        {
            var root = TryGetRootFromConfigPath(configPath)
                ?? Path.GetDirectoryName(typeof(AgentConfig).Assembly.Location)
                ?? AppDomain.CurrentDomain.BaseDirectory;
            var config = CreateDefault(root);
            config.ConfigurationDiagnostics = AgentConfigurationDiagnostics.Invalid(
                string.IsNullOrWhiteSpace(configPath) ? AppIdentity.ConfigPath(root) : configPath,
                config,
                ex == null ? "CONFIG_PRESENT_INVALID" : ex.Status);
            return config;
        }

        private static string TryGetRootFromConfigPath(string configPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(configPath)) return null;
                var dir = Path.GetDirectoryName(configPath);
                if (string.IsNullOrWhiteSpace(dir)) return null;
                if (string.Equals(Path.GetFileName(dir), "config", StringComparison.OrdinalIgnoreCase))
                {
                    var parent = Path.GetDirectoryName(dir);
                    if (!string.IsNullOrWhiteSpace(parent)) return parent;
                }
                return dir;
            }
            catch
            {
                return null;
            }
        }

        private void ApplyDefaults(string root)
        {
            ConfigSchemaVersion = DefaultIfEmpty(ConfigSchemaVersion, SupportedSchemaVersion);
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

            Templates.Defaults ??= new TemplateDefaults();
            UI.Fonts ??= new FontSettings();

            Agent.BridgePort = ResolveBridgePort(Agent.BridgePort, AppIdentity.BridgePort);
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
            if (Assistant.SharedAiModelIds != null)
            {
                SharedAiCatalog = SharedAI.Catalog.Load(Path.Combine(root, "SharedAI", "catalog"));
                Assistant.ModelProfiles = SharedAiProfiles.Adapt(SharedAiCatalog, Assistant.SharedAiModelIds);
                var primary = Array.Find(Assistant.ModelProfiles, p => p.IsDefault);
                if (primary == null) throw new InvalidDataException("SHAREDAI_DEFAULT_REFERENCE_REQUIRED");
                Assistant.Model = primary.Model;
                Assistant.ApiBaseUrl = primary.ApiBaseUrl;
            }
            Assistant.Mode = DefaultIfEmpty(Assistant.Mode, string.Empty);
            Assistant.SystemPrompt = DefaultIfEmpty(Assistant.SystemPrompt,
                "You are the BlueBrick Lab assistant. Help users troubleshoot SolidWorks workflows, drawings, generated outputs, and the BlueBrick interface using text and screenshots. Be concise, practical, and grounded in what is visible.");
            Assistant.Detail = DefaultIfEmpty(Assistant.Detail, "low");
            Assistant.ConnectionTestPrompt = DefaultIfEmpty(Assistant.ConnectionTestPrompt,
                "Reply with the word READY and one short sentence confirming the BlueBrick Lab assistant connection is working.");
            Assistant.MaxImageDimension = Assistant.MaxImageDimension <= 0 ? 1600 : Assistant.MaxImageDimension;
            Assistant.JpegQuality = Assistant.JpegQuality <= 0 ? 75 : Assistant.JpegQuality;
            if (AppIdentity.IsLabBuild)
            {
                Assistant.RequireExplicitUploadConsent = true;
            }
            Assistant.MaxHistory = Assistant.MaxHistory <= 0 ? 20 : Assistant.MaxHistory;
        Assistant.MaxTotalAttachmentBytes = Assistant.MaxTotalAttachmentBytes <= 0 ? 10 * 1024 * 1024 : Assistant.MaxTotalAttachmentBytes;
            Assistant.ReactDevServerUrl = DefaultIfEmpty(Assistant.ReactDevServerUrl, string.Empty);

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

        internal static int ResolveBridgePort(int configuredPort, int fallbackPort)
        {
            return configuredPort >= 1 && configuredPort <= 65535 ? configuredPort : fallbackPort;
        }

        private static string DefaultIfEmpty(string current, string fallback)
        {
            return string.IsNullOrWhiteSpace(current) ? fallback : current;
        }

        private static AssistantModelProfile[] EnsureModelProfiles(AssistantSettings assistant)
        {
            if (assistant.ModelProfiles != null)
            {
                return assistant.ModelProfiles;
            }

            // A legacy configured endpoint is one profile, never a discovered AionUI catalog.
            return new[] { new AssistantModelProfile
            {
                Id = "configured-default", Name = assistant.Model,
                Provider = "Configured endpoint", ProviderKind = "openai_compatible",
                ApiBaseUrl = assistant.ApiBaseUrl, Model = assistant.Model,
                KeyEnvironmentVariable = "OPENAI_API_KEY", IsDefault = true,
                Enabled = true, Source = "configured_legacy_default"
            } };
        }
    }

    internal class PdmConfig
    {
        [JsonProperty("VaultRoot")]
        internal string VaultRoot { get; set; }
        [JsonProperty("VaultName")]
        internal string VaultName { get; set; }
        [JsonProperty("AllowAssistantReadOnlySearch")]
        internal bool AllowAssistantReadOnlySearch { get; set; }
        [JsonProperty("EngineeringDbRoot")]
        internal string EngineeringDbRoot { get; set; }
        [JsonProperty("ProjectFolders")]
        internal string[] ProjectFolders { get; set; }
    }

    internal class TemplateConfig
    {
        [JsonProperty("Root")]
        internal string Root { get; set; }
        [JsonProperty("Defaults")]
        internal TemplateDefaults Defaults { get; set; }
        [JsonProperty("MaterialFolders")]
        internal string[] MaterialFolders { get; set; }
    }

    internal class TemplateDefaults
    {
        [JsonProperty("Assembly")]
        internal string Assembly { get; set; }
        [JsonProperty("Drawing")]
        internal string Drawing { get; set; }
        [JsonProperty("SheetFormat")]
        internal string SheetFormat { get; set; }
        [JsonProperty("Part")]
        internal string Part { get; set; }
    }

    internal class AgentSettings
    {
        [JsonProperty("BridgePort")]
        internal int BridgePort { get; set; }
        [JsonProperty("OverlayColor")]
        internal string OverlayColor { get; set; }
    }

    internal class MemorySettings
    {
        [JsonProperty("LocalPath")]
        internal string LocalPath { get; set; }
        [JsonProperty("PdmSyncPath")]
        internal string PdmSyncPath { get; set; }
    }

    internal class ScriptSettings
    {
        [JsonProperty("ManifestPath")]
        internal string ManifestPath { get; set; }
        [JsonProperty("QaRoot")]
        internal string QaRoot { get; set; }
        [JsonProperty("ReportRoot")]
        internal string ReportRoot { get; set; }
    }

    internal class BaselineSettings
    {
        [JsonProperty("Root")]
        internal string Root { get; set; }
    }

    internal class UISettings
    {
        [JsonProperty("Fonts")]
        internal FontSettings Fonts { get; set; }
    }

    internal class FontSettings
    {
        [JsonProperty("SpaceGroteskPath")]
        internal string SpaceGroteskPath { get; set; }
        [JsonProperty("IbmPlexSansPath")]
        internal string IbmPlexSansPath { get; set; }
        [JsonProperty("FontsPath")]
        internal string FontsPath { get; set; }
    }

    internal class VaultSettings
    {
        [JsonProperty("Root")]
        internal string Root { get; set; }
        [JsonProperty("SourceRoot")]
        internal string SourceRoot { get; set; }
        [JsonProperty("GeneratedRoot")]
        internal string GeneratedRoot { get; set; }
        [JsonProperty("ThumbsRoot")]
        internal string ThumbsRoot { get; set; }
        [JsonProperty("MetadataRoot")]
        internal string MetadataRoot { get; set; }
        [JsonProperty("LogRoot")]
        internal string LogRoot { get; set; }
        [JsonProperty("SampleSeedRoot")]
        internal string SampleSeedRoot { get; set; }
    }

    internal class AssistantSettings
    {
        [JsonProperty("Screenshots")]
        internal AssistantScreenshotSettings Screenshots { get; set; } = new AssistantScreenshotSettings();
        [JsonProperty("PreferredVisionModelId")]
        internal string PreferredVisionModelId { get; set; }
        [JsonProperty("VisionFallbackModelId")]
        internal string VisionFallbackModelId { get; set; }
        [JsonProperty("ApiBaseUrl")]
        internal string ApiBaseUrl { get; set; }
        [JsonProperty("Model")]
        internal string Model { get; set; }
        [JsonProperty("Mode")]
        internal string Mode { get; set; }
        [JsonProperty("SystemPrompt")]
        internal string SystemPrompt { get; set; }
        [JsonProperty("Detail")]
        internal string Detail { get; set; }
        [JsonProperty("EnableUploads")]
        internal bool EnableUploads { get; set; }
        [JsonProperty("MaxImageDimension")]
        internal int MaxImageDimension { get; set; }
        [JsonProperty("JpegQuality")]
        internal int JpegQuality { get; set; }
        [JsonProperty("ConnectionTestPrompt")]
        internal string ConnectionTestPrompt { get; set; }
        [JsonProperty("RequireExplicitUploadConsent")]
        internal bool RequireExplicitUploadConsent { get; set; }
        [JsonProperty("MaxHistory")]
        internal int MaxHistory { get; set; }
        [JsonProperty("MaxTotalAttachmentBytes")]
        internal long MaxTotalAttachmentBytes { get; set; }
        [JsonProperty("ModelProfiles")]
        internal AssistantModelProfile[] ModelProfiles { get; set; }

        [JsonProperty("SharedAiModelIds")]
        internal string[] SharedAiModelIds { get; set; }
        [JsonProperty("UseReactWebView")]
        internal bool UseReactWebView { get; set; }
        [JsonProperty("EnableReactDevServer")]
        internal bool EnableReactDevServer { get; set; }
        [JsonProperty("ReactDevServerUrl")]
        internal string ReactDevServerUrl { get; set; }
    }

    internal class AssistantScreenshotSettings
    {
        [JsonProperty("AutoAttachToChat")]
        internal bool AutoAttachToChat { get; set; }
        [JsonProperty("AutoApproveLocalCaptureForContext")]
        internal bool AutoApproveLocalCaptureForContext { get; set; }
    }

    internal sealed class AgentConfigurationDiagnostics
    {
        internal string ConfigPath { get; private set; }
        internal string ConfigHash { get; private set; }
        internal string ConfigSchemaVersion { get; private set; }
        internal string ConfigurationLoadStatus { get; private set; }
        internal string AssistantValueSource { get; private set; }
        internal int BridgePort { get; private set; }
        internal bool UseReactWebView { get; private set; }
        internal bool EnableUploads { get; private set; }
        internal bool RequireExplicitUploadConsent { get; private set; }
        internal string AssistantModel { get; private set; }

        internal static AgentConfigurationDiagnostics Missing(string path, AgentConfig config)
        {
            return Create(path, null, config, "CONFIG_MISSING", "CONFIG_VALUE_DEFAULTED");
        }

        internal static AgentConfigurationDiagnostics Invalid(string path, AgentConfig config, string status)
        {
            return Create(path, null, config, status, "CONFIG_VALUE_DEFAULTED");
        }

        internal static AgentConfigurationDiagnostics Present(string path, string hash, AgentConfig config, JObject assistant, bool legacySchema)
        {
            var criticalExplicit = assistant != null &&
                assistant["UseReactWebView"] != null &&
                assistant["EnableUploads"] != null &&
                assistant["RequireExplicitUploadConsent"] != null &&
                assistant["Model"] != null;
            return Create(
                path,
                hash,
                config,
                criticalExplicit && !legacySchema ? "CONFIG_PRESENT_VALID" : "CONFIG_VALUE_DEFAULTED",
                criticalExplicit ? "CONFIG_VALUE_EXPLICIT" : "CONFIG_VALUE_DEFAULTED");
        }

        private static AgentConfigurationDiagnostics Create(string path, string hash, AgentConfig config, string status, string valueSource)
        {
            return new AgentConfigurationDiagnostics
            {
                ConfigPath = path,
                ConfigHash = hash,
                ConfigSchemaVersion = config.ConfigSchemaVersion,
                ConfigurationLoadStatus = status,
                AssistantValueSource = valueSource,
                BridgePort = config.Agent.BridgePort,
                UseReactWebView = config.Assistant.UseReactWebView,
                EnableUploads = config.Assistant.EnableUploads,
                RequireExplicitUploadConsent = config.Assistant.RequireExplicitUploadConsent,
                AssistantModel = config.Assistant.Model
            };
        }
    }

    internal sealed class AgentConfigurationException : Exception
    {
        internal string Status { get; private set; }
        internal string ConfigPath { get; private set; }

        internal AgentConfigurationException(string status, string configPath, Exception innerException)
            : base(status + ": " + (innerException == null ? "configuration load failed" : innerException.Message), innerException)
        {
            Status = status;
            ConfigPath = configPath;
        }
    }

    internal class AssistantToolSettings
    {
        [JsonProperty("EnablePdmSearch")]
        internal bool EnablePdmSearch { get; set; }
        [JsonProperty("EnableEpicorSearch")]
        internal bool EnableEpicorSearch { get; set; }
        [JsonProperty("PdmMaxResults")]
        internal int PdmMaxResults { get; set; }
        [JsonProperty("EpicorMaxResults")]
        internal int EpicorMaxResults { get; set; }
        [JsonProperty("EpicorConnectionStringEnvironmentVariable")]
        internal string EpicorConnectionStringEnvironmentVariable { get; set; }
    }

    internal class RelaySettings
    {
        [JsonProperty("Enabled")]
        internal bool Enabled { get; set; }
        [JsonProperty("BaseUrl")]
        internal string BaseUrl { get; set; }
        [JsonProperty("ChatWorkspaceUrl")]
        internal string ChatWorkspaceUrl { get; set; }
        [JsonProperty("DeviceId")]
        internal string DeviceId { get; set; }
        [JsonProperty("DeviceName")]
        internal string DeviceName { get; set; }
        [JsonProperty("RegistrationToken")]
        internal string RegistrationToken { get; set; }
        [JsonProperty("HandoffPath")]
        internal string HandoffPath { get; set; }
        [JsonProperty("HeartbeatIntervalSeconds")]
        internal int HeartbeatIntervalSeconds { get; set; }
    }
}
