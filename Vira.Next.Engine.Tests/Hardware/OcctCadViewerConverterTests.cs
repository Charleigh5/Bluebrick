using Microsoft.VisualStudio.TestTools.UnitTesting;
using Vira.Next.Contracts;
using Vira.Next.Engine.Hardware;

namespace Vira.Next.Engine.Tests.Hardware;

[TestClass]
public sealed class OcctCadViewerConverterTests
{
    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task InterruptedChild_IsStoppedAndAwaitedBeforeReturn(bool timeout)
    {
        using var temp = new HardwareTestTemp();
        var source = Path.Combine(temp.Root, "source.step");
        var output = Path.Combine(temp.Root, "output.glb");
        await File.WriteAllBytesAsync(source, HardwareTestData.Step);
        using var caller = new CancellationTokenSource();
        var child = new InertOcctChild();
        var converter = Create(child, timeout);
        var operation = converter.ConvertAsync(source, output, caller.Token);
        await child.FirstWait.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(child.TryWriteOutput(), "Running fake child must claim output.");
        if (!timeout) caller.Cancel();
        try
        {
            await Task.WhenAny(child.DrainEntered.Task, operation).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsTrue(child.DrainEntered.Task.IsCompleted, "Exit must be awaited after interruption.");
            Assert.AreEqual(1, child.StopCalls);
            Assert.IsFalse(child.DrainToken.CanBeCanceled, "Exit wait must survive caller cancellation.");
            Assert.IsFalse(operation.IsCompleted, "Control escaped while the child still owns output.");
            Assert.IsTrue(child.TryWriteOutput(), "A stop request alone does not prove child exit.");
            child.Exit();
            await AssertInterrupted(operation, caller.Token, timeout);
            Assert.IsTrue(child.Disposed);
            Assert.IsTrue(child.DrainCompleted);
            Assert.IsFalse(child.TryWriteOutput(), "Exited child cannot recreate output after return.");
        }
        finally { child.Exit(); }
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task BindingCleanup_WaitsUntilInterruptedChildReleasesOutput(bool timeout)
    {
        using var caller = new CancellationTokenSource();
        var child = new InertOcctChild { HoldOutput = true };
        var repository = new InertRepository();
        var service = new HardwareCadBindingService(new InertProvider(), repository, Create(child, timeout), HardwareSecurityTests.Options());
        var operation = service.AcquireAsync(new HardwareRecord { HardwareRecordId = "HWD-TEST", McMasterPartNumber = "P1" }, caller.Token);
        await child.FirstWait.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (!timeout) caller.Cancel();
        try
        {
            await Task.WhenAny(child.DrainEntered.Task, operation).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsTrue(child.DrainEntered.Task.IsCompleted, "Binding cleanup must not race the owned child.");
            Assert.IsFalse(operation.IsCompleted);
            Assert.IsTrue(File.Exists(child.SourcePath));
            Assert.IsTrue(File.Exists(child.OutputPath));
            child.Exit();
            if (timeout)
            {
                var receipt = await operation;
                Assert.AreEqual("OCCT_TIMEOUT", receipt.Errors.Single().Code);
                Assert.IsNull(receipt.ViewerDerivative);
            }
            else
            {
                var error = await Assert.ThrowsExceptionAsync<OperationCanceledException>(async () => await operation);
                Assert.AreEqual(caller.Token, error.CancellationToken);
            }
            Assert.IsFalse(File.Exists(child.SourcePath));
            Assert.IsFalse(File.Exists(child.OutputPath));
            Assert.IsFalse(Directory.Exists(Path.GetDirectoryName(child.OutputPath)));
            Assert.AreEqual(0, repository.DerivativeWrites);
            Assert.IsFalse(child.TryWriteOutput());
        }
        finally { child.Exit(); }
    }

    [DataTestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public async Task BindingUnconfirmedExit_RetainsOwnedFilesAndInterruptionTruth(bool timeout, bool chained)
    {
        using var caller = new CancellationTokenSource();
        var child = new InertOcctChild { HoldOutput = true, StopThrows = true };
        var repository = new InertRepository();
        var fallback = new InertConverter();
        ICadViewerConverter converter = Create(child, timeout, TimeSpan.FromMilliseconds(30));
        if (chained) converter = new ChainedCadViewerConverter(new[] { converter, fallback });
        var service = new HardwareCadBindingService(new InertProvider(), repository,
            converter, HardwareSecurityTests.Options());
        try
        {
            var operation = service.AcquireAsync(new HardwareRecord { HardwareRecordId = "HWD-TEST", McMasterPartNumber = "P1" }, caller.Token);
            await child.FirstWait.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (!timeout) caller.Cancel();
            if (timeout)
            {
                var receipt = await operation.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.AreEqual("OCCT_STOP_FAILED", receipt.Errors.Single().Code);
                Assert.AreEqual(VendorCadAcquisitionStatus.Failed, receipt.Status);
                Assert.IsNull(receipt.ViewerDerivative);
                StringAssert.Contains(string.Join(" ", receipt.Limitations), "retained");
                StringAssert.Contains(string.Join(" ", receipt.Limitations), "not confirmed");
                Assert.IsFalse(System.Text.Json.JsonSerializer.Serialize(receipt).Contains("inert-private-stop-detail"));
            }
            else
            {
                var error = await Assert.ThrowsExceptionAsync<OperationCanceledException>(async () => await operation.WaitAsync(TimeSpan.FromSeconds(5)));
                Assert.AreEqual(caller.Token, error.CancellationToken);
                Assert.IsInstanceOfType<CadChildExitUnconfirmedException>(error.InnerException);
                Assert.AreEqual(Path.GetDirectoryName(child.OutputPath), error.Data["RetainedCadTempDirectory"]);
                Assert.IsFalse(error.ToString().Contains("inert-private-stop-detail"));
            }
            Assert.IsFalse(child.HasExited);
            Assert.AreEqual(1, child.StopCalls);
            Assert.IsTrue(child.DrainEntered.Task.IsCompleted);
            Assert.IsTrue(File.Exists(child.SourcePath), "Unsafe cleanup must not delete even the source.");
            Assert.IsTrue(File.Exists(child.OutputPath));
            Assert.IsTrue(Directory.Exists(Path.GetDirectoryName(child.OutputPath)));
            Assert.ThrowsException<IOException>(() => { using var probe = File.OpenRead(child.OutputPath); });
            Assert.IsTrue(child.TryWriteOutput(), "Unconfirmed child still owns its output after return.");
            Assert.AreEqual(0, repository.DerivativeWrites);
            Assert.AreEqual(0, fallback.Calls, "A chain must not reuse paths still owned by its child.");
        }
        finally
        {
            child.Exit();
            if (!string.IsNullOrEmpty(child.OutputPath)) Directory.Delete(Path.GetDirectoryName(child.OutputPath)!, recursive: true);
        }
    }

    [TestMethod]
    public async Task OrdinaryCleanupFailure_PreservesOriginalCancellation()
    {
        using var caller = new CancellationTokenSource();
        using var converter = new HeldFileCancellationConverter(caller);
        var service = new HardwareCadBindingService(new InertProvider(), new InertRepository(), converter, HardwareSecurityTests.Options());
        try
        {
            var error = await Assert.ThrowsExceptionAsync<OperationCanceledException>(() =>
                service.AcquireAsync(new HardwareRecord { HardwareRecordId = "HWD-TEST", McMasterPartNumber = "P1" }, caller.Token));
            Assert.AreSame(converter.Cancellation, error);
            Assert.AreEqual(caller.Token, error.CancellationToken);
            Assert.IsNotNull(error.Data["CadTempCleanupFailure"]);
            Assert.AreEqual(Path.GetDirectoryName(converter.OutputPath), error.Data["RetainedCadTempDirectory"]);
            Assert.ThrowsException<IOException>(() => { using var probe = File.OpenRead(converter.OutputPath); });
        }
        finally
        {
            converter.Dispose();
            if (!string.IsNullOrEmpty(converter.OutputPath)) Directory.Delete(Path.GetDirectoryName(converter.OutputPath)!, recursive: true);
        }
    }

    [DataTestMethod]
    [DataRow("success", "")]
    [DataRow("nonzero", "OCCT_NON_ZERO")]
    [DataRow("no-output", "OCCT_NO_OUTPUT")]
    [DataRow("invalid-glb", "GLB_INVALID")]
    public async Task CompletedChild_PreservesResultAndProvenance(string outcome, string errorCode)
    {
        using var temp = new HardwareTestTemp();
        var source = Path.Combine(temp.Root, "source.step");
        var output = Path.Combine(temp.Root, "output.glb");
        await File.WriteAllBytesAsync(source, HardwareTestData.Step);
        if (outcome != "no-output") await File.WriteAllBytesAsync(output, outcome == "invalid-glb" ? new byte[12] : HardwareTestData.Glb);
        var child = new InertOcctChild { Code = outcome == "nonzero" ? 7 : 0, StandardError = new string('x', 500) };
        child.Exit();
        var result = await Create(child).ConvertAsync(source, output);
        Assert.AreEqual(outcome == "success", result.Success);
        Assert.AreEqual(errorCode, result.ErrorCode);
        Assert.AreEqual("inert-test-version", result.ConverterVersion);
        Assert.AreEqual(0, child.StopCalls);
        Assert.IsTrue(child.Disposed);
        if (outcome == "success")
        {
            Assert.AreEqual(ContentAddressedCache.ComputeSha256(HardwareTestData.Step), result.SourceSha256);
            Assert.AreEqual(ContentAddressedCache.ComputeSha256(HardwareTestData.Glb), result.GlbSha256);
            Assert.AreEqual(HardwareTestData.Glb.Length, result.GlbByteLength);
        }
        if (outcome == "nonzero") Assert.AreEqual("OCCT exit 7: " + new string('x', 400) + "...", result.ErrorMessage);
    }

    [DataTestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task ExitRaces_AreAwaitedWithoutReplacingCancellation(bool timeout, bool exitDuringStop)
    {
        using var temp = new HardwareTestTemp();
        var source = Path.Combine(temp.Root, "source.step");
        await File.WriteAllBytesAsync(source, HardwareTestData.Step);
        using var caller = new CancellationTokenSource();
        var child = new InertOcctChild { ExitBeforeStop = !exitDuringStop, ExitDuringStop = exitDuringStop };
        var operation = Create(child, timeout).ConvertAsync(source, Path.Combine(temp.Root, "output.glb"), caller.Token);
        await child.FirstWait.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (!timeout) caller.Cancel();
        await AssertInterrupted(operation.WaitAsync(TimeSpan.FromSeconds(5)), caller.Token, timeout);
        Assert.AreEqual(exitDuringStop ? 1 : 0, child.StopCalls);
        Assert.IsTrue(child.DrainCompleted);
        Assert.IsFalse(child.TryWriteOutput());
    }

    [DataTestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task UnconfirmedExit_IsBoundedAndReportedWithoutLosingCallerToken(bool timeout, bool stopThrows)
    {
        using var temp = new HardwareTestTemp();
        var source = Path.Combine(temp.Root, "source.step");
        await File.WriteAllBytesAsync(source, HardwareTestData.Step);
        using var caller = new CancellationTokenSource();
        var child = new InertOcctChild { StopThrows = stopThrows };
        var operation = Create(child, timeout, TimeSpan.FromMilliseconds(30)).ConvertAsync(source, Path.Combine(temp.Root, "output.glb"), caller.Token);
        await child.FirstWait.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (!timeout) caller.Cancel();
        try
        {
            if (timeout)
            {
                var error = await Assert.ThrowsExceptionAsync<CadChildExitUnconfirmedException>(async () => await operation.WaitAsync(TimeSpan.FromSeconds(5)));
                StringAssert.Contains(error.Message, "OCCT_STOP_FAILED");
                StringAssert.Contains(error.Message, "output may still be in use");
                Assert.IsFalse(error.ToString().Contains("inert-private-stop-detail"));
            }
            else
            {
                var error = await Assert.ThrowsExceptionAsync<OperationCanceledException>(async () => await operation.WaitAsync(TimeSpan.FromSeconds(5)));
                Assert.AreEqual(caller.Token, error.CancellationToken);
                Assert.IsInstanceOfType<CadChildExitUnconfirmedException>(error.InnerException);
                StringAssert.Contains(error.Message, "OCCT_STOP_FAILED");
                Assert.IsFalse(error.ToString().Contains("inert-private-stop-detail"));
            }
            Assert.AreEqual(1, child.StopCalls);
            Assert.IsTrue(child.DrainEntered.Task.IsCompleted);
            Assert.IsFalse(child.DrainToken.CanBeCanceled);
            Assert.IsFalse(child.HasExited, "This case must not falsely claim child termination.");
        }
        finally { child.Exit(); }
    }

    [TestMethod]
    public async Task CallerCancellationDuringTimeoutDrain_TakesPrecedence()
    {
        using var temp = new HardwareTestTemp();
        var source = Path.Combine(temp.Root, "source.step");
        await File.WriteAllBytesAsync(source, HardwareTestData.Step);
        using var caller = new CancellationTokenSource();
        var child = new InertOcctChild();
        var operation = Create(child, timeout: true).ConvertAsync(source, Path.Combine(temp.Root, "output.glb"), caller.Token);
        await child.DrainEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        caller.Cancel();
        Assert.IsFalse(operation.IsCompleted);
        child.Exit();
        await AssertInterrupted(operation, caller.Token, timeout: false);
    }

    private static OcctCadViewerConverter Create(InertOcctChild child, bool timeout = false, TimeSpan? stopTimeout = null) =>
        new(new InertOcctFactory(child), timeout ? TimeSpan.FromMilliseconds(50) : TimeSpan.FromSeconds(30), "inert-test-version", stopTimeout);

    private static async Task AssertInterrupted(Task<ViewerConversionResult> operation, CancellationToken callerToken, bool timeout)
    {
        if (timeout)
        {
            var result = await operation;
            Assert.IsFalse(result.Success);
            Assert.AreEqual("OCCT_TIMEOUT", result.ErrorCode);
        }
        else
        {
            var error = await Assert.ThrowsExceptionAsync<OperationCanceledException>(async () => await operation);
            Assert.AreEqual(callerToken, error.CancellationToken);
        }
    }

    private sealed class HeldFileCancellationConverter(CancellationTokenSource caller) : ICadViewerConverter, IDisposable
    {
        private FileStream? _output;
        public string ConverterId => "inert-held-file";
        public string ConverterVersion => "test";
        public string OutputPath { get; private set; } = "";
        public OperationCanceledException? Cancellation { get; private set; }
        public Task<ViewerConversionResult> ConvertAsync(string sourceStepPath, string outputGlbPath, CancellationToken cancellationToken = default)
        {
            OutputPath = outputGlbPath;
            _output = new FileStream(outputGlbPath, FileMode.Create, FileAccess.Write, FileShare.None);
            caller.Cancel();
            Cancellation = new OperationCanceledException("Inert caller cancellation.", cancellationToken);
            return Task.FromException<ViewerConversionResult>(Cancellation);
        }
        public void Dispose() { _output?.Dispose(); _output = null; }
    }

    private sealed class InertOcctFactory(InertOcctChild child) : OcctCadViewerConverter.IProcessFactory
    {
        public bool IsAvailable => true;
        public OcctCadViewerConverter.IChildProcess Start(string sourceStepPath, string outputGlbPath)
        {
            child.SourcePath = sourceStepPath;
            child.OutputPath = outputGlbPath;
            child.ClaimOutput();
            return child;
        }
    }

    private sealed class InertOcctChild : OcctCadViewerConverter.IChildProcess
    {
        private readonly TaskCompletionSource _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private FileStream? _outputHandle;
        private int _waitCount;
        public TaskCompletionSource FirstWait { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource DrainEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool HoldOutput { get; init; }
        public bool ExitBeforeStop { get; init; }
        public bool ExitDuringStop { get; init; }
        public bool StopThrows { get; init; }
        public string SourcePath { get; set; } = "";
        public string OutputPath { get; set; } = "";
        public int Code { get; init; }
        public string StandardError { get; init; } = "";
        public int StopCalls { get; private set; }
        public bool Disposed { get; private set; }
        public bool DrainCompleted { get; private set; }
        public CancellationToken DrainToken { get; private set; }
        public bool HasExited => _exit.Task.IsCompleted;
        public int ExitCode => Code;
        public void ClaimOutput()
        {
            if (HoldOutput) _outputHandle = new FileStream(OutputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        }
        public bool TryWriteOutput()
        {
            if (HasExited) return false;
            if (_outputHandle != null) _outputHandle.WriteByte(0);
            else File.WriteAllBytes(OutputPath, new byte[] { 0 });
            return true;
        }
        public void StopTree()
        {
            StopCalls++;
            if (ExitDuringStop) Exit();
            if (StopThrows || ExitDuringStop) throw new InvalidOperationException("inert-private-stop-detail");
        }
        public async Task WaitForExitAsync(CancellationToken cancellationToken)
        {
            var first = Interlocked.Increment(ref _waitCount) == 1;
            if (first) FirstWait.TrySetResult();
            else { DrainToken = cancellationToken; DrainEntered.TrySetResult(); }
            try { await _exit.Task.WaitAsync(cancellationToken); }
            catch (OperationCanceledException) { if (first && ExitBeforeStop) Exit(); throw; }
            if (!first) DrainCompleted = true;
        }
        public Task<string> ReadStandardErrorAsync(CancellationToken cancellationToken) => Task.FromResult(StandardError);
        public void Exit() { _outputHandle?.Dispose(); _outputHandle = null; _exit.TrySetResult(); }
        // Disposing a process wrapper is deliberately not modeled as terminating its child.
        public void Dispose() { Disposed = true; }
    }
}
