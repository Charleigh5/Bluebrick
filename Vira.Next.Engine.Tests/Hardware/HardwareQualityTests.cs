using System.Net;
using System.Reflection;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Vira.Next.Contracts;
using Vira.Next.Engine.Hardware;

namespace Vira.Next.Engine.Tests.Hardware;

[TestClass]
public sealed class HardwareQualityTests
{
    [DataTestMethod]
    [DataRow("repository")]
    [DataRow("directory")]
    [DataRow("write")]
    [DataRow("existing-write")]
    public async Task CacheAncestry_IsCheckedBeforeAnyMutationOrRead(string operation)
    {
        using var temp = new HardwareTestTemp();
        var ancestor = Directory.CreateDirectory(Path.Combine(temp.Root, "modeled-junction")).FullName;
        var cache = Path.Combine(ancestor, "new-cache");
        var path = Path.Combine(cache, "object.step");
        if (operation == "existing-write")
        {
            Directory.CreateDirectory(cache);
            await File.WriteAllBytesAsync(path, HardwareTestData.Step);
        }
        var observed = new List<string>();
        var guard = new CachePathGuard(p =>
        {
            observed.Add(p);
            if (p == ancestor) return FileAttributes.Directory | FileAttributes.ReparsePoint;
            return Directory.Exists(p) ? FileAttributes.Directory : File.Exists(p) ? FileAttributes.Normal : null;
        });
        Exception? failure = null;
        try
        {
            if (operation == "repository") _ = new CadAssetRepository(cache + Path.DirectorySeparatorChar, guard);
            else if (operation == "directory") ContentAddressedCache.EnsureDirectory(path, guard);
            else await ContentAddressedCache.WriteIfNotExistsAsync(path, HardwareTestData.Step, pathGuard: guard);
        }
        catch (Exception ex) { failure = ex; }
        Assert.IsInstanceOfType<InvalidDataException>(failure);
        CollectionAssert.Contains(observed, ancestor);
        if (operation != "existing-write") Assert.IsFalse(Directory.Exists(cache), "Validation must precede creation under the modeled junction.");
        else CollectionAssert.AreEqual(HardwareTestData.Step, await File.ReadAllBytesAsync(path));
    }

    [TestMethod]
    public async Task CacheAncestry_IsRecheckedAfterConstruction()
    {
        using var temp = new HardwareTestTemp();
        var reject = false;
        var guard = new CachePathGuard(p => p == temp.Root && reject ? FileAttributes.ReparsePoint :
            Directory.Exists(p) ? FileAttributes.Directory : File.Exists(p) ? FileAttributes.Normal : null);
        var repo = new CadAssetRepository(Path.Combine(temp.Root, "cache"), guard);
        var asset = await repo.StoreAsync("P1", "/a", "https://vendor.invalid/a", HardwareTestData.Step, null);
        reject = true;
        Assert.IsNull(await repo.FindByShaAsync(asset.Sha256));
        await Assert.ThrowsExceptionAsync<InvalidDataException>(() => repo.StoreAsync("P1", "/a", "https://vendor.invalid/a", HardwareTestData.Step, null));
    }

    [DataTestMethod]
    [DataRow("source", false)]
    [DataRow("source", true)]
    [DataRow("derivative", false)]
    [DataRow("derivative", true)]
    [DataRow("failure", false)]
    [DataRow("failure", true)]
    [DataRow("exception", false)]
    [DataRow("exception", true)]
    public async Task ErrorReceipts_AreFailedWithOnlyValidatedSourceEvidence(string defect, bool cacheHit)
    {
        var repo = new QualityRepository(defect, cacheHit);
        var converter = new InertConverter { Defect = defect };
        var receipt = await new HardwareCadBindingService(new InertProvider(), repo, converter, HardwareSecurityTests.Options())
            .AcquireAsync(new HardwareRecord { HardwareRecordId = "HWD-TEST", McMasterPartNumber = "P1" });
        Assert.AreEqual(VendorCadAcquisitionStatus.Failed, receipt.Status);
        Assert.AreEqual(defect switch { "source" => "SOURCE_PROVENANCE_INVALID", "derivative" => "DERIVATIVE_PROVENANCE_INVALID", "failure" => "INERT_FAILURE", _ => "CAD_CACHE_OR_CONVERSION_FAILED" }, receipt.Errors.Single().Code);
        Assert.IsNull(receipt.ViewerDerivative);
        Assert.AreEqual(defect == "source" ? false : cacheHit, receipt.CacheHit);
        if (defect == "source") Assert.IsNull(receipt.Asset, "Invalid source evidence cannot survive in a receipt.");
        else Assert.AreSame(repo.Asset, receipt.Asset);
    }

