using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Vira.Next.Contracts;
using Vira.Next.Engine;

namespace Vira.Next.Engine.Tests;

[TestClass]
public sealed class CapabilityRouterTests
{
    [TestMethod]
    public void UnknownCapability_ReturnsUnknownId_WithoutExternalAccess()
    {
        var decision = new CapabilityRouter().Route(new CapabilityRequest
        {
            CapabilityId = "unknown.capability",
            Intent = "try unknown capability"
        });

        Assert.AreEqual(CapabilityRouteStatus.UnknownId, decision.Status);
        Assert.AreEqual(CapabilityState.NotConnected, decision.State);
        Assert.IsFalse(decision.ExternalSystemsAccessed);
        Assert.AreEqual(0, decision.ResultIds.Length);
    }

    [TestMethod]
    public void SolidWorksMutation_RequiresApproval_AndStillDeniesExecutionWhenApprovalMetadataExists()
    {
        var router = new CapabilityRouter();

        var preview = router.Route(new CapabilityRequest
        {
            CapabilityId = "solidworks.mutation",
            Intent = "change a dimension",
            Mode = ViraExecutionMode.PreviewOnly
        });

        Assert.AreEqual(CapabilityRouteStatus.ApprovalRequired, preview.Status);
        Assert.IsTrue(preview.RequiresApproval);

        var attemptedExecution = router.Route(new CapabilityRequest
        {
            CapabilityId = "solidworks.mutation",
            Intent = "change a dimension",
            Mode = ViraExecutionMode.HumanApprovedMutation,
            ApprovalGranted = true
        });

        Assert.AreEqual(CapabilityRouteStatus.PolicyDenied, attemptedExecution.Status);
        Assert.IsFalse(attemptedExecution.ExternalSystemsAccessed);
    }

    [TestMethod]
    public void UnavailableBusinessSystems_DoNotReturnFabricatedFacts()
    {
        var router = new CapabilityRouter();
        foreach (var capabilityId in new[] { "pdm.search", "epicor.lookup", "salesforce.lookup" })
        {
            var decision = router.Route(new CapabilityRequest
            {
                CapabilityId = capabilityId,
                Intent = "lookup live business data"
            });

            Assert.AreEqual(CapabilityRouteStatus.NotConnected, decision.Status, capabilityId);
            Assert.AreEqual(0, decision.ResultIds.Length, capabilityId);
            Assert.IsFalse(decision.ExternalSystemsAccessed, capabilityId);
            Assert.IsFalse(JsonSerializer.Serialize(decision).Contains("inventory quantity", StringComparison.OrdinalIgnoreCase), capabilityId);
            Assert.IsFalse(JsonSerializer.Serialize(decision).Contains("customer price", StringComparison.OrdinalIgnoreCase), capabilityId);
        }
    }

    [TestMethod]
    public void FakeHostSnapshot_IsDeterministic_AndLocalOnly()
    {
        var host = new FakeSolidWorksHost();

        var first = host.CaptureSnapshot();
        var second = host.CaptureSnapshot();

        Assert.AreEqual(first.RuntimeVersion, second.RuntimeVersion);
        Assert.AreEqual(first.ActiveDocument.Title, second.ActiveDocument.Title);
        Assert.AreEqual(first.ActiveDocument.PathHash, second.ActiveDocument.PathHash);
        Assert.IsFalse(first.ExternalSystemsAccessed);
        Assert.IsTrue(first.ActiveDocument.IsReadOnly);
    }

    [TestMethod]
    public void ReceiptSerialization_RedactsErrors_AndRecordsNoExternalAccess()
    {
        var request = new CapabilityRequest
        {
            CapabilityId = "pdm.search",
            Intent = "find vault file",
            TargetSummary = "PDM target",
            Mode = ViraExecutionMode.ReadOnlyAnalyst
        };
        var decision = new CapabilityRouter().Route(request);
        var receipt = new ExecutionReceiptService().Create(
            request,
            decision,
            new[] { new ReceiptError { Code = "STACK", Message = "redacted", Detail = "token=abc123" } });

        var json = JsonSerializer.Serialize(receipt);

        Assert.IsFalse(json.Contains("abc123", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(json.Contains("[REDACTED]", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(receipt.ExternalSystemsAccessed);
        Assert.IsFalse(receipt.CadAccessed);
        Assert.IsFalse(receipt.PdmAccessed);
        Assert.IsFalse(receipt.SecretsAccessed);
        Assert.IsFalse(receipt.ProductionDataAccessed);
    }

    [TestMethod]
    public void ManifestMapsP0DossierPorts_ToLocalContracts()
    {
        var manifest = new CapabilityManifestService();
        var capabilities = manifest.GetAll();

        Assert.IsTrue(capabilities.Any(item => item.SourceBlueBrickClass.Contains("RelayContracts", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(capabilities.Any(item => item.SourceBlueBrickClass.Contains("McpToolCatalog", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(capabilities.Any(item => item.SourceBlueBrickClass.Contains("ToolRoutingService", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(capabilities.Any(item => item.SourceBlueBrickClass.Contains("AssistantToolExecutionReceipt", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(capabilities.Any(item => item.SourceBlueBrickClass.Contains("PreviewActionPolicy", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(capabilities.Any(item => item.SourceBlueBrickClass.Contains("MockSolidWorksEnvironment", StringComparison.OrdinalIgnoreCase)));
    }
}
