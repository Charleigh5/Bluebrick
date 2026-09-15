using Microsoft.VisualStudio.TestTools.UnitTesting;
using Vira.Next.Contracts;
using Vira.Next.Engine.Hardware;

namespace Vira.Next.Engine.Tests.Hardware;

[TestClass]
public sealed class HardwareProvenanceTests
{
    [DataTestMethod]
    [DataRow("source")]
    [DataRow("digest")]
    [DataRow("length")]
    [DataRow("converter")]
    [DataRow("version")]
    [DataRow("stub")]
    [DataRow("invalid-glb")]
    public async Task ConversionMismatch_NeverStoredOrReturned(string defect)
    {
        var converter = new InertConverter { Defect = defect };
        var repo = new InertRepository();
        var result = await Service(repo, converter).AcquireAsync(Record);
        Assert.IsNull(result.ViewerDerivative);
        Assert.AreEqual(0, repo.DerivativeWrites);
        Assert.IsTrue(result.Errors.Length > 0);
        AssertClean(converter);
    }

    [TestMethod]
    public async Task Conversion_PreservesActualResultProvenance()
    {
        var converter = new InertConverter();
        var repo = new InertRepository();
        var result = await Service(repo, converter).AcquireAsync(Record);
        Assert.IsNotNull(result.ViewerDerivative);
        Assert.AreEqual("inert-actual-converter", result.ViewerDerivative.ConverterId);
        Assert.AreEqual("actual-2", result.ViewerDerivative.ConverterVersion);
        Assert.AreEqual(ContentAddressedCache.ComputeSha256(HardwareTestData.Step), result.ViewerDerivative.SourceSha256);
        Assert.AreEqual(ContentAddressedCache.ComputeSha256(HardwareTestData.Glb), result.ViewerDerivative.GlbSha256);
        Assert.AreEqual(HardwareTestData.Glb.Length, result.ViewerDerivative.GlbByteLength);
        Assert.AreEqual(1, repo.DerivativeWrites);
        AssertClean(converter);
    }

    [DataTestMethod]
    [DataRow("failure")]
    [DataRow("exception")]
    [DataRow("cancel")]
    public async Task Conversion_CleansTemporaryFilesForEveryExit(string defect)
    {
        var converter = new InertConverter { Defect = defect };
        try
        {
            var result = await Service(new InertRepository(), converter).AcquireAsync(Record);
            Assert.IsNull(result.ViewerDerivative);
            Assert.IsTrue(result.Errors.Length > 0);
        }
        catch (OperationCanceledException) when (defect == "cancel") { }
        finally { AssertClean(converter); }
    }

    [DataTestMethod]
    [DataRow("source")]
    [DataRow("asset")]
    [DataRow("invalid")]
    public async Task CachedDerivative_MustMatchSource(string defect)
    {
        var repo = new InertRepository();
        var derivative = HardwareTestData.Derivative(repo.Asset);
        repo.CachedDerivative = defect switch { "source" => derivative with { SourceSha256 = new string('a', 64) }, "asset" => derivative with { SourceAssetId = "OTHER" }, _ => derivative with { IsValid = false } };
        var converter = new InertConverter();
        var result = await Service(repo, converter).AcquireAsync(Record);
        Assert.IsNull(result.ViewerDerivative);
        Assert.IsTrue(result.Errors.Length > 0);
        Assert.AreEqual(0, converter.Calls);
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("OTHER")]
    public async Task InvalidProduct_ProducesFetchFailureBeforeCadOrStorage(string identity)
    {
        var handler = new RecordingHandler { Response = request => RecordingHandler.Json(request.Method == HttpMethod.Put ? "{}" : "{\"PartNumber\":\"" + identity + "\",\"secret\":\"response-secret\"}") };
        using var client = new HttpClient(handler);
        using var provider = HardwareSecurityTests.CreateProvider(client);
        var repo = new InertRepository();
        var converter = new InertConverter();
        var result = await new HardwareCadBindingService(provider, repo, converter, HardwareSecurityTests.Options()).AcquireAsync(Record);
        Assert.AreEqual(VendorCadAcquisitionStatus.Failed, result.Status);
        Assert.AreEqual("PRODUCT_FETCH_FAILED", result.Errors.Single().Code);
        Assert.AreEqual(2, handler.Requests.Count);
        Assert.AreEqual(0, repo.Reads);
        Assert.AreEqual(0, repo.DerivativeWrites);
        Assert.AreEqual(0, converter.Calls);
        Assert.IsFalse(System.Text.Json.JsonSerializer.Serialize(result).Contains("response-secret"));
    }

