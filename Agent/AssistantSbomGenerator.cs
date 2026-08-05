using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using BlueBrick.Audit.Core;

namespace BlueBrick.Agent
{
    public sealed class AssistantSbomGenerator
    {
        public string Format { get; } = "CycloneDX";
        public string SpecVersion { get; } = "1.4";
        public string Generator { get; } = "BlueBrick-Assistant-Hardening-Slice";
        public DateTime GeneratedAtUtc { get; } = DateTime.UtcNow;

        public JObject GenerateProjectSbom(string projectPath, string projectName, string projectVersion)
        {
            var components = new List<JObject>();
            var dependencies = new List<JObject>();

            if (File.Exists(projectPath))
            {
                var csproj = XDocument.Load(projectPath);
                var pkgRefs = csproj.Descendants()
                    .Where(n => n.Name.LocalName == "PackageReference" || n.Name.LocalName == "package")
                    .ToList();

                foreach (var pkg in pkgRefs)
                {
                    var include = pkg.Attribute("Include")?.Value ?? pkg.Attribute("id")?.Value ?? string.Empty;
                    var version = pkg.Attribute("Version")?.Value ?? pkg.Attribute("version")?.Value ?? string.Empty;

                    components.Add(new JObject
                    {
                        ["type"] = "library",
                        ["name"] = include,
                        ["version"] = version,
                        ["purl"] = "pkg:nuget/" + include + "@" + version,
                        ["licenses"] = new JArray(new JObject { ["expression"] = "Unknown" })
                    });

                    if (!string.IsNullOrEmpty(include) && !string.IsNullOrEmpty(version))
                    {
                        dependencies.Add(new JObject
                        {
                            ["ref"] = "pkg:nuget/" + include + "@" + version,
                            ["dependsOn"] = new JArray()
                        });
                    }
                }
            }

            var sbom = new JObject
            {
                ["bomFormat"] = "CycloneDX",
                ["specVersion"] = SpecVersion,
                ["serialNumber"] = "urn:uuid:" + Guid.NewGuid().ToString("D", System.Globalization.CultureInfo.InvariantCulture),
                ["version"] = 1,
                ["metadata"] = new JObject
                {
                    ["component"] = new JObject
                    {
                        ["type"] = "application",
                        ["name"] = projectName,
                        ["version"] = projectVersion,
                        ["group"] = "BlueBrick",
                        ["description"] = "BlueBrick assistant hardening SBOM"
                    },
                    ["timestamp"] = GeneratedAtUtc.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                    ["tools"] = new JArray(new JObject { ["name"] = Generator, ["vendor"] = "ViraInsight" })
                },
                ["components"] = new JArray(components),
                ["dependencies"] = new JArray(dependencies)
            };

            return sbom;
        }

        public string GenerateCanonicalJson(JObject sbom)
        {
            return AuditCanonicalSerializer.ToCanonicalJson(sbom);
        }

        public string GenerateSha256(JObject sbom)
        {
            var json = GenerateCanonicalJson(sbom);
            return AssistantIntegrityScanner.ComputeSha256String(json);
        }

        public void SaveToFile(JObject sbom, string outputPath)
        {
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(outputPath, JsonConvert.SerializeObject(sbom, Formatting.Indented), Encoding.UTF8);
        }
    }
}
