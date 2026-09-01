using BlueBrick.Relay;
using BlueBrick.Relay.Models;
using BlueBrick.Relay.Services;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BlueBrick.Relay.Tests;

[TestClass]
public class RelayCoreTests
{
    [TestMethod]
    public async Task SqliteRelayRepository_Upserts_Session_Route()
    {
        var path = Path.Combine(Path.GetTempPath(), "bb-relay-" + Guid.NewGuid().ToString("N") + ".db");
        var repo = new SqliteRelayRepository(Options.Create(new RelayOptions { SqlitePath = path }));
        await repo.EnsureCreatedAsync(CancellationToken.None);
        await repo.UpsertSessionRouteAsync(new RelaySessionRoute
        {
            SessionId = "session-1",
            DeviceId = "device-1",
            UpdatedUtc = DateTime.UtcNow
        }, CancellationToken.None);

        var route = await repo.GetSessionRouteAsync("session-1", CancellationToken.None);

        Assert.IsNotNull(route);
        Assert.AreEqual("device-1", route.DeviceId);
    }

    [TestMethod]
    public void McpToolCatalog_Exposes_Conservative_Tool_Set()
    {
        var catalog = new McpToolCatalog();
        var tools = catalog.GetAll();

        Assert.IsTrue(tools.Any(t => t.Name == "get_preview_status" && t.ReadOnlyHint));
        Assert.IsTrue(tools.Any(t => t.Name == "run_local_review" && !t.ReadOnlyHint));
        Assert.IsTrue(tools.Any(t => t.Name == "apply_safe_action" && t.Disabled));
    }

    [TestMethod]
    public async Task ToolRoutingService_Returns_Offline_When_Tunnel_Missing()
    {
        var path = Path.Combine(Path.GetTempPath(), "bb-relay-" + Guid.NewGuid().ToString("N") + ".db");
        var repo = new SqliteRelayRepository(Options.Create(new RelayOptions { SqlitePath = path, ToolTimeoutSeconds = 1 }));
        await repo.EnsureCreatedAsync(CancellationToken.None);
        await repo.UpsertSessionRouteAsync(new RelaySessionRoute
        {
            SessionId = "session-2",
            DeviceId = "device-offline",
            UpdatedUtc = DateTime.UtcNow
        }, CancellationToken.None);

        var routing = new ToolRoutingService(repo, new DeviceTunnelRegistry(), Options.Create(new RelayOptions { SqlitePath = path, ToolTimeoutSeconds = 1 }));
        var result = await routing.RouteAsync(new RelayToolInvocation
        {
            SessionId = "session-2",
            ToolName = "get_preview_status"
        }, CancellationToken.None);

        Assert.AreEqual("offline", result.Result.Status);
    }

    [TestMethod]
    public void ExecutionBoardFixtureRouter_Returns_Typed_Local_And_Gated_Results()
    {
        var local = ExecutionBoardFixtureRouter.Route("Need PDM availability for bb src 1001 before lunch", "session-3");
        var pdm = ExecutionBoardFixtureRouter.Route("Find PDM part 12345 availability", "session-3");
        var cad = ExecutionBoardFixtureRouter.Route("Route a SOLIDWORKS metadata request safely", "session-3");

        Assert.AreEqual("LOCAL_FIXTURE_RESULT", local.Status);
        CollectionAssert.Contains(local.MatchedIds.ToArray(), "BB-SRC-1001");
        Assert.AreEqual("NOT_CONNECTED", pdm.Status);
        Assert.AreEqual("NOT_CONNECTED", pdm.CapabilityStates.Single().State);
        Assert.AreEqual("APPROVAL_REQUIRED", cad.Status);
        Assert.AreEqual("APPROVAL_REQUIRED", cad.CapabilityStates.Single().State);
    }
}
