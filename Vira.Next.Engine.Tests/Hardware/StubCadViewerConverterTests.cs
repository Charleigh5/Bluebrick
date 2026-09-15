using Microsoft.VisualStudio.TestTools.UnitTesting;
using Vira.Next.Engine.Hardware;

namespace Vira.Next.Engine.Tests.Hardware;

[TestClass]
public sealed class StubCadViewerConverterTests
{
    [TestMethod]
    public async Task Convert_RejectsNonProductionPlaceholder()
    {
        using var temp = new HardwareTestTemp();
        var tmpStep = Path.Combine(temp.Root, "source.step");
        var tmpGlb = Path.Combine(temp.Root, "output.glb");
        await File.WriteAllTextAsync(tmpStep, "ISO-10303-21; HEADER; DATA; ENDSEC;");
        var sut = new StubCadViewerConverter();
        var result = await sut.ConvertAsync(tmpStep, tmpGlb);
        Assert.IsFalse(result.Success);
        Assert.IsFalse(File.Exists(tmpGlb));
        Assert.AreEqual("NON_PRODUCTION_STUB", result.ErrorCode);
        File.Delete(tmpStep);
        File.Delete(tmpGlb);
    }
}
