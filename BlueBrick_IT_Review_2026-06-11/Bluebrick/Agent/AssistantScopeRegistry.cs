using System;
using System.Collections.Generic;
using System.Linq;

namespace BlueBrick.Agent
{
    internal static class AssistantScopeRegistry
    {
        internal const string LocalVault = "local_vault";
        internal const string Pdm = "pdm";
        internal const string Epicor = "epicor";
        internal const string All = "all";

        internal static IReadOnlyList<AssistantScopeDescriptor> Build(AgentConfig config, IEnumerable<AssistantToolDescriptor> catalog)
        {
            config = config ?? new AgentConfig();
            var tools = (catalog ?? Array.Empty<AssistantToolDescriptor>()).ToDictionary(t => t.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase);
            var pdm = tools.TryGetValue("search_pdm", out var pdmTool) ? pdmTool : null;
            var epicor = tools.TryGetValue("search_epicor", out var epicorTool) ? epicorTool : null;

            return new[]
            {
                new AssistantScopeDescriptor
                {
                    Id = LocalVault,
                    Label = "Local Vault",
                    Description = "Searches only the local BlueBrick vault index.",
                    Enabled = true,
                    ToolNames = new[] { "search_local_vault" },
                    ReadOnlyToolNames = new[] { "search_local_vault" },
                    RequiresCredential = false,
                    AllowsMutation = false,
                    RiskLevel = "low"
                },
                new AssistantScopeDescriptor
                {
                    Id = Pdm,
                    Label = "PDM",
                    Description = "Read-only SOLIDWORKS PDM search wrapper.",
                    Enabled = pdm?.Enabled ?? false,
                    UnavailableReason = pdm?.Enabled == true ? string.Empty : (pdm?.UnavailableReason ?? "PDM search is unavailable."),
                    ToolNames = new[] { "search_pdm" },
                    ReadOnlyToolNames = new[] { "search_pdm" },
                    RequiresCredential = true,
                    AllowsMutation = false,
                    RiskLevel = "medium"
                },
                new AssistantScopeDescriptor
                {
                    Id = Epicor,
                    Label = "Epicor",
                    Description = "Read-only Epicor part and context search wrapper.",
                    Enabled = epicor?.Enabled ?? false,
                    UnavailableReason = epicor?.Enabled == true ? string.Empty : (epicor?.UnavailableReason ?? "Epicor search is unavailable."),
                    ToolNames = new[] { "search_epicor" },
                    ReadOnlyToolNames = new[] { "search_epicor" },
                    RequiresCredential = true,
                    AllowsMutation = false,
                    RiskLevel = "medium"
                },
                new AssistantScopeDescriptor
                {
                    Id = All,
                    Label = "Both/All",
                    Description = "Fans out to enabled read-only vault, PDM, and Epicor sources and reports unavailable sources.",
                    Enabled = true,
                    ToolNames = new[] { "search_local_vault", "search_pdm", "search_epicor" },
                    ReadOnlyToolNames = new[] { "search_local_vault", "search_pdm", "search_epicor" },
                    RequiresCredential = true,
                    AllowsMutation = false,
                    RiskLevel = "medium"
                }
            };
        }

        internal static AssistantScopeDescriptor Resolve(AgentConfig config, IEnumerable<AssistantToolDescriptor> catalog, string scopeId)
        {
            var normalized = Normalize(scopeId);
            return Build(config, catalog).FirstOrDefault(s => string.Equals(s.Id, normalized, StringComparison.OrdinalIgnoreCase))
                   ?? Build(config, catalog).First(s => s.Id == LocalVault);
        }

        internal static string Normalize(string scopeId)
        {
            var value = (scopeId ?? string.Empty).Trim().ToLowerInvariant();
            if (value == "vault" || value == "local" || value == "localvault") return LocalVault;
            if (value == "both" || value == "all_sources" || value == "both_all") return All;
            if (value == Pdm || value == Epicor || value == All || value == LocalVault) return value;
            return LocalVault;
        }
    }
}
