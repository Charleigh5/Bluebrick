using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BlueBrick.Audit.Contracts;
using BlueBrick.Audit.Core;
using BlueBrick.SolidWorks.Adapters.Internal;
using BlueBrick.SolidWorks.Runtime;
using BlueBrick.SolidWorks.Snapshots;

namespace BlueBrick.SolidWorks.Adapters
{
    /// <summary>
    /// Default read-only custom-property snapshot adapter. Per
    /// BB-M001 packet §17, this is the SOLIDWORKS-bound implementation
    /// of <see cref="ICustomPropertyReadAdapter"/>. It is constructed
    /// with:
    /// <list type="bullet">
    /// <item>a thread dispatcher (proven SOLIDWORKS UI thread);</item>
    /// <item>a runtime info POCO (captured by Slice 0 / refreshed at
    /// run);</item>
    /// <item>a receipt factory (reused from Slice 1); and</item>
    /// <item>a document source factory delegate (resolves the
    /// <c>IModelDoc2</c>-backed seam for a running session — supplied
    /// by the wiring layer).</item>
    /// </list>
    /// The adapter never launches SOLIDWORKS and never calls
    /// <c>Add3</c>/<c>Set2</c>/<c>Delete2</c>/<c>SetSaveFlag</c>/
    /// <c>Save3</c>/<c>SaveAs4</c>/<c>EditRebuild3</c>/<c>Rebuild</c>.
    /// It never displays a <c>MessageBox</c>.
    /// </summary>
    public sealed class SolidWorksCustomPropertyReadAdapter : ICustomPropertyReadAdapter
    {
        private readonly ISolidWorksMainThreadDispatcher _dispatcher;
        private readonly SolidWorksRuntimeInfo _runtimeInfo;
        private readonly AuditReceiptFactory _receiptFactory;
        private readonly Func<ISwDocumentSource> _documentSourceFactory;

        /// <summary>Initial governed property names per packet §17 (initial list). Used when caller request omits names.</summary>
        public static readonly IReadOnlyList<string> GovernedPropertyNames = new[]
        {
            "Document Number",
            "Part Number",
            "Description",
            "Number",
            "Opp",
            "Revision",
            "Customer",
            "ProductCategory"
        };

        /// <summary>
        /// Create the adapter. The supplied delegates are NOT cached
        /// across reads — every request re-resolves them. Internal because
        /// the constructor exposes the internal <see cref="ISwDocumentSource"/>
        /// seam; wiring happens in-assembly (or via InternalsVisibleTo for
        /// tests).
        /// </summary>
        internal SolidWorksCustomPropertyReadAdapter(
            ISolidWorksMainThreadDispatcher dispatcher,
            SolidWorksRuntimeInfo runtimeInfo,
            AuditReceiptFactory receiptFactory,
            Func<ISwDocumentSource> documentSourceFactory)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _runtimeInfo = runtimeInfo ?? throw new ArgumentNullException(nameof(runtimeInfo));
            _receiptFactory = receiptFactory ?? throw new ArgumentNullException(nameof(receiptFactory));
            _documentSourceFactory = documentSourceFactory ?? throw new ArgumentNullException(nameof(documentSourceFactory));
        }

        /// <inheritdoc />
        public string AdapterName => "SolidWorksCustomPropertyReadAdapter";

