using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BlueBrick.Audit.Contracts;
using BlueBrick.Audit.Core;
using BlueBrick.SolidWorks.Adapters;
using BlueBrick.SolidWorks.Runtime;
using BlueBrick.SolidWorks.Snapshots;
using Newtonsoft.Json;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System.Windows.Forms;

namespace BlueBrick.Agent
{
    // Minimal mutation-lifecycle telemetry sink (Sprint 04 contract C6). Tests inject a fake;
    // production wraps TelemetryLogger on the required (unsampled) path.
    internal interface IMutationTelemetrySink
    {
        void Record(string terminal, bool success, double durationMs, object metadata);
    }

    internal sealed class MutationTelemetryLoggerSink : IMutationTelemetrySink
    {
        private readonly TelemetryLogger _telemetry;

        internal MutationTelemetryLoggerSink(TelemetryLogger telemetry)
        {
            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        }

        public void Record(string terminal, bool success, double durationMs, object metadata)
        {
            _telemetry.LogRequiredEvent("MUTATION_LIFECYCLE", "assistant/mutation", success, durationMs, metadata);
        }
    }

    internal enum MutationNativeKind
    {
        Unsupported = 0,
        Text = 1
    }

    internal sealed class MutationAdmissionQuery
    {
        internal string TargetFullPath { get; set; }
        internal string PropertyName { get; set; }
        internal string CorrelationId { get; set; }
        internal int ConfigurationReadLimit { get; set; }
    }

    internal sealed class MutationAdmissionProof
    {
        internal bool Found { get; set; }
        internal string Literal { get; set; }
        internal string RawValue { get; set; }
        internal string ResolvedValue { get; set; }
        internal bool WasResolved { get; set; }
        internal MutationNativeKind NativeKind { get; set; }
        internal bool IsLinkedOrExpression { get; set; }
        internal bool IsEditable { get; set; }
        internal List<string> Configurations { get; set; }
        internal string ActiveConfiguration { get; set; }
        internal bool IsClean { get; set; }
        internal bool IsReadOnly { get; set; }
        internal string DocumentPath { get; set; }
        // Executor-side annotation (disk hash at capture time), not a CAD fact. Set by the
        // executor around proof captures for stability comparisons across reads.
        internal string DiskHashForExecutor { get; set; }
    }

    // Lower COM-primitive seam for the owned admission reader (Sprint 04 contract C4/D14).
    // Every member fails closed distinctly: absence (false/empty) is never conflated with
    // read failure (throw), and no boolean is ever defaulted on exception. Production
    // SwComSource maps installed interop (Lab-smoke-gated); tests hand-fake this interface.
    internal interface ISwMutationComSource
    {
        string Scope { get; }
        bool ContainsProperty(string name);
        bool TryGetText(string name, out string rawValue, out string resolvedValue, out bool wasResolved, out int nativeType, out bool linkedOrExpression, out string error);
        bool TryGetEditable(string name, out bool editable, out string error);
        IReadOnlyList<string> GetConfigurationNames();
        string GetActiveConfigurationName();
        bool GetDirtyFlag();
        bool GetReadOnlyFlag();
        string GetDocumentPath();
    }

    // Owned mutation-admission reader dependency (D14): closed proof vocabulary over the
    // primitive seam, independent of the accepted general adapter/builder (whose open string
    // vocabulary is never trusted for admission facts).
    internal interface IMutationAdmissionReader
    {
        bool TryProveTarget(MutationAdmissionQuery query, out MutationAdmissionProof proof, out string error);
    }

    internal sealed class SwComAdmissionReader : IMutationAdmissionReader
    {
        private readonly ISolidWorksMainThreadDispatcher _dispatcher;
        private readonly Func<string, ISwMutationComSource> _sourceFactory;

        internal SwComAdmissionReader(ISolidWorksMainThreadDispatcher dispatcher, Func<string, ISwMutationComSource> sourceFactory)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _sourceFactory = sourceFactory ?? throw new ArgumentNullException(nameof(sourceFactory));
        }

