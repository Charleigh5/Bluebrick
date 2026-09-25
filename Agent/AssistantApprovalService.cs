using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using BlueBrick.Audit.Core;

namespace BlueBrick.Agent
{
    internal enum ApprovalPromptOutcome
    {
        Approved = 0,
        Denied = 1,
        Timeout = 2
    }

    internal sealed class ApprovalPreview
    {
        internal ApprovalPreview(string title, string summary, string before, string after, string risk)
        {
            Title = title ?? string.Empty;
            Summary = summary ?? string.Empty;
            Before = before ?? string.Empty;
            After = after ?? string.Empty;
            Risk = risk ?? string.Empty;
        }

        internal string Title { get; }
        internal string Summary { get; }
        internal string Before { get; }
        internal string After { get; }
        internal string Risk { get; }
    }

    internal sealed class ApprovalPrompt
    {
        internal ApprovalPrompt(
            string requestId,
            string capabilityId,
            string traceId,
            string sessionId,
            string environment,
            DateTime expiresUtc,
            AssistantToolDescriptor descriptor,
            AssistantToolRequest request,
            ApprovalPreview preview,
            CancellationToken cancellationToken)
        {
            RequestId = requestId;
            CapabilityId = capabilityId;
            TraceId = traceId;
            SessionId = sessionId;
            Environment = environment;
            ExpiresUtc = expiresUtc;
            Descriptor = descriptor;
            Request = request;
            Preview = preview;
            CancellationToken = cancellationToken;
        }

        internal string RequestId { get; }
        internal string CapabilityId { get; }
        internal string TraceId { get; }
        internal string SessionId { get; }
        internal string Environment { get; }
        internal DateTime ExpiresUtc { get; }
        internal AssistantToolDescriptor Descriptor { get; }
        internal AssistantToolRequest Request { get; }
        internal ApprovalPreview Preview { get; }
        internal CancellationToken CancellationToken { get; }
    }

    internal interface IApprovalPrompt
    {
        Task<ApprovalPromptOutcome> ShowAsync(ApprovalPrompt prompt);
    }

    internal interface IPreviewBuilder
    {
        ApprovalPreview Build(AssistantToolDescriptor descriptor, AssistantToolRequest request);
    }

    internal interface IApprovalLedgerSink
    {
        void Append(AssistantApprovalLedgerEntry entry);
    }

    internal interface IApprovalLifecycleTelemetrySink
    {
        void Record(AssistantApprovalLedgerEntry entry, bool authorityIssued);
    }

    internal interface IApprovalTimeProvider
    {
        DateTime UtcNow { get; }
        Task Delay(TimeSpan delay, CancellationToken cancellationToken);
    }

    internal sealed class ApprovalLifecycleTelemetrySink : IApprovalLifecycleTelemetrySink
    {
        private readonly TelemetryLogger _telemetry;

        internal ApprovalLifecycleTelemetrySink(TelemetryLogger telemetry)
        {
            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        }

