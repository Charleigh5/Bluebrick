using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using BlueBrick.Vault;
using EPDM.Interop.epdm;

namespace BlueBrick.Agent
{
    internal class AssistantToolService
    {
        private const int DefaultLimit = 10;
        private const int MaxLimit = 25;
        private readonly AgentConfig _config;
        private readonly AssistantToolPolicy _policy;
        private readonly AssistantToolAuditLog _auditLog;
        private readonly IAssistantService _assistantService;
        private readonly BlueBrick.SolidWorks.Composition.SolidWorksAuditComposition _auditComposition;

        internal AssistantToolService(AgentConfig config)
            : this(config, null, CreateAuditLog(config), null)
        {
        }

        internal AssistantToolService(AgentConfig config, IAssistantService assistantService)
            : this(config, assistantService, CreateAuditLog(config), null)
        {
        }

        internal AssistantToolService(AgentConfig config, IAssistantService assistantService, AssistantToolAuditLog auditLog)
            : this(config, assistantService, auditLog, null)
        {
        }

        internal AssistantToolService(AgentConfig config, IAssistantService assistantService, BlueBrick.SolidWorks.Composition.SolidWorksAuditComposition auditComposition)
            : this(config, assistantService, CreateAuditLog(config), auditComposition)
        {
        }

        internal AssistantToolService(AgentConfig config, IAssistantService assistantService, AssistantToolAuditLog auditLog, BlueBrick.SolidWorks.Composition.SolidWorksAuditComposition auditComposition)
        {
            _config = config ?? new AgentConfig();
            _policy = new AssistantToolPolicy();
            _auditLog = auditLog ?? new AssistantToolAuditLog();
            _assistantService = assistantService;
            _auditComposition = auditComposition;
        }

        internal IReadOnlyList<AssistantToolDescriptor> GetCatalog()
        {
            var pdmEnabled = _config.AssistantTools?.EnablePdmSearch ?? false;
            var epicorEnabled = IsEpicorSearchConfigured();
            return new[]
            {
                new AssistantToolDescriptor
                {
                    Name = "search_local_vault",
                    DisplayName = "Search Local Vault",
                    Category = "vault",
                    Description = "Searches the local Bluebrick vault index without opening CAD, PDM, or external systems.",
                    ReadOnly = true,
                    RequiresConfirmation = false,
                    Enabled = true,
                    RiskLevel = "low",
                    AuditRequired = true,
                    AllowedInChat = true,
                    RequiresCredential = false
                },
                new AssistantToolDescriptor
                {
                    Name = "search_pdm",
                    DisplayName = "Search PDM",
                    Category = "pdm",
                    Description = "Planned read-only PDM search wrapper for production vault metadata.",
                    ReadOnly = true,
                    RequiresConfirmation = false,
                    Enabled = pdmEnabled,
                    RiskLevel = "medium",
                    AuditRequired = true,
                    AllowedInChat = true,
                    RequiresCredential = true,
                    UnavailableReason = pdmEnabled
                        ? string.Empty
                        : "PDM search is disabled until AssistantTools.EnablePdmSearch is explicitly enabled for this machine."
                },
                new AssistantToolDescriptor
                {
                    Name = "search_epicor",
                    DisplayName = "Search Epicor",
                    Category = "erp",
                    Description = "Planned read-only Epicor search wrapper for parts, customers, quotes, tasks, and opportunities.",
                    ReadOnly = true,
                    RequiresConfirmation = false,
                    Enabled = epicorEnabled,
                    RiskLevel = "medium",
                    AuditRequired = true,
                    AllowedInChat = true,
                    RequiresCredential = true,
                    UnavailableReason = epicorEnabled
                        ? string.Empty
                        : "Epicor search is disabled until AssistantTools.EnableEpicorSearch is true and the configured connection-string environment variable is present."
                },
                new AssistantToolDescriptor
                {
                    Name = "capture_screenshot",
                    DisplayName = "Capture Screenshot",
                    Category = "visual",
                    Description = "Captures the active foreground window into a local screenshot artifact for assistant analysis.",
                    ReadOnly = true,
                    RequiresConfirmation = false,
                    Enabled = true,
                    RiskLevel = "medium",
                    AuditRequired = true,
                    AllowedInChat = false,
                    ManualOnly = true,
                    SendsExternalData = false
                },
                new AssistantToolDescriptor
                {
                    Name = "create_screenshot_review_report",
                    DisplayName = "Create Screenshot Review Report",
                    Category = "assistant-evidence",
                    Description = "Creates a local Markdown review report from screenshot artifact metadata.",
                    ReadOnly = true,
                    RequiresConfirmation = false,
                    Enabled = true,
                    RiskLevel = "low",
                    AuditRequired = true,
                    AllowedInChat = false,
                    ManualOnly = true
                },
                new AssistantToolDescriptor
                {
                    Name = "solidworks.get_active_document_snapshot",
                    DisplayName = "Get Active Document Snapshot",
                    Category = "read",
                    Description = "Read-only snapshot of the active SOLIDWORKS document (type, dirty, custom properties). Never writes, saves, or rebuilds.",
                    ReadOnly = true,
                    RequiresConfirmation = false,
                    Enabled = true,
                    RiskLevel = "low",
                    AuditRequired = true,
                    AllowedInChat = true,
                    RequiresCredential = false,
                    AllowedModes = new[] { "READ_ONLY_ANALYST" },
                    FailureMode = "deny_safe"
                }
            };
        }

