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

        internal AssistantToolService(AgentConfig config)
            : this(config, null, CreateAuditLog(config))
        {
        }

        internal AssistantToolService(AgentConfig config, IAssistantService assistantService)
            : this(config, assistantService, CreateAuditLog(config))
        {
        }

        internal AssistantToolService(AgentConfig config, IAssistantService assistantService, AssistantToolAuditLog auditLog)
        {
            _config = config ?? new AgentConfig();
            _policy = new AssistantToolPolicy();
            _auditLog = auditLog ?? new AssistantToolAuditLog();
            _assistantService = assistantService;
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
                    AuditRequired = true
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
                    AuditRequired = true
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

        internal Task<AssistantToolResult> ExecuteAsync(AssistantToolRequest request, string traceId)
        {
            request = request ?? new AssistantToolRequest();
            var toolName = Normalize(request.ToolName);
            if (string.IsNullOrWhiteSpace(toolName))
            {
                return Task.FromResult(Fail(toolName, "invalid", "toolName required", traceId));
            }

            var policy = _policy.EvaluateToolName(toolName);
            if (!policy.Allowed)
            {
                return Task.FromResult(WithReceipt(
                    Fail(toolName, policy.Code, policy.Message, traceId),
                    request,
                    policy,
                    null,
                    traceId));
            }

            var descriptor = GetCatalog().FirstOrDefault(t => string.Equals(t.Name, toolName, StringComparison.OrdinalIgnoreCase));
            if (descriptor == null)
            {
                return Task.FromResult(WithReceipt(
                    Fail(toolName, "unknown", "Unknown assistant tool.", traceId),
                    request,
                    AssistantToolPolicyDecision.Deny("unknown", "Unknown assistant tool.", true),
                    null,
                    traceId));
            }

            if (!descriptor.Enabled)
            {
                return Task.FromResult(WithReceipt(
                    Fail(toolName, "disabled", descriptor.UnavailableReason ?? "Tool is not enabled.", traceId),
                    request,
                    AssistantToolPolicyDecision.Deny("disabled", descriptor.UnavailableReason ?? "Tool is not enabled.", false),
                    descriptor,
                    traceId));
            }

            AssistantToolResult result;
            if (toolName == "search_local_vault")
            {
                result = SearchLocalVault(request, traceId);
                return Task.FromResult(WithReceipt(result, request, policy, descriptor, traceId));
            }

            if (toolName == "search_pdm")
            {
                result = SearchPdmReadOnly(request, traceId);
                return Task.FromResult(WithReceipt(result, request, policy, descriptor, traceId));
            }

        if (toolName == "search_epicor")
        {
            return ExecuteEpicorWithReceiptAsync(request, policy, descriptor, traceId);
        }

        if (toolName == "capture_screenshot")
        {
            return ExecuteCaptureScreenshotAsync(request, policy, descriptor, traceId);
        }

        return Task.FromResult(WithReceipt(
                Fail(toolName, "unsupported", "Tool execution is not implemented yet.", traceId),
                request,
                AssistantToolPolicyDecision.Deny("unsupported", "Tool execution is not implemented yet.", false),
                descriptor,
                traceId));
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

        private AssistantToolResult WithReceipt(
            AssistantToolResult result,
            AssistantToolRequest request,
            AssistantToolPolicyDecision policy,
            AssistantToolDescriptor descriptor,
            string traceId)
        {
            result = result ?? Fail(request?.ToolName, "error", "Tool returned no result.", traceId);
            var receipt = AssistantToolExecutionReceipt.Create(
                request,
                policy,
                descriptor,
                request?.Authorization,
                result.Status,
                result.Message,
                traceId);
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

        private static int BoundLimit(int requested, int max)
        {
            var limit = requested <= 0 ? DefaultLimit : requested;
            return Math.Max(1, Math.Min(limit, Math.Max(1, Math.Min(max, MaxLimit))));
        }
    }
}
