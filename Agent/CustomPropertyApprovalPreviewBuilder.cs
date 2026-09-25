using System;
using System.Collections.Generic;
using BlueBrick.Audit.Contracts;
using BlueBrick.Audit.Core;
using BlueBrick.SolidWorks.Adapters;
using BlueBrick.SolidWorks.Runtime;
using BlueBrick.SolidWorks.Snapshots;

namespace BlueBrick.Agent
{
    // Custom-property preview builder (Sprint 03): formats the old→new diff for the
    // approval dialog from the settled file_path / property / value request parameters.
    // The old value is read through the public ICustomPropertyReadAdapter seam, marshaled
    // to the main thread; the concrete internal adapter is wired in the executor ticket,
    // not here. Built and tested here, intentionally unregistered. The builder opens no
    // files directly and enforces no TestFileRoot guard — both belong to the executor.
    internal sealed class CustomPropertyApprovalPreviewBuilder : IPreviewBuilder
    {
        internal const string UnavailableMarker = "current value unavailable - verify in SOLIDWORKS before approving";
        private const int DisplayPathLimit = 80;

        private readonly ICustomPropertyReadAdapter _adapter;
        private readonly ISolidWorksMainThreadDispatcher _dispatcher;

        internal CustomPropertyApprovalPreviewBuilder(
            ICustomPropertyReadAdapter adapter,
            ISolidWorksMainThreadDispatcher dispatcher)
        {
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public ApprovalPreview Build(AssistantToolDescriptor descriptor, AssistantToolRequest request)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (request == null) throw new ArgumentNullException(nameof(request));
            string filePath = Parameter(request, "file_path");
            string property = Parameter(request, "property");
            string value = Parameter(request, "value");
            string oldValue = ReadOldValue(request, property);
            string displayPath = DisplayPath(filePath);
            return new ApprovalPreview(
                "Set " + property + " in " + displayPath,
                "Custom property write: " + property,
                oldValue ?? UnavailableMarker,
                value,
                "MUTATION - custom property write");
        }

        private static string Parameter(AssistantToolRequest request, string name)
        {
            string value = null;
            if (request.Parameters != null)
            {
                request.Parameters.TryGetValue(name, out value);
            }
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Request parameter '" + name + "' is required.", nameof(request));
            return value;
        }

        private string ReadOldValue(AssistantToolRequest request, string property)
        {
            try
            {
                string found = null;
                bool marshaled = false;
                Exception dispatchError = null;
                try
                {
                    marshaled = _dispatcher.TryInvoke(() => { found = Lookup(request, property); });
                }
                catch (Exception ex)
                {
                    dispatchError = ex;
                }
                if (dispatchError != null || !marshaled) return null;
                return found;
            }
            catch
            {
                // Read failures degrade to the unavailable marker, never to a throw:
                // the dialog is still shown and the human decides with eyes open.
                return null;
            }
        }

        private string Lookup(AssistantToolRequest request, string property)
        {
            var auditRequest = new AuditRunRequest
            {
                Mode = AuditOperationMode.READ_ONLY_ANALYST,
                RequestedPropertyNames = new List<string> { property },
                CorrelationId = request.RequestId ?? string.Empty
            };
            List<AuditError> errors;
            PropertyAuditSnapshot snapshot = _adapter.ReadCustomProperties(auditRequest, out errors);
            if (snapshot == null || snapshot.Scopes == null) return null;
            string wanted = (property ?? string.Empty).Trim().ToLowerInvariant();
            // Configuration scopes first (config values override document-level in
            // SOLIDWORKS semantics), then the document-level scope.
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (var scope in snapshot.Scopes)
                {
                    if (scope == null) continue;
                    bool isConfiguration = string.Equals(scope.Scope, "Configuration", StringComparison.OrdinalIgnoreCase);
                    if ((pass == 0) != isConfiguration) continue;
                    if (scope.Properties == null) continue;
                    foreach (var candidate in scope.Properties)
                    {
                        if (candidate == null) continue;
                        string normalized = candidate.NormalizedName;
                        if (string.IsNullOrEmpty(normalized))
                            normalized = (candidate.Name ?? string.Empty).Trim().ToLowerInvariant();
                        if (string.Equals(normalized, wanted, StringComparison.Ordinal))
                            return candidate.ResolvedValue ?? candidate.RawValue;
                    }
                }
            }
            return null;
        }

        private static string DisplayPath(string filePath)
        {
            string basename = null;
            try
            {
                basename = AuditRedactionService.RedactPath(filePath).Basename;
            }
            catch
            {
                basename = null;
            }
            if (string.IsNullOrWhiteSpace(basename)) basename = "unknown file";
            basename = basename.Trim();
            return basename.Length <= DisplayPathLimit ? basename : basename.Substring(0, DisplayPathLimit);
        }
    }
}