        internal IReadOnlyList<AssistantToolExecutionReceipt> GetAuditTail(int limit)
        {
            return _auditLog.Tail(limit);
        }

        internal IReadOnlyList<AssistantToolExecutionReceipt> GetPersistedAuditTail(int limit)
        {
            return _auditLog.TailPersisted(limit);
        }

        internal async Task<AssistantToolResult> ExecuteAsync(AssistantToolRequest request, string traceId)
        {
            request = request ?? new AssistantToolRequest();
            var toolName = Normalize(request.ToolName);
            if (string.IsNullOrWhiteSpace(toolName))
            {
                return Fail(toolName, "invalid", "toolName required", traceId);
            }

            var policy = _policy.EvaluateToolName(toolName);
            if (!policy.Allowed)
            {
                return WithReceipt(
                    Fail(toolName, policy.Code, policy.Message, traceId),
                    request,
                    policy,
                    null,
                    traceId);
            }

            var catalog = GetCatalog();
            var descriptor = catalog.FirstOrDefault(t => string.Equals(t.Name, toolName, StringComparison.OrdinalIgnoreCase));
            if (descriptor == null)
            {
                return WithReceipt(
                    Fail(toolName, "unknown", "Unknown assistant tool.", traceId),
                    request,
                    AssistantToolPolicyDecision.Deny("unknown", "Unknown assistant tool.", true),
                    null,
                    traceId);
            }

            var explicitScope = !string.IsNullOrWhiteSpace(request.ScopeId);
            var scope = AssistantScopeRegistry.Resolve(_config, catalog, request.ScopeId);
            request.ScopeId = scope.Id;
            if (IsSearchTool(toolName))
            {
                if (scope.Id == AssistantScopeRegistry.All)
                {
                    var allResult = await SearchAllScopesAsync(request, catalog, traceId).ConfigureAwait(false);
                    return WithReceipt(allResult, request, policy, descriptor, traceId);
                }

                if (explicitScope && !scope.Enabled)
                {
                    return WithReceipt(
                        Fail(toolName, "scope_unavailable", scope.UnavailableReason ?? "Selected assistant scope is unavailable.", traceId),
                        request,
                        AssistantToolPolicyDecision.Deny("scope_unavailable", scope.UnavailableReason ?? "Selected assistant scope is unavailable.", false),
                        descriptor,
                        traceId);
                }

                if (explicitScope && !scope.ReadOnlyToolNames.Any(t => string.Equals(t, toolName, StringComparison.OrdinalIgnoreCase)))
                {
                    return WithReceipt(
                        Fail(toolName, "scope_mismatch", $"Tool '{toolName}' is not available in the selected scope '{scope.Label}'.", traceId),
                        request,
                        AssistantToolPolicyDecision.Deny("scope_mismatch", "Selected scope does not allow this tool.", false),
                        descriptor,
                        traceId);
                }
            }

            if (!descriptor.Enabled)
            {
                return WithReceipt(
                    Fail(toolName, "disabled", descriptor.UnavailableReason ?? "Tool is not enabled.", traceId),
                    request,
                    AssistantToolPolicyDecision.Deny("disabled", descriptor.UnavailableReason ?? "Tool is not enabled.", false),
                    descriptor,
                    traceId);
            }

            AssistantToolResult result;
            if (toolName == "search_local_vault")
            {
                result = SearchLocalVault(request, traceId);
                return WithReceipt(result, request, policy, descriptor, traceId);
            }

            if (toolName == "search_pdm")
            {
                result = SearchPdmReadOnly(request, traceId);
                return WithReceipt(result, request, policy, descriptor, traceId);
            }

        if (toolName == "search_epicor")
        {
            return await ExecuteEpicorWithReceiptAsync(request, policy, descriptor, traceId).ConfigureAwait(false);
        }

        if (toolName == "capture_screenshot")
        {
            return await ExecuteCaptureScreenshotAsync(request, policy, descriptor, traceId).ConfigureAwait(false);
        }

        if (toolName == "create_screenshot_review_report")
        {
            result = CreateScreenshotReviewReport(request, traceId);
            return WithReceipt(result, request, policy, descriptor, traceId);
        }
        if (toolName == "solidworks.get_active_document_snapshot")
        {
            result = GetActiveDocumentSnapshot(request, traceId);
            return WithReceipt(result, request, policy, descriptor, traceId);
        }

        return WithReceipt(
                Fail(toolName, "unsupported", "Tool execution is not implemented yet.", traceId),
                request,
                AssistantToolPolicyDecision.Deny("unsupported", "Tool execution is not implemented yet.", false),
                descriptor,
                traceId);
        }