        /// <summary>
        /// Read the document's custom properties. Per packet §17, returns
        /// a complete, serializable POCO bundle plus typed partial errors
        /// in <paramref name="errors"/>.
        /// </summary>
        public PropertyAuditSnapshot ReadCustomProperties(AuditRunRequest request, out List<AuditError> errors)
        {
            var localErrors = new List<AuditError>();
            errors = localErrors;

            if (request == null)
            {
                localErrors.Add(new AuditError { Code = AuditErrorCodes.INVALID_MODE, Message = "AuditRunRequest was null.", CorrelationId = string.Empty });
                return new PropertyAuditSnapshot
                {
                    Identity = new DocumentIdentitySnapshot { DocumentType = "Unknown" },
                    State = new DocumentStateSnapshot(),
                    GovernedPropertyNames = GovernedPropertyNames.ToList(),
                    RuntimeClassification = _runtimeInfo.Classification.ToString(),
                    RuntimeVersion = _runtimeInfo.Version?.DisplayVersion ?? string.Empty
                };
            }

            // Step 1: Verify main-thread access. THROWING from VerifyAccess is desired
            // per packet §15 — the caller wraps the COM_THREAD_VIOLATION around itself.
            _dispatcher.VerifyAccess();

            if (request.Mode != AuditOperationMode.READ_ONLY_ANALYST &&
                request.Mode != AuditOperationMode.MOCK)
            {
                localErrors.Add(new AuditError { Code = AuditErrorCodes.INVALID_MODE, CorrelationId = request.CorrelationId, Message = "Slice 1/2 supports only MOCK or READ_ONLY_ANALYST modes." });
                return new PropertyAuditSnapshot
                {
                    Identity = new DocumentIdentitySnapshot { DocumentType = "Unknown" },
                    State = new DocumentStateSnapshot(),
                    GovernedPropertyNames = GovernedPropertyNames.ToList(),
                    RuntimeClassification = _runtimeInfo.Classification.ToString(),
                    RuntimeVersion = _runtimeInfo.Version?.DisplayVersion ?? string.Empty
                };
            }

            // Step 2: Retrieve the document-source seam for this run.
            ISwDocumentSource doc;
            try
            {
                doc = _documentSourceFactory();
            }
            catch (Exception ex)
            {
                localErrors.Add(new AuditError { Code = AuditErrorCodes.NO_ACTIVE_DOCUMENT, CorrelationId = request.CorrelationId, Message = "No active document available: " + ex.Message });
                return new PropertyAuditSnapshot
                {
                    Identity = new DocumentIdentitySnapshot { DocumentType = "Unknown" },
                    State = new DocumentStateSnapshot(),
                    GovernedPropertyNames = GovernedPropertyNames.ToList(),
                    RuntimeClassification = _runtimeInfo.Classification.ToString(),
                    RuntimeVersion = _runtimeInfo.Version?.DisplayVersion ?? string.Empty
                };
            }

            if (doc == null)
            {
                localErrors.Add(new AuditError { Code = AuditErrorCodes.NO_ACTIVE_DOCUMENT, CorrelationId = request.CorrelationId, Message = "No active document in the SOLIDWORKS session." });
                return new PropertyAuditSnapshot
                {
                    Identity = new DocumentIdentitySnapshot { DocumentType = "Unknown" },
                    State = new DocumentStateSnapshot(),
                    GovernedPropertyNames = GovernedPropertyNames.ToList(),
                    RuntimeClassification = _runtimeInfo.Classification.ToString(),
                    RuntimeVersion = _runtimeInfo.Version?.DisplayVersion ?? string.Empty
                };
            }

            // Step 3: Capture active configuration and dirty state BEFORE any property read.
            string activeConfigBefore = doc.GetActiveConfigurationName() ?? string.Empty;
            bool dirtyBefore = doc.GetDirty();

            // Step 4: Build the per-scope snapshots via the seam. No COM object ever escapes.
            var scopes = new List<PropertyScopeSnapshot>();

            var candidateNames = new List<string>(request.RequestedPropertyNames ?? new List<string>());
            foreach (var name in GovernedPropertyNames) if (!candidateNames.Contains(name)) candidateNames.Add(name);

            // Document-level scope.
            var docSource = doc.GetDocumentLevelSource();
            if (docSource != null)
            {
                scopes.Add(BuildScopeSnapshot(docSource, candidateNames, request.CorrelationId, localErrors));
            }

            // Active-configuration scope (default).
            if (!string.IsNullOrEmpty(activeConfigBefore))
            {
                var cfgSource = doc.GetConfigurationSource(activeConfigBefore);
                if (cfgSource != null) scopes.Add(BuildScopeSnapshot(cfgSource, candidateNames, request.CorrelationId, localErrors));
            }

            // Bounded all-config option — only when explicitly requested AND limit>0.
            if (request.ReadAllConfigurations && (request.ConfigurationReadLimit ?? 0) > 0)
            {
                var cfgNames = doc.GetConfigurationNames();
                if (cfgNames == null)
                {
                    // Enumeration unavailable on this interop family. Record an interop limitation.
                    localErrors.Add(new AuditError { Code = AuditErrorCodes.INTEROP_LIMITATION, CorrelationId = request.CorrelationId, Message = "Configuration name enumeration unavailable on installed interop." });
                }
                else
                {
                    int limit = request.ConfigurationReadLimit.Value;
                    int taken = 0;
                    foreach (var cfg in cfgNames)
                    {
                        if (cfg == activeConfigBefore) continue; // already read above
                        if (taken >= limit) break;
                        var cfgSrc = doc.GetConfigurationSource(cfg);
                        if (cfgSrc != null) scopes.Add(BuildScopeSnapshot(cfgSrc, candidateNames, request.CorrelationId, localErrors));
                        taken++;
                    }
                    if (cfgNames.Count > (taken + (cfgNames.Contains(activeConfigBefore) ? 1 : 0)))
                    {
                        // Add a per-bundle limitation note (no error code — bounded by caller's explicit limit).
                        // We surface the limit-reached classifier only via the missing-configs count.
                    }
                }
            }

            // Step 5: Capture active config + dirty state AGAIN to prove unchanged.
            string activeConfigAfter = doc.GetActiveConfigurationName() ?? string.Empty;
            bool dirtyAfter = doc.GetDirty();

            if (!string.Equals(activeConfigBefore, activeConfigAfter, StringComparison.Ordinal))
            {
                localErrors.Add(new AuditError { Code = AuditErrorCodes.READ_FAILURE, CorrelationId = request.CorrelationId, Scope = "ActiveConfiguration", Message = "Active configuration changed during read (before='" + activeConfigBefore + "', after='" + activeConfigAfter + "') — this is an audit invariant violation and indicates the adapter performed a prohibited switch." });
            }
            if (dirtyBefore != dirtyAfter)
            {
                localErrors.Add(new AuditError { Code = AuditErrorCodes.READ_FAILURE, CorrelationId = request.CorrelationId, Scope = "Dirty", Message = "Dirty flag changed during read (before=" + dirtyBefore + ", after=" + dirtyAfter + ") — this is an audit invariant violation." });
            }

            // Step 6: Build the snapshot bundle.
            var (pathHash, basename) = AuditRedactionService.RedactPath(doc.GetPath());
            var identity = new DocumentIdentitySnapshot
            {
                DocumentIdentityHash = pathHash,
                DocumentType = doc.GetDocumentType() ?? "Unknown",
                ActiveConfiguration = activeConfigBefore,
                Basename = basename
            };
            var state = new DocumentStateSnapshot
            {
                DirtyBefore = dirtyBefore,
                DirtyAfter = dirtyAfter,
                IsReadOnly = doc.GetIsReadOnly(),
                ActiveConfigurationBefore = activeConfigBefore,
                ActiveConfigurationAfter = activeConfigAfter,
                AvailableConfigurations = (doc.GetConfigurationNames() ?? new string[0]).ToList()
            };
            var bundle = new PropertyAuditSnapshot
            {
                Identity = identity,
                State = state,
                Scopes = scopes,
                GovernedPropertyNames = candidateNames.OrderBy(x => x, StringComparer.Ordinal).ToList(),
                DiscoveredPropertyNames = scopes.SelectMany(s => s.Properties).Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).Distinct().ToList(),
                Limitations = new List<string>(),
                RuntimeClassification = _runtimeInfo.Classification.ToString(),
                RuntimeVersion = _runtimeInfo.Version?.DisplayVersion ?? string.Empty
            };
            return bundle;
        }

        // Helper: read one scope manager.
        private static PropertyScopeSnapshot BuildScopeSnapshot(ISwCustomPropertySource source, IReadOnlyList<string> candidateNames, string correlationId, List<AuditError> errors)
        {
            var props = new List<CustomPropertySnapshot>();
            foreach (var name in candidateNames)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                string raw, resolved, linked, editable, apiStatus;
                bool wasResolved;
                List<string> lms;
                if (!source.TryGet(name, out raw, out resolved, out wasResolved, out linked, out editable, out apiStatus, out lms))
                {
                    // Property absent from this source — NOT an error per packet §17 (we DO NOT infer missing values).
                    continue;
                }
                props.Add(new CustomPropertySnapshot
                {
                    Name = name,
                    NormalizedName = (name ?? string.Empty).Trim().ToLowerInvariant(),
                    Scope = source.Scope,
                    Configuration = source.ConfigurationName ?? string.Empty,
                    RawValue = raw ?? string.Empty,
                    ResolvedValue = resolved ?? string.Empty,
                    WasResolved = wasResolved,
                    LinkedOrExpressionStatus = linked ?? "Unknown",
                    EditableStatusWhenAvailable = editable ?? "Unknown",
                    ApiStatus = apiStatus ?? "Get2_Fallback",
                    Limitations = lms ?? new List<string>()
                });
            }

            // Optionally record discovered property names available on this manager (when interop supports enumeration).
            var allNames = source.GetPropertyNames();
            var limitations = new List<string>();
            if (allNames == null)
            {
                limitations.Add("interop_GetPropertyNames_unavailable");
            }

            return new PropertyScopeSnapshot
            {
                Scope = source.Scope,
                Configuration = source.ConfigurationName ?? string.Empty,
                Properties = props.OrderBy(p => p.Name, StringComparer.Ordinal).ToList(),
                Limitations = limitations
            };
        }
    }
}
