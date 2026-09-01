using System;
using System.Collections.Generic;
using BlueBrick.Audit.Contracts;
using BlueBrick.Audit.Core;
using BlueBrick.SolidWorks.Adapters.Internal;
using BlueBrick.SolidWorks.Runtime;
using BlueBrick.SolidWorks.Snapshots;

namespace BlueBrick.SolidWorks.Adapters
{
    public sealed class SolidWorksFeatureTreeReadAdapter : IFeatureTreeReadAdapter
    {
        private readonly ISolidWorksMainThreadDispatcher _dispatcher;
        private readonly SolidWorksRuntimeInfo _runtimeInfo;
        private readonly AuditReceiptFactory _receiptFactory;
        private readonly Func<ISwFeatureSource> _sourceFactory;

        public string AdapterName => "SolidWorksFeatureTreeReadAdapter";

        internal SolidWorksFeatureTreeReadAdapter(
            ISolidWorksMainThreadDispatcher dispatcher,
            SolidWorksRuntimeInfo runtimeInfo,
            AuditReceiptFactory receiptFactory,
            Func<ISwFeatureSource> sourceFactory)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _runtimeInfo = runtimeInfo ?? throw new ArgumentNullException(nameof(runtimeInfo));
            _receiptFactory = receiptFactory ?? throw new ArgumentNullException(nameof(receiptFactory));
            _sourceFactory = sourceFactory ?? throw new ArgumentNullException(nameof(sourceFactory));
        }

        public FeatureTreeSnapshot ReadFeatureTree(AuditRunRequest request, out List<AuditError> errors)
        {
            var localErrors = new List<AuditError>();
            errors = localErrors;
            if (request == null)
            {
                localErrors.Add(new AuditError { Code = AuditErrorCodes.INVALID_MODE, Message = "AuditRunRequest was null.", CorrelationId = string.Empty });
                return EmptySnapshot(string.Empty, "partial", localErrors);
            }
            _dispatcher.VerifyAccess();
            if (request.Mode != AuditOperationMode.READ_ONLY_ANALYST && request.Mode != AuditOperationMode.MOCK)
            {
                localErrors.Add(new AuditError { Code = AuditErrorCodes.INVALID_MODE, CorrelationId = request.CorrelationId, Message = "S4 supports only MOCK or READ_ONLY_ANALYST modes." });
                return EmptySnapshot(string.Empty, "partial", localErrors);
            }
            ISwFeatureSource src;
            try { src = _sourceFactory(); }
            catch (Exception ex)
            {
                localErrors.Add(new AuditError { Code = AuditErrorCodes.NO_ACTIVE_DOCUMENT, CorrelationId = request.CorrelationId, Message = "No active document/feature source: " + ex.Message });
                return EmptySnapshot(string.Empty, "empty", localErrors);
            }
            if (src == null)
            {
                localErrors.Add(new AuditError { Code = AuditErrorCodes.NO_ACTIVE_DOCUMENT, CorrelationId = request.CorrelationId, Message = "No active document in the SOLIDWORKS session." });
                return EmptySnapshot(string.Empty, "empty", localErrors);
            }
            string docHash = string.Empty;
            try { docHash = src.GetDocumentIdentityHash() ?? string.Empty; } catch (Exception ex) { localErrors.Add(new AuditError { Code = AuditErrorCodes.READ_FAILURE, CorrelationId = request.CorrelationId, Message = "GetDocumentIdentityHash failed: " + ex.Message }); }
            var features = new List<FeatureSnapshot>();
            var limitations = new List<string>();
            bool truncated = false;
            try
            {
                var root = src.GetFirstFeature();
                var visited = new HashSet<string>(StringComparer.Ordinal);
                if (root != null)
                    Traverse(root, 0, string.Empty, features, limitations, localErrors, request.CorrelationId, ref truncated, visited);
            }
            catch (Exception ex)
            {
                localErrors.Add(new AuditError { Code = AuditErrorCodes.READ_FAILURE, CorrelationId = request.CorrelationId, Message = "Feature traversal failed: " + ex.Message });
                limitations.Add(AuditErrorCodes.READ_FAILURE);
            }
            if (truncated && !limitations.Contains(FeatureTreeSnapshot.LimitReachedCode)) limitations.Add(FeatureTreeSnapshot.LimitReachedCode);
            if (truncated && !localErrors.Exists(e => e.Code == FeatureTreeSnapshot.LimitReachedCode))
                localErrors.Add(new AuditError { Code = FeatureTreeSnapshot.LimitReachedCode, CorrelationId = request.CorrelationId, Message = "Feature tree truncated to " + FeatureTreeSnapshot.MaxNodes + " nodes or depth " + FeatureTreeSnapshot.MaxDepth });
            string status;
            if (features.Count == 0 && localErrors.Exists(e => e.Code == AuditErrorCodes.NO_ACTIVE_DOCUMENT)) status = "empty";
            else if (truncated || localErrors.Count > 0) status = "partial";
            else status = "ok";
            if (truncated) status = "partial";
            return new FeatureTreeSnapshot
            {
                Features = features,
                Status = status,
                Limitations = new List<string>(new HashSet<string>(limitations)),
                DocumentIdentityHash = docHash,
                TotalCount = features.Count,
                Truncated = truncated
            };
        }