    [DataTestMethod]
    [DataRow("\"PartNumber\":\"P1\",\"PartNumber\":\"P1\"")]
    [DataRow("\"PartNumber\":\"P2\",\"PartNumber\":\"P1\"")]
    [DataRow("\"partNumber\":\"P2\",\"partNumber\":\"P1\"")]
    [DataRow("\"PartNumber\":\"P1\",\"partNumber\":\"P2\"")]
    [DataRow("\"partNumber\":\"P2\",\"PartNumber\":\"P1\"")]
    [DataRow("\"PartNumber\":\"P1\",\"partNumber\":\"P1\"")]
    public async Task DuplicateIdentity_FailsBeforeCadRequest(string identities)
    {
        using var handler = new RecordingHandler { Response = request => RecordingHandler.Json(request.Method == HttpMethod.Put ? "{}" :
            "{" + identities + ",\"Links\":[{\"Key\":\"3-D STEP\",\"Value\":\"/P2.STEP\"}]}") };
        using var client = new HttpClient(handler);
        using var provider = HardwareSecurityTests.CreateProvider(client);
        var repo = new InertRepository();
        var converter = new InertConverter();
        var receipt = await new HardwareCadBindingService(provider, repo, converter, HardwareSecurityTests.Options())
            .AcquireAsync(new HardwareRecord { HardwareRecordId = "HWD-TEST", McMasterPartNumber = "P1" });
        Assert.AreEqual("PRODUCT_FETCH_FAILED", receipt.Errors.Single().Code);
        Assert.AreEqual(VendorCadAcquisitionStatus.Failed, receipt.Status);
        Assert.AreEqual(2, handler.Requests.Count);
        Assert.AreEqual(0, repo.Reads);
        Assert.AreEqual(0, converter.Calls);
    }

    [DataTestMethod]
    [DataRow("PartNumber")]
    [DataRow("partNumber")]
    public async Task SingleIdentityAlias_AcceptsExactTrimmedRequest(string alias)
    {
        using var handler = new RecordingHandler { Response = _ => RecordingHandler.Json("{\"" + alias + "\":\"P1\"}") };
        using var client = new HttpClient(handler);
        using var provider = HardwareSecurityTests.CreateProvider(client);
        Assert.AreEqual("P1", (await provider.GetProductAsync(" P1 ")).PartNumber);
    }

