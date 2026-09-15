using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Vira.Next.Contracts;

namespace Vira.Next.Engine.Hardware;

public sealed class OcctCadViewerConverter : ICadViewerConverter
{
    private readonly string? _binaryPath;
    private readonly TimeSpan _timeout;
    private readonly IProcessFactory _processFactory;
    private readonly string? _converterVersion;
    private readonly TimeSpan _stopTimeout = TimeSpan.FromSeconds(10);

    public string ConverterId => "vira.occt-step-to-glb.v1";
    public string ConverterVersion => _converterVersion ?? ResolveVersion();

    public OcctCadViewerConverter(string? binaryPath = null, TimeSpan? timeout = null)
    {
        _binaryPath = binaryPath ?? Environment.GetEnvironmentVariable("OCCT_CONVERTER_PATH") ?? FindDefault();
        _timeout = timeout ?? TimeSpan.FromSeconds(int.TryParse(Environment.GetEnvironmentVariable("OCCT_CONVERTER_TIMEOUT_SECONDS"), out var s) ? s : 60);
        ValidateTimeout(_timeout, nameof(timeout));
        _processFactory = new SystemProcessFactory(_binaryPath);
    }

    // Explicit dependencies keep lifecycle tests independent of executable discovery and environment configuration.
    public OcctCadViewerConverter(IProcessFactory processFactory, TimeSpan timeout, string converterVersion, TimeSpan? stopTimeout = null)
    {
        _processFactory = processFactory ?? throw new ArgumentNullException(nameof(processFactory));
        _timeout = timeout;
        _converterVersion = converterVersion ?? throw new ArgumentNullException(nameof(converterVersion));
        _stopTimeout = stopTimeout ?? TimeSpan.FromSeconds(10);
        ValidateTimeout(_timeout, nameof(timeout));
        ValidateTimeout(_stopTimeout, nameof(stopTimeout));
    }

    public interface IProcessFactory
    {
        bool IsAvailable { get; }
        IChildProcess? Start(string sourceStepPath, string outputGlbPath);
    }

    public interface IChildProcess : IDisposable
    {
        bool HasExited { get; }
        int ExitCode { get; }
        void StopTree();
        Task WaitForExitAsync(CancellationToken cancellationToken);
        Task<string> ReadStandardErrorAsync(CancellationToken cancellationToken);
    }

    private sealed class SystemProcessFactory(string? binaryPath) : IProcessFactory
    {
        public bool IsAvailable => !string.IsNullOrWhiteSpace(binaryPath) && File.Exists(binaryPath);
        public IChildProcess? Start(string sourceStepPath, string outputGlbPath)
        {
            var psi = new ProcessStartInfo(binaryPath!, $"\"{sourceStepPath}\" \"{outputGlbPath}\"") { UseShellExecute = false, RedirectStandardOutput = false, RedirectStandardError = true, CreateNoWindow = true };
            var process = Process.Start(psi);
            return process == null ? null : new SystemChildProcess(process);
        }
    }

    private sealed class SystemChildProcess(Process process) : IChildProcess
    {
        public bool HasExited => process.HasExited;
        public int ExitCode => process.ExitCode;
        public void StopTree() => process.Kill(entireProcessTree: true);
        public Task WaitForExitAsync(CancellationToken cancellationToken) => process.WaitForExitAsync(cancellationToken);
        public async Task<string> ReadStandardErrorAsync(CancellationToken cancellationToken)
        {
            // Keep draining after the retained diagnostic fills, so the child can finish writing.
            var buffer = new char[4096];
            var retained = new StringBuilder(400);
            var truncated = false;
            int count;
            while ((count = await process.StandardError.ReadAsync(buffer.AsMemory(), cancellationToken)) != 0)
            {
                var keep = Math.Min(count, 400 - retained.Length);
                retained.Append(buffer, 0, keep);
                truncated |= keep < count;
            }
            return retained + (truncated ? "..." : string.Empty);
        }
        public void Dispose() => process.Dispose();
    }