    private async Task<AssistantToolResult> SearchAllScopesAsync(
        AssistantToolRequest request,
        IReadOnlyList<AssistantToolDescriptor> catalog,
        string traceId)
    {
        var items = new List<AssistantToolResultItem>();
        var messages = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var toolName in new[] { "search_local_vault", "search_pdm", "search_epicor" })
        {
            var descriptor = catalog.FirstOrDefault(t => string.Equals(t.Name, toolName, StringComparison.OrdinalIgnoreCase));
            if (descriptor == null)
            {
                continue;
            }

            if (!descriptor.Enabled)
            {
                messages.Add(descriptor.DisplayName + " unavailable");
                items.Add(new AssistantToolResultItem
                {
                    Id = descriptor.Name + ":unavailable",
                    Title = descriptor.DisplayName + " unavailable",
                    Subtitle = descriptor.UnavailableReason ?? "Source is unavailable.",
                    Source = descriptor.Category ?? descriptor.Name,
                    Metadata =
                    {
                        ["status"] = "unavailable",
                        ["reason"] = descriptor.UnavailableReason ?? string.Empty
                    }
                });
                continue;
            }

            var scopedRequest = new AssistantToolRequest
            {
                ToolName = toolName,
                Query = request.Query,
                Limit = request.Limit,
                ScopeId = toolName == "search_pdm"
                    ? AssistantScopeRegistry.Pdm
                    : toolName == "search_epicor"
                        ? AssistantScopeRegistry.Epicor
                        : AssistantScopeRegistry.LocalVault,
                Authorization = request.Authorization,
                Parameters = request.Parameters == null
                    ? new Dictionary<string, string>()
                    : new Dictionary<string, string>(request.Parameters)
            };

            var result = toolName == "search_local_vault"
                ? SearchLocalVault(scopedRequest, traceId)
                : toolName == "search_pdm"
                    ? SearchPdmReadOnly(scopedRequest, traceId)
                    : await SearchEpicorPartsReadOnlyAsync(scopedRequest, traceId).ConfigureAwait(false);

            messages.Add(result.Message ?? descriptor.DisplayName);
            foreach (var item in result.Items ?? new List<AssistantToolResultItem>())
            {
                var stable = (item.Source ?? string.Empty) + "|" + (item.Id ?? item.Path ?? item.Title ?? string.Empty);
                if (seen.Add(stable))
                {
                    items.Add(item);
                }
            }
        }