    [DataTestMethod]
    [DataRow("cad", "success")]
    [DataRow("cad", "redirect")]
    [DataRow("cad", "unauthorized")]
    [DataRow("cad", "retry")]
    [DataRow("cad", "rate-limit")]
    [DataRow("product", "success")]
    [DataRow("product", "parse")]
    [DataRow("product", "redirect")]
    [DataRow("product", "unauthorized")]
    [DataRow("product", "retry")]
    [DataRow("product", "rate-limit")]
    [DataRow("subscribe", "success")]
    [DataRow("subscribe", "redirect")]
    [DataRow("subscribe", "unauthorized")]
    [DataRow("subscribe", "retry")]
    [DataRow("subscribe", "rate-limit")]
    [DataRow("login", "success")]
    [DataRow("login", "parse")]
    [DataRow("login", "redirect")]
    [DataRow("login", "rate-limit")]
    public async Task HttpLifetime_DisposesRequestsResponsesAndStreams(string operation, string outcome)
    {
        using var handler = new LifetimeHandler(operation, outcome);
        using var client = new HttpClient(handler);
        var options = HardwareSecurityTests.Options() with { MaxRetries = outcome == "retry" ? 1 : 0 };
        using var auth = new McmAuthTokenProvider(options, client);
        using var provider = new McMasterCadProvider(options, auth, client);
        Exception? failure = null;
        try
        {
            if (operation == "cad") await provider.GetCadBytesAsync("https://vendor.invalid/a");
            else if (operation == "product") await provider.GetProductAsync("P1");
            else if (operation == "subscribe") await provider.EnsureSubscribedAsync("P1");
            else await auth.GetTokenAsync();
        }
        catch (Exception ex) { failure = ex; }
        if (outcome is "success" or "unauthorized" or "retry") Assert.IsNull(failure, failure?.ToString());
        else Assert.IsNotNull(failure);
        Assert.IsTrue(handler.PreviousDisposedBeforeNextRequest, "Abandoned responses must be disposed before retry or auth refresh.");
        Assert.IsTrue(handler.Contents.All(c => c.Disposed && c.Stream.Disposed), "Every response content and its stream must be disposed before return/throw.");
        foreach (var request in handler.Requests)
            await Assert.ThrowsExceptionAsync<ObjectDisposedException>(async () => await request.Content!.ReadAsByteArrayAsync());
        Assert.IsFalse(failure?.ToString().Contains("fake-body-private") ?? false);
    }

