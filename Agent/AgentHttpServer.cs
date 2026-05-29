using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using EPDM.Interop.epdm;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using BlueBrick.Vault;
using DocGenerator;

namespace BlueBrick.Agent
{
    internal class AgentHttpServer
    {
        private readonly ISldWorks _swApp;
        private readonly AgentConfig _config;
        private readonly AgentOverlay _overlay;
        private readonly TelemetryLogger _telemetry;
        private readonly GenerateReviewJobManager _generateReviewJobs;
        private readonly IAssistantService _assistantService;
        private readonly AssistantToolService _assistantTools;
        private readonly ChatGptSessionStore _chatGptSessions;
        private readonly PreviewSessionCoordinator _previewSessions;
        private readonly PreviewActionPolicy _previewActionPolicy;
        private readonly PreviewActionExecutor _previewActionExecutor;
        private readonly RelayTunnelClient _relayTunnel;
        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private static readonly HttpClient _httpClient = new HttpClient();

        internal AgentHttpServer(ISldWorks swApp, AgentConfig config, AgentOverlay overlay)
        {
            _swApp = swApp;
            _config = config;
            _overlay = overlay;
            var logDir = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                AppIdentity.TelemetryFolderName,
                "telemetry");
        _telemetry = new TelemetryLogger(logDir, "events", 0.1, 7, 2048, 500);
        _generateReviewJobs = new GenerateReviewJobManager(swApp, _telemetry);
        _assistantService = new OpenAiAssistantService(config);
        _assistantTools = new AssistantToolService(config, _assistantService);
        _chatGptSessions = new ChatGptSessionStore();
        _previewActionPolicy = new PreviewActionPolicy();
        _relayTunnel = new RelayTunnelClient(config, _telemetry, GetKnownSessionIds, HandleRelayInvocationAsync);
        _previewSessions = new PreviewSessionCoordinator(_chatGptSessions, _relayTunnel, _swApp, _config);
        _previewActionExecutor = new PreviewActionExecutor(_assistantService, _generateReviewJobs, _chatGptSessions, _relayTunnel);
        }

        internal void Start()
        {
            if (_listener != null) return;
            EnsureAuthToken();
        VaultWorkspaceFactory.Current.ReindexSampleFiles();
        _relayTunnel.Start();
            _listener = new HttpListener();
            var prefix = $"http://127.0.0.1:{_config.Agent.BridgePort}/";
            _listener.Prefixes.Add(prefix);
            _listener.Start();
            _cts = new CancellationTokenSource();
            Task.Run(() => ListenLoop(_cts.Token));
        }

        internal void Stop()
        {
            try
            {
                _cts?.Cancel();
                _listener?.Stop();
                _listener?.Close();
            }
            catch
            {
                // ignore
            }
            finally
            {
        _relayTunnel?.Stop();
                _listener = null;
            }
        }