        return new AssistantToolResult
        {
            ToolName = "search_all",
            Status = items.Any(i => i.Metadata.TryGetValue("status", out var status) && status == "unavailable") ? "partial" : "ok",
            Message = string.Join(" ", messages.Where(m => !string.IsNullOrWhiteSpace(m)).ToArray()),
            ReadOnly = true,
            TraceId = traceId,
            Items = items
        };
    }

    private async Task<AssistantToolResult> ExecuteEpicorWithReceiptAsync(
        AssistantToolRequest request,
        AssistantToolPolicyDecision policy,
        AssistantToolDescriptor descriptor,
        string traceId)
    {
        var result = await SearchEpicorPartsReadOnlyAsync(request, traceId).ConfigureAwait(false);
        return WithReceipt(result, request, policy, descriptor, traceId);
    }

    private async Task<AssistantToolResult> ExecuteCaptureScreenshotAsync(
        AssistantToolRequest request,
        AssistantToolPolicyDecision policy,
        AssistantToolDescriptor descriptor,
        string traceId)
    {
        if (_assistantService == null)
        {
            return WithReceipt(
                Fail("capture_screenshot", "unavailable", "Screenshot capture requires an active assistant service.", traceId),
                request, policy, descriptor, traceId);
        }

        try
        {
            var sessionId = request?.Parameters != null && request.Parameters.TryGetValue("sessionId", out var sid) ? sid : null;
            var artifact = await _assistantService.CaptureScreenshotArtifactAsync(sessionId ?? "tool").ConfigureAwait(false);
            return WithReceipt(new AssistantToolResult
            {
                ToolName = "capture_screenshot",
                Status = "success",
                Message = "Screenshot captured: " + (artifact?.Path ?? "unknown path"),
                Items = new List<AssistantToolResultItem>
                {
                    new AssistantToolResultItem
                    {
                        Id = artifact?.ArtifactId ?? "",
                        Title = "Screenshot Artifact",
                        Path = artifact?.Path ?? "",
                        Metadata =
                        {
                            ["capturedUtc"] = artifact?.CapturedUtc.ToString("o") ?? ""
                        }
                    }
                }
            }, request, policy, descriptor, traceId);
        }
        catch (Exception ex)
        {
            return WithReceipt(
                Fail("capture_screenshot", "error", "Screenshot capture failed: " + ex.Message, traceId),
                request, policy, descriptor, traceId);
        }
    }

    private AssistantToolResult CreateScreenshotReviewReport(AssistantToolRequest request, string traceId)
    {
        var artifactJson = GetParameter(request, "artifactJson");
        if (!string.IsNullOrWhiteSpace(artifactJson))
        {
            try
            {
                var artifact = Newtonsoft.Json.JsonConvert.DeserializeObject<AssistantScreenshotArtifact>(artifactJson);
                return AssistantScreenshotReportGenerator.GenerateReviewReport(artifact, traceId);
            }
            catch
            {
                return Fail("create_screenshot_review_report", "invalid", "Screenshot artifact payload could not be parsed.", traceId);
            }
        }

        var artifactPath = GetParameter(request, "artifactPath");
        if (string.IsNullOrWhiteSpace(artifactPath))
        {
            artifactPath = GetParameter(request, "metadataPath");
        }
        if (string.IsNullOrWhiteSpace(artifactPath))
        {
            artifactPath = GetQuery(request);
        }

        return AssistantScreenshotReportGenerator.GenerateReviewReport(artifactPath, traceId);
    }