        public void Record(AssistantApprovalLedgerEntry entry, bool authorityIssued)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            _telemetry.LogRequiredEvent(
                "APPROVAL_LIFECYCLE",
                "assistant/approval",
                authorityIssued,
                0,
                new
                {
                    traceId = entry.TraceId,
                    sessionId = entry.SessionId,
                    approvalId = entry.ApprovalId,
                    capabilityId = entry.CapabilityId,
                    argumentDigest = entry.ArgumentDigest,
                    environment = entry.Environment,
                    lifecycleEvent = entry.Event,
                    outcome = entry.Outcome
                });
        }
    }

    internal sealed class SystemApprovalTimeProvider : IApprovalTimeProvider
    {
        public DateTime UtcNow => DateTime.UtcNow;

        public Task Delay(TimeSpan delay, CancellationToken cancellationToken)
        {
            return Task.Delay(delay, cancellationToken);
        }
    }

    internal sealed class AssistantApprovalService : IDisposable
    {
        private const int DefaultTimeoutSeconds = 60;
        private const int EvidenceIdentifierLimit = 128;
        private const string ApprovalReason = "Approved by trusted native dialog for one execution.";

        private readonly AgentConfig _config;
        private readonly IApprovalPrompt _prompt;
        private readonly IPreviewBuilder _previewBuilder;
        private readonly IApprovalLedgerSink _ledger;
        private readonly IApprovalLifecycleTelemetrySink _telemetry;
        private readonly IApprovalTimeProvider _time;
        private readonly bool _isLabBuild;
        private readonly SemaphoreSlim _singleFlight = new SemaphoreSlim(1, 1);
        private readonly object _stateSync = new object();
        private readonly TaskCompletionSource<bool> _shutdownSignal =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        private bool _disposed;
        private CancellationTokenSource _activePromptCancellation;

        internal AssistantApprovalService(
            AgentConfig config,
            IApprovalPrompt prompt,
            IPreviewBuilder previewBuilder,
            AssistantApprovalLedger ledger,
            TelemetryLogger telemetry)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
            _previewBuilder = previewBuilder ?? throw new ArgumentNullException(nameof(previewBuilder));
            _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            _telemetry = new ApprovalLifecycleTelemetrySink(telemetry ?? throw new ArgumentNullException(nameof(telemetry)));
            _time = new SystemApprovalTimeProvider();
            _isLabBuild = AppIdentity.IsLabBuild;
        }

#if DEBUG
        internal AssistantApprovalService(
            AgentConfig config,
            IApprovalPrompt prompt,
            IPreviewBuilder previewBuilder,
            IApprovalLedgerSink ledger,
            IApprovalLifecycleTelemetrySink telemetry,
            IApprovalTimeProvider time,
            bool isLabBuild)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
            _previewBuilder = previewBuilder ?? throw new ArgumentNullException(nameof(previewBuilder));
            _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _isLabBuild = isLabBuild;
        }