    [DataTestMethod]
    [DataRow("provider")]
    [DataRow("auth")]
    [DataRow("internal-auth")]
    public async Task InjectedDependencies_RemainUsableForOtherOwners(string disposedOwner)
    {
        using var handler = new LifetimeHandler("product", "success");
        using var client = new HttpClient(handler);
        var options = HardwareSecurityTests.Options();
        using var sharedAuth = new McmAuthTokenProvider(options, client);
        using var survivor = new McMasterCadProvider(options, sharedAuth, client);
        if (disposedOwner == "provider") new McMasterCadProvider(options, sharedAuth, client).Dispose();
        else if (disposedOwner == "auth") new McmAuthTokenProvider(options, client).Dispose();
        else new McMasterCadProvider(options, httpClient: client).Dispose();
        Assert.IsFalse(handler.Disposed);
        Assert.AreEqual("P1", (await survivor.GetProductAsync("P1")).PartNumber);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void InternallyCreatedClients_DisposeTheirHandlers(bool cadProvider)
    {
        // Construct handlers without sending anything; inspect disposal state without an accidental socket fallback.
        using IDisposable owner = cadProvider ? new McMasterCadProvider(HardwareSecurityTests.Options()) : new McmAuthTokenProvider(HardwareSecurityTests.Options());
        var client = (HttpClient)owner.GetType().GetField("_httpClient", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
        var handler = (HttpClientHandler)typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client)!;
        var disposed = typeof(HttpClientHandler).GetField("_disposed", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.IsFalse((bool)disposed.GetValue(handler)!);
        HttpClientHandler? authHandler = null;
        if (cadProvider)
        {
            var auth = (McmAuthTokenProvider)typeof(McMasterCadProvider).GetField("_auth", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
            var authClient = (HttpClient)typeof(McmAuthTokenProvider).GetField("_httpClient", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(auth)!;
            authHandler = (HttpClientHandler)typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(authClient)!;
            Assert.IsFalse((bool)disposed.GetValue(authHandler)!);
        }
        owner.Dispose();
        Assert.IsTrue((bool)disposed.GetValue(handler)!);
        if (authHandler != null) Assert.IsTrue((bool)disposed.GetValue(authHandler)!);
    }

    [TestMethod]
    public async Task RetryCancellation_DisposesBeforeWaitingAndPreservesCallerToken()
    {
        using var caller = new CancellationTokenSource();
        using var handler = new LifetimeHandler("product", "retry") { CancelAfterRateLimitDisposed = caller };
        using var client = new HttpClient(handler);
        var options = HardwareSecurityTests.Options() with { MaxRetries = 1 };
        using var auth = new McmAuthTokenProvider(options, client);
        using var provider = new McMasterCadProvider(options, auth, client);
        var error = await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => provider.GetProductAsync("P1", caller.Token));
        Assert.AreEqual(caller.Token, error.CancellationToken);
        Assert.AreEqual(2, handler.Requests.Count, "Only login and the initial product request are allowed.");
        Assert.IsTrue(handler.Contents.All(c => c.Disposed && c.Stream.Disposed));
        foreach (var request in handler.Requests)
            await Assert.ThrowsExceptionAsync<ObjectDisposedException>(async () => await request.Content!.ReadAsByteArrayAsync());
    }

    [DataTestMethod]
    [DataRow("cad", false)]
    [DataRow("cad", true)]
    [DataRow("product", false)]
    [DataRow("product", true)]
    [DataRow("login", false)]
    [DataRow("login", true)]
    public async Task BlockedHttpBody_IsBoundedAndDisposed(string operation, bool callerCancellation)
    {
        using var caller = new CancellationTokenSource();
        using var content = new BlockingContent();
        using var handler = new BodyHandler(content);
        using var client = new HttpClient(handler) { Timeout = callerCancellation ? TimeSpan.FromSeconds(5) : TimeSpan.FromMilliseconds(50) };
        using var auth = new McmAuthTokenProvider(HardwareSecurityTests.Options(), client);
        if (operation != "login")
        {
            typeof(McmAuthTokenProvider).GetField("_token", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(auth, "fake-token");
            typeof(McmAuthTokenProvider).GetField("_expiryUtc", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(auth, DateTime.UtcNow.AddDays(1));
        }
        using var provider = new McMasterCadProvider(HardwareSecurityTests.Options(), auth, client);
        Task work = operation == "cad" ? provider.GetCadBytesAsync("https://vendor.invalid/a", caller.Token) :
            operation == "product" ? provider.GetProductAsync("P1", caller.Token) : auth.GetTokenAsync(caller.Token);
        try
        {
            await content.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (callerCancellation) caller.Cancel();
            Exception? failure = null;
            try { await work.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (Exception ex) { failure = ex; }
            Assert.IsInstanceOfType<OperationCanceledException>(failure);
            if (callerCancellation) Assert.AreEqual(caller.Token, ((OperationCanceledException)failure!).CancellationToken);
            Assert.IsTrue(content.Disposed);
            await Assert.ThrowsExceptionAsync<ObjectDisposedException>(async () => await handler.Request!.Content!.ReadAsByteArrayAsync());
        }
        finally { content.Release(); }
    }

    [DataTestMethod]
    [DataRow(-2L, 1000L)]
    [DataRow(4294967295L, 1000L)]
    [DataRow(1000L, -2L)]
    [DataRow(1000L, 4294967295L)]
    public async Task InvalidTimeout_IsRejectedBeforeChildStart(long timeoutMs, long stopTimeoutMs)
    {
        using var temp = new HardwareTestTemp();
        var source = Path.Combine(temp.Root, "source.step");
        await File.WriteAllBytesAsync(source, HardwareTestData.Step);
        var child = new PipeChild();
        var factory = new PipeFactory(child);
        Exception? failure = null;
        try
        {
            var converter = new OcctCadViewerConverter(factory, TimeSpan.FromMilliseconds(timeoutMs), "inert-version", TimeSpan.FromMilliseconds(stopTimeoutMs));
            var result = await converter.ConvertAsync(source, Path.Combine(temp.Root, "output.glb"));
            if (!result.Success) failure = new InvalidOperationException(result.ErrorCode);
        }
        catch (Exception ex) { failure = ex; }
        finally { child.Exit(); }
        Assert.IsNotNull(failure);
        Assert.AreEqual(0, factory.StartCalls, "Invalid timer state must never leave a child running.");
    }

    [TestMethod]
    public async Task LargeStderr_IsDrainedBeforeWaitingForExit()
    {
        using var temp = new HardwareTestTemp();
        var source = Path.Combine(temp.Root, "source.step");
        await File.WriteAllBytesAsync(source, HardwareTestData.Step);
        var child = new PipeChild { ExitRequiresDrain = true };
        var converter = new OcctCadViewerConverter(new PipeFactory(child), TimeSpan.FromMilliseconds(100), "inert-version", TimeSpan.FromMilliseconds(100));
        try
        {
            var result = await converter.ConvertAsync(source, Path.Combine(temp.Root, "output.glb")).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreEqual("OCCT_NON_ZERO", result.ErrorCode);
            Assert.IsTrue(child.ReadStartedBeforeWait);
            Assert.AreEqual(0, child.StopCalls, "A full modeled pipe must not force a timeout.");
            Assert.IsTrue(child.ReadCompleted);
            Assert.IsTrue(result.ErrorMessage.Length < 450);
        }
        finally { child.Exit(); }
    }

    [DataTestMethod]
    [DataRow(true, true)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(false, false)]
    public async Task PostStartSetupFailure_StopsAndConfirmsOrReportsUnconfirmedExit(bool readThrows, bool confirmStop)
    {
        using var temp = new HardwareTestTemp();
        var source = Path.Combine(temp.Root, "source.step");
        await File.WriteAllBytesAsync(source, HardwareTestData.Step);
        var child = new PipeChild { ReadThrows = readThrows, WaitThrows = !readThrows, KeepRunning = true, ConfirmStop = confirmStop };
        var converter = new OcctCadViewerConverter(new PipeFactory(child), TimeSpan.FromSeconds(5), "inert-version", TimeSpan.FromMilliseconds(30));
        try
        {
            var operation = converter.ConvertAsync(source, Path.Combine(temp.Root, "output.glb"));
            if (confirmStop)
            {
                var result = await operation.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.AreEqual("OCCT_EXCEPTION", result.ErrorCode);
                Assert.IsTrue(child.HasExited);
                Assert.IsFalse(result.ErrorMessage.Contains("fake-private-stream"));
            }
            else await Assert.ThrowsExceptionAsync<CadChildExitUnconfirmedException>(async () => await operation.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.AreEqual(1, child.StopCalls);
            Assert.IsTrue(child.Disposed);
        }
        finally { child.Exit(); child.ReleaseDrain(); }
    }

    [DataTestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task InterruptedDrain_IsFinishedAfterExitOrReportedIncomplete(bool timeout, bool confirmStop)
    {
        using var temp = new HardwareTestTemp();
        var source = Path.Combine(temp.Root, "source.step");
        await File.WriteAllBytesAsync(source, HardwareTestData.Step);
        using var caller = new CancellationTokenSource();
        var child = new PipeChild { KeepRunning = true, ConfirmStop = confirmStop, DrainWaitsForExit = true };
        var converter = new OcctCadViewerConverter(new PipeFactory(child), TimeSpan.FromMilliseconds(50), "inert-version", TimeSpan.FromMilliseconds(30));
        try
        {
            var operation = converter.ConvertAsync(source, Path.Combine(temp.Root, "output.glb"), caller.Token);
            await child.FirstWait.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (!timeout) caller.Cancel();
            if (!timeout)
            {
                var error = await Assert.ThrowsExceptionAsync<OperationCanceledException>(async () => await operation.WaitAsync(TimeSpan.FromSeconds(5)));
                Assert.AreEqual(caller.Token, error.CancellationToken);
                if (!confirmStop)
                {
                    Assert.IsInstanceOfType<CadChildExitUnconfirmedException>(error.InnerException);
                    Assert.AreEqual(true, error.Data["CadStandardErrorDrainIncomplete"]);
                }
            }
            else if (!confirmStop) await Assert.ThrowsExceptionAsync<CadChildExitUnconfirmedException>(async () => await operation.WaitAsync(TimeSpan.FromSeconds(5)));
            else Assert.AreEqual("OCCT_TIMEOUT", (await operation.WaitAsync(TimeSpan.FromSeconds(5))).ErrorCode);
            Assert.IsTrue(child.ReadStartedBeforeWait);
            Assert.AreEqual(confirmStop, child.ReadCompleted);
            Assert.AreEqual(confirmStop, child.HasExited);
            if (!confirmStop) Assert.IsTrue(child.DrainToken.IsCancellationRequested);
        }
        finally { child.Exit(); child.ReleaseDrain(); }
    }

    [TestMethod]
    public async Task ExitedChild_WithUnfinishedDrain_ReturnsBoundedFailure()
    {
        using var temp = new HardwareTestTemp();
        var source = Path.Combine(temp.Root, "source.step");
        await File.WriteAllBytesAsync(source, HardwareTestData.Step);
        var child = new PipeChild { NeverFinishDrain = true };
        var converter = new OcctCadViewerConverter(new PipeFactory(child), TimeSpan.FromSeconds(5), "inert-version", TimeSpan.FromMilliseconds(30));
        try
        {
            var result = await converter.ConvertAsync(source, Path.Combine(temp.Root, "output.glb")).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreEqual("OCCT_STDERR_INCOMPLETE", result.ErrorCode);
            Assert.IsTrue(child.HasExited);
            Assert.IsFalse(child.ReadCompleted);
            Assert.IsTrue(child.DrainToken.IsCancellationRequested);
        }
        finally { child.Exit(); child.ReleaseDrain(); }
    }

    private sealed class QualityRepository(string defect, bool cacheHit) : ICadAssetRepository
    {
        public VendorCadAsset Asset { get; } = new InertRepository().Asset with { PartNumber = defect == "source" ? "P2" : "P1" };
        public Task<VendorCadAsset?> FindByShaAsync(string sha256, CancellationToken cancellationToken = default) => Task.FromResult(cacheHit ? Asset : null);
        public Task<VendorCadAsset> StoreAsync(string partNumber, string sourceCadLink, string normalizedCadUrl, byte[] bytes, McMasterProductSnapshot? product, CancellationToken cancellationToken = default) => Task.FromResult(Asset);
        public Task<ViewerDerivative?> FindDerivativeAsync(string sourceSha256, CancellationToken cancellationToken = default) =>
            Task.FromResult(defect == "derivative" ? HardwareTestData.Derivative(Asset) with { SourceAssetId = "OTHER" } : null);
        public Task<ViewerDerivative> StoreDerivativeAsync(ViewerDerivative derivative, byte[] glbBytes, CancellationToken cancellationToken = default) => throw new AssertFailedException("Unexpected derivative write.");
    }

    private sealed class LifetimeHandler(string operation, string outcome) : HttpMessageHandler
    {
        private int _businessCalls;
        public bool Disposed { get; private set; }
        public bool PreviousDisposedBeforeNextRequest { get; private set; } = true;
        public List<HttpRequestMessage> Requests { get; } = new();
        public List<TrackedContent> Contents { get; } = new();
        public CancellationTokenSource? CancelAfterRateLimitDisposed { get; init; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Contents.Count > 0) PreviousDisposedBeforeNextRequest &= Contents[^1].Disposed;
            request.Content ??= new ByteArrayContent(Array.Empty<byte>());
            Requests.Add(request);
            var login = request.RequestUri!.AbsolutePath.EndsWith("/login", StringComparison.Ordinal);
            var active = operation == "login" || !login;
            var call = active ? ++_businessCalls : 0;
            var status = active ? outcome switch
            {
                "redirect" => HttpStatusCode.Redirect,
                "unauthorized" when call == 1 => HttpStatusCode.Unauthorized,
                "retry" when call == 1 => HttpStatusCode.TooManyRequests,
                "rate-limit" => HttpStatusCode.TooManyRequests,
                _ => HttpStatusCode.OK
            } : HttpStatusCode.OK;
            var body = active && outcome == "parse" ? "{fake-body-private" : login ? "{\"AuthToken\":\"fake-token\"}" : "{\"PartNumber\":\"P1\"}";
            var content = new TrackedContent(body);
            if (status == HttpStatusCode.TooManyRequests && CancelAfterRateLimitDisposed != null)
                content.OnDispose = CancelAfterRateLimitDisposed.Cancel;
            Contents.Add(content);
            var response = new HttpResponseMessage(status) { Content = content };
            if (status == HttpStatusCode.TooManyRequests) response.Headers.TryAddWithoutValidation("Retry-After", "0");
            return Task.FromResult(response);
        }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }

    private sealed class TrackedStream(string body) : MemoryStream(Encoding.UTF8.GetBytes(body))
    {
        public bool Disposed { get; private set; }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }

    private sealed class BodyHandler(HttpContent content) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            request.Content ??= new ByteArrayContent(Array.Empty<byte>());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }

    private sealed class BlockingContent : HttpContent
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Disposed { get; private set; }
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => SerializeToStreamAsync(stream, context, CancellationToken.None);
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            ReadStarted.TrySetResult();
            return _release.Task.WaitAsync(cancellationToken);
        }
        public void Release() => _release.TrySetResult();
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }

    private sealed class TrackedContent : StreamContent
    {
        public TrackedStream Stream { get; }
        public bool Disposed { get; private set; }
        public Action? OnDispose { get; set; }
        public TrackedContent(string body) : this(new TrackedStream(body)) { }
        private TrackedContent(TrackedStream stream) : base(stream) { Stream = stream; Headers.ContentType = new("application/json"); }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); OnDispose?.Invoke(); }
    }

    private sealed class PipeFactory(PipeChild child) : OcctCadViewerConverter.IProcessFactory
    {
        public bool IsAvailable => true;
        public int StartCalls { get; private set; }
        public OcctCadViewerConverter.IChildProcess Start(string sourceStepPath, string outputGlbPath) { StartCalls++; return child; }
    }

    private sealed class PipeChild : OcctCadViewerConverter.IChildProcess
    {
        private readonly TaskCompletionSource _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<string> _drain = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _readStarted;
        private int _waitCalls;
        public bool ExitRequiresDrain { get; init; }
        public bool ReadThrows { get; init; }
        public bool WaitThrows { get; init; }
        public bool KeepRunning { get; init; }
        public bool ConfirmStop { get; init; } = true;
        public bool DrainWaitsForExit { get; init; }
        public bool NeverFinishDrain { get; init; }
        public bool Disposed { get; private set; }
        public CancellationToken DrainToken { get; private set; }
        public TaskCompletionSource FirstWait { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool HasExited => _exit.Task.IsCompleted;
        public int ExitCode => 7;
        public bool ReadStartedBeforeWait { get; private set; }
        public bool ReadCompleted { get; private set; }
        public int StopCalls { get; private set; }
        public void StopTree() { StopCalls++; if (ConfirmStop) Exit(); }
        public async Task WaitForExitAsync(CancellationToken cancellationToken)
        {
            if (++_waitCalls == 1)
            {
                ReadStartedBeforeWait = _readStarted;
                FirstWait.TrySetResult();
                if (WaitThrows) throw new IOException("fake-private-stream");
            }
            if (!ExitRequiresDrain && !KeepRunning) Exit();
            await _exit.Task.WaitAsync(cancellationToken);
        }
        public Task<string> ReadStandardErrorAsync(CancellationToken cancellationToken)
        {
            _readStarted = true;
            DrainToken = cancellationToken;
            if (ReadThrows) throw new IOException("fake-private-stream");
            if (NeverFinishDrain) return _drain.Task;
            if (DrainWaitsForExit) return DrainUntilExitAsync();
            // Models output larger than a pipe buffer; draining is the only natural path to exit.
            if (ExitRequiresDrain) Exit();
            ReadCompleted = true;
            return Task.FromResult(new string('x', 1024 * 1024));
        }
        private async Task<string> DrainUntilExitAsync()
        {
            // Deliberately ignore cancellation to prove an unconfirmed child cannot hold up return forever.
            await _exit.Task;
            ReadCompleted = true;
            return "inert diagnostic";
        }
        public void Exit() => _exit.TrySetResult();
        public void ReleaseDrain() => _drain.TrySetResult("");
        public void Dispose() { Disposed = true; }
    }
}
