// Mirrored native contract. JSON catalog is the source of static truth.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SharedAI
{
    public sealed class Capabilities
    {
        public bool Text { get; set; }
        public bool Vision { get; set; }
        public bool Tools { get; set; }
        public bool Streaming { get; set; }
        public bool StructuredOutput { get; set; }
        public bool Satisfies(Capabilities r) => (!r.Text || Text) && (!r.Vision || Vision) &&
            (!r.Tools || Tools) && (!r.Streaming || Streaming) && (!r.StructuredOutput || StructuredOutput);
    }
    public sealed class Provider
    {
        public string Id { get; set; }
        public string Protocol { get; set; }
        public string BaseUrl { get; set; }
        public string CredentialBinding { get; set; }
    }
    public sealed class Model
    {
        public string Id { get; set; }
        public string ProviderId { get; set; }
        public string ProviderModel { get; set; }
        public string DisplayName { get; set; }
        public Capabilities Capabilities { get; set; }
        public int ContextLimit { get; set; }
        public string[] Roles { get; set; }
    }
    public sealed class RuntimeState
    {
        // Eligibility only: credentials present is not successful inference evidence.
        public bool Available { get; set; }
        public string Reason { get; set; }
    }
    public sealed class Catalog
    {
        public Provider[] Providers { get; private set; }
        public Model[] Models { get; private set; }
        public Dictionary<string, string[]> Routes { get; private set; }
        private static readonly string[] CapabilityNames = { "text", "vision", "tools", "streaming", "structuredOutput" };
        private static void Require(bool condition) { if (!condition) throw new InvalidDataException("SHAREDAI_CATALOG_INVALID"); }
        private static void Fields(JToken token, params string[] names)
        {
            var o = token as JObject;
            Require(o != null && o.Properties().Select(p => p.Name).OrderBy(n => n).SequenceEqual(names.OrderBy(n => n)));
        }
        private static string Str(JToken token) { Require(token != null && token.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string)token)); var s = (string)token; Require(s.Length <= 256 && !System.Text.RegularExpressions.Regex.IsMatch(s, @"[\r\n]|bearer\s|sk-[a-z0-9]|AIza|-----BEGIN", System.Text.RegularExpressions.RegexOptions.IgnoreCase)); return s; }
        private static JToken Parse(string text)
        {
            try { return JToken.Parse(text, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error }); }
            catch { throw new InvalidDataException("SHAREDAI_CATALOG_INVALID"); }
        }
        public static Catalog Load(string directory) => FromJson(File.ReadAllText(Path.Combine(directory, "providers.json")), File.ReadAllText(Path.Combine(directory, "models.json")), File.ReadAllText(Path.Combine(directory, "routes.json")));
        public static Catalog FromJson(string providers, string models, string routes)
        {
            var p = Parse(providers); var m = Parse(models); var r = Parse(routes);
            Require(p.Type == JTokenType.Array && p.Count() == 3 && m.Type == JTokenType.Array && m.Count() == 2);
            foreach (var x in p)
            {
                Fields(x, "id", "protocol", "baseUrl", "credentialBinding");
                var id = Str(x["id"]);
                Require(id == "nvidia" || id == "google-gemini" || id == "openrouter");
                Require(Str(x["protocol"]) == "openai-chat-completions");
                var credentialBinding = id == "nvidia" ? "NVIDIA_API_KEY" : id == "google-gemini" ? "GEMINI_API_KEY" : "OPENROUTER_API_KEY";
                var baseUrl = id == "nvidia" ? "https://integrate.api.nvidia.com/v1" : id == "google-gemini" ? "https://generativelanguage.googleapis.com/v1beta/openai" : "https://openrouter.ai/api/v1";
                Require(Str(x["credentialBinding"]) == credentialBinding);
                Require(Str(x["baseUrl"]) == baseUrl);
            }
            foreach (var x in m)
            {
                Fields(x, "id", "providerId", "providerModel", "displayName", "capabilities", "contextLimit", "roles");
                Require(new[] { "nvidia-kimi-k3", "google-gemini-3-8-flash" }.Contains(Str(x["id"])));
                Str(x["providerId"]); Str(x["providerModel"]); Str(x["displayName"]);
                Require(System.Text.RegularExpressions.Regex.IsMatch((string)x["providerModel"], "^[a-z0-9][a-z0-9./-]{0,99}$"));
                Require(((string)x["displayName"]).Length <= 80);
                Fields(x["capabilities"], CapabilityNames);
                foreach (var n in CapabilityNames) Require(x["capabilities"][n].Type == JTokenType.Boolean);
                Require(x["contextLimit"].Type == JTokenType.Integer && (long)x["contextLimit"] > 0 && (long)x["contextLimit"] <= int.MaxValue);
                Require(x["roles"].Type == JTokenType.Array && x["roles"].All(v => v.Type == JTokenType.String && new[] { "general", "vision", "engineering", "agent", "fast", "fallback" }.Contains((string)v)));
                Require(x["roles"].Count() > 0 && x["roles"].Values<string>().Distinct().Count() == x["roles"].Count());
            }
            Fields(r, "default", "vision", "tools");
            foreach (var x in ((JObject)r).Properties()) Require(x.Value.Type == JTokenType.Array && x.Value.Count() > 0 && x.Value.All(v => v.Type == JTokenType.String));
            var result = new Catalog { Providers = p.ToObject<Provider[]>(), Models = m.ToObject<Model[]>(), Routes = r.ToObject<Dictionary<string, string[]>>() };
            Require(result.Providers.Select(x => x.Id).Distinct().Count() == 3 && result.Models.Select(x => x.Id).Distinct().Count() == 2);
            Require(result.Models.All(x => result.Providers.Any(y => y.Id == x.ProviderId)));
            Require(result.Models.All(x => x.ProviderId == (x.Id == "nvidia-kimi-k3" ? "nvidia" : "google-gemini")));
            Require(result.Routes.Values.All(x => x.Distinct().Count() == x.Length && x.All(id => result.Models.Any(y => y.Id == id))));
            return result;
        }
        public string[] Resolve(string route, Capabilities requirements, IDictionary<string, RuntimeState> runtime)
        {
            if (requirements == null || runtime == null || !Routes.ContainsKey(route ?? "")) throw new ArgumentException("SHAREDAI_ROUTE_INVALID");
            return Routes[route].Where(id => {
                var caps = Models.Single(m => m.Id == id).Capabilities;
                return caps.Satisfies(requirements) && (route != "vision" || caps.Vision) && (route != "tools" || caps.Tools) &&
                    runtime.TryGetValue(id, out var s) && s != null && s.Available;
            }).ToArray();
        }
        public static bool AllowsFallback(string boundary, string failure) => boundary == "provider" &&
            new[] { "credential_missing", "provider_unavailable", "model_unavailable", "rate_limited", "timeout" }.Contains(failure);
    }
}