        private AssistantToolResult GetActiveDocumentSnapshot(AssistantToolRequest request, string traceId)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                if (_auditComposition != null)
                {
                    var run = _auditComposition.GetActiveDocumentSnapshot(traceId, traceId);
                    var errors = run.Errors ?? new System.Collections.Generic.List<BlueBrick.Audit.Contracts.AuditError>();
                    var snap = run.Snapshot;
                    var hasReadFailure = errors.Exists(e => e.Code == BlueBrick.Audit.Contracts.AuditErrorCodes.READ_FAILURE);
                    var hasNoDoc = errors.Exists(e => e.Code == BlueBrick.Audit.Contracts.AuditErrorCodes.NO_ACTIVE_DOCUMENT);
                    var status = hasNoDoc ? "empty" : (hasReadFailure || errors.Count > 0 ? "partial" : "ok");
                    sw.Stop();
                    var metadata = new System.Collections.Generic.Dictionary<string,string>
                    {
                        ["mutation_count"]="0",
                        ["runtime"]=snap?.RuntimeVersion ?? run.Receipt?.RuntimeVersion ?? "",
                        ["duration_ms"]=sw.ElapsedMilliseconds.ToString(),
                        ["correlation"]=traceId ?? "",
                        ["mode"]="READ_ONLY_ANALYST",
                        ["status"]=status
                    };
                    if (hasReadFailure) metadata["warning"] = "READ_FAILURE";
                    var result = new AssistantToolResult
                    {
                        ToolName = "solidworks.get_active_document_snapshot",
                        Status = status,
                        Message = status == "empty" ? "No active SOLIDWORKS document." : (status == "partial" ? "Snapshot partial — some properties unavailable." : "Snapshot captured."),
                        ReadOnly = true,
                        TraceId = traceId,
                        Items = new System.Collections.Generic.List<AssistantToolResultItem> { new AssistantToolResultItem { Id = "snapshot", Title = snap?.Identity?.DocumentType ?? "Unknown", Metadata = metadata } }
                    };
                    return result;
                }
                var adapter = new BlueBrick.SolidWorks.Adapters.SolidWorksCustomPropertyReadAdapter(
                    new BlueBrick.SolidWorks.Runtime.SolidWorksThreadGuard(),
                    BlueBrick.SolidWorks.Runtime.SolidWorksRuntimeInfoFactory.ForMock(),
                    new BlueBrick.Audit.Core.AuditReceiptFactory(),
                    () => null);
                System.Collections.Generic.List<BlueBrick.Audit.Contracts.AuditError> fallbackErrors;
                BlueBrick.SolidWorks.Snapshots.PropertyAuditSnapshot fallbackSnap;
                try
                {
                    fallbackSnap = adapter.ReadCustomProperties(new BlueBrick.Audit.Contracts.AuditRunRequest { CorrelationId = traceId, Mode = BlueBrick.Audit.Contracts.AuditOperationMode.READ_ONLY_ANALYST }, out fallbackErrors);
                }
                catch (BlueBrick.SolidWorks.Runtime.SolidWorksThreadViolationException ex)
                {
                    fallbackErrors = new System.Collections.Generic.List<BlueBrick.Audit.Contracts.AuditError> { new BlueBrick.Audit.Contracts.AuditError { Code = BlueBrick.Audit.Contracts.AuditErrorCodes.READ_FAILURE, CorrelationId = traceId, Message = ex.Message } };
                    fallbackSnap = new BlueBrick.SolidWorks.Snapshots.PropertyAuditSnapshot { Identity = new BlueBrick.SolidWorks.Snapshots.DocumentIdentitySnapshot { DocumentType = "Unknown" }, State = new BlueBrick.SolidWorks.Snapshots.DocumentStateSnapshot() };
                }
                if (fallbackErrors == null) fallbackErrors = new System.Collections.Generic.List<BlueBrick.Audit.Contracts.AuditError>();
                var hasFallbackReadFailure = fallbackErrors.Exists(e => e.Code == BlueBrick.Audit.Contracts.AuditErrorCodes.READ_FAILURE);
                var hasFallbackNoDoc = fallbackErrors.Exists(e => e.Code == BlueBrick.Audit.Contracts.AuditErrorCodes.NO_ACTIVE_DOCUMENT);
                var fallbackStatus = hasFallbackNoDoc ? "empty" : (hasFallbackReadFailure || fallbackErrors.Count > 0 ? "partial" : "ok");
                sw.Stop();
                var fallbackMetadata = new System.Collections.Generic.Dictionary<string,string>
                {
                    ["mutation_count"]="0",
                    ["runtime"]=fallbackSnap?.RuntimeVersion ?? "",
                    ["duration_ms"]=sw.ElapsedMilliseconds.ToString(),
                    ["correlation"]=traceId ?? "",
                    ["mode"]="READ_ONLY_ANALYST",
                    ["status"]=fallbackStatus
                };
                if (hasFallbackReadFailure) fallbackMetadata["warning"] = "READ_FAILURE";
                var fallbackResult = new AssistantToolResult
                {
                    ToolName = "solidworks.get_active_document_snapshot",
                    Status = fallbackStatus,
                    Message = fallbackStatus == "empty" ? "No active SOLIDWORKS document." : (fallbackStatus == "partial" ? "Snapshot partial — some properties unavailable." : "Snapshot captured."),
                    ReadOnly = true,
                    TraceId = traceId,
                    Items = new System.Collections.Generic.List<AssistantToolResultItem> { new AssistantToolResultItem { Id = "snapshot", Title = fallbackSnap?.Identity?.DocumentType ?? "Unknown", Metadata = fallbackMetadata } }
                };
                return fallbackResult;
            }
            catch (Exception ex) { sw.Stop(); return Fail("solidworks.get_active_document_snapshot", "error", ex.Message, traceId); }
        }

        private AssistantToolResult WithReceipt(
            AssistantToolResult result,
            AssistantToolRequest request,
            AssistantToolPolicyDecision policy,
            AssistantToolDescriptor descriptor,
            string traceId)
        {
            result = result ?? Fail(request?.ToolName, "error", "Tool returned no result.", traceId);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            sw.Stop();
            var receipt = AssistantToolExecutionReceipt.Create(
                request,
                policy,
                descriptor,
                request?.Authorization,
                result.Status,
                result.Message,
                traceId);
            receipt.CorrelationId = traceId ?? string.Empty;
            receipt.Mode = "READ_ONLY_ANALYST";
            receipt.MutationCount = 0;
            receipt.DurationMs = sw.Elapsed.TotalMilliseconds;
            if (result.Items != null)
            {
                foreach (var it in result.Items)
                {
                    if (it.Metadata != null && it.Metadata.ContainsKey("duration_ms"))
                    {
                        double d;
                        if (double.TryParse(it.Metadata["duration_ms"], out d)) receipt.DurationMs = d;
                    }
                    if (it.Metadata != null && it.Metadata.ContainsKey("warning") && !receipt.Warnings.Contains(it.Metadata["warning"]))
                        receipt.Warnings.Add(it.Metadata["warning"]);
                }
            }
            if (result.Status == "partial" && !receipt.Warnings.Contains("READ_FAILURE"))
                receipt.Warnings.Add("READ_FAILURE");
            if (result.Status == "partial" && !receipt.ErrorCodes.Contains("READ_FAILURE"))
                receipt.ErrorCodes.Add("READ_FAILURE");
            result.Receipt = receipt;
            _auditLog.Record(receipt);
            return result;
        }

        private AssistantToolResult SearchLocalVault(AssistantToolRequest request, string traceId)
        {
            var query = Normalize(request.Query);
            if (string.IsNullOrWhiteSpace(query) && request.Parameters != null)
            {
                request.Parameters.TryGetValue("query", out query);
                query = Normalize(query);
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                return Fail("search_local_vault", "invalid", "query required", traceId);
            }

            if (query.Length > 200)
            {
                return Fail("search_local_vault", "invalid", "query must be 200 characters or fewer", traceId);
            }

            var limit = request.Limit <= 0 ? DefaultLimit : request.Limit;
            limit = Math.Max(1, Math.Min(limit, MaxLimit));

            try
            {
                var workspace = new LocalVaultWorkspace();
                var results = workspace.Search(query, limit);
                var items = results.Select(r => new AssistantToolResultItem
                {
                    Id = r.Id,
                    Title = r.FileName,
                    Subtitle = r.Description,
                    Path = r.FullPath,
                    Source = "local_vault",
                    Metadata = new Dictionary<string, string>
                    {
                        { "score", r.Score.ToString() },
                        { "partNumber", r.PartNumber ?? string.Empty },
                        { "documentNumber", r.DocumentNumber ?? string.Empty },
                        { "customer", r.Customer ?? string.Empty }
                    }
                }).ToList();

                return new AssistantToolResult
                {
                    ToolName = "search_local_vault",
                    Status = "ok",
                    Message = items.Count == 0 ? "No local vault matches found." : $"Found {items.Count} local vault match(es).",
                    ReadOnly = true,
                    TraceId = traceId,
                    Items = items
                };
            }
            catch (Exception ex)
            {
                return Fail("search_local_vault", "error", "Local vault search failed: " + ex.Message, traceId);
            }
        }

        private AssistantToolResult SearchPdmReadOnly(AssistantToolRequest request, string traceId)
        {
            var query = GetQuery(request);
            if (string.IsNullOrWhiteSpace(query))
            {
                return Fail("search_pdm", "invalid", "query required", traceId);
            }

            if ((_config.AssistantTools?.EnablePdmSearch ?? false) != true)
            {
                return Fail("search_pdm", "disabled", "PDM search is disabled by AssistantTools.EnablePdmSearch.", traceId);
            }

            var limit = BoundLimit(request.Limit, _config.AssistantTools?.PdmMaxResults ?? MaxLimit);

            try
            {
                var vaultName = string.IsNullOrWhiteSpace(_config.Pdm?.VaultName) ? "_PDMVault" : _config.Pdm.VaultName;
                var vault = new EdmVault5();
                if (!vault.IsLoggedIn) vault.LoginAuto(vaultName, 0);

                var search = (IEdmSearch6)vault.CreateUtility(EdmUtility.EdmUtil_Search);
                search.SetToken(EdmSearchToken.Edmstok_FindFiles, true);
                search.SetToken(EdmSearchToken.Edmstok_FindFolders, false);
                search.FileName = "%" + query + "%";

                var items = new List<AssistantToolResultItem>();
                var result = search.GetFirstResult();
                while (result != null && items.Count < limit)
                {
                    items.Add(new AssistantToolResultItem
                    {
                        Id = result.ID.ToString(),
                        Title = System.IO.Path.GetFileName(result.Path),
                        Subtitle = "PDM file search result",
                        Path = result.Path,
                        Source = "pdm",
                        Metadata = new Dictionary<string, string>
                        {
                            { "vault", vaultName }
                        }
                    });
                    result = search.GetNextResult();
                }

                return new AssistantToolResult
                {
                    ToolName = "search_pdm",
                    Status = "ok",
                    Message = items.Count == 0 ? "No PDM matches found." : $"Found {items.Count} PDM match(es).",
                    ReadOnly = true,
                    TraceId = traceId,
                    Items = items
                };
            }
            catch (Exception ex)
            {
                return Fail("search_pdm", "error", "PDM read-only search failed: " + ex.Message, traceId);
            }
        }

        private async Task<AssistantToolResult> SearchEpicorPartsReadOnlyAsync(AssistantToolRequest request, string traceId)
        {
            var query = GetQuery(request);
            if (string.IsNullOrWhiteSpace(query))
            {
                return Fail("search_epicor", "invalid", "query required", traceId);
            }

            if (!IsEpicorSearchConfigured())
            {
                return Fail("search_epicor", "disabled", "Epicor search is not enabled or its connection string environment variable is missing.", traceId);
            }

            var limit = BoundLimit(request.Limit, _config.AssistantTools?.EpicorMaxResults ?? MaxLimit);
            var envName = _config.AssistantTools.EpicorConnectionStringEnvironmentVariable;
            var connectionString = Environment.GetEnvironmentVariable(envName);

            try
            {
                var items = new List<AssistantToolResultItem>();
                using (var conn = new SqlConnection(connectionString))
                using (var command = conn.CreateCommand())
                {
                    command.CommandText = @"
SELECT TOP (@Limit)
       [Part].[PartNum],
       [Part].[PartDescription],
       [Part].[IUM]
FROM [EpicorProd].[Erp].[Part]
WHERE [Part].[Company] = N'VIRAINS'
  AND ([Part].[PartNum] LIKE @Query OR [Part].[PartDescription] LIKE @Query)
ORDER BY [Part].[PartNum];";
                    command.Parameters.AddWithValue("@Limit", limit);
                    command.Parameters.AddWithValue("@Query", "%" + query + "%");
                    await conn.OpenAsync().ConfigureAwait(false);
                    using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync().ConfigureAwait(false))
                        {
                            var partNum = reader[0]?.ToString() ?? string.Empty;
                            var description = reader[1]?.ToString() ?? string.Empty;
                            var unit = reader[2]?.ToString() ?? string.Empty;
                            items.Add(new AssistantToolResultItem
                            {
                                Id = partNum,
                                Title = partNum,
                                Subtitle = description,
                                Source = "epicor",
                                Metadata = new Dictionary<string, string>
                                {
                                    { "unit", unit },
                                    { "queryType", "part" }
                                }
                            });
                        }
                    }
                }

                return new AssistantToolResult
                {
                    ToolName = "search_epicor",
                    Status = "ok",
                    Message = items.Count == 0 ? "No Epicor part matches found." : $"Found {items.Count} Epicor part match(es).",
                    ReadOnly = true,
                    TraceId = traceId,
                    Items = items
                };
            }
            catch (Exception ex)
            {
                return Fail("search_epicor", "error", "Epicor read-only search failed: " + ex.Message, traceId);
            }
        }

        private static AssistantToolResult Fail(string toolName, string status, string message, string traceId)
        {
            return new AssistantToolResult
            {
                ToolName = toolName,
                Status = status,
                Message = message,
                ReadOnly = true,
                TraceId = traceId
            };
        }

        private static AssistantToolAuditLog CreateAuditLog(AgentConfig config)
        {
            var logRoot = config?.Vault?.LogRoot;
            if (string.IsNullOrWhiteSpace(logRoot))
            {
                return new AssistantToolAuditLog();
            }

            return new AssistantToolAuditLog(logRoot);
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        private bool IsEpicorSearchConfigured()
        {
            if ((_config.AssistantTools?.EnableEpicorSearch ?? false) != true) return false;
            var envName = _config.AssistantTools.EpicorConnectionStringEnvironmentVariable;
            return !string.IsNullOrWhiteSpace(envName) &&
                   !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(envName));
        }

        private static string GetQuery(AssistantToolRequest request)
        {
            var query = Normalize(request?.Query);
            if (string.IsNullOrWhiteSpace(query) && request?.Parameters != null)
            {
                request.Parameters.TryGetValue("query", out query);
                query = Normalize(query);
            }

            return query;
        }

        private static string GetParameter(AssistantToolRequest request, string key)
        {
            if (request?.Parameters == null || string.IsNullOrWhiteSpace(key)) return string.Empty;
            return request.Parameters.TryGetValue(key, out var value) ? Normalize(value) : string.Empty;
        }

        private static int BoundLimit(int requested, int max)
        {
            var limit = requested <= 0 ? DefaultLimit : requested;
            return Math.Max(1, Math.Min(limit, Math.Max(1, Math.Min(max, MaxLimit))));
        }

        private static bool IsSearchTool(string toolName)
        {
            return Normalize(toolName).StartsWith("search_", StringComparison.OrdinalIgnoreCase);
        }
    }
}
