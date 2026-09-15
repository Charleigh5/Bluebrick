using System;
using System.Linq;
using SharedAI;

namespace BlueBrick.Agent
{
    internal static class SharedAiProfiles
    {
        internal static string Resolve(Catalog catalog, System.Collections.Generic.IDictionary<string, RuntimeState> runtime,
            bool vision, bool streaming, string explicitlySelectedId = null)
        {
            var candidates = catalog.Resolve(vision ? "vision" : "default",
                new Capabilities { Text = true, Vision = vision, Streaming = streaming }, runtime);
            if (candidates.Length == 0) throw new InvalidOperationException("MODEL_UNAVAILABLE: SharedAI has no runtime-eligible candidate.");
            return candidates.Contains(explicitlySelectedId) ? explicitlySelectedId : candidates[0];
        }

        internal static AssistantModelProfile[] Adapt(Catalog catalog, string[] ids)
        {
            if (ids == null || ids.Length == 0 || ids.Distinct().Count() != ids.Length || ids.Any(id => !catalog.Models.Any(m => m.Id == id)))
                throw new InvalidOperationException("SHAREDAI_MODEL_REFERENCES_INVALID");
            return ids.Select(id => {
                var m = catalog.Models.Single(x => x.Id == id);
                var p = catalog.Providers.Single(x => x.Id == m.ProviderId);
                return new AssistantModelProfile { Id = m.Id, Name = m.DisplayName, Provider = p.Id,
                    ProviderKind = p.Id, ApiBaseUrl = p.BaseUrl, Model = m.ProviderModel,
                    KeyEnvironmentVariable = p.CredentialBinding, IsDefault = id == catalog.Routes["default"][0],
                    SupportsText = m.Capabilities.Text, SupportsVision = m.Capabilities.Vision,
                    SupportsTools = m.Capabilities.Tools, SupportsStreaming = m.Capabilities.Streaming,
                    SupportsJsonMode = m.Capabilities.StructuredOutput, ContextLimit = m.ContextLimit,
                    SecretRef = "runtime-only", Enabled = true, Source = "SharedAI",
                    CapabilitySource = "SharedAI R06 declaration inherited from R05 source; live compatibility NOT_VERIFIED" };
            }).ToArray();
        }
    }
}