        public bool TryProveTarget(MutationAdmissionQuery query, out MutationAdmissionProof proof, out string error)
        {
            proof = null;
            error = null;
            if (query == null || string.IsNullOrWhiteSpace(query.PropertyName))
            {
                error = "invalid admission query";
                return false;
            }
            try
            {
                _dispatcher.VerifyAccess();
            }
            catch (Exception ex)
            {
                error = "thread affinity: " + ex.GetType().Name;
                return false;
            }
            string wanted = query.PropertyName.Trim().ToLowerInvariant();
            ISwMutationComSource document;
            try
            {
                document = _sourceFactory(string.Empty);
            }
            catch (Exception ex)
            {
                error = "source failure: " + ex.GetType().Name;
                return false;
            }
            if (document == null)
            {
                error = "source unavailable";
                return false;
            }
            IReadOnlyList<string> configurations;
            try
            {
                configurations = document.GetConfigurationNames() ?? new List<string>();
            }
            catch (Exception ex)
            {
                error = "enumeration unavailable: " + ex.GetType().Name;
                return false;
            }
            if (configurations.Count > Math.Max(1, query.ConfigurationReadLimit))
            {
                error = "configuration enumeration truncated";
                return false;
            }
            string raw;
            string resolved;
            bool wasResolved;
            int nativeType;
            bool linked;
            string readError;
            bool found;
            try
            {
                found = document.TryGetText(query.PropertyName, out raw, out resolved, out wasResolved, out nativeType, out linked, out readError);
            }
            catch (Exception ex)
            {
                error = "read failure: " + ex.GetType().Name;
                return false;
            }
            if (!string.IsNullOrEmpty(readError))
            {
                error = "read failure: " + readError;
                return false;
            }
            if (!found)
            {
                error = "property absent";
                return false;
            }
            if (nativeType != (int)swCustomInfoType_e.swCustomInfoText)
            {
                error = "unsupported native type";
                return false;
            }
            if (linked)
            {
                error = "linked or expression value";
                return false;
            }
            bool editable;
            string flagError;
            try
            {
                if (!document.TryGetEditable(query.PropertyName, out editable, out flagError))
                {
                    error = "editability unknown";
                    return false;
                }
            }
            catch (Exception ex)
            {
                error = "editability unknown: " + ex.GetType().Name;
                return false;
            }
            if (!string.IsNullOrEmpty(flagError))
            {
                error = "editability unknown";
                return false;
            }
            if (!editable)
            {
                error = "not editable";
                return false;
            }
            foreach (var configuration in configurations)
            {
                ISwMutationComSource scope;
                try
                {
                    scope = _sourceFactory(configuration ?? string.Empty);
                }
                catch (Exception ex)
                {
                    error = "scope failure: " + ex.GetType().Name;
                    return false;
                }
                if (scope == null)
                {
                    error = "scope unavailable";
                    return false;
                }
                bool present;
                try
                {
                    present = scope.ContainsProperty(query.PropertyName);
                }
                catch (Exception ex)
                {
                    error = "scope failure: " + ex.GetType().Name;
                    return false;
                }
                if (present)
                {
                    error = "same-name override in configuration scope";
                    return false;
                }
            }
            bool dirty;
            bool readOnly;
            string documentPath;
            string activeConfiguration;
            try
            {
                dirty = document.GetDirtyFlag();
                readOnly = document.GetReadOnlyFlag();
                documentPath = document.GetDocumentPath();
                activeConfiguration = document.GetActiveConfigurationName();
            }
            catch (Exception ex)
            {
                error = "status unknown: " + ex.GetType().Name;
                return false;
            }
            proof = new MutationAdmissionProof
            {
                Found = true,
                Literal = resolved,
                RawValue = raw,
                ResolvedValue = resolved,
                WasResolved = wasResolved,
                NativeKind = MutationNativeKind.Text,
                IsLinkedOrExpression = false,
                IsEditable = true,
                Configurations = new List<string>(configurations),
                ActiveConfiguration = activeConfiguration ?? string.Empty,
                IsClean = !dirty,
                IsReadOnly = readOnly,
                DocumentPath = documentPath ?? string.Empty
            };
            return true;
        }
    }

    // Production COM-primitive source over the retained model (Lab-smoke-gated call layer;
    // the mapping above is headless-tested through fakes). Every method fails closed: throws
    // on COM failure, never defaulted booleans; absence is membership-tested, never inferred
    // from empty strings.
    internal sealed class SwComSource : ISwMutationComSource
    {
        private readonly IModelDoc2 _model;
        private readonly string _configuration;

        internal SwComSource(IModelDoc2 model, string configuration)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _configuration = configuration ?? string.Empty;
        }

        public string Scope
        {
            get { return string.IsNullOrEmpty(_configuration) ? "Document" : "Configuration"; }
        }

        public bool ContainsProperty(string name)
        {
            var manager = Manager();
            if (manager == null) throw new InvalidOperationException("No property manager.");
            var names = manager.GetNames() as string[];
            if (names == null) return false;
            foreach (var candidate in names)
            {
                if (string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        internal static bool MapCustomInfoGetStatus(int status, out bool absent, out string error)
        {
            absent = false;
            error = null;
            if (status == (int)swCustomInfoGetResult_e.swCustomInfoGetResult_CachedValue
                || status == (int)swCustomInfoGetResult_e.swCustomInfoGetResult_ResolvedValue)
            {
                return true;
            }
            if (status == (int)swCustomInfoGetResult_e.swCustomInfoGetResult_NotPresent)
            {
                absent = true;
                error = "property absent";
                return false;
            }
            error = "read status " + status;
            return false;
        }

        public bool TryGetText(string name, out string rawValue, out string resolvedValue, out bool wasResolved, out int nativeType, out bool linkedOrExpression, out string error)
        {
            rawValue = null;
            resolvedValue = null;
            wasResolved = false;
            nativeType = 0;
            linkedOrExpression = false;
            error = null;
            var manager = Manager();
            if (manager == null)
            {
                error = "no property manager";
                return false;
            }
            if (!ContainsProperty(name)) return false;
            string raw;
            string resolved;
            bool resolvedFlag;
            bool linked;
            int status;
            try
            {
                status = manager.Get6(name, false, out raw, out resolved, out resolvedFlag, out linked);
            }
            catch (Exception ex)
            {
                error = "read failure: " + ex.GetType().Name;
                return false;
            }
            bool absent;
            string statusError;
            if (!MapCustomInfoGetStatus(status, out absent, out statusError))
            {
                error = statusError;
                return false;
            }
            int kind;
            try
            {
                kind = manager.GetType2(name);
            }
            catch (Exception ex)
            {
                error = "native type unknown: " + ex.GetType().Name;
                return false;
            }
            rawValue = raw;
            resolvedValue = resolved;
            wasResolved = resolvedFlag;
            nativeType = kind;
            linkedOrExpression = linked;
            return true;
        }

        public bool TryGetEditable(string name, out bool editable, out string error)
        {
            editable = false;
            error = null;
            try
            {
                var manager = Manager();
                if (manager == null)
                {
                    error = "no property manager";
                    return false;
                }
                editable = manager.IsCustomPropertyEditable(name, _configuration);
                return true;
            }
            catch (Exception ex)
            {
                error = "editability unknown: " + ex.GetType().Name;
                return false;
            }
        }

        public IReadOnlyList<string> GetConfigurationNames()
        {
            var names = _model.GetConfigurationNames() as string[];
            if (names == null) throw new InvalidOperationException("Configuration enumeration failed.");
            return names;
        }

        public string GetActiveConfigurationName()
        {
            try
            {
                var configuration = _model.GetActiveConfiguration() as IConfiguration;
                return configuration != null ? configuration.Name ?? string.Empty : string.Empty;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Active configuration unknown.", ex);
            }
        }

        public bool GetDirtyFlag()
        {
            try
            {
                return _model.GetSaveFlag();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Dirty state unknown.", ex);
            }
        }

        public bool GetReadOnlyFlag()
        {
            try
            {
                return _model.IsOpenedReadOnly();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Read-only state unknown.", ex);
            }
        }

        public string GetDocumentPath()
        {
            try
            {
                return _model.GetPathName() ?? string.Empty;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Document path unknown.", ex);
            }
        }

        private CustomPropertyManager Manager()
        {
            try
            {
                return _model.Extension.CustomPropertyManager[_configuration];
            }
            catch
            {
                return null;
            }
        }
    }

    internal struct LiveMutationTarget
    {
        internal IModelDoc2 Model;
        internal ISldWorks App;

        internal bool HasTarget
        {
            get { return Model != null && App != null; }
        }
    }

    // Reusable prompt host (D15): persists across admissions and creates a fresh single-shot
    // dialog per ShowAsync call. Stateless itself; dialogs self-dispose per single-shot rules.
    internal sealed class ApprovalPromptHost : IApprovalPrompt
    {
        private readonly ISolidWorksMainThreadDispatcher _dispatcher;
        private readonly Action<ApprovalDialog> _showSubstitute;

        internal ApprovalPromptHost(ISolidWorksMainThreadDispatcher dispatcher, Action<ApprovalDialog> showSubstitute)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _showSubstitute = showSubstitute;
        }

        public Task<ApprovalPromptOutcome> ShowAsync(ApprovalPrompt prompt)
        {
            var dialog = new ApprovalDialog(_dispatcher, _showSubstitute);
            return dialog.ShowAsync(prompt);
        }
    }

    // Routing preview builder bound once to the persistent issuer (D15): per-admission bound
    // wrappers register under (requestId, canonicalTargetHash) and unregister in finally.
    // Non-overwriting acquisition, exact request/target verification at Build, owner-only
    // removal — never a single mutable current-target slot.
    internal sealed class RoutingPreviewBuilder : IPreviewBuilder
    {
        private readonly object _sync = new object();
        private readonly Dictionary<string, BoundRegistration> _bindings = new Dictionary<string, BoundRegistration>(StringComparer.Ordinal);

        internal bool Register(string requestId, string targetHash, IPreviewBuilder wrapper)
        {
            if (string.IsNullOrWhiteSpace(requestId) || string.IsNullOrWhiteSpace(targetHash) || wrapper == null) return false;
            lock (_sync)
            {
                var key = MakeKey(requestId, targetHash);
                if (_bindings.ContainsKey(key)) return false;
                _bindings[key] = new BoundRegistration(targetHash, wrapper);
                return true;
            }
        }

        internal void Unregister(string requestId, string targetHash, IPreviewBuilder wrapper)
        {
            if (string.IsNullOrWhiteSpace(requestId) || string.IsNullOrWhiteSpace(targetHash) || wrapper == null) return;
            lock (_sync)
            {
                var key = MakeKey(requestId, targetHash);
                BoundRegistration current;
                if (_bindings.TryGetValue(key, out current) && ReferenceEquals(current.Wrapper, wrapper))
                {
                    _bindings.Remove(key);
                }
            }
        }

        public ApprovalPreview Build(AssistantToolDescriptor descriptor, AssistantToolRequest request)
        {
            string key = null;
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.RequestId)) return null;
                string filePath = null;
                if (request.Parameters == null || !request.Parameters.TryGetValue("file_path", out filePath) || string.IsNullOrWhiteSpace(filePath)) return null;
                key = MakeKey(request.RequestId, MutationExecutionJournal.HashTarget(Path.GetFullPath(filePath)));
            }
            catch
            {
                return null;
            }
            BoundRegistration registration;
            lock (_sync)
            {
                if (!_bindings.TryGetValue(key, out registration) || registration == null || registration.Wrapper == null) return null;
            }
            try
            {
                return registration.Wrapper.Build(descriptor, request);
            }
            catch
            {
                return null;
            }
        }

        private static string MakeKey(string requestId, string targetHash)
        {
            return (requestId ?? string.Empty) + "\0" + (targetHash ?? string.Empty);
        }

        private sealed class BoundRegistration
        {
            internal readonly string TargetHash;
            internal readonly IPreviewBuilder Wrapper;

            internal BoundRegistration(string targetHash, IPreviewBuilder wrapper)
            {
                TargetHash = targetHash;
                Wrapper = wrapper;
            }
        }
    }

    // Process-wide issuance ownership (D15/D16): one persistent issuer behind single-flight,
    // with explicit shutdown generation. Tests use private instances (never Shared), so no
    // cross-test contamination and no reset hook. Production uses Shared, wired once.
    internal sealed class MutationExecutionOwnership
    {
        private readonly object _sync = new object();
        private AssistantApprovalService _issuer;
        private RoutingPreviewBuilder _router;
        private bool _stopped;
        private long _generation;

        internal static readonly MutationExecutionOwnership Shared = new MutationExecutionOwnership();

        internal static void NotifyShutdownShared()
        {
            Shared.NotifyShutdown();
        }

        internal void EnsureInitialized(Func<AssistantApprovalService> issuerFactory, RoutingPreviewBuilder router)
        {
            if (issuerFactory == null) throw new ArgumentNullException(nameof(issuerFactory));
            if (router == null) throw new ArgumentNullException(nameof(router));
            lock (_sync)
            {
                if (_stopped || _issuer != null) return;
                var issuer = issuerFactory();
                if (issuer == null) throw new InvalidOperationException("Issuer factory returned no issuer.");
                _issuer = issuer;
                _router = router;
            }
        }

        internal AssistantApprovalService GetIssuer()
        {
            lock (_sync)
            {
                return _stopped ? null : _issuer;
            }
        }

        internal RoutingPreviewBuilder GetRouter()
        {
            lock (_sync)
            {
                return _stopped ? null : _router;
            }
        }

        internal long Generation
        {
            get { lock (_sync) { return _generation; } }
        }

        internal bool IsShutdown
        {
            get { lock (_sync) { return _stopped; } }
        }

        // Synchronous shutdown signal (single authorized caller: AgentHttpServer.Stop).
        // Commits stopped+generation and detaches the issuer UNDER the lock, then releases
        // it before disposing/cancelling (Sprint 02 C11 mechanism at holder level):
        // reentrant holder reads from cancellation callbacks are safe, faults contained.
        internal void NotifyShutdown()
        {
            AssistantApprovalService doomed;
            lock (_sync)
            {
                if (_stopped) return;
                _stopped = true;
                _generation++;
                doomed = _issuer;
                _issuer = null;
                _router = null;
            }
            try
            {
                if (doomed != null) doomed.Dispose();
            }
            catch
            {
            }
        }
    }

    // Live-CAD session seam for the one mutation (Sprint 04 contract C5/C6). All members run
    // on the main thread inside the executor's dispatched units (or marshaled equivalents);
    // implementations must not marshal internally. Tests use a scriptable fake that logs every
    // call (zero-COM assertions); production drives COM through the retained identity.
    internal interface ICustomPropertyMutationSession
    {
        bool IsCurrentTarget(out string activePath, out string error);
        bool TryWriteProperty(string name, string value, out string error);
        bool TrySave(out string error);
        bool TryReleaseAndReopen(out string error);
    }

    // Production session over the retained proven document identity. Every method fails
    // closed into an error string; Lab smoke proves the COM vocabulary live. Never trusts a
    // stale reference: IsCurrentTarget re-resolves the active document and compares by
    // reference; reopen callers must treat the retained reference as dead afterwards.
    internal sealed class SwLiveMutationSession : ICustomPropertyMutationSession
    {
        private readonly IModelDoc2 _model;
        private readonly ISldWorks _app;

        internal SwLiveMutationSession(IModelDoc2 model, ISldWorks app)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _app = app ?? throw new ArgumentNullException(nameof(app));
        }

        public bool IsCurrentTarget(out string activePath, out string error)
        {
            activePath = null;
            error = null;
            try
            {
                var current = _app.IActiveDoc2 as IModelDoc2;
                if (!ReferenceEquals(current, _model))
                {
                    try
                    {
                        activePath = current != null ? current.GetPathName() : null;
                    }
                    catch
                    {
                        activePath = null;
                    }
                    error = "active document is not the retained target";
                    return false;
                }
                activePath = _model.GetPathName();
                return true;
            }
            catch (Exception ex)
            {
                activePath = null;
                error = "identity check: " + ex.GetType().Name;
                return false;
            }
        }

        public bool TryWriteProperty(string name, string value, out string error)
        {
            error = null;
            try
            {
                var manager = _model.Extension.CustomPropertyManager[""];
                if (manager == null)
                {
                    error = "CustomPropertyManager unavailable.";
                    return false;
                }
                var status = manager.Add3(
                    name,
                    (int)swCustomInfoType_e.swCustomInfoText,
                    value,
                    (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                if (status != (int)swCustomInfoAddResult_e.swCustomInfoAddResult_AddedOrChanged)
                {
                    error = "CustomPropertyManager.Add3 reported status " + status + ".";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + " during property write.";
                return false;
            }
        }

        public bool TrySave(out string error)
        {
            error = null;
            try
            {
                int errors = 0;
                int warnings = 0;
                var saved = _model.Save3(
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                    ref errors,
                    ref warnings);
                if (!saved || errors != 0)
                {
                    error = "Save3 reported failure.";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + " during save.";
                return false;
            }
        }

        public bool TryReleaseAndReopen(out string error)
        {
            error = null;
            try
            {
                var path = GetActiveDocumentPath();
                if (string.IsNullOrWhiteSpace(path))
                {
                    error = "Active document path unavailable for reopen.";
                    return false;
                }
                _app.CloseDoc(path);
                int errors = 0;
                int warnings = 0;
                var reopened = _app.OpenDoc6(
                    path,
                    (int)swDocumentTypes_e.swDocPART,
                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                    string.Empty,
                    ref errors,
                    ref warnings) as IModelDoc2;
                if (reopened == null || errors != 0)
                {
                    error = "Reopen reported failure.";
                    return false;
                }
                string reopenedPath = null;
                try
                {
                    reopenedPath = reopened.GetPathName();
                }
                catch
                {
                    reopenedPath = null;
                }
                if (!string.Equals(reopenedPath, path, StringComparison.OrdinalIgnoreCase))
                {
                    error = "Reopened path mismatch.";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + " during release/reopen.";
                return false;
            }
        }

        private string GetActiveDocumentPath()
        {
            try
            {
                return _model.GetPathName();
            }
            catch
            {
                return null;
            }
        }
    }

    // set_custom_property mutation executor: the activation slice (Sprint 04 contract C2–C7,
    // v7). Single transaction per call; fully seam-injected for deterministic tests. Never
    // touches WithReceipt (all mutation-path terminals use the dedicated receipt seam), never
    // mutates the caller's request, never stores the server token anywhere but a local, and
    // never suspends on an incomplete task without test-controlled completion.
    internal sealed class SetCustomPropertyExecutor
    {
        internal const string ToolName = "solidworks.set_custom_property";
        internal const string PromptSummary = "edit and save this test file once.";
        private const int ConfigurationReadLimit = 64;
        private const int EvidenceIdentifierLimit = 128;
        private const int MaxNewValueLength = 2048;

        private static readonly object _busySync = new object();
        private static readonly Dictionary<string, bool> _busyTargets = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _decisionSync = new object();

        private readonly AgentConfig _config;
        private readonly AssistantToolPolicy _policy;
        private readonly AssistantToolAuditLog _auditLog;
        private readonly IMutationTelemetrySink _telemetrySink;
        private readonly IApprovalLedgerSink _ledgerSink;
        private readonly ISolidWorksMainThreadDispatcher _dispatcher;
        private readonly ICustomPropertyReadAdapter _adapter;
        private readonly IReadOnlyList<AssistantToolDescriptor> _catalog;
        private readonly MutationExecutionOwnership _ownership;
        private readonly Func<ICustomPropertyMutationSession> _sessionFactory;
        private readonly Func<IMutationAdmissionReader> _readerFactory;
        private readonly Func<DateTime> _utcNow;
        private readonly bool _isLabBuild;

        internal SetCustomPropertyExecutor(
            AgentConfig config,
            AssistantToolPolicy policy,
            AssistantToolAuditLog auditLog,
            IMutationTelemetrySink telemetrySink,
            IApprovalLedgerSink ledgerSink,
            ISolidWorksMainThreadDispatcher dispatcher,
            ICustomPropertyReadAdapter adapter,
            IReadOnlyList<AssistantToolDescriptor> catalog,
            MutationExecutionOwnership ownership,
            Func<ICustomPropertyMutationSession> sessionFactory,
            Func<IMutationAdmissionReader> readerFactory,
            Func<DateTime> utcNow)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _auditLog = auditLog ?? throw new ArgumentNullException(nameof(auditLog));
            _telemetrySink = telemetrySink ?? throw new ArgumentNullException(nameof(telemetrySink));
            _ledgerSink = ledgerSink ?? throw new ArgumentNullException(nameof(ledgerSink));
            _dispatcher = dispatcher;
            _adapter = adapter;
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _ownership = ownership ?? throw new ArgumentNullException(nameof(ownership));
            _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
            _readerFactory = readerFactory ?? throw new ArgumentNullException(nameof(readerFactory));
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
            _isLabBuild = AppIdentity.IsLabBuild;
        }

#if DEBUG
        // Test-only build-identity override (Sprint 02 C4 precedent, declared in C9): the
        // runtime path above always binds AppIdentity.IsLabBuild. The Lab build (no DEBUG)
        // has no such ctor; Lab verification must confirm its absence.
        internal SetCustomPropertyExecutor(
            AgentConfig config,
            AssistantToolPolicy policy,
            AssistantToolAuditLog auditLog,
            IMutationTelemetrySink telemetrySink,
            IApprovalLedgerSink ledgerSink,
            ISolidWorksMainThreadDispatcher dispatcher,
            ICustomPropertyReadAdapter adapter,
            IReadOnlyList<AssistantToolDescriptor> catalog,
            MutationExecutionOwnership ownership,
            Func<ICustomPropertyMutationSession> sessionFactory,
            Func<IMutationAdmissionReader> readerFactory,
            Func<DateTime> utcNow,
            bool isLabBuild)
            : this(config, policy, auditLog, telemetrySink, ledgerSink, dispatcher, adapter, catalog, ownership, sessionFactory, readerFactory, utcNow)
        {
            _isLabBuild = isLabBuild;
        }
#endif

        internal async Task<AssistantToolResult> ExecuteAsync(AssistantToolRequest request, string traceId)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            request = request ?? new AssistantToolRequest();
            traceId = traceId ?? string.Empty;

            if (!GateIsOpen())
            {
                return await FinishTerminal(stopwatch, null, null, null, null, "disabled",
                    "Tool is not enabled.", false, 0, "no-change",
                    "complete", request.ToolName, traceId, null).ConfigureAwait(false);
            }

            if (_dispatcher == null)
            {
                return await FinishTerminal(stopwatch, null, null, null, null, "denied",
                    "Denied — no change. (mutation composition unavailable)", false, 0, "no-change",
                    "complete", request.ToolName, traceId, null).ConfigureAwait(false);
            }

            FrozenRequest frozen;
            string freezeError;
            if (!TryFreeze(request, traceId, out frozen, out freezeError))
            {
                return await FinishTerminal(stopwatch, null, null, null, null, "denied",
                    "Denied — no change. (" + freezeError + ")", false, 0, "no-change",
                    "complete", request.ToolName, traceId, null).ConfigureAwait(false);
            }

            var descriptor = _catalog.FirstOrDefault(d => string.Equals(d.Name, ToolName, StringComparison.OrdinalIgnoreCase));
            if (descriptor == null)
            {
                return await FinishTerminal(stopwatch, frozen, null, null, null, "unknown",
                    "Unknown assistant tool.", false, 0, "no-change",
                    "complete", ToolName, traceId, null).ConfigureAwait(false);
            }
            if (!descriptor.Enabled)
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, null, null, "disabled",
                    descriptor.UnavailableReason ?? "Tool is not enabled.", false, 0, "no-change",
                    "complete", ToolName, traceId, null).ConfigureAwait(false);
            }

            string targetFull;
            string targetError;
            if (!TryValidateTargetPath(frozen.FilePath, out targetFull, out targetError))
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, null, null, "denied",
                    "Denied — no change. (" + targetError + ")", false, 0, "no-change",
                    "complete", ToolName, traceId, null).ConfigureAwait(false);
            }
            frozen.TargetFullPath = targetFull;
            frozen.TargetId = targetFull;
            frozen.TargetHash = MutationExecutionJournal.HashTarget(targetFull);

            string aliasError;
            if (!CheckNoReparseAncestors(targetFull, out aliasError))
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, null, null, "denied",
                    "Denied — no change. (" + aliasError + ")", false, 0, "no-change",
                    "complete", ToolName, traceId, null).ConfigureAwait(false);
            }

            if (!TryEnterTarget(frozen.TargetId))
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, null, null, "denied",
                    "Denied — no change. (approval_in_progress for this target)", false, 0, "no-change",
                    "complete", ToolName, traceId, null).ConfigureAwait(false);
            }
            try
            {
                return await RunTransaction(stopwatch, frozen, descriptor, traceId).ConfigureAwait(false);
            }
            finally
            {
                ReleaseTarget(frozen.TargetId);
            }
        }

        private async Task<AssistantToolResult> RunTransaction(
            System.Diagnostics.Stopwatch stopwatch,
            FrozenRequest frozen,
            AssistantToolDescriptor descriptor,
            string traceId)
        {
            var journal = new MutationExecutionJournal(MutationExecutionJournal.DefaultJournalPath(ResolvedTestRoot()));
            try
            {
                MutationExecutionJournal.EnsureScanned(journal.JournalPath);
            }
            catch (MutationJournalCorruptException)
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, null, null, "denied",
                    "Denied — no change. (recovery journal unreadable; all mutation blocked)", false, 0, "no-change",
                    "complete", ToolName, traceId, null).ConfigureAwait(false);
            }
            if (MutationExecutionJournal.IsBlockedCached(journal.JournalPath, frozen.TargetId)
                || journal.IsBlocked(frozen.TargetId))
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, null, null, "denied",
                    "Denied — no change. (target blocked by unfinished recovery record)", false, 0, "no-change",
                    "complete", ToolName, traceId, null).ConfigureAwait(false);
            }

            long generation = _ownership.Generation;

            ICustomPropertyMutationSession session = null;
            bool sessionResolved = false;
            string sessionError = null;
            try
            {
                sessionResolved = _dispatcher.TryInvoke(() =>
                {
                    try
                    {
                        session = _sessionFactory();
                    }
                    catch (Exception ex)
                    {
                        sessionError = ex.GetType().Name;
                        session = null;
                    }
                });
            }
            catch (Exception ex)
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, null, null, "denied",
                    "Denied — no change. (dispatch: " + ex.GetType().Name + ")", false, 0, "no-change",
                    "complete", ToolName, traceId, null).ConfigureAwait(false);
            }
            if (!sessionResolved || session == null)
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, null, null, "denied",
                    "Denied — no change. (no live target session" + (sessionError == null ? "" : ": " + sessionError) + ")", false, 0, "no-change",
                    "complete", ToolName, traceId, null).ConfigureAwait(false);
            }

            var reader = _readerFactory();
            if (reader == null)
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, null, null, "denied",
                    "Denied — no change. (admission reader unavailable)", false, 0, "no-change",
                    "complete", ToolName, traceId, null).ConfigureAwait(false);
            }

            MutationAdmissionProof proof;
            string proofError;
            if (!TryCaptureProof(frozen, reader, out proof, out proofError))
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, null, null, "denied",
                    "Denied — no change. (" + proofError + ")", false, 0, "no-change",
                    "complete", ToolName, traceId, null).ConfigureAwait(false);
            }
            frozen.BaselineHash = proof.DiskHashForExecutor;
            frozen.ValidatedLiteral = proof.Literal;
            frozen.ConfigurationInventory = proof.Configurations;

            string evidenceError;
            if (!DestinationsWritable(journal, out evidenceError))
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, null, null, "denied",
                    "Denied — no change. (" + evidenceError + ")", false, 0, "no-change",
                    "complete", ToolName, traceId, null).ConfigureAwait(false);
            }

            var displayBuilder = CreateDisplayBuilder();
            if (displayBuilder == null)
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, null, null, "denied",
                    "Denied — no change. (preview builder unavailable)", false, 0, "no-change",
                    "complete", ToolName, traceId, null).ConfigureAwait(false);
            }
            var boundBuilder = new BoundPreviewWrapper(displayBuilder, reader, _dispatcher, new MutationAdmissionQuery
            {
                TargetFullPath = frozen.TargetFullPath,
                PropertyName = frozen.Property,
                CorrelationId = frozen.RequestId,
                ConfigurationReadLimit = ConfigurationReadLimit
            });
            var routing = _ownership.GetRouter();
            if (routing == null)
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, null, null, "denied",
                    "Denied — no change. (preview routing unavailable)", false, 0, "no-change",
                    "complete", ToolName, traceId, null).ConfigureAwait(false);
            }
            if (!routing.Register(frozen.RequestId, frozen.TargetHash, boundBuilder))
            {
                return await FinishTerminal(stopwatch, frozen, descriptor, null, null, "denied",
                    "Denied — no change. (duplicate approval binding)", false, 0, "no-change",
                    "complete", ToolName, traceId, null).ConfigureAwait(false);
            }
            try
            {
                var issuer = _ownership.GetIssuer();
                if (issuer == null)
                {
                    return await FinishTerminal(stopwatch, frozen, descriptor, null, null, "denied",
                        "Denied — no change. (service shutdown)", false, 0, "no-change",
                        "complete", ToolName, traceId, null).ConfigureAwait(false);
                }
                var token = await issuer.RequestApprovalAsync(descriptor, frozen.ExecRequest, traceId).ConfigureAwait(false);
                if (token == null)
                {
                    return await FinishTerminal(stopwatch, frozen, descriptor, null, null, "denied",
                        "Denied — no change. (approval denied, expired, or orphaned)", false, 0, "no-change",
                        "complete", ToolName, traceId, null).ConfigureAwait(false);
                }

                var capabilityPolicy = _policy.EvaluateCapability(
                    descriptor,
                    frozen.ExecRequest,
                    AssistantToolInvocationSource.AssistantTool,
                    token,
                    frozen.RequestId,
                    frozen.SessionId,
                    frozen.Environment,
                    _utcNow());
                if (!capabilityPolicy.Allowed)
                {
                    return await FinishTerminal(stopwatch, frozen, descriptor, token, null, "denied",
                        "Denied — no change. (post-approval policy refusal)", false, 0, "no-change",
                        "complete", ToolName, traceId, null).ConfigureAwait(false);
                }

                MutationAdmissionProof recheck;
                string recheckError;
                if (!TryCaptureProof(frozen, reader, out recheck, out recheckError) || !ProofsMatch(recheck, recheck.DiskHashForExecutor, proof, proof.DiskHashForExecutor))
                {
                    return await FinishTerminal(stopwatch, frozen, descriptor, token, null, "denied",
                        "Denied — no change. (target changed after approval; fresh approval required)", false, 0, "no-change",
                        "complete", ToolName, traceId, null).ConfigureAwait(false);
                }

                var checkpointPath = MutationExecutionJournal.DefaultCheckpointPath(ResolvedTestRoot(), frozen.TargetHash, MutationExecutionJournal.HashRequest(frozen.RequestId));
                try
                {
                    var checkpointDirectory = Path.GetDirectoryName(checkpointPath);
                    if (!string.IsNullOrWhiteSpace(checkpointDirectory))
                    {
                        Directory.CreateDirectory(checkpointDirectory);
                    }
                    File.Copy(frozen.TargetFullPath, checkpointPath, true);
                    var checkpointHash = ComputeFileHash(checkpointPath);
                    if (!string.Equals(checkpointHash, frozen.BaselineHash, StringComparison.Ordinal))
                    {
                        return await FinishTerminal(stopwatch, frozen, descriptor, token, null, "failed_before_write",
                            "Failed before write. No CAD or disk change was made. (checkpoint hash mismatch)", false, 0, "no-change",
                            "complete", ToolName, traceId, null).ConfigureAwait(false);
                    }
                    frozen.CheckpointHash = checkpointHash;
                }
                catch (Exception ex)
                {
                    return await FinishTerminal(stopwatch, frozen, descriptor, token, null, "failed_before_write",
                        "Failed before write. No CAD or disk change was made. (checkpoint: " + ex.GetType().Name + ")", false, 0, "no-change",
                        "complete", ToolName, traceId, null).ConfigureAwait(false);
                }

                try
                {
                    journal.Append(frozen.TargetId, frozen.RequestId, token.ApprovalId, MutationStage.Prepared, frozen.BaselineHash, frozen.CheckpointHash);
                }
                catch (Exception ex)
                {
                    return await FinishTerminal(stopwatch, frozen, descriptor, token, null, "failed_before_write",
                        "Failed before write. No CAD or disk change was made. (journal: " + ex.GetType().Name + ")", false, 0, "no-change",
                        "complete", ToolName, traceId, null).ConfigureAwait(false);
                }

                string consumeError;
                if (!TryConsume(token, frozen, traceId, generation, out consumeError))
                {
                    if (consumeError != null && consumeError.StartsWith("consumption evidence", StringComparison.Ordinal))
                    {
                        return await FinishTerminal(stopwatch, frozen, descriptor, token, null, "failed_before_write",
                            "Failed before write. No CAD or disk change was made. (" + consumeError + ")", false, 0, "no-change",
                            "complete", ToolName, traceId, null).ConfigureAwait(false);
                    }
                    return await FinishTerminal(stopwatch, frozen, descriptor, token, null, "denied",
                        "Denied — no change. (" + consumeError + ")", false, 0, "no-change",
                        "complete", ToolName, traceId, null).ConfigureAwait(false);
                }

                try
                {
                    journal.Append(frozen.TargetId, frozen.RequestId, token.ApprovalId, MutationStage.WriteStarted, frozen.BaselineHash, frozen.CheckpointHash);
                }
                catch (Exception ex)
                {
                    return await FinishTerminal(stopwatch, frozen, descriptor, token, null, "failed_before_write",
                        "Failed before write. No CAD or disk change was made. (journal: " + ex.GetType().Name + ")", false, 0, "no-change",
                        "complete", ToolName, traceId, null).ConfigureAwait(false);
                }

                string finalOutcome = null;
                string finalError = null;
                bool marshaled = false;
                try
                {
                    marshaled = _dispatcher.TryInvoke(() =>
                    {
                        if (!AuthorityFresh(token, descriptor, generation, out finalError))
                        {
                            finalOutcome = "authority-stale";
                            return;
                        }
                        MutationAdmissionProof finalProof;
                        string finalProofError;
                        string unitSessionPath;
                        string unitIdentityError;
                        bool unitIsCurrent = false;
                        try
                        {
                            unitIsCurrent = session.IsCurrentTarget(out unitSessionPath, out unitIdentityError);
                        }
                        catch (Exception ex)
                        {
                            finalOutcome = "check-failed";
                            finalError = "retained identity: " + ex.GetType().Name;
                            return;
                        }
                        if (!unitIsCurrent
                            || !string.Equals(unitSessionPath, frozen.TargetFullPath, StringComparison.OrdinalIgnoreCase))
                        {
                            finalOutcome = "check-failed";
                            finalError = unitIdentityError ?? "retained identity mismatch";
                            return;
                        }
                        if (!TryCaptureProofInline(frozen, reader, out finalProof, out finalProofError) || !ProofsMatch(finalProof, finalProof.DiskHashForExecutor, proof, proof.DiskHashForExecutor))
                        {
                            finalOutcome = "check-failed";
                            finalError = finalProofError ?? "final recheck mismatch";
                            return;
                        }
                        string writeError;
                        frozen.MutatorInvoked = true;
                        if (!session.TryWriteProperty(frozen.Property, frozen.NewValue, out writeError))
                        {
                            finalOutcome = "write-failed";
                            finalError = writeError ?? "property write failed";
                            return;
                        }
                        finalOutcome = "ok";
                    });
                }
                catch (Exception ex)
                {
                    return await FinishTerminal(stopwatch, frozen, descriptor, token, null, "failed_before_write",
                        "Failed before write. No CAD or disk change was made. (dispatch: " + ex.GetType().Name + ")", false, 0, "no-change",
                        "complete", ToolName, traceId, null).ConfigureAwait(false);
                }
                if (!marshaled)
                {
                    return await FinishTerminal(stopwatch, frozen, descriptor, token, null, "failed_before_write",
                        "Failed before write. No CAD or disk change was made. (main-thread marshal unavailable)", false, 0, "no-change",
                        "complete", ToolName, traceId, null).ConfigureAwait(false);
                }
                if (string.Equals(finalOutcome, "authority-stale", StringComparison.Ordinal))
                {
                    return await FinishTerminal(stopwatch, frozen, descriptor, token, null, "denied",
                        "Denied — no change. (" + finalError + ")", false, 0, "no-change",
                        "complete", ToolName, traceId, null).ConfigureAwait(false);
                }
                if (string.Equals(finalOutcome, "check-failed", StringComparison.Ordinal))
                {
                    return await FinishTerminal(stopwatch, frozen, descriptor, token, null, "failed_before_write",
                        "Failed before write. No CAD or disk change was made. (" + finalError + ")", false, 0, "no-change",
                        "complete", ToolName, traceId, null).ConfigureAwait(false);
                }
                if (!string.Equals(finalOutcome, "ok", StringComparison.Ordinal))
                {
                    return await FinishTerminal(stopwatch, frozen, descriptor, token, checkpointPath, "save_uncertain",
                        "In-memory change or save uncertain — recovery required. (" + (finalError ?? "property write failed") + ")", false, 1, "uncertain",
                        "complete", ToolName, traceId, null).ConfigureAwait(false);
                }
                frozen.WriteResult = "ok";

                string preSaveError;
                if (!CheckPreSaveIdentity(frozen, session, frozen.BaselineHash, out preSaveError))
                {
                    return await FinishTerminal(stopwatch, frozen, descriptor, token, checkpointPath, "save_uncertain",
                        "In-memory change or save uncertain — recovery required. (" + preSaveError + ")", false, 1, "uncertain",
                        "complete", ToolName, traceId, null).ConfigureAwait(false);
                }

                try
                {
                    journal.Append(frozen.TargetId, frozen.RequestId, token.ApprovalId, MutationStage.SaveAttempted, frozen.BaselineHash, frozen.CheckpointHash);
                }
                catch (Exception ex)
                {
                    return await FinishTerminal(stopwatch, frozen, descriptor, token, checkpointPath, "save_uncertain",
                        "In-memory change or save uncertain — recovery required. (journal: " + ex.GetType().Name + ")", false, 1, "uncertain",
                        "complete", ToolName, traceId, null).ConfigureAwait(false);
                }
                string saveError = null;
                bool saveMarshaled = false;
                try
                {
                    saveMarshaled = _dispatcher.TryInvoke(() =>
                    {
                        string innerError;
                        if (!session.TrySave(out innerError))
                        {
                            saveError = innerError ?? "save failed";
                        }
                    });
                }
                catch (Exception ex)
                {
                    saveError = "dispatch: " + ex.GetType().Name;
                    saveMarshaled = true;
                }
                if (!saveMarshaled)
                {
                    return await FinishTerminal(stopwatch, frozen, descriptor, token, checkpointPath, "save_uncertain",
                        "In-memory change or save uncertain — recovery required. (main-thread marshal unavailable)", false, 1, "uncertain",
                        "complete", ToolName, traceId, null).ConfigureAwait(false);
                }
                frozen.SaveResult = saveError == null ? "ok" : "failed";
                if (saveError != null)
                {
                    return await FinishTerminal(stopwatch, frozen, descriptor, token, checkpointPath, "save_uncertain",
                        "In-memory change or save uncertain — recovery required. (" + saveError + ")", false, 1, "uncertain",
                        "complete", ToolName, traceId, null).ConfigureAwait(false);
                }
                string postSaveHash;
                try
                {
                    postSaveHash = ComputeFileHash(frozen.TargetFullPath);
                }
                catch (Exception ex)
                {
                    return await FinishTerminal(stopwatch, frozen, descriptor, token, checkpointPath, "save_uncertain",
                        "In-memory change or save uncertain — recovery required. (post-save hash: " + ex.GetType().Name + ")", false, 1, "uncertain",
                        "complete", ToolName, traceId, null).ConfigureAwait(false);
                }
                frozen.FinalHash = postSaveHash;

                try
                {
                    journal.Append(frozen.TargetId, frozen.RequestId, token.ApprovalId, MutationStage.SaveReturned, frozen.BaselineHash, frozen.CheckpointHash);
                }
                catch
                {
                    return await FinishTerminal(stopwatch, frozen, descriptor, token, checkpointPath, "save_uncertain",
                        "In-memory change or save uncertain — recovery required. (journal)", false, 1, "uncertain",
                        "complete", ToolName, traceId, null).ConfigureAwait(false);
                }

                MutationAdmissionProof cleanProof;
                string cleanError;
                if (!TryCaptureProof(frozen, reader, out cleanProof, out cleanError))
                {
                    return await FinishTerminal(stopwatch, frozen, descriptor, token, checkpointPath, "save_returned",
                        "Save returned but disk verification incomplete — document left open and recovery required. (" + cleanError + ")", false, 1, "uncertain",
                        "incomplete", ToolName, traceId, null).ConfigureAwait(false);
                }
                if (!cleanProof.IsClean)
                {
                    return await FinishTerminal(stopwatch, frozen, descriptor, token, checkpointPath, "save_returned",
                        "Save returned but disk verification incomplete — document left open and recovery required. (document not clean)", false, 1, "uncertain",
                        "incomplete", ToolName, traceId, null).ConfigureAwait(false);
                }

                string reopenError = null;
                bool reopenMarshaled = false;
                try
                {
                    reopenMarshaled = _dispatcher.TryInvoke(() =>
                    {
                        string innerError;
                        if (!session.TryReleaseAndReopen(out innerError))
                        {
                            reopenError = innerError ?? "reopen failed";
                        }
                    });
                }
                catch (Exception ex)
                {
                    reopenError = "dispatch: " + ex.GetType().Name;
                    reopenMarshaled = true;
                }
                if (!reopenMarshaled)
                {
                    return await FinishTerminal(stopwatch, frozen, descriptor, token, checkpointPath, "save_uncertain",
                        "In-memory change or save uncertain — recovery required. (main-thread marshal unavailable)", false, 1, "uncertain",
                        "complete", ToolName, traceId, null).ConfigureAwait(false);
                }
                if (reopenError != null)
                {
                    return await FinishTerminal(stopwatch, frozen, descriptor, token, checkpointPath, "save_uncertain",
                        "In-memory change or save uncertain — recovery required. (" + reopenError + ")", false, 1, "uncertain",
                        "complete", ToolName, traceId, null).ConfigureAwait(false);
                }
                MutationAdmissionProof verify;
                string verifyError;
                string reopenHash;
                try
                {
                    reopenHash = ComputeFileHash(frozen.TargetFullPath);
                }
                catch (Exception ex)
                {
                    return await FinishTerminal(stopwatch, frozen, descriptor, token, checkpointPath, "save_uncertain",
                        "In-memory change or save uncertain — recovery required. (reopen hash: " + ex.GetType().Name + ")", false, 1, "uncertain",
                        "complete", ToolName, traceId, null).ConfigureAwait(false);
                }
                if (!TryCaptureProof(frozen, reader, out verify, out verifyError)
                    || !string.Equals(verify.Literal, frozen.NewValue, StringComparison.Ordinal)
                    || !string.Equals(verify.DocumentPath, frozen.TargetFullPath, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(reopenHash, postSaveHash, StringComparison.Ordinal))
                {
                    return await FinishTerminal(stopwatch, frozen, descriptor, token, checkpointPath, "save_uncertain",
                        "In-memory change or save uncertain — recovery required. (" + (verifyError ?? "reopened value mismatch") + ")", false, 1, "uncertain",
                        "complete", ToolName, traceId, null).ConfigureAwait(false);
                }

                try
                {
                    journal.Append(frozen.TargetId, frozen.RequestId, token.ApprovalId, MutationStage.SavedVerified, frozen.BaselineHash, frozen.CheckpointHash);
                }
                catch
                {
                    return await FinishTerminal(stopwatch, frozen, descriptor, token, checkpointPath, "evidence_incomplete",
                        "Saved but evidence incomplete — reconciliation required. (journal)", true, 1, "saved-verified",
                        "incomplete", ToolName, traceId, null).ConfigureAwait(false);
                }

                var appliedReceipt = BuildReceipt(stopwatch, frozen, descriptor, token, checkpointPath, "applied", "Saved and verified.");
                string receiptError;
                if (!TryPersistReceipt(appliedReceipt, out receiptError))
                {
                    return await FinishTerminal(stopwatch, frozen, descriptor, token, checkpointPath, "evidence_incomplete",
                        "Saved but evidence incomplete — reconciliation required. (" + receiptError + ")", true, 1, "saved-verified",
                        "incomplete", ToolName, traceId, appliedReceipt.ReceiptId).ConfigureAwait(false);
                }

                string telemetryError;
                if (!TryRecordTerminalTelemetry("applied", true, stopwatch, frozen, token, appliedReceipt.ReceiptId, checkpointPath, out telemetryError))
                {
                    return await FinishTerminal(stopwatch, frozen, descriptor, token, checkpointPath, "evidence_incomplete",
                        "Saved but evidence incomplete — reconciliation required. (" + telemetryError + ")", true, 1, "saved-verified",
                        "incomplete", ToolName, traceId, appliedReceipt.ReceiptId).ConfigureAwait(false);
                }

                try
                {
                    journal.Append(frozen.TargetId, frozen.RequestId, token.ApprovalId, MutationStage.EvidenceComplete, frozen.BaselineHash, frozen.CheckpointHash);
                }
                catch
                {
                    return await FinishTerminal(stopwatch, frozen, descriptor, token, checkpointPath, "evidence_incomplete",
                        "Saved but evidence incomplete — reconciliation required. (journal)", true, 1, "saved-verified",
                        "incomplete", ToolName, traceId, appliedReceipt.ReceiptId).ConfigureAwait(false);
                }

                return FinishApplied(frozen, descriptor, token, checkpointPath, appliedReceipt, traceId);
            }
            finally
            {
                routing.Unregister(frozen.RequestId, frozen.TargetHash, boundBuilder);
            }
        }

        private bool GateIsOpen()
        {
            return _isLabBuild && (_config.Assistant != null && _config.Assistant.Mutations != null && _config.Assistant.Mutations.Enabled);
        }

        private string ResolvedTestRoot()
        {
            var root = _config.Assistant != null && _config.Assistant.Mutations != null ? _config.Assistant.Mutations.TestFileRoot : null;
            if (string.IsNullOrWhiteSpace(root))
            {
                root = Path.Combine(AppIdentity.DefaultWorkingFolder, "AssistantTestFiles");
            }
            try
            {
                return Path.GetFullPath(root);
            }
            catch
            {
                return string.Empty;
            }
        }

        private bool TryFreeze(AssistantToolRequest request, string traceId, out FrozenRequest frozen, out string error)
        {
            frozen = null;
            error = null;
            string toolName = request.ToolName ?? string.Empty;
            string query = request.Query ?? string.Empty;
            int limit = request.Limit;
            string requestId = string.IsNullOrWhiteSpace(request.RequestId) ? traceId : request.RequestId;
            string sessionId = request.SessionId ?? string.Empty;
            string environment = _isLabBuild ? "Lab" : "Production";
            Dictionary<string, string> parameters = request.Parameters == null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(request.Parameters, StringComparer.Ordinal);
            string filePath;
            string property;
            string value;
            if (!parameters.TryGetValue("file_path", out filePath) || string.IsNullOrWhiteSpace(filePath)
                || !parameters.TryGetValue("property", out property) || string.IsNullOrWhiteSpace(property)
                || !parameters.TryGetValue("value", out value))
            {
                error = "invalid_request";
                return false;
            }
            string valueError;
            if (!IsSupportedNewValue(value, out valueError))
            {
                error = valueError;
                return false;
            }
            string scopeId;
            try
            {
                scopeId = AssistantScopeRegistry.Resolve(_config, _catalog, request.ScopeId).Id;
            }
            catch
            {
                scopeId = AssistantScopeRegistry.Normalize(request.ScopeId);
            }
            if (string.IsNullOrWhiteSpace(requestId))
            {
                error = "invalid_request";
                return false;
            }
            var execRequest = new AssistantToolRequest
            {
                ToolName = toolName,
                Query = query,
                Limit = limit,
                ScopeId = scopeId,
                SessionId = sessionId,
                RequestId = requestId,
                Environment = environment,
                Authorization = AssistantToolAuthorization.None(),
                Parameters = parameters
            };
            frozen = new FrozenRequest
            {
                ExecRequest = execRequest,
                RequestId = requestId,
                SessionId = sessionId,
                Environment = environment,
                FilePath = filePath.Trim(),
                Property = property.Trim(),
                NewValue = value,
                TraceId = traceId ?? string.Empty
            };
            return true;
        }

        internal static bool IsSupportedNewValue(string value, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                error = "new value is blank";
                return false;
            }
            if (value.Length > MaxNewValueLength)
            {
                error = "new value too long";
                return false;
            }
            if (value.IndexOf("$PRP", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                error = "new value must be literal text, not an expression";
                return false;
            }
            foreach (var ch in value)
            {
                if (ch < 0x20)
                {
                    error = "new value contains control characters";
                    return false;
                }
            }
            return true;
        }

        // Ownership-aware lock seams (Sprint 05 v2): internal settable members with production
        // defaults; both ctor surfaces unchanged. Null locker provider ⇒ legacy exclusive-open
        // probe (all pre-v2 rows byte-identical). Set provider ⇒ Restart Manager ownership rule:
        // enumeration failure/inconclusive ⇒ "target lock state unknown"; foreign locker ⇒
        // "target locked"; empty-or-owned-only ⇒ probe passes and retained identity decides.
        internal Func<string, IReadOnlyList<int>> LockerListProvider { get; set; }

        internal Func<int> OwnedProcessIdProvider { get; set; }

        private bool TargetLockersOwnedBySelf(string fullPath, out string error)
        {
            error = null;
            IReadOnlyList<int> lockers;
            try
            {
                lockers = LockerListProvider(fullPath);
            }
            catch
            {
                error = "target lock state unknown";
                return false;
            }
            if (lockers == null)
            {
                error = "target lock state unknown";
                return false;
            }
            int owned;
            try
            {
                owned = OwnedProcessIdProvider != null ? OwnedProcessIdProvider() : Process.GetCurrentProcess().Id;
            }
            catch
            {
                error = "target lock state unknown";
                return false;
            }
            foreach (int locker in lockers)
            {
                if (locker != owned)
                {
                    error = "target locked";
                    return false;
                }
            }
            return true;
        }

        internal static IReadOnlyList<int> QueryRestartManagerLockers(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath)) return null;
            uint session = 0;
            int start = NativeRestartManager.RmStartSession(out session, 0, "BlueBrickMutationAdmission");
            if (start != 0) return null;
            IReadOnlyList<int> outcome = null;
            bool endOk = false;
            try
            {
                outcome = EnumerateSessionLockers(session, fullPath);
            }
            finally
            {
                try
                {
                    endOk = NativeRestartManager.RmEndSession(session) == 0;
                }
                catch
                {
                    endOk = false;
                }
            }
            return endOk ? outcome : null;
        }

        private static IReadOnlyList<int> EnumerateSessionLockers(uint session, string fullPath)
        {
            int registered = NativeRestartManager.RmRegisterResources(session, 1, new[] { fullPath }, 0, null, 0, null);
            if (registered != 0) return null;
            uint needed = 0;
            uint count = 0;
            uint reboot = 0;
            int listed = NativeRestartManager.RmGetList(session, out needed, ref count, null, ref reboot);
            if (listed != 0 && listed != 234) return null;
            if (needed == 0) return new int[0];
            var infos = new NativeRestartManager.RM_PROCESS_INFO[needed];
            count = needed;
            listed = NativeRestartManager.RmGetList(session, out needed, ref count, infos, ref reboot);
            if (listed != 0) return null;
            var pids = new List<int>();
            for (int i = 0; i < count && i < infos.Length; i++)
            {
                pids.Add(infos[i].Process.dwProcessId);
            }
            return pids;
        }

        private static class NativeRestartManager
        {
            [StructLayout(LayoutKind.Sequential)]
            internal struct RM_UNIQUE_PROCESS
            {
                internal int dwProcessId;
                internal System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
            }

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            internal struct RM_PROCESS_INFO
            {
                internal RM_UNIQUE_PROCESS Process;
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
                internal string strAppName;
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
                internal string strServiceShortName;
                internal int ApplicationType;
                internal uint AppStatus;
                internal uint TSSessionId;
                [MarshalAs(UnmanagedType.Bool)]
                internal bool bRestartable;
            }

            [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
            internal static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, string strSessionKey);

            [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
            internal static extern int RmRegisterResources(uint dwSessionHandle, uint nFiles, string[] rgsFilenames, uint nApplications, RM_UNIQUE_PROCESS[] rgApplications, uint nServices, string[] rgsServiceNames);

            [DllImport("rstrtmgr.dll")]
            internal static extern int RmGetList(uint dwSessionHandle, out uint pnProcInfoNeeded, ref uint pnProcInfo, [In, Out] RM_PROCESS_INFO[] rgAffectedApps, ref uint lpdwRebootReasons);

            [DllImport("rstrtmgr.dll")]
            internal static extern int RmEndSession(uint dwSessionHandle);
        }

        private bool TryValidateTargetPath(string filePath, out string fullPath, out string error)
        {
            fullPath = null;
            error = null;
            string root = ResolvedTestRoot();
            if (string.IsNullOrWhiteSpace(root))
            {
                error = "test root unresolvable";
                return false;
            }
            string candidate;
            try
            {
                candidate = Path.GetFullPath(filePath);
            }
            catch
            {
                error = "path malformed";
                return false;
            }
            string prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                error = "target outside test root";
                return false;
            }
            if (!string.Equals(Path.GetExtension(candidate), ".sldprt", StringComparison.OrdinalIgnoreCase))
            {
                error = "not a test part";
                return false;
            }
            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(candidate);
            }
            catch
            {
                error = "target missing";
                return false;
            }
            if ((attributes & FileAttributes.Directory) != 0)
            {
                error = "target is a directory";
                return false;
            }
            if ((attributes & FileAttributes.ReadOnly) != 0)
            {
                error = "target read-only or vault-locked";
                return false;
            }
            try
            {
                if (LockerListProvider != null)
                {
                    if (!TargetLockersOwnedBySelf(candidate, out error)) return false;
                }
                else
                {
                    using (var probe = new FileStream(candidate, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                    }
                }
            }
            catch
            {
                error = "target locked";
                return false;
            }
            fullPath = candidate;
            return true;
        }

        private static bool CheckNoReparseAncestors(string fullPath, out string error)
        {
            return CheckNoReparseAncestors(fullPath, p => File.GetAttributes(p), out error);
        }

        internal static bool CheckNoReparseAncestors(string fullPath, Func<string, FileAttributes> getAttributes, out string error)
        {
            error = null;
            if (getAttributes == null)
            {
                error = "alias check unavailable";
                return false;
            }
            string current;
            try
            {
                current = Path.GetFullPath(fullPath);
            }
            catch
            {
                error = "path malformed";
                return false;
            }
            while (!string.IsNullOrEmpty(current))
            {
                FileAttributes attributes;
                try
                {
                    attributes = getAttributes(current);
                }
                catch
                {
                    error = "alias unresolvable";
                    return false;
                }
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    error = "ambiguous alias in target path";
                    return false;
                }
                var parent = Path.GetDirectoryName(current.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (string.IsNullOrEmpty(parent) || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase)) break;
                current = parent;
            }
            return true;
        }

        private bool TryCaptureProof(FrozenRequest frozen, IMutationAdmissionReader reader, out MutationAdmissionProof proof, out string error)
        {
            proof = null;
            error = null;
            MutationAdmissionProof captured = null;
            string capturedError = null;
            bool marshaled = false;
            try
            {
                marshaled = _dispatcher.TryInvoke(() =>
                {
                    MutationAdmissionProof inline;
                    string inlineError;
                    if (!TryCaptureProofInline(frozen, reader, out inline, out inlineError))
                    {
                        captured = null;
                        capturedError = inlineError;
                        return;
                    }
                    captured = inline;
                });
            }
            catch (Exception ex)
            {
                error = "proof dispatch: " + ex.GetType().Name;
                return false;
            }
            if (!marshaled)
            {
                error = "main-thread marshal unavailable";
                return false;
            }
            if (captured == null)
            {
                error = capturedError ?? "typed proof unavailable";
                return false;
            }
            proof = captured;
            return true;
        }

        private bool TryCaptureProofInline(FrozenRequest frozen, IMutationAdmissionReader reader, out MutationAdmissionProof proof, out string error)
        {
            proof = null;
            error = null;
            if (reader == null)
            {
                error = "admission reader unavailable";
                return false;
            }
            var query = new MutationAdmissionQuery
            {
                TargetFullPath = frozen.TargetFullPath,
                PropertyName = frozen.Property,
                CorrelationId = frozen.RequestId,
                ConfigurationReadLimit = ConfigurationReadLimit
            };
            MutationAdmissionProof candidate;
            try
            {
                if (!reader.TryProveTarget(query, out candidate, out error) || candidate == null)
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                error = "proof failure: " + ex.GetType().Name;
                return false;
            }
            string acceptError;
            if (!IsProofAcceptable(candidate, frozen.TargetFullPath, out acceptError))
            {
                error = acceptError;
                return false;
            }
            string diskHash;
            try
            {
                diskHash = ComputeFileHash(frozen.TargetFullPath);
            }
            catch (Exception ex)
            {
                error = "disk hash: " + ex.GetType().Name;
                return false;
            }
            proof = candidate;
            proof.DiskHashForExecutor = diskHash;
            return true;
        }

        internal static bool IsProofAcceptable(MutationAdmissionProof proof, string targetFullPath, out string error)
        {
            error = null;
            if (proof == null || !proof.Found)
            {
                error = "property absent";
                return false;
            }
            if (proof.Literal == null || proof.RawValue == null || proof.ResolvedValue == null)
            {
                error = "unreadable value";
                return false;
            }
            if (!string.Equals(proof.RawValue, proof.ResolvedValue, StringComparison.Ordinal))
            {
                error = "raw/resolved divergence";
                return false;
            }
            if (!proof.WasResolved)
            {
                error = "value unresolved";
                return false;
            }
            if (proof.NativeKind != MutationNativeKind.Text)
            {
                error = "unsupported native type";
                return false;
            }
            if (proof.IsLinkedOrExpression)
            {
                error = "linked or expression value";
                return false;
            }
            if (!proof.IsEditable)
            {
                error = "not editable";
                return false;
            }
            if (!proof.IsClean)
            {
                error = "document not clean";
                return false;
            }
            if (proof.IsReadOnly)
            {
                error = "document read-only";
                return false;
            }
            if (!string.Equals(proof.DocumentPath, targetFullPath, StringComparison.OrdinalIgnoreCase))
            {
                error = "active document mismatch";
                return false;
            }
            return true;
        }

        private static bool ProofsMatch(MutationAdmissionProof left, string leftHash, MutationAdmissionProof right, string rightHash)
        {
            if (left == null || right == null) return false;
            if (!string.Equals(left.Literal, right.Literal, StringComparison.Ordinal)) return false;
            if (!string.Equals(leftHash, rightHash, StringComparison.Ordinal)) return false;
            if (!string.Equals(left.DocumentPath, right.DocumentPath, StringComparison.OrdinalIgnoreCase)) return false;
            var leftConfigs = left.Configurations ?? new List<string>();
            var rightConfigs = right.Configurations ?? new List<string>();
            if (leftConfigs.Count != rightConfigs.Count) return false;
            for (int i = 0; i < leftConfigs.Count; i++)
            {
                if (!string.Equals(leftConfigs[i], rightConfigs[i], StringComparison.Ordinal)) return false;
            }
            return true;
        }

        private bool DestinationsWritable(MutationExecutionJournal journal, out string error)
        {
            error = null;
            if (!IsDirectoryWritable(Path.GetDirectoryName(journal.JournalPath)))
            {
                error = "execution journal unwritable";
                return false;
            }
            if (!IsDirectoryWritable(Path.GetDirectoryName(MutationExecutionJournal.DefaultCheckpointPath(ResolvedTestRoot(), "probe", "probe"))))
            {
                error = "checkpoint location unwritable";
                return false;
            }
            if (string.IsNullOrWhiteSpace(_auditLog.CurrentLogPath()))
            {
                error = "tool audit destination unconfigured";
                return false;
            }
            try
            {
                var directory = Path.GetDirectoryName(_auditLog.CurrentLogPath());
                if (!IsDirectoryWritable(directory))
                {
                    error = "tool audit destination unwritable";
                    return false;
                }
            }
            catch
            {
                error = "tool audit destination unwritable";
                return false;
            }
            return true;
        }

        private static bool IsDirectoryWritable(string directory)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(directory)) return false;
                Directory.CreateDirectory(directory);
                var probe = Path.Combine(directory, ".writability");
                File.WriteAllBytes(probe, new byte[0]);
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private IPreviewBuilder CreateDisplayBuilder()
        {
            try
            {
                if (_adapter == null) return null;
                return new CustomPropertyApprovalPreviewBuilder(_adapter, _dispatcher);
            }
            catch
            {
                return null;
            }
        }

        private bool TryConsume(AssistantToolAuthorization token, FrozenRequest frozen, string traceId, long generation, out string error)
        {
            error = null;
            lock (_decisionSync)
            {
                if (!GateIsOpen()
                    || !token.ExpiresUtc.HasValue || token.ExpiresUtc.Value <= _utcNow()
                    || _ownership.IsShutdown
                    || _ownership.Generation != generation
                    || token.ConsumedUtc.HasValue)
                {
                    error = "authority stale at consumption";
                    return false;
                }
                token.ConsumedUtc = _utcNow();
                try
                {
                    _ledgerSink.Append(ConsumedEntry(frozen, token, traceId));
                }
                catch (Exception ex)
                {
                    error = "consumption evidence: " + ex.GetType().Name;
                    return false;
                }
                return true;
            }
        }

        private bool AuthorityFresh(AssistantToolAuthorization token, AssistantToolDescriptor descriptor, long generation, out string error)
        {
            error = null;
            if (!GateIsOpen())
            {
                error = "mutations disabled";
                return false;
            }
            if (descriptor == null || !descriptor.Enabled)
            {
                error = "tool disabled";
                return false;
            }
            if (token == null || !token.ExpiresUtc.HasValue || token.ExpiresUtc.Value <= _utcNow() || token.ConsumedUtc == null)
            {
                error = "authority stale";
                return false;
            }
            if (_ownership.IsShutdown || _ownership.Generation != generation)
            {
                error = "service shutdown";
                return false;
            }
            return true;
        }

        private bool CheckPreSaveIdentity(FrozenRequest frozen, ICustomPropertyMutationSession session, string baselineHash, out string error)
        {
            error = null;
            string activePath = null;
            string identityError = null;
            bool current = false;
            bool marshaled = false;
            try
            {
                marshaled = _dispatcher.TryInvoke(() =>
                {
                    current = session.IsCurrentTarget(out activePath, out identityError);
                });
            }
            catch (Exception ex)
            {
                error = "identity dispatch: " + ex.GetType().Name;
                return false;
            }
            if (!marshaled)
            {
                error = "main-thread marshal unavailable";
                return false;
            }
            if (!current)
            {
                error = identityError ?? "retained identity mismatch";
                return false;
            }
            if (!string.Equals(activePath, frozen.TargetFullPath, StringComparison.OrdinalIgnoreCase))
            {
                error = "active document mismatch";
                return false;
            }
            string hash;
            try
            {
                hash = ComputeFileHash(frozen.TargetFullPath);
            }
            catch (Exception ex)
            {
                error = "pre-save hash: " + ex.GetType().Name;
                return false;
            }
            if (!string.Equals(hash, baselineHash, StringComparison.Ordinal))
            {
                error = "disk changed before save";
                return false;
            }
            return true;
        }

        private AssistantApprovalLedgerEntry ConsumedEntry(FrozenRequest frozen, AssistantToolAuthorization token, string traceId)
        {
            return new AssistantApprovalLedgerEntry
            {
                TimestampUtc = _utcNow().ToString("o", CultureInfo.InvariantCulture),
                TraceId = Evidence(traceId),
                SessionId = Evidence(frozen.SessionId),
                ApprovalId = Evidence(token.ApprovalId),
                CapabilityId = Evidence(token.CapabilityId),
                ArgumentDigest = token.ArgumentDigest,
                Environment = Evidence(token.Environment),
                Event = AssistantApprovalLedgerEntry.Consumed,
                Outcome = "consumed"
            };
        }

        private static string Evidence(string value)
        {
            var redacted = AuditRedactionService.RedactSecrets(value ?? string.Empty);
            return redacted.Length <= EvidenceIdentifierLimit ? redacted : redacted.Substring(0, EvidenceIdentifierLimit);
        }

        private bool TryPersistReceipt(AssistantToolExecutionReceipt receipt, out string error)
        {
            error = null;
            if (receipt == null)
            {
                error = "no receipt";
                return false;
            }
            try
            {
                _auditLog.Record(receipt);
            }
            catch (Exception ex)
            {
                error = "audit append: " + ex.GetType().Name;
                return false;
            }
            try
            {
                var persisted = _auditLog.TailPersisted(25);
                foreach (var candidate in persisted)
                {
                    if (candidate != null && string.Equals(candidate.ReceiptId, receipt.ReceiptId, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
                error = "receipt not on disk";
                return false;
            }
            catch (Exception ex)
            {
                error = "receipt read-back: " + ex.GetType().Name;
                return false;
            }
        }

        // Applied-path telemetry only (single caller): success isLiteral true and the
        // physical/evidence facts are the applied terminal's own. Other terminals record
        // their telemetry inline in FinishTerminal.
        private bool TryRecordTerminalTelemetry(
            string terminal,
            bool success,
            System.Diagnostics.Stopwatch stopwatch,
            FrozenRequest frozen,
            AssistantToolAuthorization token,
            string receiptId,
            string checkpointPath,
            out string error)
        {
            error = null;
            try
            {
                _telemetrySink.Record(terminal, success, stopwatch.Elapsed.TotalMilliseconds, new
                {
                    traceId = Evidence(frozen.TraceId),
                    requestId = Evidence(frozen.RequestId),
                    sessionId = Evidence(frozen.SessionId),
                    approvalId = Evidence(token != null ? token.ApprovalId : null),
                    receiptId = receiptId ?? string.Empty,
                    targetHash = MutationExecutionJournal.HashTarget(frozen.TargetId),
                    physical = "saved-verified",
                    evidence = "complete",
                    checkpoint = checkpointPath != null ? "present" : string.Empty
                });
                return true;
            }
            catch (Exception ex)
            {
                error = "telemetry: " + ex.GetType().Name;
                return false;
            }
        }

        private AssistantToolExecutionReceipt BuildReceipt(
            System.Diagnostics.Stopwatch stopwatch,
            FrozenRequest frozen,
            AssistantToolDescriptor descriptor,
            AssistantToolAuthorization token,
            string checkpointPath,
            string status,
            string message)
        {
            return new AssistantToolExecutionReceipt
            {
                ReceiptId = Guid.NewGuid().ToString("N"),
                TimestampUtc = _utcNow(),
                CorrelationId = Evidence(frozen.TraceId),
                TraceId = Evidence(frozen.TraceId),
                ToolName = ToolName,
                RequestId = Evidence(frozen.RequestId),
                SessionId = Evidence(frozen.SessionId),
                Environment = Evidence(frozen.Environment),
                CapabilityId = descriptor != null ? Evidence(descriptor.CapabilityId ?? descriptor.Name) : string.Empty,
                ExecutionBoundary = descriptor != null ? descriptor.ExecutionBoundary ?? "assistant_tool_service" : "assistant_tool_service",
                AuthorizationState = token != null ? "server_issued" : "none",
                Mode = "HUMAN_APPROVED_MUTATION",
                Version = "1",
                DocumentType = "Unknown",
                State = status,
                DurationMs = stopwatch.Elapsed.TotalMilliseconds,
                MutationCount = frozen.MutatorInvoked ? 1 : 0,
                RiskLevel = descriptor != null ? descriptor.RiskLevel ?? "unknown" : "unknown",
                Allowed = token != null,
                ReadOnly = false,
                ApprovalRequired = true,
                ApprovalGranted = token != null,
                ApprovalId = Evidence(token != null ? token.ApprovalId : null),
                PolicyCode = token != null ? "capability_allow" : "approval_required",
                ResultStatus = status,
                Message = message,
                InputSummary = new Dictionary<string, string>
                {
                    { "parameterCount", "3" },
                    { "property", frozen.Property ?? string.Empty },
                    { "targetHash", MutationExecutionJournal.HashTarget(frozen.TargetId) },
                    { "baselineHash", frozen.BaselineHash ?? string.Empty },
                    { "checkpointHash", frozen.CheckpointHash ?? string.Empty },
                    { "finalHash", frozen.FinalHash ?? string.Empty },
                    { "writeResult", frozen.WriteResult ?? "not-attempted" },
                    { "saveResult", frozen.SaveResult ?? "not-attempted" }
                }
            };
        }

        private async Task<AssistantToolResult> FinishTerminal(
            System.Diagnostics.Stopwatch stopwatch,
            FrozenRequest frozen,
            AssistantToolDescriptor descriptor,
            AssistantToolAuthorization token,
            string checkpointPath,
            string status,
            string message,
            bool telemetrySuccess,
            int attemptCount,
            string physical,
            string evidence,
            string toolName,
            string traceId,
            string reuseReceiptId)
        {
            string requestId = frozen != null ? frozen.RequestId : traceId;
            AssistantToolExecutionReceipt receipt;
            if (frozen != null)
            {
                receipt = BuildReceipt(stopwatch, frozen, descriptor, token, checkpointPath, status, message);
            }
            else
            {
                receipt = new AssistantToolExecutionReceipt
                {
                    ReceiptId = Guid.NewGuid().ToString("N"),
                    TimestampUtc = _utcNow(),
                    CorrelationId = Evidence(traceId),
                    TraceId = Evidence(traceId),
                    ToolName = toolName ?? ToolName,
                    RequestId = Evidence(requestId),
                    SessionId = string.Empty,
                    Environment = string.Empty,
                    ExecutionBoundary = "assistant_tool_service",
                    AuthorizationState = "none",
                    Mode = "HUMAN_APPROVED_MUTATION",
                    Version = "1",
                    DocumentType = "Unknown",
                    State = status,
                    DurationMs = stopwatch.Elapsed.TotalMilliseconds,
                    MutationCount = attemptCount,
                    RiskLevel = "unknown",
                    Allowed = false,
                    ReadOnly = false,
                    ApprovalRequired = true,
                    ApprovalGranted = false,
                    PolicyCode = "approval_required",
                    ResultStatus = status,
                    Message = message,
                    InputSummary = new Dictionary<string, string>()
                };
            }
            if (!string.IsNullOrEmpty(reuseReceiptId))
            {
                receipt.ReceiptId = reuseReceiptId;
            }
            bool auditOk = false;
            try
            {
                _auditLog.Record(receipt);
                auditOk = true;
            }
            catch
            {
                auditOk = false;
            }
            bool telemetryOk = false;
            try
            {
                _telemetrySink.Record(status, telemetrySuccess, stopwatch.Elapsed.TotalMilliseconds, new
                {
                    traceId = Evidence(traceId),
                    requestId = Evidence(requestId),
                    sessionId = Evidence(frozen != null ? frozen.SessionId : null),
                    approvalId = Evidence(token != null ? token.ApprovalId : null),
                    receiptId = receipt.ReceiptId,
                    targetHash = frozen != null ? MutationExecutionJournal.HashTarget(frozen.TargetId) : string.Empty,
                    physical = physical,
                    evidence = evidence,
                    checkpoint = checkpointPath != null ? "present" : string.Empty
                });
                telemetryOk = true;
            }
            catch
            {
                telemetryOk = false;
            }
            string actualEvidence = string.Equals(evidence, "complete", StringComparison.Ordinal) && auditOk && telemetryOk
                ? "complete"
                : "incomplete";
            var items = new List<AssistantToolResultItem>
            {
                new AssistantToolResultItem
                {
                    Id = "mutation",
                    Title = frozen != null ? ("Set " + frozen.Property + " in " + DisplayName(frozen)) : "Set custom property",
                    Subtitle = message,
                    Source = "mutation",
                    Metadata = new Dictionary<string, string>
                    {
                        ["physical"] = physical,
                        ["evidence"] = actualEvidence,
                        ["receiptId"] = receipt.ReceiptId,
                        ["requestId"] = Evidence(requestId),
                        ["approvalId"] = Evidence(token != null ? token.ApprovalId : null),
                        ["target"] = frozen != null ? DisplayName(frozen) : string.Empty,
                        ["targetHash"] = frozen != null ? MutationExecutionJournal.HashTarget(frozen.TargetId) : string.Empty,
                        ["property"] = frozen != null ? frozen.Property ?? string.Empty : string.Empty
                    }
                }
            };
            if (frozen != null && !string.IsNullOrEmpty(checkpointPath))
            {
                items[0].Metadata["recovery"] = "receipt " + receipt.ReceiptId + " / target " + MutationExecutionJournal.HashTarget(frozen.TargetId);
            }
            await Task.FromResult(0).ConfigureAwait(false);
            return new AssistantToolResult
            {
                ToolName = toolName ?? ToolName,
                Status = status,
                Message = message,
                ReadOnly = false,
                TraceId = traceId ?? string.Empty,
                Items = items,
                Receipt = receipt
            };
        }

        // Applied return for the already-persisted-and-acked receipt: builds items and returns
        // the result WITHOUT re-recording the audit entry and WITHOUT re-recording telemetry
        // (both already recorded on the applied path before the evidence_complete journal line).
        private AssistantToolResult FinishApplied(
            FrozenRequest frozen,
            AssistantToolDescriptor descriptor,
            AssistantToolAuthorization token,
            string checkpointPath,
            AssistantToolExecutionReceipt receipt,
            string traceId)
        {
            var items = new List<AssistantToolResultItem>
            {
                new AssistantToolResultItem
                {
                    Id = "mutation",
                    Title = "Set " + frozen.Property + " in " + DisplayName(frozen),
                    Subtitle = "Saved and verified.",
                    Source = "mutation",
                    Metadata = new Dictionary<string, string>
                    {
                        ["physical"] = "saved-verified",
                        ["evidence"] = "complete",
                        ["receiptId"] = receipt != null ? receipt.ReceiptId ?? string.Empty : string.Empty,
                        ["requestId"] = Evidence(frozen.RequestId),
                        ["approvalId"] = Evidence(token != null ? token.ApprovalId : null),
                        ["target"] = DisplayName(frozen),
                        ["targetHash"] = MutationExecutionJournal.HashTarget(frozen.TargetId),
                        ["property"] = frozen.Property ?? string.Empty,
                        ["recovery"] = "receipt " + (receipt != null ? receipt.ReceiptId ?? string.Empty : string.Empty) + " / target " + MutationExecutionJournal.HashTarget(frozen.TargetId)
                    }
                }
            };
            return new AssistantToolResult
            {
                ToolName = ToolName,
                Status = "applied",
                Message = "Saved and verified.",
                ReadOnly = false,
                TraceId = traceId ?? string.Empty,
                Items = items,
                Receipt = receipt
            };
        }

        private static string DisplayName(FrozenRequest frozen)
        {
            string basename = null;
            try
            {
                basename = AuditRedactionService.RedactPath(frozen.TargetFullPath).Basename;
            }
            catch
            {
                basename = null;
            }
            if (string.IsNullOrWhiteSpace(basename)) basename = "unknown file";
            basename = basename.Trim();
            return basename.Length <= 80 ? basename : basename.Substring(0, 80);
        }

        private static bool TryEnterTarget(string targetId)
        {
            lock (_busySync)
            {
                if (_busyTargets.ContainsKey(targetId)) return false;
                _busyTargets[targetId] = true;
                return true;
            }
        }

        private static void ReleaseTarget(string targetId)
        {
            lock (_busySync)
            {
                _busyTargets.Remove(targetId);
            }
        }

        private static string ComputeFileHash(string path)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = File.ReadAllBytes(path);
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private sealed class FrozenRequest
        {
            internal AssistantToolRequest ExecRequest { get; set; }
            internal string RequestId { get; set; }
            internal string SessionId { get; set; }
            internal string Environment { get; set; }
            internal string FilePath { get; set; }
            internal string Property { get; set; }
            internal string NewValue { get; set; }
            internal string TraceId { get; set; }
            internal string TargetFullPath { get; set; }
            internal string TargetId { get; set; }
            internal string TargetHash { get; set; }
            internal string BaselineHash { get; set; }
            internal string ValidatedLiteral { get; set; }
            internal List<string> ConfigurationInventory { get; set; }
            internal string CheckpointHash { get; set; }
            internal bool MutatorInvoked { get; set; }
            internal string WriteResult { get; set; }
            internal string SaveResult { get; set; }
            internal string FinalHash { get; set; }
        }

        // Bound preview wrapper: revalidates the FULL admission facts through the owned reader
        // at the prompt boundary, delegates to the frozen Sprint 03 display builder, and returns
        // a rebuilt preview (frozen DTO shape) whose Before must equal the reader literal and
        // whose summary carries the mandated save consequence. The revalidation is marshaled
        // through the executor's dispatcher (C4 dispatched units): the issuer invokes Build on
        // its background continuation thread, where the production reader's VerifyAccess guard
        // would otherwise refuse every call (R1). Same-thread callers run inline via CheckAccess.
        private sealed class BoundPreviewWrapper : IPreviewBuilder
        {
            internal const string MandatedSummary = "edit and save this test file once.";

            private readonly IPreviewBuilder _inner;
            private readonly IMutationAdmissionReader _reader;
            private readonly ISolidWorksMainThreadDispatcher _dispatcher;
            private readonly MutationAdmissionQuery _query;

            internal BoundPreviewWrapper(IPreviewBuilder inner, IMutationAdmissionReader reader, ISolidWorksMainThreadDispatcher dispatcher, MutationAdmissionQuery query)
            {
                _inner = inner;
                _reader = reader;
                _dispatcher = dispatcher;
                _query = query;
            }

            public ApprovalPreview Build(AssistantToolDescriptor descriptor, AssistantToolRequest request)
            {
                MutationAdmissionProof proof = null;
                string error = null;
                bool proved = false;
                try
                {
                    if (_reader == null || _query == null || _dispatcher == null) return null;
                    if (_dispatcher.CheckAccess())
                    {
                        proved = _reader.TryProveTarget(_query, out proof, out error);
                    }
                    else
                    {
                        bool marshaled = _dispatcher.TryInvoke(() =>
                        {
                            MutationAdmissionProof inline;
                            string inlineError;
                            if (_reader.TryProveTarget(_query, out inline, out inlineError) && inline != null)
                            {
                                proof = inline;
                            }
                            else
                            {
                                error = inlineError;
                            }
                        });
                        if (!marshaled) return null;
                        proved = proof != null;
                    }
                }
                catch
                {
                    return null;
                }
                if (!proved || proof == null) return null;
                string acceptError;
                if (!SetCustomPropertyExecutor.IsProofAcceptable(proof, _query.TargetFullPath, out acceptError)) return null;
                var preview = _inner.Build(descriptor, request);
                if (preview == null) return null;
                if (!string.Equals(preview.Before, proof.Literal, StringComparison.Ordinal)) return null;
                return new ApprovalPreview(preview.Title, MandatedSummary, preview.Before, preview.After, preview.Risk);
            }
        }
    }
}