        private void Traverse(ISwFeatureNode node, int depth, string parentId, List<FeatureSnapshot> outFeatures, List<string> limitations, List<AuditError> errors, string correlationId, ref bool truncated, HashSet<string> visited)
        {
            var current = node;
            var currentDepth = depth;
            var currentParent = parentId;
            while (current != null)
            {
                if (outFeatures.Count >= FeatureTreeSnapshot.MaxNodes)
                {
                    truncated = true;
                    return;
                }
                if (currentDepth > FeatureTreeSnapshot.MaxDepth)
                {
                    truncated = true;
                    if (!limitations.Contains(FeatureTreeSnapshot.LimitReachedCode)) limitations.Add(FeatureTreeSnapshot.LimitReachedCode);
                    return;
                }
                string id = string.Empty, name = string.Empty, type = string.Empty, suppression = string.Empty, state = string.Empty;
                var nodeLimitations = new List<string>();
                try { id = current.GetId() ?? string.Empty; } catch (Exception ex) { errors.Add(new AuditError { Code = AuditErrorCodes.READ_FAILURE, CorrelationId = correlationId, Message = "GetId failed: " + ex.Message }); nodeLimitations.Add(AuditErrorCodes.READ_FAILURE); }
                try { name = current.GetName() ?? string.Empty; } catch (Exception ex) { errors.Add(new AuditError { Code = AuditErrorCodes.READ_FAILURE, CorrelationId = correlationId, Message = "GetName failed: " + ex.Message }); nodeLimitations.Add(AuditErrorCodes.READ_FAILURE); }
                try { type = current.GetTypeName() ?? string.Empty; } catch (Exception ex) { errors.Add(new AuditError { Code = AuditErrorCodes.READ_FAILURE, CorrelationId = correlationId, Message = "GetTypeName failed: " + ex.Message }); nodeLimitations.Add(AuditErrorCodes.READ_FAILURE); }
                try { suppression = current.GetSuppressionState() ?? string.Empty; } catch (Exception ex) { errors.Add(new AuditError { Code = AuditErrorCodes.READ_FAILURE, CorrelationId = correlationId, Message = "GetSuppressionState failed: " + ex.Message }); nodeLimitations.Add(AuditErrorCodes.READ_FAILURE); }
                try { state = current.GetState() ?? string.Empty; } catch (Exception ex) { errors.Add(new AuditError { Code = AuditErrorCodes.READ_FAILURE, CorrelationId = correlationId, Message = "GetState failed: " + ex.Message }); nodeLimitations.Add(AuditErrorCodes.READ_FAILURE); }
                if (name.Length > 256) name = name.Substring(0, 256);
                name = AuditRedactionService.RedactSecrets(name);
                if (type.Length > 256) type = type.Substring(0, 256);
                var key = id + "|" + name + "|" + type + "|" + currentDepth + "|" + currentParent;
                if (!visited.Add(key))
                {
                    current = SafeNext(current, correlationId, errors, limitations);
                    continue;
                }
                var snap = new FeatureSnapshot
                {
                    Id = string.IsNullOrWhiteSpace(id) ? DeterministicId(name, type, currentDepth, currentParent, outFeatures.Count) : id,
                    Name = name,
                    Type = type,
                    Depth = currentDepth,
                    Parent = currentParent,
                    Suppression = suppression,
                    State = state,
                    Limitations = nodeLimitations
                };
                outFeatures.Add(snap);
                ISwFeatureNode child = null;
                try { child = current.GetFirstSubFeature(); } catch (Exception ex) { errors.Add(new AuditError { Code = AuditErrorCodes.READ_FAILURE, CorrelationId = correlationId, Message = "GetFirstSubFeature failed: " + ex.Message }); limitations.Add(AuditErrorCodes.READ_FAILURE); }
                if (child != null)
                {
                    if (currentDepth + 1 > FeatureTreeSnapshot.MaxDepth)
                    {
                        truncated = true;
                        if (!limitations.Contains(FeatureTreeSnapshot.LimitReachedCode)) limitations.Add(FeatureTreeSnapshot.LimitReachedCode);
                    }
                    else
                    {
                        Traverse(child, currentDepth + 1, snap.Id, outFeatures, limitations, errors, correlationId, ref truncated, visited);
                        if (truncated) return;
                    }
                }
                current = SafeNext(current, correlationId, errors, limitations);
            }
        }

        private static ISwFeatureNode SafeNext(ISwFeatureNode node, string correlationId, List<AuditError> errors, List<string> limitations)
        {
            try { return node.GetNext(); }
            catch (Exception ex)
            {
                errors.Add(new AuditError { Code = AuditErrorCodes.READ_FAILURE, CorrelationId = correlationId, Message = "GetNext failed: " + ex.Message });
                limitations.Add(AuditErrorCodes.READ_FAILURE);
                return null;
            }
        }

        private static string DeterministicId(string name, string type, int depth, string parent, int index)
        {
            var raw = name + "|" + type + "|" + depth + "|" + parent + "|" + index;
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(raw));
                return "fid-" + BitConverter.ToString(bytes).Replace("-", string.Empty).Substring(0, 12).ToLowerInvariant();
            }
        }

        private static FeatureTreeSnapshot EmptySnapshot(string docHash, string status, List<AuditError> errors)
        {
            return new FeatureTreeSnapshot
            {
                Features = new List<FeatureSnapshot>(),
                Status = status,
                Limitations = new List<string>(),
                DocumentIdentityHash = docHash ?? string.Empty,
                TotalCount = 0,
                Truncated = false
            };
        }
    }
}