    [TestMethod]
    public async Task StubChain_NeverPromotesPlaceholder()
    {
        using var temp = new HardwareTestTemp();
        var source = Path.Combine(temp.Root, "source.step");
        var output = Path.Combine(temp.Root, "output.glb");
        await File.WriteAllBytesAsync(source, HardwareTestData.Step);
        var chain = new ChainedCadViewerConverter(new ICadViewerConverter[] { new StubCadViewerConverter() });
        var result = await chain.ConvertAsync(source, output);
        Assert.IsFalse(result.Success);
        Assert.IsFalse(File.Exists(output));
    }

    private static HardwareRecord Record => new() { HardwareRecordId = "HWD-TEST", McMasterPartNumber = "P1" };
    private static HardwareCadBindingService Service(InertRepository repo, InertConverter converter) => new(new InertProvider(), repo, converter, HardwareSecurityTests.Options());
    private static void AssertClean(InertConverter converter)
    {
        Assert.IsFalse(File.Exists(converter.StepPath), "Temporary STEP leaked.");
        Assert.IsFalse(File.Exists(converter.GlbPath), "Temporary GLB leaked.");
    }
}

internal sealed class InertConverter : ICadViewerConverter
{
    public string Defect { get; init; } = "";
    public int Calls { get; private set; }
    public string StepPath { get; private set; } = "";
    public string GlbPath { get; private set; } = "";
    public string ConverterId => "wrapper-must-not-be-used";
    public string ConverterVersion => "wrapper-version";
    public async Task<ViewerConversionResult> ConvertAsync(string sourceStepPath, string outputGlbPath, CancellationToken cancellationToken = default)
    {
        Calls++; StepPath = sourceStepPath; GlbPath = outputGlbPath;
        await File.WriteAllBytesAsync(outputGlbPath, Defect == "invalid-glb" ? new byte[12] : HardwareTestData.Glb, cancellationToken);
        if (Defect == "cancel") throw new OperationCanceledException();
        if (Defect == "exception") throw new IOException("inert conversion exception");
        var result = new ViewerConversionResult { Success = Defect != "failure", SourceSha256 = ContentAddressedCache.ComputeSha256(sourceStepPath), GlbSha256 = ContentAddressedCache.ComputeSha256(HardwareTestData.Glb), GlbByteLength = HardwareTestData.Glb.Length, ConverterId = "inert-actual-converter", ConverterVersion = "actual-2", ErrorCode = "INERT_FAILURE", ErrorMessage = "inert diagnostic" };
        return Defect switch {
            "source" => result with { SourceSha256 = new string('a', 64) }, "digest" => result with { GlbSha256 = new string('a', 64) },
            "length" => result with { GlbByteLength = 1 }, "converter" => result with { ConverterId = "" },
            "version" => result with { ConverterVersion = "" }, "stub" => result with { ConverterId = "vira.stub-step-to-glb.v1" }, _ => result
        };
    }
}

internal sealed class InertProvider : IVendorCadProvider
{
    public Task EnsureSubscribedAsync(string partNumber, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<McMasterProductSnapshot> GetProductAsync(string partNumber, CancellationToken cancellationToken = default) => Task.FromResult(new McMasterProductSnapshot { PartNumber = partNumber, Links = new[] { new McMasterLink { Key = "3-D STEP", Value = "/a" } } });
    public Task<byte[]> GetCadBytesAsync(string normalizedCadUrl, CancellationToken cancellationToken = default) => Task.FromResult(HardwareTestData.Step);
}

internal sealed class InertRepository : ICadAssetRepository
{
    public VendorCadAsset Asset { get; } = new() { AssetId = ContentAddressedCache.ComputeSha256(HardwareTestData.Step), Sha256 = ContentAddressedCache.ComputeSha256(HardwareTestData.Step), ByteLength = HardwareTestData.Step.Length, PartNumber = "P1" };
    public ViewerDerivative? CachedDerivative { get; set; }
    public int Reads { get; private set; }
    public int DerivativeWrites { get; private set; }
    public Task<VendorCadAsset?> FindByShaAsync(string sha256, CancellationToken cancellationToken = default) { Reads++; return Task.FromResult<VendorCadAsset?>(Asset); }
    public Task<VendorCadAsset> StoreAsync(string partNumber, string sourceCadLink, string normalizedCadUrl, byte[] bytes, McMasterProductSnapshot? product, CancellationToken cancellationToken = default) => throw new AssertFailedException("Unexpected source cache write.");
    public Task<ViewerDerivative?> FindDerivativeAsync(string sourceSha256, CancellationToken cancellationToken = default) => Task.FromResult(CachedDerivative);
    public Task<ViewerDerivative> StoreDerivativeAsync(ViewerDerivative derivative, byte[] glbBytes, CancellationToken cancellationToken = default) { DerivativeWrites++; return Task.FromResult(derivative); }
}