    public async Task<ViewerConversionResult> ConvertAsync(string sourceStepPath, string outputGlbPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourceStepPath))
            return Fail("SOURCE_NOT_FOUND", $"Source STEP not found: {sourceStepPath}");
        if (!_processFactory.IsAvailable)
            return Fail("OCCT_NOT_AVAILABLE", $"OCCT binary not found: {_binaryPath ?? "(null)"} — set OCCT_CONVERTER_PATH");
        var bytes = await File.ReadAllBytesAsync(sourceStepPath, cancellationToken);
        if (bytes.Length == 0) return Fail("EMPTY_SOURCE", "Source STEP is empty.");
        var sourceSha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var dir = Path.GetDirectoryName(outputGlbPath);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
        try
        {
            // Construct all timer/token state before ownership of a child begins.
            using var timeoutCts = new CancellationTokenSource(_timeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            using var drainCts = new CancellationTokenSource();
            cancellationToken.ThrowIfCancellationRequested();
            using var proc = _processFactory.Start(sourceStepPath, outputGlbPath);
            if (proc == null) return Fail("OCCT_SPAWN_FAILED", "Failed to start OCCT process.");
            Task<string>? errorDrain = null;
            string? standardError;
            try
            {
                errorDrain = proc.ReadStandardErrorAsync(drainCts.Token);
                await proc.WaitForExitAsync(linked.Token);
                standardError = await FinishErrorDrainAsync(errorDrain, true, drainCts, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                var stopped = await StopAndAwaitExitAsync(proc);
                standardError = await FinishErrorDrainAsync(errorDrain, stopped, drainCts);
                var stopFailure = stopped ? null : new CadChildExitUnconfirmedException();
                // The linked token is an implementation detail; caller cancellation retains its own identity.
                // Caller cancellation also wins if it arrives while a timed-out child is being stopped.
                if (cancellationToken.IsCancellationRequested)
                {
                    var cancelled = new OperationCanceledException(stopFailure?.Message ?? "OCCT conversion cancelled.", stopFailure, cancellationToken);
                    if (standardError == null) cancelled.Data["CadStandardErrorDrainIncomplete"] = true;
                    throw cancelled;
                }
                if (stopFailure != null) throw stopFailure;
                if (timeoutCts.IsCancellationRequested) return Fail("OCCT_TIMEOUT", $"OCCT timed out after {_timeout.TotalSeconds}s" + (standardError == null ? "; stderr drain completion was not confirmed." : string.Empty));
                throw;
            }
            catch (Exception)
            {
                // Includes synchronous drain/wait setup failures after Start, before the first await.
                var stopped = await StopAndAwaitExitAsync(proc);
                await FinishErrorDrainAsync(errorDrain, stopped, drainCts);
                var stopFailure = stopped ? null : new CadChildExitUnconfirmedException();
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException(stopFailure?.Message ?? "OCCT conversion cancelled.", stopFailure, cancellationToken);
                if (stopFailure != null) throw stopFailure;
                return Fail("OCCT_EXCEPTION", "OCCT process observation failed after confirmed child exit.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (standardError == null) return Fail("OCCT_STDERR_INCOMPLETE", "Child exited, but stderr drain completion was not confirmed.");
            if (proc.ExitCode != 0)
            {
                return Fail("OCCT_NON_ZERO", $"OCCT exit {proc.ExitCode}: {SanitizeDiagnostic(Truncate(standardError, 400))}");
            }
            if (!File.Exists(outputGlbPath)) return Fail("OCCT_NO_OUTPUT", "OCCT produced no output file.");
            var glbBytes = await File.ReadAllBytesAsync(outputGlbPath, cancellationToken);
            var (valid, errMsg) = GlbValidator.Validate(glbBytes);
            if (!valid) return Fail("GLB_INVALID", errMsg);
            var glbSha = Convert.ToHexString(SHA256.HashData(glbBytes)).ToLowerInvariant();
            return new ViewerConversionResult { Success = true, ConverterId = ConverterId, ConverterVersion = ConverterVersion, SourceSha256 = sourceSha, GlbSha256 = glbSha, GlbByteLength = glbBytes.Length };
        }
        catch (OperationCanceledException) { throw; }
        catch (CadChildExitUnconfirmedException) { throw; }
        catch (Exception) { return Fail("OCCT_EXCEPTION", "OCCT conversion failed."); }
    }

    private ViewerConversionResult Fail(string code, string message) => new() { Success = false, ConverterId = ConverterId, ConverterVersion = ConverterVersion, ErrorCode = code, ErrorMessage = message };

    private static void ValidateTimeout(TimeSpan timeout, string name)
    {
        if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > uint.MaxValue - 1d)
            throw new ArgumentOutOfRangeException(name, "Timeout must be positive and within the supported timer range.");
    }

    private async Task<string?> FinishErrorDrainAsync(Task<string>? drain, bool exited, CancellationTokenSource drainCts, CancellationToken cancellationToken = default)
    {
        if (drain == null) return null;
        if (exited)
        {
            try { return await drain.WaitAsync(_stopTimeout, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception) { /* Report incomplete drainage; do not expose raw stream errors. */ }
        }
        // Unconfirmed exit must not wait on a child that can keep its pipe open indefinitely.
        try { drainCts.Cancel(); }
        catch (AggregateException) { /* A failing cancellation callback cannot replace child ownership truth. */ }
        _ = drain.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return null;
    }

    private static string SanitizeDiagnostic(string value) => new(value.Select(c => char.IsControl(c) ? ' ' : c).ToArray());

    private async Task<bool> StopAndAwaitExitAsync(IChildProcess process)
    {
        try { if (!process.HasExited) process.StopTree(); }
        catch (Exception) { /* An exit racing StopTree is resolved by the exit wait below. */ }
        try
        {
            // Never reuse the cancelled caller/timeout token for owned-child cleanup.
            // A separate deadline reports an unconfirmed exit instead of hanging or claiming timeout cleanup succeeded.
            await process.WaitForExitAsync(CancellationToken.None).WaitAsync(_stopTimeout);
            return process.HasExited;
        }
        catch (Exception) { return false; }
    }

    private static string? FindDefault()
    {
        var candidates = new[] { "occt-step-to-glb", "occt-step-to-glb.exe", Path.Combine(AppContext.BaseDirectory, "occt-step-to-glb.exe"), "/usr/local/bin/occt-step-to-glb" };
        foreach (var c in candidates) { try { if (File.Exists(c)) return Path.GetFullPath(c); } catch { } }
        return null;
    }

    private static string ResolveVersion()
    {
        var v = Environment.GetEnvironmentVariable("OCCT_CONVERTER_VERSION");
        if (!string.IsNullOrWhiteSpace(v)) return v;
        return "occt-7.8.1+vira1.0.1";
    }

    private static string Truncate(string v, int max) => v.Length <= max ? v : v[..max] + "...";
}
