using Microsoft.VisualStudio.TestTools.UnitTesting;
using Vira.Next.Contracts;
using Vira.Next.Engine.Hardware;

namespace Vira.Next.Engine.Tests.Hardware;

[TestClass]
public sealed class CadLinkNormalizerTests
{
    private readonly CadLinkNormalizer _sut = new();

    [TestMethod]
    public void Normalize_SelectsPreferredStepLink()
    {
        var links = new[]
        {
            new McMasterLink { Key = "2-D DXF", Value = "/v1/cad/123.dxf" },
            new McMasterLink { Key = "3-D STEP", Value = "/v1/cad/123.STEP" }
        };
        var result = _sut.Normalize(links, "https://api.mcmaster.com", "91251A632");
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("https://api.mcmaster.com/v1/cad/123.STEP", result.NormalizedUrl);
    }

    [TestMethod]
    public void Normalize_FailsWhenNoStepKey()
    {
        var links = new[] { new McMasterLink { Key = "2-D DXF", Value = "/v1/cad/123.dxf" } };
        var result = _sut.Normalize(links, "https://api.mcmaster.com", "91251A632");
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual("NO_STEP_LINK", result.ErrorCode);
    }

    [TestMethod]
    public void Normalize_FailsWhenEmptyLinks()
    {
        var result = _sut.Normalize(Array.Empty<McMasterLink>(), "https://api.mcmaster.com", "91251A632");
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual("NO_CAD_LINKS", result.ErrorCode);
    }

    [TestMethod]
    public void Normalize_HandlesAbsoluteUrl()
    {
        var links = new[] { new McMasterLink { Key = "3-D STEP", Value = "https://api.mcmaster.com/v1/cad/abs.STEP" } };
        var result = _sut.Normalize(links, "https://api.mcmaster.com", "91251A632");
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("https://api.mcmaster.com/v1/cad/abs.STEP", result.NormalizedUrl);
    }
}