#endif

        internal async Task<AssistantToolAuthorization> RequestApprovalAsync(
            AssistantToolDescriptor descriptor,
            AssistantToolRequest request,
            string traceId)
        {
            ApprovalSnapshot snapshot;
            if (!TryCreateSnapshot(descriptor, request, traceId, out snapshot))
            {
                TryRecordTerminal(CreateFallbackEntry(descriptor, request, traceId, "invalid_request"));
                return null;
            }

            if (IsDisposed())
            {
                TryRecordTerminal(CreateEntry(snapshot, AssistantApprovalLedgerEntry.Denied, "service_disposed", null));
                return null;
            }

            if (!GateIsOpen())
            {
                TryRecordTerminal(CreateEntry(snapshot, AssistantApprovalLedgerEntry.Denied, "mutations_disabled", null));
                return null;
            }

            if (!_singleFlight.Wait(0))
            {
                TryRecordTerminal(CreateEntry(snapshot, AssistantApprovalLedgerEntry.Denied, "approval_in_progress", null));
                return null;
            }

            CancellationTokenSource promptCancellation = null;
            CancellationTokenSource delayCancellation = null;
            Task<ApprovalPromptOutcome> promptTask = null;
            try
            {
                if (IsDisposed())
                {
                    TryRecordTerminal(CreateEntry(snapshot, AssistantApprovalLedgerEntry.Denied, "service_disposed", null));
                    return null;
                }

                var admittedUtc = _time.UtcNow;
                ApprovalPreview preview;
                try
                {
                    preview = _previewBuilder.Build(CloneDescriptor(snapshot.Descriptor), CloneRequest(snapshot.Request));
                }
                catch
                {
                    TryRecordTerminal(CreateEntry(snapshot, AssistantApprovalLedgerEntry.Denied, "preview_failed", null));
                    return null;
                }

                if (preview == null)
                {
                    TryRecordTerminal(CreateEntry(snapshot, AssistantApprovalLedgerEntry.Denied, "preview_unavailable", null));
                    return null;
                }

                var timeoutSeconds = _config.Assistant?.Mutations?.ApprovalTimeoutSeconds ?? DefaultTimeoutSeconds;
                if (timeoutSeconds <= 0) timeoutSeconds = DefaultTimeoutSeconds;
                DateTime expiresUtc;
                try
                {
                    expiresUtc = admittedUtc.AddSeconds(timeoutSeconds);
                }
                catch (ArgumentOutOfRangeException)
                {
                    TryRecordTerminal(CreateEntry(snapshot, AssistantApprovalLedgerEntry.Denied, "invalid_timeout", null));
                    return null;
                }

                if (!TryRecordTransition(CreateEntry(snapshot, AssistantApprovalLedgerEntry.Requested, "pending", null)))
                {
                    return null;
                }

                promptCancellation = new CancellationTokenSource();
                delayCancellation = new CancellationTokenSource();
                var disposedBeforePrompt = false;
                lock (_stateSync)
                {
                    if (_disposed)
                    {
                        disposedBeforePrompt = true;
                    }
                    else
                    {
                        _activePromptCancellation = promptCancellation;
                    }
                }
                if (disposedBeforePrompt)
                {
                    TryRecordTerminal(CreateEntry(snapshot, AssistantApprovalLedgerEntry.Orphaned, "shutdown", null));
                    return null;
                }

                if (_time.UtcNow >= expiresUtc)
                {
                    TryRecordTerminal(CreateEntry(snapshot, AssistantApprovalLedgerEntry.Expired, "timeout", null));
                    return null;
                }

                var prompt = new ApprovalPrompt(
                    snapshot.RequestId,
                    snapshot.CapabilityId,
                    snapshot.TraceId,
                    snapshot.SessionId,
                    snapshot.Environment,
                    expiresUtc,
                    CloneDescriptor(snapshot.Descriptor),
                    CloneRequest(snapshot.Request),
                    ClonePreview(preview),
                    promptCancellation.Token);

                try
                {
                    promptTask = _prompt.ShowAsync(prompt);
                }
                catch
                {
                    TryRecordTerminal(IsDisposed()
                        ? CreateEntry(snapshot, AssistantApprovalLedgerEntry.Orphaned, "shutdown", null)
                        : CreateEntry(snapshot, AssistantApprovalLedgerEntry.Denied, "prompt_failed", null));
                    return null;
                }

                if (promptTask == null)
                {
                    TryRecordTerminal(IsDisposed()
                        ? CreateEntry(snapshot, AssistantApprovalLedgerEntry.Orphaned, "shutdown", null)
                        : CreateEntry(snapshot, AssistantApprovalLedgerEntry.Denied, "prompt_unavailable", null));
                    return null;
                }

                Task delayTask;
                try
                {
                    var remaining = expiresUtc - _time.UtcNow;
                    delayTask = _time.Delay(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero, delayCancellation.Token);
                }
                catch
                {
                    SignalCancellation(promptCancellation);
                    ObserveFault(promptTask);
                    TryRecordTerminal(IsDisposed()
                        ? CreateEntry(snapshot, AssistantApprovalLedgerEntry.Orphaned, "shutdown", null)
                        : CreateEntry(snapshot, AssistantApprovalLedgerEntry.Denied, "timeout_unavailable", null));
                    return null;
                }
                if (delayTask == null)
                {
                    SignalCancellation(promptCancellation);
                    ObserveFault(promptTask);
                    TryRecordTerminal(IsDisposed()
                        ? CreateEntry(snapshot, AssistantApprovalLedgerEntry.Orphaned, "shutdown", null)
                        : CreateEntry(snapshot, AssistantApprovalLedgerEntry.Denied, "timeout_unavailable", null));
                    return null;
                }

                var completed = await Task.WhenAny(promptTask, delayTask, _shutdownSignal.Task).ConfigureAwait(false);
                if (completed == _shutdownSignal.Task || IsDisposed())
                {
                    SignalCancellation(promptCancellation);
                    ObserveFault(promptTask);
                    TryRecordTerminal(CreateEntry(snapshot, AssistantApprovalLedgerEntry.Orphaned, "shutdown", null));
                    return null;
                }

                if (completed == delayTask)
                {
                    SignalCancellation(promptCancellation);
                    ObserveFault(promptTask);
                    TryRecordTerminal(CreateEntry(snapshot, AssistantApprovalLedgerEntry.Expired, "timeout", null));
                    return null;
                }

                SignalCancellation(delayCancellation);
                ApprovalPromptOutcome outcome;
                try
                {
                    outcome = await promptTask.ConfigureAwait(false);
                }
                catch
                {
                    TryRecordTerminal(CreateEntry(snapshot, AssistantApprovalLedgerEntry.Denied, "prompt_failed", null));
                    return null;
                }

                if (outcome == ApprovalPromptOutcome.Denied)
                {
                    TryRecordTerminal(CreateEntry(snapshot, AssistantApprovalLedgerEntry.Denied, "denied", null));
                    return null;
                }

                if (outcome == ApprovalPromptOutcome.Timeout)
                {
                    SignalCancellation(promptCancellation);
                    TryRecordTerminal(CreateEntry(snapshot, AssistantApprovalLedgerEntry.Expired, "timeout", null));
                    return null;
                }

                if (outcome != ApprovalPromptOutcome.Approved)
                {
                    TryRecordTerminal(CreateEntry(snapshot, AssistantApprovalLedgerEntry.Denied, "invalid_prompt_outcome", null));
                    return null;
                }

                AssistantToolAuthorization authorization = null;
                AssistantApprovalLedgerEntry nonIssuedTerminal = null;
                lock (_stateSync)
                {
                    if (_disposed)
                    {
                        nonIssuedTerminal = CreateEntry(snapshot, AssistantApprovalLedgerEntry.Orphaned, "shutdown", null);
                    }
                    else if (!GateIsOpen())
                    {
                        nonIssuedTerminal = CreateEntry(snapshot, AssistantApprovalLedgerEntry.Denied, "mutations_disabled", null);
                    }
                    else if (_time.UtcNow >= expiresUtc)
                    {
                        nonIssuedTerminal = CreateEntry(snapshot, AssistantApprovalLedgerEntry.Expired, "timeout", null);
                    }
                    else
                    {
                        try
                        {
                            authorization = AssistantToolAuthorization.CreateServerApproval(
                                snapshot.RequestId,
                                snapshot.CapabilityId,
                                snapshot.Request,
                                snapshot.SessionId,
                                snapshot.Environment,
                                "native_dialog",
                                ApprovalReason,
                                expiresUtc);
                        }
                        catch (ArgumentOutOfRangeException)
                        {
                            nonIssuedTerminal = CreateEntry(snapshot, AssistantApprovalLedgerEntry.Expired, "timeout", null);
                        }

                        if (authorization != null)
                        {
                            var issued = CreateEntry(snapshot, AssistantApprovalLedgerEntry.Issued, "issued", authorization.ApprovalId);
                            if (!TryRecordTransition(issued))
                            {
                                authorization = null;
                            }
                        }
                    }
                }

                if (nonIssuedTerminal != null)
                {
                    if (nonIssuedTerminal.Event == AssistantApprovalLedgerEntry.Expired)
                    {
                        SignalCancellation(promptCancellation);
                    }
                    TryRecordTerminal(nonIssuedTerminal);
                    return null;
                }

                return authorization;
            }
            finally
            {
                if (promptTask != null && !promptTask.IsCompleted)
                {
                    ObserveFault(promptTask);
                }
                SignalCancellation(delayCancellation);
                lock (_stateSync)
                {
                    if (ReferenceEquals(_activePromptCancellation, promptCancellation))
                    {
                        _activePromptCancellation = null;
                    }
                }
                _singleFlight.Release();
            }
        }

        public void Dispose()
        {
            CancellationTokenSource activePrompt;
            lock (_stateSync)
            {
                if (_disposed) return;
                _disposed = true;
                activePrompt = _activePromptCancellation;
                _shutdownSignal.TrySetResult(true);
            }

            SignalCancellation(activePrompt);
        }

        private bool GateIsOpen()
        {
            return _isLabBuild && (_config.Assistant?.Mutations?.Enabled ?? false);
        }

        private bool IsDisposed()
        {
            lock (_stateSync)
            {
                return _disposed;
            }
        }

        private bool TryRecordTerminal(AssistantApprovalLedgerEntry entry)
        {
            return TryRecordTransition(entry);
        }

        private bool TryRecordTransition(AssistantApprovalLedgerEntry entry)
        {
            try
            {
                _ledger.Append(entry);
            }
            catch
            {
                return false;
            }

            try
            {
                _telemetry.Record(entry, string.Equals(entry.Event, AssistantApprovalLedgerEntry.Issued, StringComparison.Ordinal));
                return true;
            }
            catch
            {
                return false;
            }
        }

        private AssistantApprovalLedgerEntry CreateEntry(
            ApprovalSnapshot snapshot,
            string lifecycleEvent,
            string outcome,
            string approvalId)
        {
            return new AssistantApprovalLedgerEntry
            {
                TimestampUtc = _time.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                TraceId = Evidence(snapshot.TraceId),
                SessionId = Evidence(snapshot.SessionId),
                ApprovalId = string.IsNullOrWhiteSpace(approvalId) ? null : Evidence(approvalId),
                CapabilityId = Evidence(snapshot.CapabilityId),
                ArgumentDigest = snapshot.ArgumentDigest,
                Environment = Evidence(snapshot.Environment),
                Event = lifecycleEvent,
                Outcome = outcome
            };
        }

        private AssistantApprovalLedgerEntry CreateFallbackEntry(
            AssistantToolDescriptor descriptor,
            AssistantToolRequest request,
            string traceId,
            string outcome)
        {
            var capability = descriptor?.CapabilityId;
            if (string.IsNullOrWhiteSpace(capability)) capability = descriptor?.Name;
            if (string.IsNullOrWhiteSpace(capability)) capability = "unknown";
            return new AssistantApprovalLedgerEntry
            {
                TimestampUtc = _time.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                TraceId = Evidence(traceId),
                SessionId = Evidence(request?.SessionId),
                CapabilityId = Evidence(capability),
                Environment = Evidence(_isLabBuild ? "Lab" : "Production"),
                Event = AssistantApprovalLedgerEntry.Denied,
                Outcome = outcome
            };
        }

        private bool TryCreateSnapshot(
            AssistantToolDescriptor descriptor,
            AssistantToolRequest request,
            string traceId,
            out ApprovalSnapshot snapshot)
        {
            snapshot = null;
            if (descriptor == null || request == null) return false;

            var requestId = string.IsNullOrWhiteSpace(request.RequestId) ? traceId : request.RequestId;
            if (descriptor.CapabilityId != null && string.IsNullOrWhiteSpace(descriptor.CapabilityId)) return false;
            var capabilityId = string.IsNullOrWhiteSpace(descriptor.CapabilityId) ? descriptor.Name : descriptor.CapabilityId;
            if (string.IsNullOrWhiteSpace(requestId) || string.IsNullOrWhiteSpace(capabilityId)) return false;

            var descriptorCopy = CloneDescriptor(descriptor);
            var requestCopy = CloneRequest(request);
            requestCopy.RequestId = requestId;
            requestCopy.Environment = _isLabBuild ? "Lab" : "Production";
            requestCopy.Authorization = AssistantToolAuthorization.None();

            snapshot = new ApprovalSnapshot
            {
                Descriptor = descriptorCopy,
                Request = requestCopy,
                RequestId = requestId,
                CapabilityId = capabilityId,
                TraceId = traceId ?? string.Empty,
                SessionId = requestCopy.SessionId ?? string.Empty,
                Environment = requestCopy.Environment,
                ArgumentDigest = AssistantToolAuthorization.ComputeArgumentDigest(requestCopy)
            };
            return true;
        }

        private static AssistantToolDescriptor CloneDescriptor(AssistantToolDescriptor source)
        {
            if (source == null) return null;
            return new AssistantToolDescriptor
            {
                Name = source.Name,
                CapabilityId = source.CapabilityId,
                DisplayName = source.DisplayName,
                Category = source.Category,
                Description = source.Description,
                ReadOnly = source.ReadOnly,
                RequiresConfirmation = source.RequiresConfirmation,
                Enabled = source.Enabled,
                UnavailableReason = source.UnavailableReason,
                RiskLevel = source.RiskLevel,
                AuditRequired = source.AuditRequired,
                AllowedInChat = source.AllowedInChat,
                ManualOnly = source.ManualOnly,
                Mutating = source.Mutating,
                MutatesCad = source.MutatesCad,
                MutatesPdm = source.MutatesPdm,
                MutatesEpicor = source.MutatesEpicor,
                SendsExternalData = source.SendsExternalData,
                RequiresCredential = source.RequiresCredential,
                FilesystemAccess = source.FilesystemAccess,
                GenerationCapability = source.GenerationCapability,
                ApprovalPolicy = source.ApprovalPolicy,
                AllowedEnvironments = source.AllowedEnvironments == null ? null : (string[])source.AllowedEnvironments.Clone(),
                ProductionAllowed = source.ProductionAllowed,
                ExecutionBoundary = source.ExecutionBoundary,
                AllowedModes = source.AllowedModes == null ? null : (string[])source.AllowedModes.Clone(),
                FailureMode = source.FailureMode
            };
        }

        private static AssistantToolRequest CloneRequest(AssistantToolRequest source)
        {
            if (source == null) return null;
            return new AssistantToolRequest
            {
                ToolName = source.ToolName,
                Query = source.Query,
                Limit = source.Limit,
                ScopeId = source.ScopeId,
                SessionId = source.SessionId,
                RequestId = source.RequestId,
                Environment = source.Environment,
                Authorization = AssistantToolAuthorization.None(),
                Parameters = source.Parameters == null
                    ? new Dictionary<string, string>()
                    : new Dictionary<string, string>(source.Parameters, StringComparer.Ordinal)
            };
        }

        private static ApprovalPreview ClonePreview(ApprovalPreview source)
        {
            return source == null
                ? null
                : new ApprovalPreview(source.Title, source.Summary, source.Before, source.After, source.Risk);
        }

        private static string Evidence(string value)
        {
            var redacted = AuditRedactionService.RedactSecrets(value ?? string.Empty);
            return redacted.Length <= EvidenceIdentifierLimit
                ? redacted
                : redacted.Substring(0, EvidenceIdentifierLimit);
        }

        private static void SignalCancellation(CancellationTokenSource source)
        {
            if (source == null || source.IsCancellationRequested) return;
            try
            {
                // Callers hold no state lock: publish cancellation before releasing admission.
                source.Cancel(false);
            }
            catch
            {
                // Callback faults cannot reverse shutdown or prevent observing cancellation.
            }
        }

        private static void ObserveFault(Task task)
        {
            if (task == null) return;
            task.ContinueWith(
                completed =>
                {
                    var ignored = completed.Exception;
                },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private sealed class ApprovalSnapshot
        {
            internal AssistantToolDescriptor Descriptor { get; set; }
            internal AssistantToolRequest Request { get; set; }
            internal string RequestId { get; set; }
            internal string CapabilityId { get; set; }
            internal string TraceId { get; set; }
            internal string SessionId { get; set; }
            internal string Environment { get; set; }
            internal string ArgumentDigest { get; set; }
        }
    }
}
