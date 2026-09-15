using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Vira.Next.Contracts;
using Vira.Next.Engine.Hardware;

namespace Vira.Next.Engine.Tests.Hardware;

[TestClass]
public sealed class HardwareCadBindingServiceTests
{
    private sealed class FakeProvider : IVendorCadProvider
    {
        public McMasterProductSnapshot Product { get; set; } = new();
        public byte[] CadBytes { get; set; } = Encoding.UTF8.GetBytes("ISO-10303-21; STEP MOCK");
        public bool Subscribed { get; private set; }
        public Task EnsureSubscribedAsync(string partNumber, CancellationToken ct = default) { Subscribed = true; return Task.CompletedTask; }
        public Task<McMasterProductSnapshot> GetProductAsync(string partNumber, CancellationToken ct = default) => Task.FromResult(Product);
        public Task<byte[]> GetCadBytesAsync(string normalizedCadUrl, CancellationToken ct = default) => Task.FromResult(CadBytes);
    }

    [TestMethod]
    public async Task Acquire_ReturnsAcquiredThenCacheHit()
    {
        using var temp = new HardwareTestTemp();
        var tmp = temp.Root;
        var fake = new FakeProvider
        {
            Product = new McMasterProductSnapshot
            {
                PartNumber = "91251A632",
                ProductStatus = "Active",
                Links = new[] { new McMasterLink { Key = "3-D STEP", Value = "/v1/cad/91251A632.STEP" } }
            },
            CadBytes = Encoding.UTF8.GetBytes("ISO-10303-21; HEADER; DATA; ENDSEC;")
        };
        var repo = new CadAssetRepository(tmp);
        var converter = new InertConverter();
        var svc = new HardwareCadBindingService(fake, repo, converter, new McMasterOptions { BaseUrl = "https://api.mcmaster.com" });
        var record = new HardwareRecord { HardwareRecordId = "HWD-001", McMasterPartNumber = "91251A632" };

        var first = await svc.AcquireAsync(record);
        Assert.AreEqual(VendorCadAcquisitionStatus.Acquired, first.Status);
        Assert.IsNotNull(first.Asset);
        Assert.IsNotNull(first.ViewerDerivative);
        Assert.IsTrue(first.ViewerDerivative!.IsValid);
        Assert.IsFalse(first.CacheHit);
        Assert.AreEqual(EngineeringMutationBoundary.ReadOnly, first.MutationBoundary);

        var second = await svc.AcquireAsync(record);
        Assert.AreEqual(VendorCadAcquisitionStatus.CacheHit, second.Status);
        Assert.IsTrue(second.CacheHit);
        Assert.AreEqual(first.Asset!.Sha256, second.Asset!.Sha256);


    }

    [TestMethod]
    public async Task Acquire_NoCadAvailable_WhenNoStepLink()
    {
        using var temp = new HardwareTestTemp();
        var tmp = temp.Root;
        var fake = new FakeProvider
        {
            Product = new McMasterProductSnapshot { PartNumber = "00000", Links = new[] { new McMasterLink { Key = "2-D DXF", Value = "/v1/cad/000.dxf" } } }
        };
        var svc = new HardwareCadBindingService(fake, new CadAssetRepository(tmp), new StubCadViewerConverter(), new McMasterOptions());
        var result = await svc.AcquireAsync(new HardwareRecord { HardwareRecordId = "HWD-002", McMasterPartNumber = "00000" });
        Assert.AreEqual(VendorCadAcquisitionStatus.NoCadAvailable, result.Status);

    }

    [TestMethod]
    public async Task Acquire_FailsWhenMissingPartNumber()
    {
        using var temp = new HardwareTestTemp();
        var svc = new HardwareCadBindingService(new FakeProvider(), new CadAssetRepository(temp.Root), new StubCadViewerConverter());
        var result = await svc.AcquireAsync(new HardwareRecord { HardwareRecordId = "HWD-003", McMasterPartNumber = "" });
        Assert.AreEqual(VendorCadAcquisitionStatus.Failed, result.Status);
    }
}
