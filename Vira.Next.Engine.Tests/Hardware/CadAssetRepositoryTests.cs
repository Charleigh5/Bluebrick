using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Vira.Next.Engine.Hardware;

namespace Vira.Next.Engine.Tests.Hardware;

[TestClass]
public sealed class CadAssetRepositoryTests
{
    [TestMethod]
    public async Task StoreAndFind_RoundTrips()
    {
        using var temp = new HardwareTestTemp();
        var tmp = temp.Root;
        var repo = new CadAssetRepository(tmp);
        var bytes = Encoding.UTF8.GetBytes("ISO-10303-21; TEST STEP CONTENT");
        var asset = await repo.StoreAsync("91251A632", "/v1/cad/91251A632.STEP", "https://api.mcmaster.com/v1/cad/91251A632.STEP", bytes, null);
        Assert.IsFalse(string.IsNullOrWhiteSpace(asset.Sha256));
        Assert.IsTrue(File.Exists(asset.ContentAddressedPath));

        var found = await repo.FindByShaAsync(asset.Sha256);
        Assert.IsNotNull(found);
        Assert.AreEqual(asset.Sha256, found!.Sha256);

    }

    [TestMethod]
    public async Task Derivative_RoundTrips()
    {
        using var temp = new HardwareTestTemp();
        var tmp = temp.Root;
        var repo = new CadAssetRepository(tmp);
        var bytes = Encoding.UTF8.GetBytes("STEP");
        var asset = await repo.StoreAsync("P1", "/v1/cad/P1.STEP", "https://api.mcmaster.com/v1/cad/P1.STEP", bytes, null);
        var glbBytes = HardwareTestData.Glb;
        var derivative = HardwareTestData.Derivative(asset);
        var stored = await repo.StoreDerivativeAsync(derivative, glbBytes);
        var found = await repo.FindDerivativeAsync(asset.Sha256);
        Assert.IsNotNull(found);
        Assert.AreEqual(stored.GlbSha256, found!.GlbSha256);

    }

    [TestMethod]
    public void ContentHash_IsDeterministic()
    {
        var a = Encoding.UTF8.GetBytes("hello");
        var b = Encoding.UTF8.GetBytes("hello");
        Assert.AreEqual(ContentAddressedCache.ComputeSha256(a), ContentAddressedCache.ComputeSha256(b));
    }
}
