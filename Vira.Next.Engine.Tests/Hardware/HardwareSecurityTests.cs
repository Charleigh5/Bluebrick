using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Vira.Next.Contracts;
using Vira.Next.Engine.Hardware;

namespace Vira.Next.Engine.Tests.Hardware;

[TestClass]
public sealed class HardwareSecurityTests
{
    [DataTestMethod]
    [DataRow("http://vendor.invalid/a", "https://vendor.invalid")]
    [DataRow("//evil.invalid/a", "https://vendor.invalid")]
    [DataRow("https://user:secret@vendor.invalid/a", "https://vendor.invalid")]
    [DataRow("https://@vendor.invalid/a", "https://vendor.invalid")]
    [DataRow("https://evil.invalid/a", "https://vendor.invalid")]
    [DataRow("https://vendor.invalid:444/a", "https://vendor.invalid")]
    [DataRow("/a", "http://vendor.invalid")]
    [DataRow("https://vendor.invalid/a", "not a base")]
    [DataRow("/a", "https://user:secret@vendor.invalid")]
    [DataRow("\\\\evil.invalid/a", "https://vendor.invalid")]
    public void Normalizer_RejectsUnsafeOrigin(string url, string baseUrl) => Assert.IsNull(CadLinkNormalizer.NormalizeUrl(url, baseUrl));

    [DataTestMethod]
    [DataRow("/a.STEP", "https://vendor.invalid")]
    [DataRow("a.STEP?x=1", "https://vendor.invalid/api")]
    [DataRow("https://VENDOR.invalid:443/a?x=1", "https://vendor.invalid/api")]
    [DataRow("https://xn--bcher-kva.invalid/a", "https://bücher.invalid")]
    public void Normalizer_AcceptsSameOrigin(string url, string baseUrl) => Assert.IsNotNull(CadLinkNormalizer.NormalizeUrl(url, baseUrl));

    [DataTestMethod]
    [DataRow("http://vendor.invalid/a")]
    [DataRow("//evil.invalid/a")]
    [DataRow("https://user:secret@vendor.invalid/a")]
    [DataRow("https://evil.invalid/a")]
    [DataRow("https://vendor.invalid:444/a")]
    [DataRow("/relative")]
    public async Task DirectCadCaller_IsRejectedBeforeAuth(string url)
    {
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        using var provider = CreateProvider(client);
        Exception? failure = null;
        try { await provider.GetCadBytesAsync(url); } catch (Exception ex) { failure = ex; }
        Assert.IsNotNull(failure);
        Assert.AreEqual(0, handler.Requests.Count, "No auth or CAD request may occur.");
    }

    [TestMethod]
    public async Task CadRedirect_IsFailure_WithoutFollowingLocation()
    {
        var handler = new RecordingHandler { Response = _ => new HttpResponseMessage(HttpStatusCode.Redirect) { Headers = { Location = new Uri("https://evil.invalid/leak") }, Content = new StringContent("response-secret") } };
        using var client = new HttpClient(handler);
        using var provider = CreateProvider(client);
        var ex = await Assert.ThrowsExceptionAsync<HttpRequestException>(() => provider.GetCadBytesAsync("https://vendor.invalid/a"));
        Assert.AreEqual(1, handler.Requests.Count);
        Assert.IsFalse(ex.ToString().Contains("response-secret"));
    }

    [DataTestMethod]
    [DataRow(typeof(McMasterCadProvider))]
    [DataRow(typeof(McmAuthTokenProvider))]
    public void OwnedHttpClient_DisablesRedirects(Type owner)
    {
        using var client = (HttpClient)owner.GetMethod("CreateHttpClient", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { Options() })!;
        var handler = (HttpClientHandler)typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(client)!;
        Assert.IsFalse(handler.AllowAutoRedirect);
    }