        private async Task ListenLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                HttpListenerContext context = null;
                Stopwatch sw = null;
                string traceId = null;
                try
                {
                    context = await _listener.GetContextAsync();
                    traceId = GetTraceId(context);
                    sw = Stopwatch.StartNew();
                    await HandleRequest(context, traceId);
                    sw.Stop();
                    var status = context.Response?.StatusCode ?? 200;
                    var op = $"{context.Request.HttpMethod} {context.Request.Url.AbsolutePath}";
                    _telemetry.LogEvent("API_CALL", op, status < 400, sw.Elapsed.TotalMilliseconds, new { status, traceId });
                }
                catch (Exception ex)
                {
                    if (context != null)
                    {
                        try { context.Response.StatusCode = 500; } catch { }
                        try { await WriteJson(context, new { error = "Internal server error", traceId }); } catch { }
                    }
                    var duration = sw != null ? sw.Elapsed.TotalMilliseconds : 0;
                    var op = context != null ? $"{context.Request.HttpMethod} {context.Request.Url.AbsolutePath}" : "listener";
                    _telemetry.LogEvent("ERROR", op, false, duration, new { error = ex.Message, traceId });
                }
            }
        }

        private async Task HandleRequest(HttpListenerContext context, string traceId)
        {
            // Verify authentication token
            var token = context.Request.Headers["X-Agent-Auth"];
            var expectedToken = GetAuthToken();
            
            if (string.IsNullOrEmpty(token) || token != expectedToken)
            {
                context.Response.StatusCode = 403;
                await WriteJson(context, new { error = "Invalid or missing authentication token", traceId });
                return;
            }
            
            var path = context.Request.Url.AbsolutePath.ToLowerInvariant();
            
            // Security: Origin/Referer check for PDM/CAD endpoints
            if (path.StartsWith("/pdm/") || path.StartsWith("/sw/"))
            {
                var origin = context.Request.Headers["Origin"];
                if (!string.IsNullOrEmpty(origin))
                {
                    Uri originUri = null;
                    try { originUri = new Uri(origin); } catch { }
                    
                    if (originUri != null && 
                        !originUri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) && 
                        !originUri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase))
                    {
                        context.Response.StatusCode = 403;
                        await WriteJson(context, new { error = "Origin not allowed", traceId });
                        return;
                    }
                }
            }

            var method = context.Request.HttpMethod.ToUpperInvariant();

            if (path == "/agent/overlay/show" && method == "POST")
            {
                _overlay.ShowOverlay();
                await WriteJson(context, new { status = "ok", traceId });
                return;
            }
            if (path == "/agent/overlay/hide" && method == "POST")
            {
                _overlay.HideOverlay();
                await WriteJson(context, new { status = "ok", traceId });
                return;
            }
            if (path == "/agent/telemetry/summary" && method == "GET")
            {
                await WriteJson(context, _telemetry.Summary());
                return;
            }
            if (path == "/agent/telemetry/events" && method == "GET")
            {
                var limitRaw = context.Request.QueryString["limit"];
                var limit = 100;
                if (!string.IsNullOrEmpty(limitRaw) && int.TryParse(limitRaw, out var parsed))
                {
                    limit = Math.Max(1, Math.Min(parsed, 1000));
                }
                await WriteJson(context, new { events = _telemetry.Tail(limit), traceId });
                return;
            }
            if (path == "/agent/telemetry/trace" && method == "GET")
            {
                var traceQuery = context.Request.QueryString["traceId"];
                if (string.IsNullOrEmpty(traceQuery))
                {
                    context.Response.StatusCode = 400;
                    await WriteJson(context, new { error = "traceId required", traceId });
                    return;
                }
                var limitRaw = context.Request.QueryString["limit"];
                var limit = 500;
                if (!string.IsNullOrEmpty(limitRaw) && int.TryParse(limitRaw, out var parsed))
                {
                    limit = Math.Max(1, Math.Min(parsed, 2000));
                }
                var events = _telemetry.FindByTraceId(traceQuery, limit);
                await WriteJson(context, new { events, traceId = traceQuery });
                return;
            }
            if (path == "/agent/selfcheck" && method == "GET")
            {
                var summary = _telemetry.Summary();
                var status = "healthy";
                var errorRate = 0.0;
                try
                {
                    errorRate = Convert.ToDouble(summary["errorRate"]);
                    if (errorRate > 0.2) status = "degraded";
                }
                catch
                {
                    status = "degraded";
                }

                await WriteJson(context, new
                {
                    status,
                    timestamp = DateTime.UtcNow.ToString("O"),
                    uptime_seconds = summary.ContainsKey("uptimeSeconds") ? summary["uptimeSeconds"] : 0,
                components = new object[]
                {
                    new { name = "bridge", status, latency_ms = summary.ContainsKey("averageLatencyMs") ? Convert.ToInt32(summary["averageLatencyMs"]) : 0, message = "" },
                    new { name = "telemetry", status = "healthy", latency_ms = 0, message = "" }
                },
                    traceId
                });
                return;
            }
            if (path == "/agent/knowledge_base/hotset" && method == "GET")
            {
                await WriteJson(context, new
                {
                    customers = Array.Empty<object>(),
                    hotCacheSize = 0,
                    traceId
                });
                return;
            }
            if (path == "/agent/knowledge_base/refresh" && method == "POST")
            {
                await WriteJson(context, new { status = "ok", refreshed = true, traceId });
                return;
            }
        if (path == "/assistant/status" && method == "GET")
        {
            var status = await _assistantService.GetStatusAsync().ConfigureAwait(false);
            var tools = _assistantTools.GetCatalog();
            status.RelayConfigured = !string.IsNullOrWhiteSpace(_config.Relay?.BaseUrl);
            status.RelayConnected = _relayTunnel?.State?.Connected ?? false;
            status.RelayBaseUrl = _config.Relay?.BaseUrl;
            status.ChatWorkspaceUrl = _config.Relay?.ChatWorkspaceUrl;
            status.ToolAvailability = AssistantToolAvailabilitySummary.FromCatalog(tools);
            await WriteJson(context, status).ConfigureAwait(false);
            return;
        }
        if (path == "/assistant/models" && method == "GET")
        {
            var models = await _assistantService.GetModelsAsync().ConfigureAwait(false);
            await WriteJson(context, new { models, traceId }).ConfigureAwait(false);
            return;
        }
        if (path == "/assistant/tools" && method == "GET")
        {
            await WriteJson(context, new { tools = _assistantTools.GetCatalog(), traceId }).ConfigureAwait(false);
            return;
        }
        if (path == "/assistant/tool-audit" && method == "GET")
        {
            var limitRaw = context.Request.QueryString["limit"];
            var limit = 10;
            if (!string.IsNullOrWhiteSpace(limitRaw) && int.TryParse(limitRaw, out var parsed))
            {
                limit = Math.Max(1, Math.Min(parsed, 50));
            }
            var receipts = _assistantTools.GetPersistedAuditTail(limit);
            if (receipts.Count == 0)
            {
                receipts = _assistantTools.GetAuditTail(limit);
            }
            await WriteJson(context, new { receipts, traceId }).ConfigureAwait(false);
            return;
        }
        if (path == "/assistant/integrations" && method == "GET")
        {
            await WriteJson(context, new { integrations = AssistantProductCatalog.GetIntegrations(), traceId }).ConfigureAwait(false);
            return;
        }
        if (path == "/assistant/document-catalog" && method == "GET")
        {
            await WriteJson(context, new { documents = AssistantProductCatalog.GetDocuments(), traceId }).ConfigureAwait(false);
            return;
        }

            var body = await ReadBody(context.Request);
            JObject json;
            if (string.IsNullOrWhiteSpace(body))
            {
                json = new JObject();
            }
            else
            {
                try
                {
                    json = JObject.Parse(body);
                }
                catch
                {
                    context.Response.StatusCode = 400;
                    await WriteJson(context, new { error = "Invalid JSON body", traceId });
                    return;
                }
            }

            switch (path)
            {
                case "/sw/open":
                    await HandleSwOpen(context, json, traceId);
                    return;
                case "/sw/create_drawing":
                    await HandleCreateDrawing(context, json, traceId);
                    return;
                case "/sw/add_views":
                    await HandleAddViews(context, json, traceId);
                    return;
case "/sw/apply_properties":
await HandleApplyProperties(context, json, traceId);
return;
case "/sw/generate_step":
await HandleSwGenerateStep(context, json, traceId);
return;
case "/pdm/check_out":
                    await HandlePdmCheckOut(context, json, traceId);
                    return;
                case "/pdm/check_in":
                    await HandlePdmCheckIn(context, json, traceId);
                    return;
                case "/pdm/search":
                    await HandlePdmSearch(context, json, traceId);
                    return;
        case "/pdm/get_props":
            await HandlePdmGetProps(context, json, traceId);
            return;
        case "/pdm/get_file":
            await HandlePdmGetFile(context, json, traceId);
            return;
                case "/qa/run":
                    await HandleQaRun(context, json, traceId);
                    return;
                case "/sw/live-review/start":
                case "/sw/generate-review":
                    await HandleGenerateReview(context, json, traceId);
                    return;
                case "/sw/live-review/checkpoint":
                    await HandleLiveReviewCheckpoint(context, json, traceId);
                    return;
                case "/sw/live-review/decision":
                    await HandleLiveReviewDecision(context, json, traceId);
                    return;
                case "/sw/live-review/apply-action":
                    await HandleApplyAction(context, json, traceId);
                    return;
                case "/sw/live-review/finalize":
                    await HandleLiveReviewFinalize(context, json, traceId);
                    return;
            case "/assistant/session":
                await HandleAssistantSession(context, traceId);
                return;
            case "/assistant/test":
                await HandleAssistantTest(context, traceId);
                return;
            case "/assistant/mode":
                await HandleAssistantMode(context, json, traceId);
                return;
            case "/assistant/model":
                await HandleAssistantModel(context, json, traceId);
                return;
            case "/assistant/tool":
                await HandleAssistantTool(context, json, traceId);
                return;
            case "/assistant/message":
                await HandleAssistantMessage(context, json, traceId);
                return;
            case "/assistant/screenshot":
                await HandleAssistantScreenshot(context, json, traceId);
                return;
            case "/assistant/screenshot/analyze":
                await HandleAssistantScreenshotAnalyze(context, json, traceId);
                return;
            case "/assistant/history":
                await HandleAssistantHistory(context, traceId);
                return;
            case "/lab/vault/reindex":
                await HandleVaultReindex(context, traceId);
                return;
            case "/lab/vault/reset":
                await HandleVaultReset(context, traceId);
                return;
            case "/lab/vault/status":
                await HandleVaultStatus(context, traceId);
                return;
            case "/chatgpt/session/create":
                await HandleChatGptSessionCreate(context, json, traceId);
                return;
            case "/relay/register":
                await HandleRelayRegister(context, traceId);
                return;
            case "/relay/heartbeat":
                await HandleRelayHeartbeat(context, traceId);
                return;
            case "/relay/tool-result":
                await HandleRelayToolResult(context, json, traceId);
                return;
                case "/sw/jobs/override":
                    await HandleJobOverride(context, json, traceId);
                    return;
                default:
            if (path.StartsWith("/chatgpt/session/"))
            {
                await HandleChatGptSessionRoute(context, path, json, traceId);
                return;
            }
                    if (path.StartsWith("/sw/jobs/"))
                    {
                        if (method == "POST" && path.EndsWith("/override"))
                        {
                            await HandlePathJobOverride(context, path, json, traceId);
                            return;
                        }
                        await HandleGetJob(context, path, traceId);
                        return;
                    }
                    context.Response.StatusCode = 404;
                    await WriteJson(context, new { error = "Not found", traceId });
                    return;
            }
        }

        private async Task HandleSwOpen(HttpListenerContext context, JObject json, string traceId)
        {
            var path = json.Value<string>("path");
            if (string.IsNullOrEmpty(path))
            {
                context.Response.StatusCode = 400;
                await WriteJson(context, new { error = "path required", traceId });
                return;
            }

            var type = ResolveDocType(path);
            var err = 0;
            var warn = 0;
            var doc = _swApp.OpenDoc6(path, type, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref err, ref warn);
            await WriteJson(context, new { status = doc != null ? "ok" : "fail", error = err, warning = warn, traceId });
        }

        private async Task HandleCreateDrawing(HttpListenerContext context, JObject json, string traceId)
        {
            var modelPath = json.Value<string>("modelPath");
            var templatePath = json.Value<string>("templatePath");
            var sheetFormatPath = json.Value<string>("sheetFormatPath");
            if (string.IsNullOrEmpty(templatePath))
            {
                templatePath = Path.Combine(_config.Templates.Root, _config.Templates.Defaults.Drawing);
            }
            if (string.IsNullOrEmpty(sheetFormatPath))
            {
                sheetFormatPath = Path.Combine(_config.Templates.Root, _config.Templates.Defaults.SheetFormat);
            }

            var err = 0;
            var warn = 0;
            if (!string.IsNullOrEmpty(modelPath))
            {
                var modelType = ResolveDocType(modelPath);
                _swApp.OpenDoc6(modelPath, modelType, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref err, ref warn);
            }

            var doc = _swApp.NewDocument(templatePath, 0, 0, 0) as ModelDoc2;
            if (doc == null)
            {
                context.Response.StatusCode = 500;
                await WriteJson(context, new { status = "fail", error = "Failed to create drawing", traceId });
                return;
            }

            var drawingDoc = doc as DrawingDoc;
            ApplySheetFormat(drawingDoc, sheetFormatPath);

            string drawingPath = null;
            if (!string.IsNullOrEmpty(modelPath))
            {
                drawingPath = Path.ChangeExtension(modelPath, ".slddrw");
                    int saveErr = 0;
                    int saveWarn = 0;
                    bool saveOk = doc.SaveAs4(drawingPath, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref saveErr, ref saveWarn);
            }

            await WriteJson(context, new { status = "ok", drawingPath, traceId });
        }

        private async Task HandleAddViews(HttpListenerContext context, JObject json, string traceId)
        {
            var drawingPath = json.Value<string>("drawingPath");
            var modelPath = json.Value<string>("modelPath");
            var viewPreset = json.Value<string>("viewPreset") ?? "standard_3";

            if (string.IsNullOrEmpty(drawingPath) || string.IsNullOrEmpty(modelPath))
            {
                context.Response.StatusCode = 400;
                await WriteJson(context, new { error = "drawingPath and modelPath required", traceId });
                return;
            }

            var err = 0;
            var warn = 0;
            var drawingDoc = _swApp.OpenDoc6(drawingPath, (int)swDocumentTypes_e.swDocDRAWING,
                (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref err, ref warn) as DrawingDoc;

            if (drawingDoc == null)
            {
                context.Response.StatusCode = 500;
                await WriteJson(context, new { error = "Failed to open drawing", traceId });
                return;
            }

            if (viewPreset == "standard_3")
            {
                drawingDoc.Create3rdAngleViews2(modelPath);
            }

            await WriteJson(context, new { status = "ok", traceId });
        }

        private async Task HandleApplyProperties(HttpListenerContext context, JObject json, string traceId)
        {
            var modelPath = json.Value<string>("modelPath");
            var props = json["properties"]?.ToObject<Dictionary<string, string>>();
            if (string.IsNullOrEmpty(modelPath) || props == null)
            {
                context.Response.StatusCode = 400;
                await WriteJson(context, new { error = "modelPath and properties required", traceId });
                return;
            }

            var err = 0;
            var warn = 0;
            var model = _swApp.OpenDoc6(modelPath, ResolveDocType(modelPath), (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref err, ref warn) as ModelDoc2;
            if (model == null)
            {
                context.Response.StatusCode = 500;
                await WriteJson(context, new { error = "Failed to open model", traceId });
                return;
            }

            var mgr = model.Extension.CustomPropertyManager[""];
            foreach (var kv in props)
            {
                mgr.Add3(kv.Key, (int)swCustomInfoType_e.swCustomInfoText, kv.Value,
                    (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
            }
model.SetSaveFlag();
await WriteJson(context, new { status = "ok", traceId });
}

    private async Task HandleSwGenerateStep(HttpListenerContext context, JObject json, string traceId)
    {
        var modelPath = json.Value<string>("modelPath");
        var saveToPdm = json.Value<bool?>("saveToPdm") ?? true;
        if (string.IsNullOrEmpty(modelPath))
        {
            context.Response.StatusCode = 400;
            await WriteJson(context, new { error = "modelPath required", traceId });
            return;
        }

        try
        {
            if (!File.Exists(modelPath))
            {
                try
                {
                    var vaultName = _config?.Pdm?.VaultName;
                    if (string.IsNullOrEmpty(vaultName)) vaultName = "_PDMVault";
                    var vault = new EdmVault5();
                    if (!vault.IsLoggedIn) vault.LoginAuto(vaultName, 0);
                    var pdmFile = vault.GetFileFromPath(modelPath, out IEdmFolder5 folder) as IEdmFile5;
                    if (pdmFile != null)
                    {
                        pdmFile.GetFileCopy(folder.ID, 0);
                    }
                }
                catch (Exception pdmEx)
                {
                    Console.WriteLine($"[STEP_GEN] PDM get failed: {pdmEx.Message}");
                }
            }

            var err = 0;
            var warn = 0;
            var model = _swApp.OpenDoc6(modelPath, ResolveDocType(modelPath), (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref err, ref warn) as ModelDoc2;
            if (model == null)
            {
                await WriteJson(context, new { status = "fail", error = $"Failed to open model (error={err}, warning={warn})", traceId });
                return;
            }

            var options = (int)ClsEnums.EnumGenOptions.Step + (int)ClsEnums.EnumGenOptions.Silent + (int)ClsEnums.EnumGenOptions.One;
            if (saveToPdm) options += (int)ClsEnums.EnumGenOptions.SaveToPdm;

            var generator = new ClsGenerators();
            generator.SetStatus += (sender, args) => { Console.WriteLine($"[STEP_GEN] {args.Message}"); };
            generator.GenerateDoc(_swApp, options, string.Empty);

            var partNo = model.CustomInfo["Part Number"] ?? Path.GetFileNameWithoutExtension(modelPath);
            var stepPath = saveToPdm
                ? Path.Combine(Path.GetDirectoryName(modelPath) ?? "", "..", "DXF-STP's", partNo + ".STEP")
                : Path.ChangeExtension(modelPath, ".STEP");

            _swApp.CloseDoc(modelPath);

            await WriteJson(context, new { status = "ok", stepPath, traceId });
        }
        catch (Exception ex)
        {
            await WriteJson(context, new { status = "fail", error = ex.Message, traceId });
        }
    }

    private string ResolveVaultName()
    {
        var name = _config?.Pdm?.VaultName;
        return !string.IsNullOrEmpty(name) ? name : "_PDMVault";
    }

    private async Task HandlePdmCheckOut(HttpListenerContext context, JObject json, string traceId)
    {
        var paths = json["paths"]?.ToObject<List<string>>();
        if (paths == null || paths.Count == 0)
        {
            context.Response.StatusCode = 400;
            await WriteJson(context, new { error = "paths required", traceId });
            return;
        }

        var vault = new EdmVault5();
        if (!vault.IsLoggedIn) vault.LoginAuto(ResolveVaultName(), 0);

            var results = new List<object>();
            foreach (var path in paths)
            {
                var file = vault.GetFileFromPath(path, out IEdmFolder5 folder) as IEdmFile5;
                if (file == null)
                {
                    results.Add(new { path, status = "not_found" });
                    continue;
                }
                if (!file.IsLocked)
                {
                    file.LockFile(folder.ID, 0);
                }
                results.Add(new { path, status = file.IsLocked ? "checked_out" : "failed" });
            }

            await WriteJson(context, new { status = "ok", results, traceId });
        }

        private async Task HandlePdmCheckIn(HttpListenerContext context, JObject json, string traceId)
        {
            var paths = json["paths"]?.ToObject<List<string>>();
            var comment = json.Value<string>("comment") ?? "Checked in via VIRA agent";
            if (paths == null || paths.Count == 0)
            {
                context.Response.StatusCode = 400;
                await WriteJson(context, new { error = "paths required", traceId });
                return;
            }

            var vault = new EdmVault5();
            if (!vault.IsLoggedIn) vault.LoginAuto(ResolveVaultName(), 0);

            var results = new List<object>();
            foreach (var path in paths)
            {
                var file = vault.GetFileFromPath(path, out IEdmFolder5 folder) as IEdmFile5;
                if (file == null)
                {
                    results.Add(new { path, status = "not_found" });
                    continue;
                }
                if (file.IsLocked)
                {
                    file.UnlockFile(folder.ID, comment);
                    results.Add(new { path, status = "checked_in" });
                }
                else
                {
                    results.Add(new { path, status = "not_locked" });
                }
            }

            await WriteJson(context, new { status = "ok", results, traceId });
        }

    private async Task HandlePdmSearch(HttpListenerContext context, JObject json, string traceId)
    {
        var query = json.Value<string>("query");
        if (string.IsNullOrEmpty(query))
        {
            context.Response.StatusCode = 400;
            await WriteJson(context, new { error = "query required", traceId });
            return;
        }

        try
        {
            var vault = new EdmVault5();
            if (!vault.IsLoggedIn) vault.LoginAuto(ResolveVaultName(), 0);

            var search = (IEdmSearch6)vault.CreateUtility(EdmUtility.EdmUtil_Search);
            search.SetToken(EdmSearchToken.Edmstok_FindFiles, true);
            search.SetToken(EdmSearchToken.Edmstok_FindFolders, false);
            search.FileName = "%" + query + "%.SLD%";

            var result = search.GetFirstResult();
            var results = new List<string>();
            var count = 0;
            while (result != null && count < 50)
            {
                results.Add(result.Path);
                result = search.GetNextResult();
                count++;
            }

            await WriteJson(context, new { status = "ok", results, traceId });
        }
        catch (Exception ex)
        {
            await WriteJson(context, new { status = "fail", error = ex.Message, traceId });
        }
    }

        private async Task HandlePdmGetProps(HttpListenerContext context, JObject json, string traceId)
        {
            var path = json.Value<string>("path");
            if (string.IsNullOrEmpty(path))
            {
                context.Response.StatusCode = 400;
                await WriteJson(context, new { error = "path required", traceId });
                return;
            }

            var vault = new EdmVault5();
            if (!vault.IsLoggedIn) vault.LoginAuto(ResolveVaultName(), 0);
            var file = vault.GetFileFromPath(path, out IEdmFolder5 folder) as IEdmFile5;
            if (file == null)
            {
                context.Response.StatusCode = 404;
                await WriteJson(context, new { error = "not found", traceId });
                return;
            }

        await WriteJson(context, new { status = "ok", name = file.Name, folderId = folder.ID, traceId });
    }

    private async Task HandlePdmGetFile(HttpListenerContext context, JObject json, string traceId)
    {
        var path = json.Value<string>("path");
        if (string.IsNullOrEmpty(path))
        {
            context.Response.StatusCode = 400;
            await WriteJson(context, new { error = "path required", traceId });
            return;
        }

        try
        {
            var vault = new EdmVault5();
            if (!vault.IsLoggedIn) vault.LoginAuto(ResolveVaultName(), 0);
            var file = vault.GetFileFromPath(path, out IEdmFolder5 folder) as IEdmFile5;
            if (file == null)
            {
                await WriteJson(context, new { status = "fail", error = "not found in vault", traceId });
                return;
            }
            file.GetFileCopy(folder.ID, 0);
            await WriteJson(context, new { status = "ok", path, name = file.Name, traceId });
        }
        catch (Exception ex)
        {
            await WriteJson(context, new { status = "fail", error = ex.Message, traceId });
        }
    }

    private async Task HandleQaRun(HttpListenerContext context, JObject json, string traceId)
        {
            var scriptId = json.Value<string>("scriptId");
            if (string.IsNullOrEmpty(scriptId))
            {
                context.Response.StatusCode = 400;
                await WriteJson(context, new { error = "scriptId required", traceId });
                return;
            }

            // proxy to agent service
            var payload = JsonConvert.SerializeObject(new { scriptId });
            var request = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:17178/qa/run");
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
            if (!string.IsNullOrEmpty(traceId))
            {
                request.Headers.Add("X-Trace-Id", traceId);
            }
            var res = await _httpClient.SendAsync(request);
            var body = await res.Content.ReadAsStringAsync();
            context.Response.StatusCode = (int)res.StatusCode;
            await WriteRaw(context, body);
        }

        private async Task HandleGenerateReview(HttpListenerContext context, JObject json, string traceId)
        {
            var req = json.ToObject<GenerateReviewRequest>() ?? new GenerateReviewRequest();
            if (string.IsNullOrEmpty(req.CustomerId))
            {
                context.Response.StatusCode = 400;
                await WriteJson(context, new { error = "customerId required", traceId });
                return;
            }
            if (string.IsNullOrEmpty(req.ViraAccessToken))
            {
                context.Response.StatusCode = 400;
                await WriteJson(context, new { error = "viraAccessToken required", traceId });
                return;
            }

            var result = _generateReviewJobs.Queue(req, traceId);
            await WriteJson(context, result);
        }

        private async Task HandleGetJob(HttpListenerContext context, string path, string traceId)
        {
            var segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 3)
            {
                context.Response.StatusCode = 400;
                await WriteJson(context, new { error = "job id required", traceId });
                return;
            }

            var jobId = segments[2];
            var job = _generateReviewJobs.Get(jobId);
            if (segments.Length >= 4 && string.Equals(segments[3], "manifest", StringComparison.OrdinalIgnoreCase))
            {
                await WriteJson(context, new { jobId, manifest = job.Manifest, traceId });
                return;
            }
            await WriteJson(context, job);
        }

        private async Task HandleJobOverride(HttpListenerContext context, JObject json, string traceId)
        {
            var jobId = json.Value<string>("jobId");
            if (string.IsNullOrEmpty(jobId))
            {
                context.Response.StatusCode = 400;
                await WriteJson(context, new { error = "jobId required", traceId });
                return;
            }
            var reason = json.Value<string>("reason") ?? "Manual override";
            var job = _generateReviewJobs.Override(jobId, reason);
            await WriteJson(context, job);
        }

        private async Task HandleLiveReviewCheckpoint(HttpListenerContext context, JObject json, string traceId)
        {
            var request = json.ToObject<LiveCheckpointRequest>() ?? new LiveCheckpointRequest();
            if (string.IsNullOrEmpty(request.JobId) || string.IsNullOrEmpty(request.PreviewImagePath))
            {
                context.Response.StatusCode = 400;
                await WriteJson(context, new { error = "jobId and previewImagePath required", traceId });
                return;
            }

            var result = _generateReviewJobs.PushCheckpoint(request.JobId, request);
            await WriteJson(context, result);
        }

        private async Task HandleLiveReviewDecision(HttpListenerContext context, JObject json, string traceId)
        {
            var request = json.ToObject<LiveDecisionRequest>() ?? new LiveDecisionRequest();
            if (string.IsNullOrEmpty(request.JobId))
            {
                context.Response.StatusCode = 400;
                await WriteJson(context, new { error = "jobId required", traceId });
                return;
            }

            var result = _generateReviewJobs.SubmitDecision(request.JobId, request);
            await WriteJson(context, result);
        }

        private async Task HandleApplyAction(HttpListenerContext context, JObject json, string traceId)
        {
            var request = json.ToObject<LiveDecisionRequest>() ?? new LiveDecisionRequest();
            if (string.IsNullOrEmpty(request.JobId))
            {
                context.Response.StatusCode = 400;
                await WriteJson(context, new { error = "jobId required", traceId });
                return;
            }

            request.DecisionType = string.IsNullOrEmpty(request.DecisionType) ? "APPLY_ACTION" : request.DecisionType;
            request.DecisionStatus = string.IsNullOrEmpty(request.DecisionStatus) ? "APPROVED" : request.DecisionStatus;
            var result = _generateReviewJobs.SubmitDecision(request.JobId, request);
            await WriteJson(context, result);
        }

        private async Task HandleLiveReviewFinalize(HttpListenerContext context, JObject json, string traceId)
        {
            var jobId = json.Value<string>("jobId");
            if (string.IsNullOrEmpty(jobId))
            {
                context.Response.StatusCode = 400;
                await WriteJson(context, new { error = "jobId required", traceId });
                return;
            }

            var result = _generateReviewJobs.Finalize(jobId);
            await WriteJson(context, result);
        }

        private async Task HandlePathJobOverride(HttpListenerContext context, string path, JObject json, string traceId)
        {
            var segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 4)
            {
                context.Response.StatusCode = 400;
                await WriteJson(context, new { error = "job id required", traceId });
                return;
            }
            var jobId = segments[2];
            var reason = json.Value<string>("reason") ?? "Manual override";
            var job = _generateReviewJobs.Override(jobId, reason);
            await WriteJson(context, job);
        }

        private void ApplySheetFormat(DrawingDoc drawingDoc, string sheetFormatPath)
        {
            try
            {
                if (drawingDoc == null || string.IsNullOrEmpty(sheetFormatPath)) return;
                var sheet = (Sheet)drawingDoc.GetCurrentSheet();
                if (sheet == null) return;
                sheet.SetTemplateName(sheetFormatPath);
                sheet.ReloadTemplate(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to apply sheet format: " + ex.Message);
        }
    }

    private async Task HandleAssistantSession(HttpListenerContext context, string traceId)
        {
            var session = await _assistantService.CreateSessionAsync().ConfigureAwait(false);
            await WriteJson(context, new
            {
                sessionId = session.SessionId,
                createdUtc = session.CreatedUtc,
                traceId
            }).ConfigureAwait(false);
        }

        private async Task HandleAssistantTest(HttpListenerContext context, string traceId)
        {
            var result = await _assistantService.TestConnectionAsync().ConfigureAwait(false);
            await WriteJson(context, new
            {
                success = result.Success,
                mode = result.Mode,
                configured = result.Configured,
                keySource = result.KeySource,
                latencyMs = result.LatencyMs,
                message = result.Message,
                traceId
            }).ConfigureAwait(false);
        }

        private async Task HandleAssistantMode(HttpListenerContext context, JObject json, string traceId)
        {
            var mode = json.Value<string>("mode");
            if (string.IsNullOrWhiteSpace(mode))
            {
                context.Response.StatusCode = 400;
                await WriteJson(context, new { error = "mode required", traceId }).ConfigureAwait(false);
                return;
            }

            var status = await _assistantService.SetModeAsync(mode).ConfigureAwait(false);
            await WriteJson(context, new
            {
                mode = status.AssistantMode,
                configured = status.Configured,
                keySource = status.KeySource,
                traceId
            }).ConfigureAwait(false);
        }

        private async Task HandleAssistantModel(HttpListenerContext context, JObject json, string traceId)
        {
            var modelId = json.Value<string>("modelId");
            if (string.IsNullOrWhiteSpace(modelId))
            {
                context.Response.StatusCode = 400;
                await WriteJson(context, new { error = "modelId required", traceId }).ConfigureAwait(false);
                return;
            }

            var status = await _assistantService.SetModelAsync(modelId).ConfigureAwait(false);
            await WriteJson(context, new
            {
                model = status.Model,
                apiBaseUrl = status.ApiBaseUrl,
                configured = status.Configured,
                keySource = status.KeySource,
                traceId
            }).ConfigureAwait(false);
        }

        private async Task HandleAssistantTool(HttpListenerContext context, JObject json, string traceId)
        {
            var request = json.ToObject<AssistantToolRequest>() ?? new AssistantToolRequest();
            var result = await _assistantTools.ExecuteAsync(request, traceId).ConfigureAwait(false);
            if (result.Status == "invalid")
            {
                context.Response.StatusCode = 400;
            }
            else if (result.Status == "unknown")
            {
                context.Response.StatusCode = 404;
            }

            await WriteJson(context, result).ConfigureAwait(false);
        }

        private async Task HandleAssistantMessage(HttpListenerContext context, JObject json, string traceId)
        {
            var sessionId = json.Value<string>("sessionId");
            var message = json.Value<string>("message");
            var attachments = json["attachmentPaths"]?.ToObject<string[]>() ?? new string[0];
            var result = await _assistantService.SendMessageAsync(sessionId, message, attachments).ConfigureAwait(false);
            await WriteJson(context, new
            {
                sessionId = result.SessionId,
                assistantAvailable = result.AssistantAvailable,
                error = result.Error,
                errorCode = result.ErrorCode,
                message = result.Message,
                traceId
            }).ConfigureAwait(false);
        }

        private async Task HandleAssistantScreenshot(HttpListenerContext context, JObject json, string traceId)
        {
            var request = json.ToObject<AssistantScreenshotCaptureRequest>() ?? new AssistantScreenshotCaptureRequest();
            request.SessionId = string.IsNullOrWhiteSpace(request.SessionId) ? json.Value<string>("sessionId") : request.SessionId;
            var artifact = await _assistantService.CaptureScreenshotArtifactAsync(request).ConfigureAwait(false);
            if (artifact == null || string.IsNullOrWhiteSpace(artifact.Path))
            {
                context.Response.StatusCode = 500;
                await WriteJson(context, new { error = "Failed to capture screenshot.", traceId }).ConfigureAwait(false);
                return;
            }

            await WriteJson(context, new { path = artifact.Path, artifact, traceId }).ConfigureAwait(false);
        }

        private async Task HandleAssistantScreenshotAnalyze(HttpListenerContext context, JObject json, string traceId)
        {
            var request = json.ToObject<AssistantScreenshotAnalysisRequest>() ?? new AssistantScreenshotAnalysisRequest();
            var result = await _assistantService.AnalyzeScreenshotAsync(request).ConfigureAwait(false);
            if (result == null || result.Artifact == null)
            {
                context.Response.StatusCode = 500;
                await WriteJson(context, new { error = "Failed to analyze screenshot.", traceId }).ConfigureAwait(false);
                return;
            }

            await WriteJson(context, new
            {
                status = result.Status,
                message = result.Message,
                mockMode = result.MockMode,
                artifact = result.Artifact,
                traceId
            }).ConfigureAwait(false);
        }

        private async Task HandleAssistantHistory(HttpListenerContext context, string traceId)
        {
            var sessionId = context.Request.QueryString["sessionId"];
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                context.Response.StatusCode = 400;
                await WriteJson(context, new { error = "sessionId required", traceId }).ConfigureAwait(false);
                return;
            }

            var session = await _assistantService.GetSessionAsync(sessionId).ConfigureAwait(false);
            if (session == null)
            {
                context.Response.StatusCode = 404;
                await WriteJson(context, new { error = "Session not found", traceId }).ConfigureAwait(false);
                return;
            }

            await WriteJson(context, new
            {
                sessionId = session.SessionId,
                createdUtc = session.CreatedUtc,
                messages = session.Messages,
                traceId
            }).ConfigureAwait(false);
        }

        private async Task HandleVaultReindex(HttpListenerContext context, string traceId)
        {
            VaultWorkspaceFactory.Current.ReindexSampleFiles();
            await WriteJson(context, new { status = "ok", action = "reindex", traceId }).ConfigureAwait(false);
        }

        private async Task HandleVaultReset(HttpListenerContext context, string traceId)
        {
            VaultWorkspaceFactory.Current.Reset();
            await WriteJson(context, new { status = "ok", action = "reset", traceId }).ConfigureAwait(false);
        }

        private async Task HandleVaultStatus(HttpListenerContext context, string traceId)
        {
            var root = _config.Vault?.Root ?? AppIdentity.LocalVaultRoot;
            await WriteJson(context, new
            {
                status = "ok",
                root,
                sampleSeedRoot = _config.Vault?.SampleSeedRoot,
                traceId
            }).ConfigureAwait(false);
        }

        private async Task HandleChatGptSessionCreate(HttpListenerContext context, JObject json, string traceId)
        {
            var session = _previewSessions.CreateSession(json.Value<string>("lastScreenshotPath"));
            await WriteJson(context, new ChatGptHandoffPayload
            {
                SessionId = session.SessionId,
                HandoffUrl = session.HandoffUrl,
                ChatWorkspaceUrl = _config.Relay?.ChatWorkspaceUrl,
                RelayUrl = _config.Relay?.BaseUrl,
                RelayConfigured = !string.IsNullOrWhiteSpace(_config.Relay?.BaseUrl),
                Message = string.IsNullOrWhiteSpace(_config.Relay?.BaseUrl)
                    ? "Relay is not configured yet. The session was created locally."
                    : "Preview session created and ready for ChatGPT handoff."
            }).ConfigureAwait(false);
        }

        private async Task HandleChatGptSessionRoute(HttpListenerContext context, string path, JObject json, string traceId)
        {
            var segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 3)
            {
                context.Response.StatusCode = 400;
                await WriteJson(context, new { error = "session id required", traceId }).ConfigureAwait(false);
                return;
            }

            var sessionId = segments[2];
            var session = _previewSessions.Get(sessionId);
            if (session == null)
            {
                context.Response.StatusCode = 404;
                await WriteJson(context, new { error = "session not found", traceId }).ConfigureAwait(false);
                return;
            }

            if (segments.Length == 3 && string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                await WriteJson(context, session).ConfigureAwait(false);
                return;
            }

            if (segments.Length >= 4 && string.Equals(segments[3], "confirm", StringComparison.OrdinalIgnoreCase))
            {
                await HandleChatGptConfirm(context, session, json, traceId).ConfigureAwait(false);
                return;
            }

            if (segments.Length >= 4 && string.Equals(segments[3], "action", StringComparison.OrdinalIgnoreCase))
            {
                await HandleChatGptAction(context, session, json, traceId).ConfigureAwait(false);
                return;
            }

            context.Response.StatusCode = 404;
            await WriteJson(context, new { error = "chatgpt session route not found", traceId }).ConfigureAwait(false);
        }

        private async Task HandleChatGptAction(HttpListenerContext context, PreviewSession session, JObject json, string traceId)
        {
            var request = json.ToObject<PreviewActionRequest>() ?? new PreviewActionRequest();
            request.SessionId = session.SessionId;
            if (string.IsNullOrWhiteSpace(request.ActionName))
            {
                context.Response.StatusCode = 400;
                await WriteJson(context, new { error = "actionName required", traceId }).ConfigureAwait(false);
                return;
            }

            var decision = _previewActionPolicy.Evaluate(session, request);
            if (!decision.Allowed)
            {
                await WriteJson(context, BuildActionResult(session.SessionId, request.ActionName, "denied", decision.Reason, traceId)).ConfigureAwait(false);
                return;
            }

            if (decision.RequiresConfirmation)
            {
                var confirmation = _previewSessions.QueueConfirmation(session, request);
                await WriteJson(context, BuildActionResult(session.SessionId, request.ActionName, "pending_confirmation",
                    "Action requires local confirmation.", traceId,
                    new Dictionary<string, string> { ["confirmationId"] = confirmation.ConfirmationId })).ConfigureAwait(false);
                return;
            }

            var result = await _previewActionExecutor.ExecuteAsync(session, request, traceId).ConfigureAwait(false);
            session.History.Add(result);
            _previewSessions.Save(session);
            await WriteJson(context, result).ConfigureAwait(false);
        }

        private async Task HandleChatGptConfirm(HttpListenerContext context, PreviewSession session, JObject json, string traceId)
        {
            var confirmation = json.ToObject<PreviewConfirmationRequest>() ?? new PreviewConfirmationRequest();
            var pending = _previewSessions.ResolveConfirmation(session, confirmation.ConfirmationId);
            if (pending == null)
            {
                context.Response.StatusCode = 404;
                await WriteJson(context, new { error = "confirmation not found", traceId }).ConfigureAwait(false);
                return;
            }

            session.PendingConfirmations.Remove(pending);
            if (!confirmation.Approved)
            {
                var denied = BuildActionResult(session.SessionId, pending.ActionName, "denied",
                    string.IsNullOrWhiteSpace(confirmation.Reason) ? "Action denied by operator." : confirmation.Reason,
                    traceId);
                session.History.Add(denied);
                _previewSessions.Save(session);
                await WriteJson(context, denied).ConfigureAwait(false);
                return;
            }

            var request = new PreviewActionRequest
            {
                SessionId = session.SessionId,
                ActionName = pending.ActionName,
                Parameters = pending.Parameters,
                RequestedBy = "operator-confirmed",
                RequiresConfirmation = false
            };
            var result = await _previewActionExecutor.ExecuteAsync(session, request, traceId).ConfigureAwait(false);
            session.History.Add(result);
            _previewSessions.Save(session);
            await WriteJson(context, result).ConfigureAwait(false);
        }

        private async Task<PreviewActionResult> ExecutePreviewActionAsync(PreviewSession session, PreviewActionRequest request, string traceId)
        {
            return await _previewActionExecutor.ExecuteAsync(session, request, traceId).ConfigureAwait(false);
        }

        private async Task HandleRelayRegister(HttpListenerContext context, string traceId)
        {
            var state = await _relayTunnel.RegisterAsync().ConfigureAwait(false);
            await WriteJson(context, new { state, traceId }).ConfigureAwait(false);
        }

        private async Task HandleRelayHeartbeat(HttpListenerContext context, string traceId)
        {
            var state = await _relayTunnel.HeartbeatAsync().ConfigureAwait(false);
            await WriteJson(context, new { state, traceId }).ConfigureAwait(false);
        }

        private async Task HandleRelayToolResult(HttpListenerContext context, JObject json, string traceId)
        {
            var result = json.ToObject<PreviewActionResult>() ?? new PreviewActionResult();
            if (string.IsNullOrWhiteSpace(result.SessionId))
            {
                context.Response.StatusCode = 400;
                await WriteJson(context, new { error = "sessionId required", traceId }).ConfigureAwait(false);
                return;
            }

            var session = _previewSessions.Get(result.SessionId);
            if (session == null)
            {
                context.Response.StatusCode = 404;
                await WriteJson(context, new { error = "session not found", traceId }).ConfigureAwait(false);
                return;
            }

            result.CreatedUtc = result.CreatedUtc == default ? DateTime.UtcNow : result.CreatedUtc;
            result.TraceId = string.IsNullOrWhiteSpace(result.TraceId) ? traceId : result.TraceId;
            session.History.Add(result);
            _previewSessions.Save(session);
            await WriteJson(context, new { status = "ok", traceId }).ConfigureAwait(false);
        }

        private IEnumerable<string> GetKnownSessionIds()
        {
            return _previewSessions.GetKnownSessionIds();
        }

        private async Task<PreviewActionResult> HandleRelayInvocationAsync(RelayToolInvocation invocation)
        {
            var session = _previewSessions.Get(invocation?.SessionId);
            if (session == null)
            {
                return BuildActionResult(invocation?.SessionId, invocation?.ToolName, "error", "Preview session not found.", Guid.NewGuid().ToString("N"));
            }

            var request = new PreviewActionRequest
            {
                SessionId = session.SessionId,
                ActionName = invocation.ToolName,
                RequestedBy = invocation.RequestedBy,
                RequiresConfirmation = false,
                Parameters = invocation.Arguments ?? new Dictionary<string, string>()
            };

            var decision = _previewActionPolicy.Evaluate(session, request);
            if (!decision.Allowed)
            {
                return BuildActionResult(session.SessionId, request.ActionName, "denied", decision.Reason, Guid.NewGuid().ToString("N"));
            }

            var result = await _previewActionExecutor.ExecuteAsync(session, request, Guid.NewGuid().ToString("N")).ConfigureAwait(false);
            session.History.Add(result);
            _previewSessions.Save(session);
            return result;
        }

        private PreviewActionResult BuildActionResult(string sessionId, string actionName, string status, string message,
            string traceId, Dictionary<string, string> data = null)
        {
            return new PreviewActionResult
            {
                SessionId = sessionId,
                ActionName = actionName,
                Status = status,
                Message = message,
                TraceId = traceId,
                CreatedUtc = DateTime.UtcNow,
                Data = data ?? new Dictionary<string, string>()
        };
        }

    private static int ResolveDocType(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            switch (ext)
            {
                case ".sldprt":
                    return (int)swDocumentTypes_e.swDocPART;
                case ".sldasm":
                    return (int)swDocumentTypes_e.swDocASSEMBLY;
                case ".slddrw":
                    return (int)swDocumentTypes_e.swDocDRAWING;
                default:
                    return (int)swDocumentTypes_e.swDocNONE;
            }
        }

        private static async Task<string> ReadBody(HttpListenerRequest request)
        {
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                return await reader.ReadToEndAsync();
            }
        }

        private static async Task WriteJson(HttpListenerContext context, object obj)
        {
            var json = JsonConvert.SerializeObject(obj);
            await WriteRaw(context, json);
        }

        private static async Task WriteRaw(HttpListenerContext context, string body)
        {
            var buffer = Encoding.UTF8.GetBytes(body);
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            context.Response.OutputStream.Close();
        }

        private static string GetTraceId(HttpListenerContext context)
        {
            var traceId = context.Request.Headers["X-Trace-Id"];
            if (string.IsNullOrEmpty(traceId))
            {
                traceId = Guid.NewGuid().ToString();
            }
            context.Response.Headers["X-Trace-Id"] = traceId;
            return traceId;
        }
        
        private static string GetAuthToken()
        {
            var tokenPath = EnsureAuthToken();
            return File.ReadAllText(tokenPath).Trim();
        }

        private static string EnsureAuthToken()
        {
    var tokenPath = Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                "VIRA",
                ".agent_token");

            var directory = Path.GetDirectoryName(tokenPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!File.Exists(tokenPath) || string.IsNullOrWhiteSpace(File.ReadAllText(tokenPath)))
            {
                File.WriteAllText(tokenPath, Guid.NewGuid().ToString("N"));
            }

            return tokenPath;
        }
    }
}
