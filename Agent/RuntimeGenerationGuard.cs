using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BlueBrick.Agent
{
    internal sealed class RuntimeGenerationCheck
    {
        internal bool IsMatch { get; private set; }
        internal string Code { get; private set; }
        internal string Detail { get; private set; }
        internal string BuildId { get; private set; }

        internal static RuntimeGenerationCheck Match(string buildId)
        {
            return new RuntimeGenerationCheck { IsMatch = true, Code = "RUNTIME_GENERATION_MATCH", Detail = "Runtime artifacts match the deployment manifest.", BuildId = buildId };
        }

        internal static RuntimeGenerationCheck Mismatch(string detail)
        {
            return new RuntimeGenerationCheck { IsMatch = false, Code = "RUNTIME_GENERATION_MISMATCH", Detail = detail ?? "Runtime generation mismatch." };
        }
    }

    internal static class RuntimeGenerationGuard
    {
        internal static RuntimeGenerationCheck Validate(
            string manifestPath,
            string expectedTarget,
            string dllPath,
            string configPath,
            string frontendRoot,
            string configSchemaVersion)
        {
            if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
                return RuntimeGenerationCheck.Mismatch("missing runtime-manifest.json");

            RuntimeManifest manifest;
            try
            {
                manifest = JsonConvert.DeserializeObject<RuntimeManifest>(File.ReadAllText(manifestPath));
            }
            catch (Exception ex)
            {
                return RuntimeGenerationCheck.Mismatch("invalid runtime manifest: " + ex.GetType().Name);
            }

            if (manifest == null || !string.Equals(manifest.Product, "BlueBrick", StringComparison.Ordinal))
                return RuntimeGenerationCheck.Mismatch("manifest product was missing or invalid");
            if (!string.Equals(manifest.Channel, expectedTarget, StringComparison.OrdinalIgnoreCase))
                return RuntimeGenerationCheck.Mismatch("target expected " + expectedTarget + " observed " + (manifest.Channel ?? "missing"));
            if (!string.Equals(manifest.ConfigSchemaVersion, configSchemaVersion, StringComparison.Ordinal))
                return RuntimeGenerationCheck.Mismatch("config schema expected " + configSchemaVersion + " observed " + (manifest.ConfigSchemaVersion ?? "missing"));
            if (string.IsNullOrWhiteSpace(manifest.BuildId) || !string.Equals(manifest.BuildId, manifest.FrontendBuildId, StringComparison.Ordinal))
                return RuntimeGenerationCheck.Mismatch("DLL/deployment buildId did not match frontendBuildId");

            if (!string.IsNullOrEmpty(manifest.Schema) && manifest.Schema != "bluebrick-lab-runtime-manifest.v1" && manifest.Schema != "bluebrick-lab-runtime-manifest.v2")
                return RuntimeGenerationCheck.Mismatch("unsupported runtime manifest schema");
            var isV2 = string.Equals(manifest.Schema, "bluebrick-lab-runtime-manifest.v2", StringComparison.Ordinal);
            bool hasSharedAiProperty;
            string[] sharedIds = null;
            try
            {
                var config = JObject.Parse(File.ReadAllText(configPath), new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                var assistant = config["Assistant"] as JObject;
                hasSharedAiProperty = assistant != null && assistant.Property("SharedAiModelIds") != null;
                if (hasSharedAiProperty)
                {
                    var ids = assistant["SharedAiModelIds"] as JArray;
                    if (ids == null || ids.Count == 0)
                        return RuntimeGenerationCheck.Mismatch("SharedAiModelIds must be a non-empty array");
                    sharedIds = ids.Values<string>().ToArray();
                    if (sharedIds.Distinct(StringComparer.Ordinal).Count() != sharedIds.Length)
                        return RuntimeGenerationCheck.Mismatch("SharedAiModelIds must be unique");
                    foreach (var id in ids)
                        if (id.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)id))
                            return RuntimeGenerationCheck.Mismatch("SharedAiModelIds must contain non-empty strings");
                }
            }
            catch (Exception ex)
            {
                return RuntimeGenerationCheck.Mismatch("invalid runtime config: " + ex.GetType().Name);
            }

            if (hasSharedAiProperty && !isV2)
                return RuntimeGenerationCheck.Mismatch("SharedAiModelIds requires runtime manifest v2");

            var mismatch = VerifyHash("DLL", dllPath, manifest.Dll == null ? null : manifest.Dll.Sha256);
            if (mismatch != null) return mismatch;
            mismatch = VerifyHash("config", configPath, manifest.Config == null ? null : manifest.Config.Sha256);
            if (mismatch != null) return mismatch;

            foreach (var asset in new[] { "index.html", "assistant-index.css", "assistant-web.js" })
            {
                RuntimeArtifact expected;
                if (manifest.Frontend == null || manifest.Frontend.Artifacts == null || !manifest.Frontend.Artifacts.TryGetValue(asset, out expected))
                    return RuntimeGenerationCheck.Mismatch("frontend manifest missing " + asset);
                mismatch = VerifyHash("frontend " + asset, Path.Combine(frontendRoot, asset), expected.Sha256);
                if (mismatch != null) return mismatch;
            }

            if (isV2)
            {
                var embeddedBuildId = ReadEmbeddedBuildId(Path.Combine(frontendRoot, "index.html"));
                if (string.IsNullOrWhiteSpace(embeddedBuildId) ||
                    string.Equals(embeddedBuildId, "UNKNOWN", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(manifest.BuildId, "UNKNOWN", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(manifest.FrontendBuildId, "UNKNOWN", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(embeddedBuildId, manifest.FrontendBuildId, StringComparison.Ordinal) ||
                    !string.Equals(embeddedBuildId, manifest.BuildId, StringComparison.Ordinal))
                    return RuntimeGenerationCheck.Mismatch("embedded frontend build identity is missing, unknown, duplicated, or inconsistent");
            }

            if (isV2)
            {
                var configDirectory = Path.GetDirectoryName(configPath);
                var catalogRoot = Path.Combine(configDirectory, "SharedAI", "catalog");
                if (!Directory.Exists(catalogRoot))
                    catalogRoot = Path.Combine(Directory.GetParent(configDirectory).FullName, "SharedAI", "catalog");
                foreach (var catalog in new[] { "providers.json", "models.json", "routes.json" })
                {
                    RuntimeArtifact expected;
                    if (manifest.SharedAiCatalog == null || manifest.SharedAiCatalog.Artifacts == null ||
                        !manifest.SharedAiCatalog.Artifacts.TryGetValue(catalog, out expected))
                        return RuntimeGenerationCheck.Mismatch("SharedAI catalog manifest missing " + catalog);
                    mismatch = VerifyHash("SharedAI catalog " + catalog, Path.Combine(catalogRoot, catalog), expected.Sha256);
                    if (mismatch != null) return mismatch;
                }
                try
                {
                    var catalog = SharedAI.Catalog.Load(catalogRoot);
                    if (sharedIds != null && sharedIds.Any(id => !catalog.Models.Any(model => model.Id == id)))
                        return RuntimeGenerationCheck.Mismatch("SharedAiModelIds contains an unknown model");
                }
                catch (Exception ex) { return RuntimeGenerationCheck.Mismatch("invalid SharedAI catalog: " + ex.GetType().Name); }
            }

            return RuntimeGenerationCheck.Match(manifest.BuildId);
        }

        private static string ReadEmbeddedBuildId(string indexPath)
        {
            if (!File.Exists(indexPath)) return null;
            var html = File.ReadAllText(indexPath);
            var identities = new System.Collections.Generic.List<string>();
            foreach (Match tag in Regex.Matches(html, @"<!--.*?(?:-->|$)|<(script|style|textarea|title|xmp|iframe|noembed|noframes|template)\b[^>]*>.*?(?:</\1\s*>|$)|<plaintext\b.*$|<[a-z][a-z0-9:-]*(?=\s|/?>)(?:[^>""']|""[^""]*""|'[^']*')*>", RegexOptions.IgnoreCase | RegexOptions.Singleline))
            {
                if (!Regex.IsMatch(tag.Value, @"^<meta(?=\s|/?>)", RegexOptions.IgnoreCase)) continue;
                var attributes = Regex.Matches(tag.Value.Substring(5, tag.Length - 6), @"(?<name>[^\s=/>]+)(?:\s*=\s*(?:""(?<value>[^""]*)""|'(?<value>[^']*)'|(?<value>[^\s>]+)))?", RegexOptions.Singleline).Cast<Match>().ToArray();
                var names = attributes.Where(attribute => string.Equals(attribute.Groups["name"].Value, "name", StringComparison.OrdinalIgnoreCase)).ToArray();
                if (!names.Any(name => string.Equals(System.Net.WebUtility.HtmlDecode(name.Groups["value"].Value), "bluebrick-build-id", StringComparison.OrdinalIgnoreCase))) continue;
                var contents = attributes.Where(attribute => string.Equals(attribute.Groups["name"].Value, "content", StringComparison.OrdinalIgnoreCase)).ToArray();
                if (names.Length != 1 || contents.Length != 1) return null;
                identities.Add(System.Net.WebUtility.HtmlDecode(contents[0].Groups["value"].Value).Trim());
            }
            return identities.Count == 1 ? identities[0] : null;
        }

        private static RuntimeGenerationCheck VerifyHash(string label, string path, string expectedHash)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return RuntimeGenerationCheck.Mismatch(label + " file missing");
            if (string.IsNullOrWhiteSpace(expectedHash))
                return RuntimeGenerationCheck.Mismatch(label + " expected hash missing");
            var observed = ComputeSha256(path);
            return string.Equals(observed, expectedHash, StringComparison.OrdinalIgnoreCase)
                ? null
                : RuntimeGenerationCheck.Mismatch(label + " hash expected " + expectedHash + " observed " + observed);
        }

        private static string ComputeSha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
        }

        private sealed class RuntimeManifest
        {
            [JsonProperty("schema")] public string Schema { get; set; }
            [JsonProperty("product")] public string Product { get; set; }
            [JsonProperty("channel")] public string Channel { get; set; }
            [JsonProperty("buildId")] public string BuildId { get; set; }
            [JsonProperty("frontendBuildId")] public string FrontendBuildId { get; set; }
            [JsonProperty("configSchemaVersion")] public string ConfigSchemaVersion { get; set; }
            [JsonProperty("dll")] public RuntimeArtifact Dll { get; set; }
            [JsonProperty("config")] public RuntimeArtifact Config { get; set; }
            [JsonProperty("frontend")] public RuntimeFrontend Frontend { get; set; }
            [JsonProperty("sharedAiCatalog")] public RuntimeFrontend SharedAiCatalog { get; set; }
        }

        private sealed class RuntimeFrontend
        {
            [JsonProperty("artifacts")] public System.Collections.Generic.Dictionary<string, RuntimeArtifact> Artifacts { get; set; }
        }

        private sealed class RuntimeArtifact
        {
            [JsonProperty("sha256")] public string Sha256 { get; set; }
        }
    }
}