    [DataTestMethod]
    [DataRow("{}")]
    [DataRow("{\"PartNumber\":\"\"}")]
    [DataRow("{\"PartNumber\":\"OTHER\",\"secret\":\"response-secret\"}")]
    [DataRow("{\"PartNumber\":\"p1\"}")]
    public async Task ProductIdentity_FailsClosed(string json)
    {
        var handler = new RecordingHandler { Response = _ => RecordingHandler.Json(json) };
        using var client = new HttpClient(handler);
        using var provider = CreateProvider(client);
        var ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => provider.GetProductAsync(" P1 "));
        Assert.IsFalse(ex.ToString().Contains("response-secret"));
        Assert.IsFalse(ex.ToString().Contains("fake-token"));
    }

    [TestMethod]
    public async Task ProductIdentity_MatchesRequestedTrimmedOrdinal()
    {
        var handler = new RecordingHandler { Response = _ => RecordingHandler.Json("{\"PartNumber\":\"P1\"}") };
        using var client = new HttpClient(handler);
        using var provider = CreateProvider(client);
        Assert.AreEqual("P1", (await provider.GetProductAsync(" P1 ")).PartNumber);
    }

    [TestMethod]
    public async Task SameOriginCad_UsesOnlyInertTokenAndReturnsBytes()
    {
        var handler = new RecordingHandler { Response = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(HardwareTestData.Step) } };
        using var client = new HttpClient(handler);
        using var provider = CreateProvider(client);
        CollectionAssert.AreEqual(HardwareTestData.Step, await provider.GetCadBytesAsync("https://VENDOR.invalid:443/cad"));
        Assert.AreEqual("Bearer fake-token", handler.Requests.Single().Authorization);
    }

    [DataTestMethod]
    [DataRow("http://vendor.invalid")]
    [DataRow("https://user:secret@vendor.invalid")]
    [DataRow("malformed")]
    public void InvalidBase_IsRejectedBeforeAnyRequest(string baseUrl)
    {
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        Assert.ThrowsException<ArgumentException>(() => new McMasterCadProvider(Options() with { BaseUrl = baseUrl }, httpClient: client));
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task InertLogin_ParsesTokenAliasesWithoutJsonCollision()
    {
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        using var auth = new McmAuthTokenProvider(Options(), client);
        Assert.AreEqual("fake-token", await auth.GetTokenAsync());
        Assert.AreEqual(1, handler.Requests.Count);
    }

    [DataTestMethod]
    [DataRow("bytes")]
    [DataRow("digest")]
    [DataRow("length")]
    [DataRow("path")]
    [DataRow("identity")]
    [DataRow("missing")]
    [DataRow("malformed")]
    public async Task StepTampering_IsNotCacheHit(string defect)
    {
        using var temp = new HardwareTestTemp();
        var repo = new CadAssetRepository(temp.Root);
        var asset = await repo.StoreAsync("P1", "/a", "https://vendor.invalid/a", HardwareTestData.Step, null);
        var meta = asset.ContentAddressedPath + ".meta.json";
        if (defect == "bytes") await File.WriteAllTextAsync(asset.ContentAddressedPath, "tampered");
        else if (defect == "missing") File.Delete(meta);
        else if (defect == "malformed") await File.WriteAllTextAsync(meta, "{broken");
        else await File.WriteAllTextAsync(meta, JsonSerializer.Serialize(defect switch {
            "digest" => asset with { Sha256 = new string('a', 64) },
            "length" => asset with { ByteLength = 999 },
            "identity" => asset with { AssetId = "OTHER" },
            _ => asset with { ContentAddressedPath = Path.Combine(temp.Root, "..", "escape.step") }
        }));
        Assert.IsNull(await repo.FindByShaAsync(asset.Sha256));
    }

    [DataTestMethod]
    [DataRow("../escape")]
    [DataRow("aa/../../escape")]
    [DataRow("aa\\..\\escape")]
    [DataRow("aa")]
    [DataRow("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    public void Sha_RejectsInvalidPathInput(string sha) => Assert.ThrowsException<ArgumentException>(() => ContentAddressedCache.GetShardPath(Path.GetTempPath(), sha, ".step"));

    [DataTestMethod]
    [DataRow("bytes")]
    [DataRow("metadata")]
    [DataRow("missing")]
    [DataRow("other-part")]
    public async Task StoreCollision_FailsWithoutOverwrite(string defect)
    {
        using var temp = new HardwareTestTemp();
        var repo = new CadAssetRepository(temp.Root);
        var asset = await repo.StoreAsync("P1", "/a", "https://vendor.invalid/a", HardwareTestData.Step, null);
        var meta = asset.ContentAddressedPath + ".meta.json";
        if (defect == "bytes") await File.WriteAllTextAsync(asset.ContentAddressedPath, "tampered");
        if (defect == "metadata") await File.WriteAllTextAsync(meta, "{}");
        if (defect == "missing") File.Delete(meta);
        var before = await File.ReadAllBytesAsync(asset.ContentAddressedPath);
        var beforeMeta = File.Exists(meta) ? await File.ReadAllTextAsync(meta) : null;
        await Assert.ThrowsExceptionAsync<InvalidDataException>(() => repo.StoreAsync(defect == "other-part" ? "P2" : "P1", "/a", "https://vendor.invalid/a", HardwareTestData.Step, null));
        CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(asset.ContentAddressedPath));
        Assert.AreEqual(beforeMeta, File.Exists(meta) ? await File.ReadAllTextAsync(meta) : null);
    }

    [DataTestMethod]
    [DataRow("bytes")]
    [DataRow("magic")]
    [DataRow("source")]
    [DataRow("asset")]
    [DataRow("digest")]
    [DataRow("length")]
    [DataRow("path")]
    [DataRow("invalid")]
    [DataRow("converter")]
    [DataRow("version")]
    [DataRow("stub")]
    [DataRow("missing")]
    [DataRow("malformed")]
    [DataRow("source-bytes")]
    public async Task DerivativeTampering_IsNotViewerReady(string defect)
    {
        using var temp = new HardwareTestTemp();
        var repo = new CadAssetRepository(temp.Root);
        var asset = await repo.StoreAsync("P1", "/a", "https://vendor.invalid/a", HardwareTestData.Step, null);
        var stored = await repo.StoreDerivativeAsync(HardwareTestData.Derivative(asset), HardwareTestData.Glb);
        if (defect == "missing") File.Delete(stored.GlbContentAddressedPath + ".meta.json");
        else if (defect == "malformed") await File.WriteAllTextAsync(stored.GlbContentAddressedPath + ".meta.json", "{broken");
        else if (defect == "source-bytes") await File.WriteAllTextAsync(asset.ContentAddressedPath, "tampered STEP");
        else if (defect is "bytes" or "magic")
        {
            var bytes = HardwareTestData.Glb.ToArray(); bytes[defect == "magic" ? 0 : bytes.Length - 1] ^= 1;
            await File.WriteAllBytesAsync(stored.GlbContentAddressedPath, bytes);
        }
        else await File.WriteAllTextAsync(stored.GlbContentAddressedPath + ".meta.json", JsonSerializer.Serialize(defect switch {
            "source" => stored with { SourceSha256 = new string('a', 64) },
            "asset" => stored with { SourceAssetId = "OTHER" },
            "digest" => stored with { GlbSha256 = new string('a', 64) },
            "length" => stored with { GlbByteLength = 1 },
            "invalid" => stored with { IsValid = false },
            "converter" => stored with { ConverterId = "" },
            "version" => stored with { ConverterVersion = "" },
            "stub" => stored with { ConverterId = "vira.stub-step-to-glb.v1" },
            _ => stored with { GlbContentAddressedPath = Path.Combine(temp.Root, "..", "escape.glb") }
        }));
        Assert.IsNull(await repo.FindDerivativeAsync(asset.Sha256));
    }

    [TestMethod]
    public async Task DerivativeCollision_DoesNotOverwriteExistingEvidence()
    {
        using var temp = new HardwareTestTemp();
        var repo = new CadAssetRepository(temp.Root);
        var asset = await repo.StoreAsync("P1", "/a", "https://vendor.invalid/a", HardwareTestData.Step, null);
        var stored = await repo.StoreDerivativeAsync(HardwareTestData.Derivative(asset), HardwareTestData.Glb);
        var metaBefore = await File.ReadAllTextAsync(stored.GlbContentAddressedPath + ".meta.json");
        await Assert.ThrowsExceptionAsync<InvalidDataException>(() => repo.StoreDerivativeAsync(stored with { ConverterVersion = "other-version" }, HardwareTestData.Glb));
        Assert.AreEqual(metaBefore, await File.ReadAllTextAsync(stored.GlbContentAddressedPath + ".meta.json"));
        CollectionAssert.AreEqual(HardwareTestData.Glb, await File.ReadAllBytesAsync(stored.GlbContentAddressedPath));
    }

    [DataTestMethod]
    [DataRow("source")]
    [DataRow("asset")]
    [DataRow("digest")]
    [DataRow("length")]
    [DataRow("glb")]
    [DataRow("converter")]
    [DataRow("version")]
    [DataRow("stub")]
    public async Task DerivativeStore_RejectsInvalidEvidence(string defect)
    {
        using var temp = new HardwareTestTemp();
        var repo = new CadAssetRepository(temp.Root);
        var asset = await repo.StoreAsync("P1", "/a", "https://vendor.invalid/a", HardwareTestData.Step, null);
        var d = HardwareTestData.Derivative(asset);
        d = defect switch {
            "source" => d with { SourceSha256 = new string('a', 64) }, "asset" => d with { SourceAssetId = "other" },
            "digest" => d with { GlbSha256 = new string('a', 64) }, "length" => d with { GlbByteLength = 1 },
            "converter" => d with { ConverterId = "" }, "version" => d with { ConverterVersion = "" },
            "stub" => d with { ConverterId = "vira.stub-step-to-glb.v1" }, _ => d
        };
        await Assert.ThrowsExceptionAsync<InvalidDataException>(() => repo.StoreDerivativeAsync(d, defect == "glb" ? new byte[12] : HardwareTestData.Glb));
        Assert.IsNull(await repo.FindDerivativeAsync(asset.Sha256));
    }

    internal static McMasterCadProvider CreateProvider(HttpClient client)
    {
        var provider = new McMasterCadProvider(Options(), httpClient: client);
        var auth = (McmAuthTokenProvider)typeof(McMasterCadProvider).GetField("_auth", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(provider)!;
        typeof(McmAuthTokenProvider).GetField("_token", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(auth, "fake-token");
        typeof(McmAuthTokenProvider).GetField("_expiryUtc", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(auth, DateTime.UtcNow.AddDays(1));
        return provider;
    }

    internal static McMasterOptions Options() => new() { BaseUrl = "https://vendor.invalid", Username = "fake-user", Password = "fake-password", EnableRateLimit = false, MaxRetries = 0 };
}

internal sealed class RecordingHandler : HttpMessageHandler
{
    public List<(Uri? Url, string? Authorization)> Requests { get; } = new();
    public Func<HttpRequestMessage, HttpResponseMessage> Response { get; init; } = _ => Json("{}");
    public static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK) { Content = new StringContent(value, Encoding.UTF8, "application/json") };
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add((request.RequestUri, request.Headers.Authorization?.ToString()));
        return Task.FromResult(request.RequestUri!.AbsolutePath == "/v1/login" ? Json("{\"AuthToken\":\"fake-token\"}") : Response(request));
    }
}

internal sealed class HardwareTestTemp : IDisposable
{
    public string Root { get; } = Directory.CreateTempSubdirectory("vira-hardware-test-").FullName;
    public void Dispose()
    {
        var full = Path.GetFullPath(Root);
        var parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(parent, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Cleanup root escaped temp directory.");
        if (Directory.Exists(full)) Directory.Delete(full, true);
    }
}

internal static class HardwareTestData
{
    public static byte[] Step => Encoding.UTF8.GetBytes("ISO-10303-21; INERT TEST ONLY;");
    public static byte[] Glb
    {
        get
        {
            var json = Encoding.UTF8.GetBytes("{\"asset\":{\"version\":\"2.0\"}} ");
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
            writer.Write(0x46546C67); writer.Write(2); writer.Write(20 + json.Length); writer.Write(json.Length); writer.Write(0x4E4F534A); writer.Write(json);
            return stream.ToArray();
        }
    }
    public static ViewerDerivative Derivative(VendorCadAsset asset) => new() { SourceSha256 = asset.Sha256, SourceAssetId = asset.AssetId, GlbSha256 = ContentAddressedCache.ComputeSha256(Glb), GlbByteLength = Glb.Length, ConverterId = "inert-converter", ConverterVersion = "test-1", IsValid = true };
}
