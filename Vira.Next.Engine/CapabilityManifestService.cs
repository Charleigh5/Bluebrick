using Vira.Next.Contracts;

namespace Vira.Next.Engine;

public sealed class CapabilityManifestService
{
    private static readonly CapabilityDefinition[] Capabilities =
    {
        new()
        {
            Id = "relay.contracts",
            Label = "Relay contract schema",
            SourceBlueBrickClass = "BlueBrick.Relay.Models.RelayContracts",
            State = CapabilityState.Local,
            Category = ToolCategory.Read,
            ResultIds = new[] { "RelayContracts.cs" }
        },
        new()
        {
            Id = "relay.tool-catalog",
            Label = "Capability manifest catalog",
            SourceBlueBrickClass = "BlueBrick.Relay.Services.McpToolCatalog",
            State = CapabilityState.Local,
            Category = ToolCategory.Read,
            ResultIds = new[] { "McpToolCatalog.cs" }
        },
        new()
        {
            Id = "relay.routing",
            Label = "Capability router",
            SourceBlueBrickClass = "BlueBrick.Relay.Services.ToolRoutingService",
            State = CapabilityState.Local,
            Category = ToolCategory.Preview,
            ResultIds = new[] { "ToolRoutingService.cs" }
        },
        new()
        {
            Id = "assistant.receipts",
            Label = "Execution receipt service",
            SourceBlueBrickClass = "BlueBrick.Agent.AssistantToolExecutionReceipt",
            State = CapabilityState.Local,
            Category = ToolCategory.Read,
            ResultIds = new[] { "AssistantToolExecutionReceipt.cs", "AssistantToolAuditLog.cs" }
        },
        new()
        {
            Id = "preview.policy",
            Label = "Capability policy evaluator",
            SourceBlueBrickClass = "BlueBrick.Agent.PreviewActionPolicy",
            State = CapabilityState.Local,
            Category = ToolCategory.Preview,
            ResultIds = new[] { "PreviewActionPolicy.cs", "PreviewSessionCoordinator.cs" }
        },
        new()
        {
            Id = "fake-host.snapshot",
            Label = "Fake host document snapshot",
            SourceBlueBrickClass = "BlueBrick.Simulation.MockSolidWorksEnvironment",
            State = CapabilityState.Mock,
            Category = ToolCategory.Read,
            ResultIds = new[] { "MockSolidWorksEnvironment.cs", "MockDocument.cs" }
        },
        new()
        {
            Id = "solidworks.active-document-snapshot",
            Label = "SOLIDWORKS active document snapshot",
            SourceBlueBrickClass = "SolidWorks.Interop.sldworks.ISldWorks",
            State = CapabilityState.ApprovalRequired,
            Category = ToolCategory.Read,
            RequiresApproval = true,
            LiveSystem = "SOLIDWORKS"
        },
        new()
        {
            Id = "solidworks.mutation",
            Label = "SOLIDWORKS mutation route",
            SourceBlueBrickClass = "BlueBrick.Agent.AgentHttpServer /sw/*",
            State = CapabilityState.ApprovalRequired,
            Category = ToolCategory.Mutation,
            RequiresApproval = true,
            LiveSystem = "SOLIDWORKS"
        },
        new()
        {
            Id = "pdm.search",
            Label = "PDM search",
            SourceBlueBrickClass = "BlueBrick.Vault.PdmVaultWorkspace",
            State = CapabilityState.NotConnected,
            Category = ToolCategory.External,
            LiveSystem = "PDM"
        },
        new()
        {
            Id = "epicor.lookup",
            Label = "Epicor part lookup",
            SourceBlueBrickClass = "Planned Epicor adapter",
            State = CapabilityState.NotConnected,
            Category = ToolCategory.External,
            LiveSystem = "Epicor"
        },
        new()
        {
            Id = "salesforce.lookup",
            Label = "Salesforce account lookup",
            SourceBlueBrickClass = "Planned Salesforce adapter",
            State = CapabilityState.NotConnected,
            Category = ToolCategory.External,
            LiveSystem = "Salesforce"
        }
    };

    public IReadOnlyList<CapabilityDefinition> GetAll() => Capabilities;

    public CapabilityDefinition? Find(string id) =>
        Capabilities.FirstOrDefault(capability => string.Equals(capability.Id, id, StringComparison.OrdinalIgnoreCase));
}
