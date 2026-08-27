using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using BlueBrick.Agent;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace BlueBrick
{
    public partial class AssistantPanel : UserControl
    {
        private readonly TaskCompletionSource<bool> _pageReady = new TaskCompletionSource<bool>();
        private string _sessionId;
        private string _pendingAttachment;
        private string _assistantMode;
        private string _activeModel = "AionUI";
        private JArray _toolCatalog = new JArray();
        private JArray _toolReceipts = new JArray();
        private JArray _modelCatalog = new JArray();
        private JArray _scopeCatalog = new JArray();
        private JArray _integrationCatalog = new JArray();
        private JArray _documentCatalog = new JArray();
        private string _selectedScopeId = AssistantScopeRegistry.LocalVault;
        private bool _initialized;
        private bool _initFailed;
        private bool _isStreaming;
        private bool _loadingModels;
        private readonly object _initLock = new object();
        private readonly object _errorLogLock = new object();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _sdkStreamRequests = new ConcurrentDictionary<string, CancellationTokenSource>();
        private CancellationTokenSource _streamCts;
        private AssistantWebViewHost _webHost;
        private static readonly int _diagPid = Process.GetCurrentProcess().Id;
        private static readonly object _diagLogLock = new object();

        private const int ReactBridgeReadyMaxAttempts = 40;
        private const int ReactBridgeReadyDelayMilliseconds = 250;

        private static readonly string ReactBridgeReadyProbeScript = @"
(() => {
    const required = [
        'bbReset',
        'bbAppend',
        'bbTypingStart',
        'bbAppendChunk',
        'bbTypingStop',
        'bbSetModel',
        'bbSetModels',
        'bbSetScope',
        'bbSetScopes',
        'bbSetStatus',
        'bbSetTools',
        'bbSetToolReceipts',
        'bbSetProductCatalogs',
        'bbAppendToolResult',
        'bbAppendScreenshotArtifact',
        'bbUpdateScreenshotArtifact',
        'bbGetTranscript'
    ];

    return required.every(
        name => typeof window[name] === 'function'
    );
})()";

        internal enum AssistantStreamEventKind
        {
            Unknown,
            Text,
            ToolCall,
            ToolResult,
            Screenshot,
            Error,
            Final
        }

        internal sealed class AssistantStreamEvent
        {
            internal AssistantStreamEventKind Kind { get; set; }
            internal string Text { get; set; }
            internal JObject Payload { get; set; }
            internal string Id { get; set; }
            internal string Status { get; set; }
            internal bool IsFinal => Kind == AssistantStreamEventKind.Final;
        }

        public AssistantPanel()
        {
            InitializeComponent();
            _webView.Visible = false;
            lblChatStatus.Text = "Ready";
            txtChatInput.Text = "";
            txtChatInput.GotFocus += (s, e) =>
            {
                if (txtChatInput.Text == "Chat") txtChatInput.Text = "";
            };
            txtChatInput.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtChatInput.Text)) txtChatInput.Text = "Chat";
            };
            lblChatStatus.VisibleChanged += (s, e) =>
            {
                if (IsReactShellActive() && lblChatStatus.Visible)
                {
                    lblChatStatus.Visible = false;
                }
            };
        }

        internal async Task EnsureInitializedAsync()
        {
            lock (_initLock)
            {
                if (_initialized || _initFailed) return;
            }

            try
            {
                _webHost = new AssistantWebViewHost(_webView, AgentConfig.Load());
                var webViewReady = await _webHost.InitializeAsync(BuildShellHtml);
                if (!webViewReady)
                {
                    throw new InvalidOperationException(_webHost.LastLoadError ?? "WebView2 unavailable");
                }

                _webView.Visible = true;
                AttachWebViewMessageBridge();
                ConfigureNativeChromeForShell();
                lblChatStatus.Visible = false;

                // NavigationCompleted only proves that the document finished navigation.
                // React installs the legacy window.bb* compatibility surface later from its
                // mount effect. Do not emit authoritative state until that surface exists.
                bool browserBridgeReady =
                    await WaitForReactBridgeReadyAsync();

                _initialized = true;

                if (browserBridgeReady)
                {
                    // Deterministic initial-state replay after all 17 host callbacks exist.
                    await ReplayAuthoritativeWebViewStateAsync();
                }
                else
                {
                    // Preserve the historical fail-soft behavior if the React bridge never
                    // reaches ready state. Do not turn a renderer timing issue into an add-in
                    // initialization failure.
                    await RefreshStatusAsync();
                    await LoadModelsAsync();
                    await LoadToolsAsync();
                    await LoadScopesAsync();
                    await LoadToolAuditAsync();
                    await LoadProductCatalogsAsync();
                }

                // Start/reset the assistant session only after the browser callback surface
                // has had its readiness opportunity. StartSessionAsync retains its existing
                // bbReset/status behavior.
                await StartSessionAsync();
            }
            catch (Exception)
            {
            _initFailed = true;
            lblChatStatus.Text = "WebView2 unavailable";
            _webView.Visible = false;
                lblChatStatus.Visible = true;
                DisableAllButtons();
            }
        }

        private async Task<bool> WaitForReactBridgeReadyAsync()
        {
            if (!IsReactShellActive())
            {
                // The fallback shell installs its window.bb* callbacks inline and
                // synchronously, so its existing initialization behavior remains valid.
                return true;
            }

            for (int attempt = 0;
                 attempt < ReactBridgeReadyMaxAttempts;
                 attempt++)
            {
                if (_webView == null ||
                    _webView.IsDisposed ||
                    _webView.CoreWebView2 == null)
                {
                    return false;
                }

                try
                {
                    string result =
                        await _webView.CoreWebView2.ExecuteScriptAsync(
                            ReactBridgeReadyProbeScript);

                    if (string.Equals(
                        result == null
                            ? string.Empty
                            : result.Trim(),
                        "true",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch (InvalidOperationException)
                {
                    // The renderer can briefly reject script execution while the
                    // navigated React document finishes committing.
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    // Treat transient WebView2 COM timing failure as "not ready"
                    // instead of failing SOLIDWORKS initialization.
                }

                await Task.Delay(
                    ReactBridgeReadyDelayMilliseconds);
            }

            return false;
        }

        private async Task ReplayAuthoritativeWebViewStateAsync()
        {
            if (!_initialized)
            {
                return;
            }

            await RefreshStatusAsync();
            await LoadModelsAsync();
            await LoadToolsAsync();
            await LoadScopesAsync();
            await LoadToolAuditAsync();
            await LoadProductCatalogsAsync();
        }

        private async Task<Newtonsoft.Json.Linq.JObject> ExecuteHostCallbackWithAckAsync(
            string callbackName,
            string argumentListExpression)
        {
            if (_webView == null || _webView.IsDisposed || _webView.CoreWebView2 == null)
            {
                var unavailable = new Newtonsoft.Json.Linq.JObject();
                unavailable["ok"] = false;
                unavailable["stage"] = "webview-unavailable";
                unavailable["callback"] = callbackName ?? string.Empty;
                RecordHostCallbackDispatchAck(unavailable);
                return unavailable;
            }

            string callbackNameJson = Newtonsoft.Json.JsonConvert.SerializeObject(
                callbackName ?? string.Empty);
            string arguments = argumentListExpression ?? string.Empty;

            string script = @"(() => {" +
                @"const name = " + callbackNameJson + @";" +
                @"const fn = window[name];" +
                @"const meta = {" +
                @"href:String(window.location.href)," +
                @"readyState:String(document.readyState)," +
                @"rootPresent:!!document.getElementById('root')" +
                @"};" +
                @"if(typeof fn !== 'function'){" +
                @"return Object.assign({" +
                @"ok:false," +
                @"stage:'missing'," +
                @"callback:name" +
                @"},meta);" +
                @"}" +
                @"try{" +
                @"fn(" + arguments + @");" +
                @"return Object.assign({" +
                @"ok:true," +
                @"stage:'invoked'," +
                @"callback:name" +
                @"},meta);" +
                @"}catch(e){" +
                @"return Object.assign({" +
                @"ok:false," +
                @"stage:'threw'," +
                @"callback:name," +
                @"error:String(" +
                @"e && (e.stack || e.message) ? " +
                @"(e.stack || e.message) : e" +
                @")" +
                @"},meta);" +
                @"}" +
                @"})()";

            string result;
            try
            {
                result = await _webView.CoreWebView2.ExecuteScriptAsync(
                    script);
            }
            catch (Exception ex)
            {
                var transportFailure = new Newtonsoft.Json.Linq.JObject();
                transportFailure["ok"] = false;
                transportFailure["stage"] = "execute-script-exception";
                transportFailure["callback"] = callbackName ?? string.Empty;
                transportFailure["error"] = ex.GetType().FullName + ": " + ex.Message;
                RecordHostCallbackDispatchAck(
                    transportFailure);
                return transportFailure;
            }

            if (string.IsNullOrWhiteSpace(result) ||
                string.Equals(
                    result.Trim(),
                    "null",
                    StringComparison.OrdinalIgnoreCase))
            {
                var nullResult = new Newtonsoft.Json.Linq.JObject();
                nullResult["ok"] = false;
                nullResult["stage"] = "null-result";
                nullResult["callback"] = callbackName ?? string.Empty;
                RecordHostCallbackDispatchAck(
                    nullResult);
                return nullResult;
            }

            Newtonsoft.Json.Linq.JObject ack;
            try
            {
                ack = Newtonsoft.Json.Linq.JObject.Parse(
                    result);
            }
            catch (Exception ex)
            {
                ack = new Newtonsoft.Json.Linq.JObject();
                ack["ok"] = false;
                ack["stage"] = "invalid-result-json";
                ack["callback"] = callbackName ?? string.Empty;
                ack["error"] = ex.GetType().FullName + ": " + ex.Message;
                ack["rawResult"] = result.Length > 512 ? result.Substring(0, 512) : result;
            }

            RecordHostCallbackDispatchAck(
                ack);
            return ack;
        }

        private static void RecordHostCallbackDispatchAck(
            Newtonsoft.Json.Linq.JObject ack)
        {
            try
            {
                string directory = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "BlueBrick",
                    "WebViewDiagnostics");
                System.IO.Directory.CreateDirectory(
                    directory);

                string path = System.IO.Path.Combine(
                    directory,
                    "host_callback_dispatch.jsonl");

                var row = new Newtonsoft.Json.Linq.JObject();
                row["utc"] = DateTime.UtcNow.ToString(
                    "o",
                    System.Globalization.CultureInfo.InvariantCulture);
                row["ack"] = ack;

                System.IO.File.AppendAllText(
                    path,
                    row.ToString(Newtonsoft.Json.Formatting.None) + Environment.NewLine);
            }
            catch
            {
                // Diagnostic receipt failure must never crash SOLIDWORKS.
            }
        }

        private void DisableAllButtons()
        {
            btnNewSession.Enabled = false;
            btnCapture.Enabled = false;
            btnAttach.Enabled = false;
            btnSearchVault.Enabled = false;
            btnReindex.Enabled = false;
            btnResetVault.Enabled = false;
            btnOpenWorking.Enabled = false;
            btnOpenChatGpt.Enabled = false;
            btnTestConnection.Enabled = false;
            btnToggleMode.Enabled = false;
            cmbModel.Enabled = false;
            cmbSearchTool.Enabled = false;
            txtChatInput.Enabled = false;
            btnSend.Enabled = false;
        }

        private bool IsReactShellActive()
        {
            return _webHost != null && _webHost.LoadedReactShell;
        }

        private void ConfigureNativeChromeForShell()
        {
            var react = IsReactShellActive();
            tlpChatButtons.Visible = !react;
            pnlChatInput.Visible = !react;
            if (tlpChatMain.RowStyles.Count > 2)
            {
                tlpChatMain.RowStyles[0].Height = react ? 0F : 72F;
                tlpChatMain.RowStyles[2].Height = react ? 0F : 46F;
            }
            cmbModel.Visible = !react;
            cmbSearchTool.Visible = !react;
            lblChatStatus.Visible = false;
        }

        private void AttachWebViewMessageBridge()
        {
            if (_webView.CoreWebView2 == null) return;
            _webView.CoreWebView2.WebMessageReceived -= WebView_WebMessageReceived;
            _webView.CoreWebView2.WebMessageReceived += WebView_WebMessageReceived;
        }

        private void WebView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            var rawJson = e.WebMessageAsJson;
            TraceBridgeDiagnostic("WEBMESSAGE_ENTRY", "rawLength=" + (rawJson == null ? 0 : rawJson.Length));
            _ = HandleWebViewMessageAsync(rawJson);
        }

        internal static void TraceBridgeDiagnostic(string eventName, string detail)
        {
            try
            {
                var dir = AppIdentity.AssistantHistoryRoot;
                Directory.CreateDirectory(dir);
                var entry = new JObject
                {
                    ["timestamp"] = DateTime.UtcNow.ToString("o"),
                    ["pid"] = _diagPid,
                    ["event"] = eventName ?? "",
                    ["detail"] = detail ?? ""
                };
                var path = Path.Combine(dir, "bridge-diag-" + _diagPid + ".log");
                var line = entry.ToString(Formatting.None);
                lock (_diagLogLock)
                {
                    File.AppendAllText(path, line + Environment.NewLine);
                }
            }
            catch { }
        }

        private async Task HandleWebViewMessageAsync(string rawJson)
        {
            try
            {
                var msg = JObject.Parse(rawJson ?? "{}");
                var type = msg.Value<string>("type") ?? string.Empty;
                TraceBridgeDiagnostic("WEBMESSAGE_PARSED", "type=" + type);
                if (string.Equals(type, "selectModel", StringComparison.OrdinalIgnoreCase))
                {
                    await SelectModelAsync(msg.Value<string>("modelId")).ConfigureAwait(true);
                }
                else if (string.Equals(type, "selectScope", StringComparison.OrdinalIgnoreCase))
                {
                    await SelectScopeAsync(msg.Value<string>("scopeId")).ConfigureAwait(true);
                }
                else if (string.Equals(type, "newSession", StringComparison.OrdinalIgnoreCase))
                {
                    await StartSessionAsync().ConfigureAwait(true);
                }
                else if (string.Equals(type, "captureScreenshot", StringComparison.OrdinalIgnoreCase))
                {
                    await CaptureAsync().ConfigureAwait(true);
                }
                else if (string.Equals(type, "attach", StringComparison.OrdinalIgnoreCase))
                {
                    Attach_Click(this, EventArgs.Empty);
                }
                else if (string.Equals(type, "search", StringComparison.OrdinalIgnoreCase))
                {
                    var requestedScopeId = msg.Value<string>("scopeId");
                    if (!string.IsNullOrWhiteSpace(requestedScopeId))
                    {
                        await SelectScopeAsync(requestedScopeId).ConfigureAwait(true);
                    }
                    var message = msg.Value<string>("message");
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        txtChatInput.Text = message;
                    }
                    await SearchSelectedToolAsync().ConfigureAwait(true);
                }
                else if (string.Equals(type, "sendMessage", StringComparison.OrdinalIgnoreCase))
                {
                    var message = msg.Value<string>("message") ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        txtChatInput.Text = message;
                        await SendAsync().ConfigureAwait(true);
                    }
                }
                else if (string.Equals(type, "sdkSendMessage", StringComparison.OrdinalIgnoreCase))
                {
                    await HandleSdkSendMessageAsync(msg).ConfigureAwait(true);
                }
                else if (string.Equals(type, "sdkCancelMessage", StringComparison.OrdinalIgnoreCase))
                {
                    CancelSdkSendMessage(msg.Value<string>("requestId"));
                }
                else if (string.Equals(type, "cancelMessage", StringComparison.OrdinalIgnoreCase))
                {
                    _streamCts?.Cancel();
                }
                else if (string.Equals(type, "reviewScreenshotItem", StringComparison.OrdinalIgnoreCase))
                {
                    await ReviewScreenshotItemAsync(
                        msg.Value<string>("screenshotId"),
                        msg.Value<string>("targetType"),
                        msg.Value<string>("targetId"),
                        msg.Value<string>("reviewStatus"),
                        msg.Value<string>("reviewNote")).ConfigureAwait(true);
                }
                else if (string.Equals(type, "saveScreenshotAnnotation", StringComparison.OrdinalIgnoreCase))
                {
                    await SaveScreenshotAnnotationAsync(msg).ConfigureAwait(true);
                }
            }
            catch
            {
                // Ignore malformed shell messages. The bridge is intentionally allowlisted.
            }
        }

        private async Task HandleSdkSendMessageAsync(JObject msg)
        {
            var requestId = msg.Value<string>("requestId");
            if (string.IsNullOrWhiteSpace(requestId))
            {
                requestId = Guid.NewGuid().ToString("N");
            }

            var payload = msg["payload"] as JObject ?? new JObject();
            var requestCts = new CancellationTokenSource();
            if (!_sdkStreamRequests.TryAdd(requestId, requestCts))
            {
                EmitSdkStreamError(requestId, "duplicate_request", "A BlueBrick SDK stream request with the same id is already running.");
                requestCts.Dispose();
                return;
            }

            try
            {
                await AgentPanelClient.PostStreamingAsync("/assistant/message/stream", payload, chunk =>
                {
                    EmitSdkStreamEvent(requestId, ParseSdkStreamChunk(chunk), false);
                }, requestCts.Token).ConfigureAwait(false);

                EmitSdkStreamEvent(requestId, null, true);
            }
            catch (OperationCanceledException)
            {
                EmitSdkStreamError(requestId, "aborted", "BlueBrick SDK stream request was canceled.");
            }
            catch (Exception ex)
            {
                var classified = AssistantErrorClassifier.FromException(ex);
                EmitSdkStreamError(requestId, classified.Code, classified.Message);
            }
            finally
            {
                CancelSdkSendMessage(requestId, cancel: false);
                requestCts.Dispose();
            }
        }

        private void CancelSdkSendMessage(string requestId, bool cancel = true)
        {
            if (string.IsNullOrWhiteSpace(requestId)) return;
            if (_sdkStreamRequests.TryRemove(requestId, out var requestCts) && cancel)
            {
                requestCts.Cancel();
            }
        }

        private static JToken ParseSdkStreamChunk(string chunk)
        {
            if (string.IsNullOrWhiteSpace(chunk))
            {
                return new JObject
                {
                    ["type"] = "error",
                    ["errorCode"] = "empty_chunk",
                    ["errorMessage"] = "Assistant stream returned an empty event."
                };
            }

            try
            {
                return JToken.Parse(chunk);
            }
            catch
            {
                return new JObject
                {
                    ["type"] = "error",
                    ["errorCode"] = "malformed_sse",
                    ["errorMessage"] = "Assistant stream returned a malformed event."
                };
            }
        }

        private void EmitSdkStreamEvent(string requestId, JToken eventToken, bool done)
        {
            var envelope = new JObject
            {
                ["requestId"] = requestId ?? string.Empty
            };
            if (eventToken != null) envelope["event"] = eventToken;
            if (done) envelope["done"] = true;
            ExecuteSdkStreamCallback(envelope);
        }

        private void EmitSdkStreamError(string requestId, string code, string message)
        {
            var envelope = new JObject
            {
                ["requestId"] = requestId ?? string.Empty,
                ["error"] = new JObject
                {
                    ["code"] = string.IsNullOrWhiteSpace(code) ? "request_failed" : code,
                    ["message"] = string.IsNullOrWhiteSpace(message) ? "BlueBrick SDK stream request failed." : message
                }
            };
            ExecuteSdkStreamCallback(envelope);
        }

        private void ExecuteSdkStreamCallback(JObject envelope)
        {
            if (_webView?.CoreWebView2 == null) return;
            var json = envelope.ToString(Formatting.None);
            var script = "window.bbSdkStreamEvent&&window.bbSdkStreamEvent(" + json + ");";
            _ = _webView.ExecuteScriptAsync(script);
        }

        private async Task SaveScreenshotAnnotationAsync(JObject msg)
        {
            var screenshotId = msg.Value<string>("screenshotId");
            if (string.IsNullOrWhiteSpace(screenshotId))
            {
                return;
            }

            var annotation = msg["annotation"] as JObject ?? new JObject();
            var annotations = new JArray { annotation };
            var result = await AgentPanelClient.SaveScreenshotAnnotationsAsync(
                screenshotId,
                annotations,
                msg.Value<int?>("imageWidth") ?? 0,
                msg.Value<int?>("imageHeight") ?? 0).ConfigureAwait(true);

            if (result.Ok)
            {
                var artifact = result.Data?["artifact"] as JObject;
                if (artifact != null && _webView != null)
                {
                    var payload = NormalizeScreenshotArtifact(artifact);
                    await _webView.ExecuteScriptAsync("if(window.bbUpdateScreenshotArtifact)window.bbUpdateScreenshotArtifact(" + payload.ToString(Formatting.None) + ");").ConfigureAwait(true);
                }

                await AppendMessageAsync(
                    "assistant",
                    "Annotation saved locally for review. It will remain pending until approved.",
                    null).ConfigureAwait(true);
                return;
            }

            await AppendMessageAsync(
                "assistant",
                "Annotation save failed: " + (result.Error ?? "unknown error"),
                null).ConfigureAwait(true);
        }

        private async Task ReviewScreenshotItemAsync(
            string screenshotId,
            string targetType,
            string targetId,
            string reviewStatus,
            string reviewNote)
        {
            if (string.IsNullOrWhiteSpace(screenshotId) ||
                string.IsNullOrWhiteSpace(targetType) ||
                string.IsNullOrWhiteSpace(targetId) ||
                string.IsNullOrWhiteSpace(reviewStatus))
            {
                return;
            }

            var result = await AgentPanelClient.ReviewScreenshotItemAsync(
                screenshotId,
                targetType,
                targetId,
                reviewStatus,
                reviewNote).ConfigureAwait(true);

            if (result.Ok)
            {
                var artifact = result.Data?["artifact"] as JObject;
                if (artifact != null && _webView != null)
                {
                    var payload = NormalizeScreenshotArtifact(artifact);
                    await _webView.ExecuteScriptAsync("if(window.bbUpdateScreenshotArtifact)window.bbUpdateScreenshotArtifact(" + payload.ToString(Formatting.None) + ");").ConfigureAwait(true);
                }

                await AppendMessageAsync(
                    "assistant",
                    "Review updated locally: " + targetType + " " + targetId + " -> " + reviewStatus + ".",
                    null).ConfigureAwait(true);
                return;
            }

            await AppendMessageAsync(
                "assistant",
                "Review update failed: " + (result.Error ?? "unknown error"),
                null).ConfigureAwait(true);
        }

        internal async Task StartSessionAsync()
        {
            TraceBridgeDiagnostic("STARTSESSION_BEGIN", "initialized=" + _initialized);
            if (!_initialized) return;

            var result = await AgentPanelClient.PostJsonAsync("/assistant/session", new JObject());
            if (!result.Ok)
            {
                TraceBridgeDiagnostic("STARTSESSION_END", "ok=false; errorCode=" + (result.ErrorCode ?? ""));
                lblChatStatus.Text = result.Error;
                lblChatStatus.Visible = true;
                return;
            }

            _sessionId = result.Data.Value<string>("sessionId");
            _pendingAttachment = null;
            lblChatStatus.Text = "Session ready";
            lblChatStatus.Visible = false;
            await ResetTranscriptAsync();
            await RefreshStatusAsync();
            TraceBridgeDiagnostic("STARTSESSION_END", "ok=true");
        }

        private async Task RefreshStatusAsync()
        {
            if (!_initialized) return;

            var result = await AgentPanelClient.GetJsonAsync("/assistant/status");
            if (!result.Ok)
            {
                lblChatStatus.Text = result.Error;
                lblChatStatus.Visible = true;
                return;
            }

            var data = result.Data;
            _assistantMode = data.Value<string>("AssistantMode") ?? data.Value<string>("assistant_mode") ?? "mock";
            var configured = data.Value<bool?>("Configured") ?? data.Value<bool?>("configured") ?? false;
            _activeModel = data.Value<string>("Model") ?? data.Value<string>("model") ?? "AionUI";
            var relayConnected = data.Value<bool?>("RelayConnected") ?? data.Value<bool?>("relayConnected") ?? false;
            var bridgePort = data.Value<int?>("BridgePort") ?? data.Value<int?>("bridgePort") ?? AppIdentity.BridgePort;
            var toolAvailability = data["ToolAvailability"] as JObject ?? data["toolAvailability"] as JObject;
            var activeModel = data["ActiveModel"] as JObject ?? data["activeModel"] as JObject;

            btnToggleMode.Text = string.Equals(_assistantMode, "real", StringComparison.OrdinalIgnoreCase) ? "Mock" : "Real";

            var checklist = data["Checklist"] as JArray;
            if (checklist != null && checklist.Count > 0)
            {
                lblChatStatus.Text = checklist[checklist.Count - 1]?.ToString() ?? "Ready";
            }
            else if (configured && string.Equals(_assistantMode, "real", StringComparison.OrdinalIgnoreCase))
            {
                lblChatStatus.Text = "Real mode ready";
            }
            else
            {
                lblChatStatus.Text = "Mock mode ready";
            }
            lblChatStatus.Visible = false;

            if (_webView.CoreWebView2 != null)
            {
                await ExecuteHostCallbackWithAckAsync(
                    "bbSetModel",
                    JsonConvert.SerializeObject(_activeModel));
                var uiState = new JObject
                {
                    ["mode"] = _assistantMode,
                    ["model"] = _activeModel,
                    ["scopeId"] = _selectedScopeId,
                    ["configured"] = configured,
                    ["bridge"] = "127.0.0.1:" + bridgePort,
                    ["relayConnected"] = relayConnected,
                    ["activeModel"] = activeModel,
                    ["scopes"] = _scopeCatalog,
                    ["toolAvailability"] = toolAvailability,
                    ["status"] = lblChatStatus.Text
                };
                await ExecuteHostCallbackWithAckAsync(
                    "bbSetStatus",
                    uiState.ToString(Formatting.None));
            }

            UpdateSelectedModel();
        }

        private async Task LoadModelsAsync()
        {
            if (!_initialized) return;

            _loadingModels = true;
            try
            {
                cmbModel.Items.Clear();
                _modelCatalog = await AgentPanelClient.FetchModelsAsync().ConfigureAwait(true);
                foreach (var model in _modelCatalog)
                {
                    var modelObject = model as JObject;
                    if (modelObject == null) continue;
                    var id = model.Value<string>("Id") ?? model.Value<string>("id");
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    cmbModel.Items.Add(new ModelListItem(id, BuildModelDisplayText(modelObject)));
                }

                cmbModel.Enabled = cmbModel.Items.Count > 0;
                UpdateSelectedModel();
                if (_webView.CoreWebView2 != null)
                {
                    await ExecuteHostCallbackWithAckAsync(
                        "bbSetModels",
                        _modelCatalog.ToString(Formatting.None));
                }
            }
            finally
            {
                _loadingModels = false;
            }
        }

        private async Task LoadToolsAsync()
        {
            if (!_initialized) return;

            _toolCatalog = await AgentPanelClient.FetchToolsAsync().ConfigureAwait(true);
            PopulateSearchToolSelector(_toolCatalog);
            btnSearchVault.Text = "Search";
            btnSearchVault.Enabled = cmbSearchTool.Items.Count > 0;
            if (!btnSearchVault.Enabled)
            {
                btnSearchVault.Text = "Search Off";
            }

            if (_webView.CoreWebView2 != null)
            {
                await ExecuteHostCallbackWithAckAsync(
                    "bbSetModels",
                    _modelCatalog.ToString(Formatting.None));
                await ExecuteHostCallbackWithAckAsync(
                    "bbSetTools",
                    _toolCatalog.ToString(Formatting.None));
            }
        }

        private async Task LoadScopesAsync()
        {
            if (!_initialized) return;

            _scopeCatalog = await AgentPanelClient.FetchScopesAsync().ConfigureAwait(true);
            if (_webView.CoreWebView2 != null)
            {
                await ExecuteHostCallbackWithAckAsync(
                    "bbSetScopes",
                    _scopeCatalog.ToString(Formatting.None));
                await ExecuteHostCallbackWithAckAsync(
                    "bbSetScope",
                    JsonConvert.SerializeObject(_selectedScopeId));
            }
        }

        private async Task LoadToolAuditAsync()
        {
            if (!_initialized) return;

            _toolReceipts = await AgentPanelClient.FetchToolAuditAsync(6).ConfigureAwait(true);
            if (_webView.CoreWebView2 != null)
            {
                await ExecuteHostCallbackWithAckAsync(
                    "bbSetToolReceipts",
                    _toolReceipts.ToString(Formatting.None));
            }
        }

        private async Task LoadProductCatalogsAsync()
        {
            if (!_initialized) return;

            _integrationCatalog = await AgentPanelClient.FetchIntegrationsAsync().ConfigureAwait(true);
            _documentCatalog = await AgentPanelClient.FetchDocumentCatalogAsync().ConfigureAwait(true);
            if (_webView.CoreWebView2 != null)
            {
                var payload = new JObject
                {
                    ["integrations"] = _integrationCatalog,
                    ["documents"] = _documentCatalog
                };
                await ExecuteHostCallbackWithAckAsync(
                    "bbSetProductCatalogs",
                    payload.ToString(Formatting.None));
            }
        }

        private bool IsToolEnabled(string toolName)
        {
            if (_toolCatalog == null) return false;
            foreach (var token in _toolCatalog)
            {
                var name = token.Value<string>("Name") ?? token.Value<string>("name");
                if (!string.Equals(name, toolName, StringComparison.OrdinalIgnoreCase)) continue;
                return token.Value<bool?>("Enabled") ?? token.Value<bool?>("enabled") ?? false;
            }

            return false;
        }

        private void UpdateSelectedModel()
        {
            if (cmbModel.Items.Count == 0 || string.IsNullOrWhiteSpace(_activeModel)) return;

            for (var i = 0; i < cmbModel.Items.Count; i++)
            {
                var item = cmbModel.Items[i] as ModelListItem;
                if (item != null && item.Text.IndexOf(_activeModel, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    cmbModel.SelectedIndex = i;
                    return;
                }
            }
        }

        private async Task SelectModelAsync()
        {
            if (!_initialized || _loadingModels) return;
            var item = cmbModel.SelectedItem as ModelListItem;
            if (item == null) return;
            await SelectModelAsync(item.Id);
        }

        private async Task SelectModelAsync(string modelId)
        {
            if (!_initialized || _loadingModels || string.IsNullOrWhiteSpace(modelId)) return;

            var result = await AgentPanelClient.PostJsonAsync("/assistant/model", new JObject { ["modelId"] = modelId });
            if (!result.Ok)
            {
                lblChatStatus.Text = result.Error;
                lblChatStatus.Visible = true;
                return;
            }

            _activeModel = result.Data.Value<string>("model") ?? modelId;
            lblChatStatus.Text = "Model set: " + _activeModel;
            lblChatStatus.Visible = false;
            await RefreshStatusAsync();
        }

        private async Task SelectScopeAsync(string scopeId)
        {
            var normalized = AssistantScopeRegistry.Normalize(scopeId);
            _selectedScopeId = normalized;
            if (_webView.CoreWebView2 != null)
            {
                try
                {
                    await _webView.ExecuteScriptAsync("if(window.bbSetScope)window.bbSetScope(" + JsonConvert.SerializeObject(_selectedScopeId) + ");");
                }
                catch { }
            }
            await RefreshStatusAsync();
        }

        private async Task CaptureAsync()
        {
            if (!_initialized) return;
            if (string.IsNullOrWhiteSpace(_sessionId)) await StartSessionAsync();

            var payload = new JObject
            {
                ["sessionId"] = _sessionId,
                ["captureTarget"] = "solidworks_or_foreground"
            };
            var result = await AgentPanelClient.PostJsonAsync("/assistant/screenshot", payload);
            if (!result.Ok)
            {
                lblChatStatus.Text = result.Error;
                lblChatStatus.Visible = true;
                return;
            }

            _pendingAttachment = result.Data.Value<string>("path");
            var artifact = result.Data["artifact"] as JObject;
            var analyzed = await AnalyzeScreenshotArtifactAsync(artifact);
            var finalArtifact = analyzed ?? artifact;
            await AppendScreenshotArtifactAsync(finalArtifact);
            await CreateScreenshotReviewReportAsync(finalArtifact);
            lblChatStatus.Text = "Screenshot attached; review report created";
            lblChatStatus.Visible = false;
        }

        private void Attach_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "Images and PDFs|*.png;*.jpg;*.jpeg;*.bmp;*.pdf";
                if (dialog.ShowDialog() != DialogResult.OK) return;
                _pendingAttachment = dialog.FileName;
                lblChatStatus.Text = "Attached " + Path.GetFileName(dialog.FileName);
                lblChatStatus.Visible = false;
            }
        }

        private bool HasSearchTools()
        {
            return GetToolDescriptor("search_local_vault") != null ||
                   GetToolDescriptor("search_pdm") != null ||
                   GetToolDescriptor("search_epicor") != null ||
                   GetToolDescriptor("search_salesforce") != null ||
                   GetToolDescriptor("search_aionui_database") != null;
        }

        private JObject GetToolDescriptor(string toolName)
        {
            if (_toolCatalog == null) return null;
            foreach (var token in _toolCatalog)
            {
                var obj = token as JObject;
                if (obj == null) continue;
                var name = obj.Value<string>("Name") ?? obj.Value<string>("name");
                if (string.Equals(name, toolName, StringComparison.OrdinalIgnoreCase)) return obj;
            }
            return null;
        }

        private void PopulateSearchToolSelector(JArray tools)
        {
            cmbSearchTool.Items.Clear();
            foreach (var item in BuildSearchToolItems(tools))
            {
                cmbSearchTool.Items.Add(item);
            }
            cmbSearchTool.Enabled = cmbSearchTool.Items.Count > 0;
            if (cmbSearchTool.Items.Count > 0)
            {
                cmbSearchTool.SelectedIndex = 0;
            }
            UpdateSelectedSearchToolStatus();
        }

        internal static ToolListItem[] BuildSearchToolItems(JArray tools)
        {
            var items = new System.Collections.Generic.List<ToolListItem>();
            if (tools == null) return items.ToArray();
            foreach (var token in tools)
            {
                var tool = token as JObject;
                if (tool == null) continue;
                var name = tool.Value<string>("Name") ?? tool.Value<string>("name");
                if (name != "search_local_vault" &&
                    name != "search_pdm" &&
                    name != "search_epicor" &&
                    name != "search_salesforce" &&
                    name != "search_aionui_database") continue;
                var display = tool.Value<string>("DisplayName") ?? tool.Value<string>("displayName") ?? name;
                var enabled = tool.Value<bool?>("Enabled") ?? tool.Value<bool?>("enabled") ?? false;
                var unavailable = tool.Value<string>("UnavailableReason") ?? tool.Value<string>("unavailableReason") ?? string.Empty;
                items.Add(new ToolListItem(name, display, enabled, unavailable));
            }
            return items.ToArray();
        }

        internal static string BuildSelectedToolStatus(ToolListItem item)
        {
            if (item == null) return "No search source available";
            if (item.Enabled) return item.DisplayText + " ready";
            if (!string.IsNullOrWhiteSpace(item.UnavailableReason))
            {
                return item.DisplayText + " unavailable: " + item.UnavailableReason;
            }
            return item.DisplayText + " unavailable";
        }

        private void UpdateSelectedSearchToolStatus()
        {
            var selected = cmbSearchTool.SelectedItem as ToolListItem;
            if (selected == null)
            {
                btnSearchVault.Text = "Search Off";
                btnSearchVault.Enabled = false;
                return;
            }

            btnSearchVault.Text = selected.ButtonText;
            btnSearchVault.Enabled = true;
            lblChatStatus.Text = BuildSelectedToolStatus(selected);
            lblChatStatus.Visible = !selected.Enabled;
        }

        private async Task SearchSelectedToolAsync()
        {
            if (!_initialized) return;

            var query = txtChatInput.Text.Trim();
            if (query == "Chat") query = string.Empty;
            var selected = cmbSearchTool.SelectedItem as ToolListItem;
            var command = ResolveSearchCommand(query, ToolNameForScope(_selectedScopeId) ?? selected?.ToolName);
            query = command.Query;
            if (string.IsNullOrWhiteSpace(query))
            {
                await AppendMessageAsync("assistant", "Type a search query, optionally prefixed with /vault, /pdm, /epicor, /salesforce, or /aionui, then click Search.", null);
                return;
            }

            var descriptor = GetToolDescriptor(command.ToolName);
            var label = descriptor?.Value<string>("DisplayName") ?? descriptor?.Value<string>("displayName") ?? selected?.DisplayText ?? command.Label;
            var enabled = descriptor?.Value<bool?>("Enabled") ?? descriptor?.Value<bool?>("enabled") ?? selected?.Enabled ?? false;

            lblChatStatus.Text = enabled ? "Searching " + label + "..." : label + " unavailable";
            lblChatStatus.Visible = enabled ? false : !IsReactShellActive();
            if (!enabled)
            {
                var unavailable = descriptor?.Value<string>("UnavailableReason") ??
                    descriptor?.Value<string>("unavailableReason") ??
                    selected?.UnavailableReason ??
                    "This search source is not available in the current assistant mode.";
                await AppendToolResultAsync(label, query, "unavailable", label + " unavailable: " + unavailable, new JArray(), new JObject
                {
                    ["ToolName"] = command.ToolName,
                    ["RiskLevel"] = "read-only",
                    ["Allowed"] = false,
                    ["ApprovalRequired"] = false,
                    ["PolicyCode"] = "tool_unavailable",
                    ["ResultStatus"] = "unavailable"
                });
                await SaveSessionAsync();
                return;
            }

            var result = await AgentPanelClient.ExecuteToolAsync(command.ToolName, query, 8, ScopeIdForSearchCommand(command.ToolName, _selectedScopeId));
            if (!result.Ok)
            {
                lblChatStatus.Text = result.Error;
                lblChatStatus.Visible = true;
                await AppendMessageAsync("assistant", label + " search failed: " + result.Error, null);
                return;
            }

            var status = result.Data.Value<string>("Status") ?? result.Data.Value<string>("status") ?? "unknown";
            var message = result.Data.Value<string>("Message") ?? result.Data.Value<string>("message") ?? label + " search completed.";
            var items = result.Data["Items"] as JArray ?? result.Data["items"] as JArray ?? new JArray();
            var receipt = result.Data["Receipt"] as JObject ?? result.Data["receipt"] as JObject;
            await AppendToolResultAsync(label, query, status, message, items, receipt);
            await LoadToolAuditAsync();

            lblChatStatus.Text = status == "ok" ? label + " search complete" : message;
            lblChatStatus.Visible = status != "ok";
            await SaveSessionAsync();
        }

        internal static AssistantSearchCommand ResolveSearchCommand(string rawQuery)
        {
            return ResolveSearchCommand(rawQuery, null);
        }

        internal static AssistantSearchCommand ResolveSearchCommand(string rawQuery, string selectedToolName)
        {
            var query = (rawQuery ?? string.Empty).Trim();
            if (query.StartsWith("/pdm ", StringComparison.OrdinalIgnoreCase))
            {
                return new AssistantSearchCommand("search_pdm", "PDM", query.Substring(5).Trim());
            }
            if (string.Equals(query, "/pdm", StringComparison.OrdinalIgnoreCase))
            {
                return new AssistantSearchCommand("search_pdm", "PDM", string.Empty);
            }
            if (query.StartsWith("/epicor ", StringComparison.OrdinalIgnoreCase))
            {
                return new AssistantSearchCommand("search_epicor", "Epicor", query.Substring(8).Trim());
            }
            if (string.Equals(query, "/epicor", StringComparison.OrdinalIgnoreCase))
            {
                return new AssistantSearchCommand("search_epicor", "Epicor", string.Empty);
            }
            if (query.StartsWith("/salesforce ", StringComparison.OrdinalIgnoreCase))
            {
                return new AssistantSearchCommand("search_salesforce", "Salesforce", query.Substring(12).Trim());
            }
            if (string.Equals(query, "/salesforce", StringComparison.OrdinalIgnoreCase))
            {
                return new AssistantSearchCommand("search_salesforce", "Salesforce", string.Empty);
            }
            if (query.StartsWith("/sf ", StringComparison.OrdinalIgnoreCase))
            {
                return new AssistantSearchCommand("search_salesforce", "Salesforce", query.Substring(4).Trim());
            }
            if (string.Equals(query, "/sf", StringComparison.OrdinalIgnoreCase))
            {
                return new AssistantSearchCommand("search_salesforce", "Salesforce", string.Empty);
            }
            if (query.StartsWith("/aionui ", StringComparison.OrdinalIgnoreCase))
            {
                return new AssistantSearchCommand("search_aionui_database", "AionUI DB", query.Substring(8).Trim());
            }
            if (string.Equals(query, "/aionui", StringComparison.OrdinalIgnoreCase))
            {
                return new AssistantSearchCommand("search_aionui_database", "AionUI DB", string.Empty);
            }
            if (query.StartsWith("/vault ", StringComparison.OrdinalIgnoreCase))
            {
                return new AssistantSearchCommand("search_local_vault", "Local vault", query.Substring(7).Trim());
            }
            if (string.Equals(query, "/vault", StringComparison.OrdinalIgnoreCase))
            {
                return new AssistantSearchCommand("search_local_vault", "Local vault", string.Empty);
            }
            var selected = string.IsNullOrWhiteSpace(selectedToolName) ? "search_local_vault" : selectedToolName.Trim();
            if (string.Equals(selected, "search_pdm", StringComparison.OrdinalIgnoreCase))
            {
                return new AssistantSearchCommand("search_pdm", "PDM", query);
            }
            if (string.Equals(selected, "search_epicor", StringComparison.OrdinalIgnoreCase))
            {
                return new AssistantSearchCommand("search_epicor", "Epicor", query);
            }
            if (string.Equals(selected, "search_salesforce", StringComparison.OrdinalIgnoreCase))
            {
                return new AssistantSearchCommand("search_salesforce", "Salesforce", query);
            }
            if (string.Equals(selected, "search_aionui_database", StringComparison.OrdinalIgnoreCase))
            {
                return new AssistantSearchCommand("search_aionui_database", "AionUI DB", query);
            }
            return new AssistantSearchCommand("search_local_vault", "Local vault", query);
        }

        internal static string ToolNameForScope(string scopeId)
        {
            var scope = AssistantScopeRegistry.Normalize(scopeId);
            if (scope == AssistantScopeRegistry.Pdm) return "search_pdm";
            if (scope == AssistantScopeRegistry.Epicor) return "search_epicor";
            if (scope == AssistantScopeRegistry.All) return "search_local_vault";
            return null;
        }

        internal static string ScopeIdForSearchCommand(string toolName, string selectedScopeId)
        {
            var scope = AssistantScopeRegistry.Normalize(selectedScopeId);
            if (scope == AssistantScopeRegistry.All) return AssistantScopeRegistry.All;
            if (scope == AssistantScopeRegistry.Pdm && string.Equals(toolName, "search_pdm", StringComparison.OrdinalIgnoreCase)) return AssistantScopeRegistry.Pdm;
            if (scope == AssistantScopeRegistry.Epicor && string.Equals(toolName, "search_epicor", StringComparison.OrdinalIgnoreCase)) return AssistantScopeRegistry.Epicor;
            if (scope == AssistantScopeRegistry.LocalVault && string.Equals(toolName, "search_local_vault", StringComparison.OrdinalIgnoreCase)) return AssistantScopeRegistry.LocalVault;
            return string.Empty;
        }

        private async Task AppendToolResultAsync(string label, string query, string status, string message, JArray items, JObject receipt)
        {
            if (!_initialized || _webView.CoreWebView2 == null) return;

            var payload = new JObject
            {
                ["label"] = label,
                ["query"] = query,
                ["status"] = status,
                ["message"] = message,
                ["items"] = items ?? new JArray(),
                ["receipt"] = NormalizeToolReceipt(receipt)
            };
            await _webView.ExecuteScriptAsync("if(window.bbAppendToolResult)window.bbAppendToolResult(" + payload.ToString(Formatting.None) + ");");
        }

        internal static JObject NormalizeToolReceipt(JObject receipt)
        {
            receipt = receipt ?? new JObject();
            return new JObject
            {
                ["receiptId"] = receipt.Value<string>("ReceiptId") ?? receipt.Value<string>("receiptId") ?? string.Empty,
                ["traceId"] = receipt.Value<string>("TraceId") ?? receipt.Value<string>("traceId") ?? string.Empty,
                ["toolName"] = receipt.Value<string>("ToolName") ?? receipt.Value<string>("toolName") ?? string.Empty,
                ["riskLevel"] = receipt.Value<string>("RiskLevel") ?? receipt.Value<string>("riskLevel") ?? "unknown",
                ["allowed"] = receipt.Value<bool?>("Allowed") ?? receipt.Value<bool?>("allowed") ?? false,
                ["approvalRequired"] = receipt.Value<bool?>("ApprovalRequired") ?? receipt.Value<bool?>("approvalRequired") ?? false,
                ["policyCode"] = receipt.Value<string>("PolicyCode") ?? receipt.Value<string>("policyCode") ?? string.Empty,
                ["resultStatus"] = receipt.Value<string>("ResultStatus") ?? receipt.Value<string>("resultStatus") ?? string.Empty
            };
        }

        private async Task AppendScreenshotArtifactAsync(JObject artifact)
        {
            if (!_initialized || _webView.CoreWebView2 == null || artifact == null) return;
            var payload = NormalizeScreenshotArtifact(artifact);
            await _webView.ExecuteScriptAsync("if(window.bbAppendScreenshotArtifact)window.bbAppendScreenshotArtifact(" + payload.ToString(Formatting.None) + ");");
        }

        private async Task CreateScreenshotReviewReportAsync(JObject artifact)
        {
            if (artifact == null) return;
            var descriptor = GetToolDescriptor("create_screenshot_review_report");
            var enabled = descriptor?.Value<bool?>("Enabled") ?? descriptor?.Value<bool?>("enabled") ?? false;
            if (!enabled) return;

            var payload = BuildScreenshotReviewReportToolParameters(artifact);
            var artifactPath = payload.Value<string>("artifactPath") ?? string.Empty;
            var result = await AgentPanelClient.ExecuteToolAsync("create_screenshot_review_report", artifactPath, 1, payload);
            if (!result.Ok)
            {
                await AppendMessageAsync("assistant", "Screenshot review report failed: " + result.Error, null);
                return;
            }

            var status = result.Data.Value<string>("Status") ?? result.Data.Value<string>("status") ?? "unknown";
            var message = result.Data.Value<string>("Message") ?? result.Data.Value<string>("message") ?? "Screenshot review report completed.";
            var items = result.Data["Items"] as JArray ?? result.Data["items"] as JArray ?? new JArray();
            var receipt = result.Data["Receipt"] as JObject ?? result.Data["receipt"] as JObject;
            await AppendToolResultAsync("Screenshot Review Report", artifactPath, status, message, items, receipt);
            await LoadToolAuditAsync();
        }

        internal static JObject BuildScreenshotReviewReportToolParameters(JObject artifact)
        {
            artifact = artifact ?? new JObject();
            var path = artifact.Value<string>("Path") ?? artifact.Value<string>("path") ?? string.Empty;
            var metadataPath =
                artifact.Value<string>("MetadataPath") ??
                artifact.Value<string>("metadataPath") ??
                (string.IsNullOrWhiteSpace(path)
                    ? string.Empty
                    : Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, Path.GetFileNameWithoutExtension(path) + ".metadata.json"));
            return new JObject
            {
                ["artifactPath"] = path,
                ["metadataPath"] = metadataPath,
                ["artifactJson"] = artifact.ToString(Formatting.None)
            };
        }

        private async Task<JObject> AnalyzeScreenshotArtifactAsync(JObject artifact)
        {
            if (artifact == null) return null;
            if (!ActiveModelSupportsVision())
            {
                await AppendMessageAsync("assistant", "Screenshot captured. Analysis skipped because the selected model does not advertise vision support. Switch to a vision-capable model to extract annotations or contacts.", null);
                return artifact;
            }

            var result = await AgentPanelClient.PostJsonAsync("/assistant/screenshot/analyze", new JObject
            {
                ["artifact"] = artifact,
                ["sessionId"] = _sessionId ?? string.Empty,
                ["hintText"] = txtChatInput.Text == "Chat" ? string.Empty : txtChatInput.Text,
                ["modelProfileId"] = _activeModel ?? string.Empty,
                ["cloudSendApproved"] = false
            });
            if (!result.Ok)
            {
                await AppendMessageAsync("assistant", "Screenshot analysis was skipped: " + result.Error, null);
                return artifact;
            }

            return result.Data["artifact"] as JObject ?? artifact;
        }

        internal bool ActiveModelSupportsVision()
        {
            return ModelSupportsVision(_modelCatalog, _activeModel);
        }

        internal static bool ModelSupportsVision(JArray models, string activeModel)
        {
            if (models == null || string.IsNullOrWhiteSpace(activeModel)) return false;
            foreach (var token in models)
            {
                var id = token.Value<string>("Id") ?? token.Value<string>("id");
                var name = token.Value<string>("Name") ?? token.Value<string>("name") ?? string.Empty;
                var provider = token.Value<string>("Provider") ?? token.Value<string>("provider") ?? string.Empty;
                var display = (provider + " " + name).Trim();
                if (!string.Equals(id, activeModel, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(name, activeModel, StringComparison.OrdinalIgnoreCase) &&
                    display.IndexOf(activeModel, StringComparison.OrdinalIgnoreCase) < 0 &&
                    activeModel.IndexOf(name, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                return token.Value<bool?>("SupportsVision") ?? token.Value<bool?>("supportsVision") ?? false;
            }

            return false;
        }

        internal static string BuildModelDisplayText(JObject model)
        {
            model = model ?? new JObject();
            var id = model.Value<string>("Id") ?? model.Value<string>("id");
            var name = model.Value<string>("Name") ?? model.Value<string>("name") ?? id ?? "Model";
            var provider = model.Value<string>("Provider") ?? model.Value<string>("provider") ?? "AI";
            var enabled = model.Value<bool?>("Enabled") ?? model.Value<bool?>("enabled") ?? true;
            var caps = new System.Collections.Generic.List<string>();
            if (model.Value<bool?>("SupportsVision") ?? model.Value<bool?>("supportsVision") ?? false) caps.Add("vision");
            if (model.Value<bool?>("SupportsTools") ?? model.Value<bool?>("supportsTools") ?? false) caps.Add("tools");
            if (model.Value<bool?>("SupportsStreaming") ?? model.Value<bool?>("supportsStreaming") ?? false) caps.Add("stream");
            if (model.Value<bool?>("SupportsJsonMode") ?? model.Value<bool?>("supportsJsonMode") ?? false) caps.Add("json");
            if (caps.Count == 0) caps.Add("text");

            var suffix = string.Join("/", caps.ToArray());
            return provider + " · " + name + " · " + suffix + (enabled ? string.Empty : " (off)");
        }

        internal static JObject NormalizeScreenshotArtifact(JObject artifact)
        {
            artifact = artifact ?? new JObject();
            var path = artifact.Value<string>("Path") ?? artifact.Value<string>("path") ?? string.Empty;
            var title = artifact.Value<string>("SourceWindowTitle") ?? artifact.Value<string>("sourceWindowTitle") ?? string.Empty;
            var width = artifact.Value<int?>("Width") ?? artifact.Value<int?>("width") ?? 0;
            var height = artifact.Value<int?>("Height") ?? artifact.Value<int?>("height") ?? 0;
            var artifactId = artifact.Value<string>("ArtifactId") ?? artifact.Value<string>("artifactId") ?? string.Empty;
            var solidWorksDocumentTitle = artifact.Value<string>("SolidWorksDocumentTitle") ?? artifact.Value<string>("solidWorksDocumentTitle") ?? string.Empty;
            var solidWorksDocumentPathHash = artifact.Value<string>("SolidWorksDocumentPathHash") ?? artifact.Value<string>("solidWorksDocumentPathHash") ?? string.Empty;
            var captureTarget = artifact.Value<string>("CaptureTarget") ?? artifact.Value<string>("captureTarget") ?? string.Empty;
            var captureSource = artifact.Value<string>("CaptureSource") ?? artifact.Value<string>("captureSource") ?? string.Empty;
            var retentionPolicy = artifact.Value<string>("RetentionPolicy") ?? artifact.Value<string>("retentionPolicy") ?? string.Empty;
            var modelProfileId = artifact.Value<string>("ModelProfileId") ?? artifact.Value<string>("modelProfileId") ?? string.Empty;
            var screenshotId = artifact.Value<string>("ScreenshotId") ?? artifact.Value<string>("screenshotId") ?? artifactId;
            var capturedUtc = artifact.Value<string>("CapturedUtc") ?? artifact.Value<string>("capturedUtc") ?? string.Empty;
            var metadataPath = artifact.Value<string>("MetadataPath") ?? artifact.Value<string>("metadataPath") ?? string.Empty;
            var thumbnailPath = artifact.Value<string>("ThumbnailPath") ?? artifact.Value<string>("thumbnailPath") ?? string.Empty;
            var annotationsPath = artifact.Value<string>("AnnotationsPath") ?? artifact.Value<string>("annotationsPath") ?? string.Empty;
            var localOnlyCloudState = artifact.Value<string>("LocalOnlyCloudState") ?? artifact.Value<string>("localOnlyCloudState") ?? string.Empty;
            var redactionApplied = artifact.Value<bool?>("RedactionApplied") ?? artifact.Value<bool?>("redactionApplied") ?? false;
            var sentToModel = artifact.Value<bool?>("SentToModel") ?? artifact.Value<bool?>("sentToModel") ?? false;
            var annotations = artifact["Annotations"] as JArray ?? artifact["annotations"] as JArray ?? new JArray();
            var contacts = artifact["ExtractedContacts"] as JArray ?? artifact["extractedContacts"] as JArray ?? new JArray();
            var receipt = artifact["Receipt"] as JObject ?? artifact["receipt"] as JObject;

            return new JObject
            {
                ["artifactId"] = artifactId,
                ["screenshotId"] = screenshotId,
                ["path"] = path,
                ["fileName"] = string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetFileName(path),
                ["capturedUtc"] = capturedUtc,
                ["metadataPath"] = metadataPath,
                ["thumbnailPath"] = thumbnailPath,
                ["annotationsPath"] = annotationsPath,
                ["sourceWindowTitle"] = title,
                ["solidWorksDocumentTitle"] = solidWorksDocumentTitle,
                ["solidWorksDocumentPathHash"] = solidWorksDocumentPathHash,
                ["captureTarget"] = captureTarget,
                ["captureSource"] = captureSource,
                ["retentionPolicy"] = retentionPolicy,
                ["localOnlyCloudState"] = localOnlyCloudState,
                ["modelProfileId"] = modelProfileId,
                ["redactionApplied"] = redactionApplied,
                ["sentToModel"] = sentToModel,
                ["width"] = width,
                ["height"] = height,
                ["annotationCount"] = annotations.Count,
                ["contactCount"] = contacts.Count,
                ["annotations"] = annotations,
                ["contacts"] = contacts,
                ["receipt"] = receipt ?? new JObject()
            };
        }

        internal static AssistantStreamEvent NormalizeAssistantStreamEvent(JObject chunk)
        {
            if (chunk == null)
            {
                return new AssistantStreamEvent { Kind = AssistantStreamEventKind.Unknown, Payload = new JObject() };
            }

            var rawType = chunk.Value<string>("type") ?? chunk.Value<string>("kind") ?? string.Empty;
            var type = rawType.Trim().ToLowerInvariant();

            if (type == "done" || type == "final")
            {
                return new AssistantStreamEvent
                {
                    Kind = AssistantStreamEventKind.Final,
                    Text = ExtractStreamText(chunk),
                    Payload = chunk,
                    Status = type
                };
            }

            if (type == "error" || chunk["error"] != null || chunk["errorMessage"] != null)
            {
                return new AssistantStreamEvent
                {
                    Kind = AssistantStreamEventKind.Error,
                    Id = ExtractStreamErrorCode(chunk),
                    Text = ExtractStreamErrorMessage(chunk),
                    Payload = chunk,
                    Status = "error"
                };
            }

            if (type == "tool_call" || type == "tool_call_start")
            {
                var toolName = FirstString(chunk, "toolName", "ToolName", "name", "Name", "tool", "Tool");
                var label = string.IsNullOrWhiteSpace(toolName) ? "Tool" : toolName;
                var toolCallId = FirstString(chunk, "toolCallId", "ToolCallId", "id", "Id");
                return new AssistantStreamEvent
                {
                    Kind = AssistantStreamEventKind.ToolCall,
                    Id = toolCallId,
                    Text = label,
                    Status = "pending",
                    Payload = new JObject
                    {
                        ["label"] = label,
                        ["query"] = string.Empty,
                        ["status"] = "pending",
                        ["message"] = "Calling " + label + "...",
                        ["items"] = new JArray(),
                        ["receipt"] = NormalizeToolReceipt(chunk["receipt"] as JObject ?? chunk["Receipt"] as JObject)
                    }
                };
            }

            if (type == "tool_result" || chunk["toolResultContent"] != null || chunk["ToolResultContent"] != null)
            {
                return NormalizeToolResultStreamEvent(chunk);
            }

            if (type == "screenshot_receipt" || type == "screenshot_artifact" || type == "artifact" || chunk["artifact"] != null || chunk["Artifact"] != null)
            {
                var artifact = chunk["artifact"] as JObject ?? chunk["Artifact"] as JObject ?? chunk;
                return new AssistantStreamEvent
                {
                    Kind = AssistantStreamEventKind.Screenshot,
                    Id = artifact.Value<string>("artifactId") ?? artifact.Value<string>("ArtifactId") ?? artifact.Value<string>("screenshotId") ?? artifact.Value<string>("ScreenshotId"),
                    Text = "Screenshot captured",
                    Status = "local-only",
                    Payload = NormalizeScreenshotArtifact(artifact)
                };
            }

            var text = ExtractStreamText(chunk);
            if (type == "text_delta" || type == "text" || !string.IsNullOrEmpty(text))
            {
                return new AssistantStreamEvent
                {
                    Kind = AssistantStreamEventKind.Text,
                    Text = text,
                    Payload = chunk,
                    Status = type
                };
            }

            return new AssistantStreamEvent
            {
                Kind = AssistantStreamEventKind.Unknown,
                Payload = chunk,
                Status = type
            };
        }

        internal static bool ShouldAppendFinalAssistantMessage(bool streamedAnyText, bool finalAppended, string streamedText, string finalText)
        {
            if (finalAppended || string.IsNullOrWhiteSpace(finalText)) return false;
            if (!streamedAnyText) return true;

            var streamed = (streamedText ?? string.Empty).Trim();
            var final = finalText.Trim();
            if (string.Equals(streamed, final, StringComparison.Ordinal)) return false;
            if (streamed.IndexOf(final, StringComparison.Ordinal) >= 0) return false;
            if (final.IndexOf(streamed, StringComparison.Ordinal) >= 0) return false;
            return true;
        }

        private static AssistantStreamEvent NormalizeToolResultStreamEvent(JObject chunk)
        {
            var resultContent = chunk.Value<string>("toolResultContent") ?? chunk.Value<string>("ToolResultContent") ?? string.Empty;
            JObject resultObj = null;
            if (!string.IsNullOrWhiteSpace(resultContent))
            {
                try
                {
                    resultObj = JObject.Parse(resultContent);
                }
                catch
                {
                    resultObj = null;
                }
            }

            var source = resultObj ?? chunk;
            var toolName = FirstString(chunk, "toolName", "ToolName", "name", "Name", "tool", "Tool") ??
                FirstString(source, "toolName", "ToolName", "name", "Name", "label", "Label");
            var label = string.IsNullOrWhiteSpace(toolName) ? "Tool" : toolName;
            var status = FirstString(source, "status", "Status", "resultStatus", "ResultStatus") ?? "ok";
            var message = FirstString(source, "message", "Message", "summary", "Summary") ?? "Tool completed.";
            var items = source["items"] as JArray ?? source["Items"] as JArray ?? new JArray();
            var receipt = source["receipt"] as JObject ?? source["Receipt"] as JObject ?? chunk["receipt"] as JObject ?? chunk["Receipt"] as JObject;

            return new AssistantStreamEvent
            {
                Kind = AssistantStreamEventKind.ToolResult,
                Id = FirstString(chunk, "toolCallId", "ToolCallId", "id", "Id"),
                Text = message,
                Status = status,
                Payload = new JObject
                {
                    ["label"] = label,
                    ["query"] = FirstString(source, "query", "Query") ?? string.Empty,
                    ["status"] = status,
                    ["message"] = message,
                    ["items"] = items,
                    ["receipt"] = NormalizeToolReceipt(receipt)
                }
            };
        }

        private static string ExtractStreamText(JObject chunk)
        {
            if (chunk == null) return string.Empty;
            var direct = FirstString(chunk, "text", "Text", "content", "Content");
            if (!string.IsNullOrEmpty(direct)) return direct;

            var delta = chunk["delta"] as JObject ?? chunk["Delta"] as JObject;
            if (delta != null)
            {
                var deltaText = FirstString(delta, "content", "Content", "text", "Text");
                if (!string.IsNullOrEmpty(deltaText)) return deltaText;
            }

            var message = chunk["message"] as JObject ?? chunk["Message"] as JObject;
            if (message != null)
            {
                return FirstString(message, "text", "Text", "content", "Content") ?? string.Empty;
            }

            return string.Empty;
        }

        private static string ExtractStreamErrorCode(JObject chunk)
        {
            var error = chunk?["error"] as JObject ?? chunk?["Error"] as JObject;
            return FirstString(chunk, "errorCode", "ErrorCode", "code", "Code") ??
                FirstString(error, "errorCode", "ErrorCode", "code", "Code") ??
                "request_failed";
        }

        private static string ExtractStreamErrorMessage(JObject chunk)
        {
            var error = chunk?["error"] as JObject ?? chunk?["Error"] as JObject;
            return FirstString(chunk, "errorMessage", "ErrorMessage", "message", "Message") ??
                FirstString(error, "errorMessage", "ErrorMessage", "message", "Message") ??
                "Assistant request failed.";
        }

        private static string FirstString(JObject source, params string[] names)
        {
            if (source == null || names == null) return null;
            foreach (var name in names)
            {
                var value = source.Value<string>(name);
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
            return null;
        }

        private static string BuildToolSearchMessage(string label, string query, string status, string message, JArray items)
        {
            var builder = new StringBuilder();
            builder.AppendLine(label + " search: " + query);
            builder.AppendLine(message);

            if (!string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase) || items.Count == 0)
            {
                return builder.ToString().Trim();
            }

            builder.AppendLine();
            var count = Math.Min(items.Count, 8);
            for (var i = 0; i < count; i++)
            {
                var item = items[i];
                var title = item.Value<string>("Title") ?? item.Value<string>("title") ?? "Untitled";
                var subtitle = item.Value<string>("Subtitle") ?? item.Value<string>("subtitle") ?? string.Empty;
                var path = item.Value<string>("Path") ?? item.Value<string>("path") ?? string.Empty;
                builder.Append(i + 1).Append(". ").AppendLine(title);
                if (!string.IsNullOrWhiteSpace(subtitle)) builder.Append("   ").AppendLine(subtitle);
                if (!string.IsNullOrWhiteSpace(path)) builder.Append("   ").AppendLine(path);
            }

            return builder.ToString().Trim();
        }

        private void QueueWebViewScript(string script)
        {
            if (!_initialized || _webView.CoreWebView2 == null || string.IsNullOrWhiteSpace(script)) return;
            try
            {
                _webView.ExecuteScriptAsync(script);
            }
            catch
            {
                // WebView teardown during cancellation should not hide the stream cleanup path.
            }
        }

        private Task StopWebViewTypingAsync()
        {
            if (!_initialized || _webView.CoreWebView2 == null) return Task.CompletedTask;
            return _webView.ExecuteScriptAsync("if(window.bbTypingStop)window.bbTypingStop();");
        }

        // Delivers a final assistant text so exactly ONE assistant transcript record
        // exists per logical request. When the React shell owns the transcript, the
        // pending record created by handleSend is completed in place using only the
        // frozen callback set (bbAppendChunk + bbTypingStop); the legacy shell keeps
        // its original append behavior behind explicit gating.
        private async Task DeliverFinalAssistantTextAsync(string text)
        {
            var safeText = text ?? string.Empty;
            if (!_initialized || _webView.CoreWebView2 == null) return;

            if (IsReactShellActive())
            {
                QueueWebViewScript("if(window.bbAppendChunk)window.bbAppendChunk(" + JsonConvert.SerializeObject(safeText) + ");");
                await StopWebViewTypingAsync();
                return;
            }

            await StopWebViewTypingAsync();
            await AppendMessageAsync("assistant", safeText, null);
        }

        // Builds a safe PROVIDER_FAILURE receipt from an error message carrying a
        // "[prov provider=... model=... httpStatus=... category=...]" tag. Only the
        // provenance fields are written to diagnostics; sanitized message contents
        // are never logged here.
        private static string BuildProviderFailureReceipt(string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage)) return "provenance=absent";
            var text = errorMessage.Trim();
            if (!text.StartsWith("[prov ", StringComparison.Ordinal)) return "provenance=absent";
            var end = text.IndexOf(']');
            if (end < 0) return "provenance=malformed";
            return text.Substring("[prov ".Length, end - "[prov ".Length).Trim();
        }

        private void RenderNormalizedAssistantStreamEvent(
            AssistantStreamEvent streamEvent,
            StringBuilder streamedText,
            ref bool streamedAnyText,
            ref bool renderedStreamEvent,
            ref bool finalAppended,
            ref string finalText)
        {
            if (streamEvent == null) return;

            if (streamEvent.Kind == AssistantStreamEventKind.Text)
            {
                var textChunk = streamEvent.Text ?? string.Empty;
                if (textChunk.Length == 0) return;

                streamedText.Append(textChunk);
                streamedAnyText = true;
                QueueWebViewScript("if(window.bbAppendChunk)window.bbAppendChunk(" + JsonConvert.SerializeObject(textChunk) + ");");
                return;
            }

            if (streamEvent.Kind == AssistantStreamEventKind.ToolCall || streamEvent.Kind == AssistantStreamEventKind.ToolResult)
            {
                var payload = streamEvent.Payload ?? new JObject();
                renderedStreamEvent = true;
                QueueWebViewScript("if(window.bbAppendToolResult)window.bbAppendToolResult(" + payload.ToString(Formatting.None) + ");");
                return;
            }

            if (streamEvent.Kind == AssistantStreamEventKind.Screenshot)
            {
                var payload = streamEvent.Payload ?? new JObject();
                renderedStreamEvent = true;
                QueueWebViewScript("if(window.bbAppendScreenshotArtifact)window.bbAppendScreenshotArtifact(" + payload.ToString(Formatting.None) + ");");
                return;
            }

            if (streamEvent.Kind == AssistantStreamEventKind.Error)
            {
                var errorText = BuildUserFacingError(streamEvent.Id, streamEvent.Text);
                TraceBridgeDiagnostic("STREAM_ERROR_EVENT",
                    "code=" + (streamEvent.Id ?? "") +
                    "; messageLength=" + (streamEvent.Text == null ? 0 : streamEvent.Text.Length));
                TraceBridgeDiagnostic("PROVIDER_FAILURE", BuildProviderFailureReceipt(streamEvent.Text));
                var payload = new JObject
                {
                    ["role"] = "assistant",
                    ["text"] = errorText,
                    ["attachment"] = string.Empty
                };
                finalText = errorText;
                finalAppended = true;
                renderedStreamEvent = true;
                if (IsReactShellActive())
                {
                    // React owns the transcript: deliver the failure text into the pending
                    // assistant record created by handleSend, then let bbTypingStop finalize
                    // that same record. Exactly one assistant record per logical request;
                    // no second record may be appended while the React shell is active.
                    QueueWebViewScript("if(window.bbAppendChunk)window.bbAppendChunk(" + JsonConvert.SerializeObject(errorText) + ");");
                    QueueWebViewScript("if(window.bbTypingStop)window.bbTypingStop();");
                }
                else
                {
                    QueueWebViewScript("if(window.bbTypingStop)window.bbTypingStop();if(window.bbAppend)window.bbAppend(" + payload.ToString(Formatting.None) + ");");
                }
                return;
            }

            if (streamEvent.Kind == AssistantStreamEventKind.Final)
            {
                if (!string.IsNullOrWhiteSpace(streamEvent.Text))
                {
                    finalText = streamEvent.Text;
                }
            }
        }

        private async Task SendAsync()
        {
            TraceBridgeDiagnostic("SENDASYNC_ENTRY", "initialized=" + _initialized);
            if (!_initialized) return;

            var text = txtChatInput.Text.Trim();
            if ((text.Length == 0 || text == "Chat") && string.IsNullOrWhiteSpace(_pendingAttachment)) return;
            if (text == "Chat") text = "";
            if (string.IsNullOrWhiteSpace(_sessionId)) await StartSessionAsync();

            var userText = text;
            if (!IsReactShellActive())
            {
                // React already appends the user message optimistically in
                // handleSend; echoing it here duplicates the transcript entry.
                await AppendMessageAsync("user", userText, _pendingAttachment);
            }
            txtChatInput.Text = "";

            var payload = new JObject
            {
                ["sessionId"] = _sessionId,
                ["message"] = userText,
                ["scopeId"] = _selectedScopeId,
                ["attachmentPaths"] = _pendingAttachment == null
                ? new JArray()
                : new JArray(_pendingAttachment)
            };
            _pendingAttachment = null;

            _isStreaming = true;
            _streamCts?.Cancel();
            _streamCts?.Dispose();
            var requestCts = new CancellationTokenSource();
            _streamCts = requestCts;

            lblChatStatus.Text = "Ready";
            lblChatStatus.Visible = false;
            if (!IsReactShellActive())
            {
                // React handleSend already created the streaming placeholder.
                await _webView.ExecuteScriptAsync("if(window.bbTypingStart)window.bbTypingStart();");
            }

            var streamedText = new StringBuilder();
            var streamedAnyText = false;
            var renderedStreamEvent = false;
            var finalAppended = false;
            var finalText = string.Empty;
            try
            {
                TraceBridgeDiagnostic("SEND_REQUEST_BEGIN", "route=/assistant/message/stream sessionId=" + (_sessionId ?? "none"));
                await AgentPanelClient.PostStreamingAsync("/assistant/message/stream", payload, chunk =>
                {
                    JObject jObj;
                    try
                    {
                        jObj = JObject.Parse(chunk);
                    }
                    catch
                    {
                        jObj = new JObject
                        {
                            ["type"] = "error",
                            ["errorCode"] = "malformed_sse",
                            ["errorMessage"] = "Assistant stream returned a malformed event."
                        };
                    }

                    var streamEvent = NormalizeAssistantStreamEvent(jObj);
                    RenderNormalizedAssistantStreamEvent(streamEvent, streamedText, ref streamedAnyText, ref renderedStreamEvent, ref finalAppended, ref finalText);
                }, requestCts.Token);

                if (string.IsNullOrWhiteSpace(finalText))
                {
                    finalText = streamedText.ToString();
                }
                TraceBridgeDiagnostic("SEND_REQUEST_END",
                    "finalTextLength=" + (finalText == null ? 0 : finalText.Length) +
                    "; streamedAnyText=" + streamedAnyText +
                    "; renderedStreamEvent=" + renderedStreamEvent);

                if (string.IsNullOrWhiteSpace(finalText) && !renderedStreamEvent && !streamedAnyText)
                {
                    var fallback = await AgentPanelClient.PostJsonAsync("/assistant/message", payload, requestCts.Token);
                    if (fallback.Ok)
                    {
                        var error = fallback.Data.Value<string>("error");
                        if (!string.IsNullOrWhiteSpace(error))
                        {
                            finalText = BuildUserFacingError(fallback.Data.Value<string>("errorCode"), error);
                            await LogErrorAsync(error, "", "SendAsync-nonstream-error");
                        }
                        else
                        {
                            finalText = fallback.Data["message"]?["Text"]?.ToString()
                                ?? fallback.Data["message"]?["text"]?.ToString()
                                ?? fallback.Data.Value<string>("message")
                                ?? "No response text returned.";
                        }
                    }
                    else
                    {
                        finalText = BuildUserFacingError(fallback.ErrorCode, fallback.Error);
                        await LogErrorAsync(fallback.Error, "", "SendAsync-fallback-fail");
                    }
                }

                if (ShouldAppendFinalAssistantMessage(streamedAnyText, finalAppended, streamedText.ToString(), finalText))
                {
                    await DeliverFinalAssistantTextAsync(finalText);
                    finalAppended = true;
                }
                else
                {
                    await StopWebViewTypingAsync();
                }
            }
            catch (OperationCanceledException)
            {
                await StopWebViewTypingAsync();
                if (!finalAppended)
                {
                    await DeliverFinalAssistantTextAsync("Request canceled.");
                    finalAppended = true;
                }
            }
            catch (Exception ex)
            {
                TraceBridgeDiagnostic("SEND_EXCEPTION",
                    "type=" + ex.GetType().Name +
                    "; messageLength=" + (ex.Message == null ? 0 : ex.Message.Length));
                var classified = AssistantErrorClassifier.FromException(ex);
                TraceBridgeDiagnostic("PROVIDER_FAILURE", BuildProviderFailureReceipt(AssistantErrorClassifier.FormatWithProvenance(classified)));
                await StopWebViewTypingAsync();
                if (!finalAppended)
                {
                    await DeliverFinalAssistantTextAsync(BuildUserFacingError(classified.Code, AssistantErrorClassifier.FormatWithProvenance(classified)));
                    finalAppended = true;
                }
                await LogErrorAsync(ex.Message, ex.StackTrace, "SendAsync-streaming");
            }
            finally
            {
                if (OwnsStreamCancellationSource(_streamCts, requestCts))
                {
                    _isStreaming = false;
                    _streamCts = null;
                    lblChatStatus.Text = "Ready";
                    lblChatStatus.Visible = false;
                    await RefreshStatusAsync();
                    await SaveSessionAsync();
                }
                requestCts.Dispose();
            }
        }

        internal static bool OwnsStreamCancellationSource(CancellationTokenSource current, CancellationTokenSource request)
        {
            return ReferenceEquals(current, request);
        }

        internal static string BuildUserFacingError(string code, string message)
        {
            var safeCode = string.IsNullOrWhiteSpace(code) ? "request_failed" : code.Trim();
            var safeMessage = string.IsNullOrWhiteSpace(message) ? "Assistant request failed." : message.Trim();
            return "Request failed (" + safeCode + "): " + safeMessage;
        }

        private async Task TestConnectionAsync()
        {
            if (!_initialized) return;

            lblChatStatus.Text = "Testing...";
            lblChatStatus.Visible = true;

            var result = await AgentPanelClient.PostJsonAsync("/assistant/test", new JObject());
            if (!result.Ok)
            {
                lblChatStatus.Text = result.Error;
                lblChatStatus.Visible = true;
                return;
            }

            var success = result.Data.Value<bool?>("success") ?? false;
            var mode = result.Data.Value<string>("mode") ?? _assistantMode ?? "mock";
            var message = result.Data.Value<string>("message") ?? string.Empty;
            var latency = result.Data.Value<double?>("latencyMs") ?? 0d;
            lblChatStatus.Text = success
                ? $"{mode} test ok ({latency:0}ms)"
                : $"{mode} test failed";
            lblChatStatus.Visible = false;
            await AppendMessageAsync("assistant", message, null);
            await RefreshStatusAsync();
        }

        private async Task ToggleModeAsync()
        {
            if (!_initialized) return;

            var nextMode = string.Equals(_assistantMode, "real", StringComparison.OrdinalIgnoreCase) ? "mock" : "real";
            var result = await AgentPanelClient.PostJsonAsync("/assistant/mode", new JObject { ["mode"] = nextMode });
            if (!result.Ok)
            {
                lblChatStatus.Text = result.Error;
                lblChatStatus.Visible = true;
                return;
            }

            await RefreshStatusAsync();
        }

        private async Task OpenChatGptAsync()
        {
            if (!_initialized) return;
            if (string.IsNullOrWhiteSpace(_sessionId)) await StartSessionAsync();

            lblChatStatus.Text = "⏳ Preparing handoff...";
            lblChatStatus.Visible = true;

            var payload = new JObject();
            if (!string.IsNullOrWhiteSpace(_pendingAttachment))
            {
                payload["lastScreenshotPath"] = _pendingAttachment;
            }

            var result = await AgentPanelClient.PostJsonAsync("/chatgpt/session/create", payload);
            if (!result.Ok)
            {
                lblChatStatus.Text = "🔴 ChatGPT handoff failed: " + result.Error;
                lblChatStatus.Visible = true;
                await AppendMessageAsync("assistant", "ChatGPT handoff failed: " + result.Error, null);
                return;
            }

            var handoffUrl = result.Data.Value<string>("handoffUrl");
            var relayConfigured = result.Data.Value<bool?>("relayConfigured") ?? false;
            var message = result.Data.Value<string>("message") ?? string.Empty;
            var targetUrl = !string.IsNullOrWhiteSpace(handoffUrl)
                ? handoffUrl
                : result.Data.Value<string>("chatWorkspaceUrl");

            if (string.IsNullOrWhiteSpace(targetUrl))
            {
                lblChatStatus.Text = "🔴 ChatGPT unavailable";
                lblChatStatus.Visible = true;
                await AppendMessageAsync("assistant", string.IsNullOrWhiteSpace(message) ? "Relay is not configured yet." : message, null);
                return;
            }

            try
            {
                Process.Start(targetUrl);
                lblChatStatus.Text = relayConfigured ? "🟢 Opened ChatGPT handoff" : "🟢 Opened ChatGPT workspace";
                lblChatStatus.Visible = false;
                if (!string.IsNullOrWhiteSpace(message))
                {
                    await AppendMessageAsync("assistant", message, null);
                }
            }
            catch (Exception ex)
            {
                lblChatStatus.Text = "🔴 Failed to open ChatGPT";
                lblChatStatus.Visible = true;
                await AppendMessageAsync("assistant", "ChatGPT URL could not be opened: " + ex.Message, null);
            }
        }

        private async Task CallVaultAsync(string path, string statusText)
        {
            if (!_initialized) return;

            var result = await AgentPanelClient.PostJsonAsync(path, new JObject());
            lblChatStatus.Text = result.Ok ? statusText : result.Error;
            lblChatStatus.Visible = !result.Ok;
            await RefreshStatusAsync();
        }

        private void OpenWorkingFolder()
        {
            Directory.CreateDirectory(AppIdentity.DefaultWorkingFolder);
            Process.Start("explorer.exe", AppIdentity.DefaultWorkingFolder);
        }

        private async Task ResetTranscriptAsync()
        {
            if (!_initialized || _webView.CoreWebView2 == null) return;
            await ExecuteHostCallbackWithAckAsync(
                "bbReset",
                string.Empty);
        }

        private async Task AppendMessageAsync(string role, string text, string attachmentPath)
        {
            if (!_initialized || _webView.CoreWebView2 == null) return;

            var payload = new JObject
            {
                ["role"] = role,
                ["text"] = text ?? string.Empty,
                ["attachment"] = attachmentPath == null ? string.Empty : Path.GetFileName(attachmentPath)
            };
            await _webView.ExecuteScriptAsync("window.bbAppend(" + payload.ToString(Formatting.None) + ");");
        }

    private async Task LogErrorAsync(string error, string stack, string context)
    {
        try
        {
            var dir = AppIdentity.AssistantHistoryRoot;
            Directory.CreateDirectory(dir);
            var entry = new JObject
            {
                ["sessionId"] = _sessionId ?? "none",
                ["timestamp"] = DateTime.UtcNow.ToString("o"),
                ["error"] = error ?? "",
                ["stack"] = stack ?? "",
                ["context"] = context ?? "",
                ["model"] = _activeModel ?? "",
                ["mode"] = _assistantMode ?? "",
                ["streaming"] = _isStreaming
            };
            var path = Path.Combine(dir, "errors-" + (_sessionId ?? "global") + ".log");
            var line = entry.ToString(Formatting.None);
            await Task.Run(() => { lock (_errorLogLock) { File.AppendAllText(path, line + Environment.NewLine); } });
        }
        catch { }
    }

        private async Task SaveSessionAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_sessionId)) return;
                var dir = AppIdentity.AssistantHistoryRoot;
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, "session-" + _sessionId + ".json");
        var transcript = await _webView.ExecuteScriptAsync("window.bbGetTranscript ? window.bbGetTranscript() : [];");
        JArray transcriptArr;
        try { transcriptArr = JArray.Parse(transcript ?? "[]"); }
        catch { transcriptArr = new JArray(); }
        var session = new JObject
        {
            ["sessionId"] = _sessionId,
            ["savedUtc"] = DateTime.UtcNow.ToString("o"),
            ["model"] = _activeModel ?? "",
            ["mode"] = _assistantMode ?? "",
                    ["transcript"] = transcriptArr
                };
                await Task.Run(() => File.WriteAllText(path, session.ToString(Formatting.Indented)));
            }
            catch { }
        }

        private static string BuildShellHtml()
        {
            return @"<!doctype html>
<html>
<head>
<meta charset='utf-8'/>
<style>
*{box-sizing:border-box;}
html,body{min-height:100%;margin:0;}
body{font-family:Segoe UI,Arial,sans-serif;background:#eef3f7;color:#18212c;font-size:12px;}
#shell{min-height:100vh;display:flex;flex-direction:column;background:#eef3f7;}
#header{position:sticky;top:0;z-index:2;background:#ffffff;color:#111827;padding:9px 10px 8px;border-bottom:1px solid #cbd6e2;border-left:4px solid #3ba7a4;}
#headerTop{display:grid;grid-template-columns:minmax(0,1fr) minmax(78px,44%);align-items:center;gap:8px;}
#header .title{font-size:13px;font-weight:700;letter-spacing:0;color:#111827;line-height:1.15;}
#header .subtitle{margin-top:2px;color:#526171;font-size:10px;line-height:1.25;}
#header .model{font-size:10px;color:#263244;display:flex;align-items:center;gap:5px;min-width:0;justify-content:flex-end;}
#modelName{overflow:hidden;text-overflow:ellipsis;white-space:nowrap;}
#header .model-dot{width:8px;height:8px;border-radius:50%;background:#3ba7a4;flex:0 0 auto;}
.model-capability{border:1px solid #b9c8d7;background:#eef6f5;color:#114541;border-radius:999px;padding:1px 5px;font-size:9px;white-space:nowrap;flex:0 0 auto;}
#statusRail{display:grid;grid-template-columns:1fr 1fr 1fr;gap:5px;margin-top:7px;}
.pill{background:#f3f6f9;border:1px solid #d5dee8;color:#334155;border-radius:6px;padding:4px 6px;font-size:10px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;}
.pill strong{color:#0f514c;font-weight:650;}
#toolRail{display:flex;gap:5px;overflow-x:auto;padding:6px 9px;background:#eef3f7;border-bottom:1px solid #d6dee8;}
.toolchip{border:1px solid #c7d2e0;background:#ffffff;color:#273449;border-radius:6px;padding:3px 7px;font-size:10px;white-space:nowrap;max-width:126px;overflow:hidden;text-overflow:ellipsis;}
.toolchip.enabled{border-color:#3ba7a4;background:#edf9f7;color:#114541;}
.toolchip.disabled{border-color:#d8dee8;background:#f7f9fb;color:#7a8696;}
#workflowStrip{display:flex;gap:5px;overflow-x:auto;padding:6px 9px;background:#ffffff;border-bottom:1px solid #d6dee8;}
#workflowStrip span{flex:0 0 auto;border:1px solid #d5dee8;background:#f8fafc;color:#334155;border-radius:999px;padding:3px 7px;font-size:9px;white-space:nowrap;}
#safetyRail{display:grid;grid-template-columns:1fr 1fr 1fr;gap:5px;padding:6px 9px;background:#ffffff;border-bottom:1px solid #d6dee8;}
.safety-item{border:1px solid #dfe7f1;background:#f8fafc;border-radius:6px;padding:5px;min-width:0;}
.safety-item strong{display:block;color:#111827;font-size:10px;line-height:1.2;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;}
.safety-item span{display:block;color:#64748b;font-size:9px;line-height:1.25;margin-top:2px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;}
.safety-item.ready{border-left:3px solid #3ba7a4;}
.safety-item.preview{border-left:3px solid #f59e0b;}
.safety-item.blocked{border-left:3px solid #ef4444;}
#contextPanel{display:flex;gap:6px;overflow-x:auto;padding:7px 9px;background:#f8fafc;border-bottom:1px solid #d6dee8;}
.context-section{border:1px solid #dfe7f1;background:#ffffff;border-radius:7px;padding:6px;flex:0 0 156px;min-width:0;}
.context-title{font-size:9px;text-transform:uppercase;color:#64748b;font-weight:700;margin-bottom:5px;letter-spacing:0;}
.context-grid{display:grid;grid-template-columns:1fr;gap:4px;}
.context-card{border:1px solid #e2e8f0;background:#f8fafc;border-radius:6px;padding:5px;min-width:0;}
.context-card strong{display:block;color:#111827;font-size:10px;line-height:1.2;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;}
.context-card span{display:block;color:#64748b;font-size:9px;line-height:1.25;margin-top:2px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;}
.context-card.planned{border-left:3px solid #f59e0b;}
.context-card.ready{border-left:3px solid #3ba7a4;}
.context-card.gated{border-left:3px solid #64748b;}
.receipt-list{display:flex;flex-direction:column;gap:5px;}
.receipt-row{display:grid;grid-template-columns:1fr auto;gap:5px;align-items:center;border:1px solid #e2e8f0;background:#f8fafc;border-radius:7px;padding:6px;min-width:0;}
.receipt-row strong{font-size:10px;color:#111827;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;}
.receipt-row span{font-size:9px;color:#64748b;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;}
.receipt-badge{font-size:9px;border-radius:999px;padding:2px 6px;background:#eef2f7;color:#475569;white-space:nowrap;}
.receipt-badge.ok{background:#e6fbf8;color:#0f514c;}
.receipt-badge.denied{background:#fff1f2;color:#9f1239;}
#log{flex:1;display:flex;flex-direction:column;gap:8px;padding:9px;min-height:120px;overflow-wrap:anywhere;}
.empty{margin:auto 4px;color:#526171;line-height:1.4;text-align:left;border:1px dashed #cbd5e1;border-radius:7px;padding:10px;background:#ffffff;}
.msg{padding:8px 9px;border-radius:7px;max-width:96%;white-space:pre-wrap;line-height:1.38;word-break:break-word;font-size:12px;}
.user{align-self:flex-end;background:#d9ff5a;color:#101410;border:1px solid #bddf43;}
.assistant{align-self:flex-start;background:#ffffff;color:#1f2937;border:1px solid #d9e0e8;}
.tool-result{align-self:stretch;max-width:100%;background:#ffffff;border:1px solid #ccd8e5;border-left:4px solid #3ba7a4;border-radius:7px;padding:8px 9px;}
.tool-head{display:flex;justify-content:space-between;gap:8px;align-items:center;margin-bottom:6px;}
.tool-title{font-weight:700;color:#111827;}
.tool-status{font-size:9px;color:#475569;background:#eef2f7;border-radius:999px;padding:2px 6px;white-space:nowrap;}
.tool-query{font-size:10px;color:#475569;margin-bottom:7px;word-break:break-word;}
.tool-receipt{display:grid;grid-template-columns:1fr auto;gap:6px;align-items:center;margin:7px 0;padding:6px;border:1px solid #e2e8f0;background:#f8fafc;border-radius:7px;font-size:9px;color:#475569;}
.tool-receipt strong{display:block;font-size:10px;color:#111827;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;}
.tool-receipt span{white-space:nowrap;overflow:hidden;text-overflow:ellipsis;}
.result-list{display:flex;flex-direction:column;gap:6px;}
.result-card{border:1px solid #e2e8f0;background:#f8fafc;border-radius:7px;padding:7px;}
.result-title{font-weight:650;color:#111827;font-size:11px;line-height:1.25;}
.result-subtitle{font-size:10px;color:#475569;margin-top:2px;line-height:1.25;}
.result-path{font-family:Consolas,'Courier New',monospace;font-size:9px;color:#64748b;margin-top:5px;word-break:break-all;}
.screenshot-card{align-self:stretch;max-width:100%;background:#ffffff;color:#18212c;border:1px solid #cbd6e2;border-left:4px solid #d9ff5a;border-radius:7px;padding:8px 9px;}
.screenshot-head{display:flex;justify-content:space-between;gap:8px;align-items:center;margin-bottom:6px;}
.screenshot-title{font-weight:700;color:#111827;}
.screenshot-chip{font-size:9px;color:#111827;background:#d9ff5a;border-radius:999px;padding:2px 6px;white-space:nowrap;}
.screenshot-meta{font-size:10px;color:#526171;line-height:1.35;margin-top:3px;word-break:break-word;}
.contact-list{display:flex;flex-direction:column;gap:5px;margin-top:8px;}
.contact-row{border:1px solid #d5dee8;background:#f8fafc;border-radius:6px;padding:6px;font-size:10px;color:#273449;}
.contact-row strong{color:#111827;}
.contact-status{display:inline-block;border-radius:999px;padding:1px 6px;margin-left:4px;background:#eef6f5;color:#0f514c;font-size:9px;}
.contact-note{color:#64748b;font-size:9px;line-height:1.25;margin-top:2px;}
.annotation-list{display:flex;flex-direction:column;gap:5px;margin-top:8px;}
.annotation-row{border:1px solid #d5dee8;background:#f8fafc;border-radius:6px;padding:6px;font-size:10px;color:#273449;}
.annotation-row strong{display:block;color:#111827;font-size:10px;line-height:1.2;}
.annotation-row span{display:block;color:#64748b;font-size:9px;line-height:1.25;margin-top:2px;}
.annotation-badge{display:inline-block;border-radius:999px;padding:1px 6px;margin-left:4px;background:#eef6f5;color:#0f514c;font-size:9px;}
.screenshot-action{margin-top:8px;border:1px solid #c6d886;background:#fbffe9;color:#3f4d16;border-radius:6px;padding:6px;font-size:10px;line-height:1.3;}
.screenshot-action strong{display:block;color:#465915;font-size:10px;}
.assistant-footer{display:flex;align-items:center;gap:5px;font-size:9px;color:#64748b;margin-top:6px;padding-top:5px;border-top:1px solid #eef2f7;}
.assistant-footer .dot{width:6px;height:6px;border-radius:50%;background:#3ba7a4;flex:0 0 auto;}
.meta{display:block;font-size:10px;color:#475569;background:#eef6ff;border:1px solid #cfe8ff;border-radius:6px;padding:3px 5px;margin-top:6px;}
pre{background:#111827;color:#e5e7eb;padding:8px;border-radius:6px;overflow-x:auto;margin:4px 0;}
code{font-family:Consolas,'Courier New',monospace;font-size:11px;color:#e5e7eb;}
.typing-indicator{align-self:flex-start;background:#ffffff;border:1px solid #d9e0e8;padding:8px 14px;border-radius:8px;display:none;align-items:center;gap:4px;}
.typing-indicator.active{display:flex;}
.typing-indicator span{width:6px;height:6px;border-radius:50%;background:#111827;animation:bbBounce 1.2s infinite ease-in-out;}
.typing-indicator span:nth-child(2){animation-delay:.2s;}
.typing-indicator span:nth-child(3){animation-delay:.4s;}
@keyframes bbBounce{0%,80%,100%{transform:scale(.4);opacity:.4;}40%{transform:scale(1);opacity:1;}}
.streaming-cursor{display:inline-block;width:2px;height:12px;background:#d9ff5a;margin-left:2px;vertical-align:middle;animation:bbCursor .8s infinite;}
@keyframes bbCursor{0%,100%{opacity:1;}50%{opacity:0;}}
</style>
</head>
<body>
<div id='shell'>
<div id='header'>
<div id='headerTop'><div><div class='title'>Bluebrick Assistant</div><div class='subtitle'>CAD-safe copilot for screenshots, models, vault context, and handoff</div></div><div class='model'><span class='model-dot'></span><span id='modelName'>AionUI</span><span id='modelCaps' class='model-capability'>text</span></div></div>
<div id='statusRail'><div id='bridgePill' class='pill'><strong>Bridge</strong> local</div><div id='modePill' class='pill'><strong>Mode</strong> ready</div><div id='toolsPill' class='pill'><strong>Tools</strong> loading</div></div>
</div>
<div id='toolRail'><span class='toolchip disabled'>Loading tools</span></div>
<div id='workflowStrip'>
<span>Screenshot -> annotation -> review report</span>
<span>Vault/PDM/Epicor read-only</span>
<span>CAD mutation blocked</span>
</div>
<div id='safetyRail'>
<div class='safety-item ready'><strong>Read Only</strong><span>chat, search, screenshots</span></div>
<div class='safety-item preview'><strong>Preview First</strong><span>diffs before CAD actions</span></div>
<div class='safety-item blocked'><strong>Mutation Blocked</strong><span>approval required</span></div>
</div>
<div id='contextPanel'>
<div class='context-section'><div class='context-title'>Activity Receipts</div><div id='receiptGrid' class='receipt-list'><div class='receipt-row'><div><strong>No activity yet</strong><span>tool receipts will appear here</span></div><div class='receipt-badge'>idle</div></div></div></div>
<div class='context-section'><div class='context-title'>Integrations</div><div id='integrationGrid' class='context-grid'><div class='context-card gated'><strong>Loading</strong><span>integration catalog</span></div></div></div>
<div class='context-section'><div class='context-title'>Documents</div><div id='documentGrid' class='context-grid'><div class='context-card gated'><strong>Loading</strong><span>document catalog</span></div></div></div>
</div>
<div id='log'><div class='empty'>Start a session, capture the SolidWorks view, or ask for help with drawings, PDM, Epicor, or generated output.</div></div>
<div id='typingIndicator' class='typing-indicator'><span></span><span></span><span></span></div>
</div>
<script>
var _streamingNode=null;
var _streamingText='';
var _modelColor='#3ba7a4';
var _modelProviders={'OpenAI':'#10a37f','Gemini':'#4285f4','Claude':'#d97757','Codex':'#1da1f2','OpenRouter':'#6366f1','NVIDIA':'#76b900','Local':'#8b949e','AionUI':'#3ba7a4'};
function esc(value){
return String(value||'').replace(/[&<>'""]/g,function(ch){
if(ch==='&')return '&amp;';
if(ch==='<')return '&lt;';
if(ch==='>')return '&gt;';
if(ch==='\'' )return '&#39;';
if(ch==='""')return '&quot;';
return ch;
});
}
window.bbReset=function(){
document.getElementById('log').innerHTML='';
_streamingNode=null;
_streamingText='';
};
window.bbAppend=function(raw){
var payload=JSON.parse(raw);
var empty=document.querySelector('.empty');
if(empty)empty.remove();
var node=document.createElement('div');
node.className='msg '+payload.role;
node.textContent=payload.text||'';
if(payload.attachment){
var meta=document.createElement('span');
meta.className='meta';
meta.textContent='Attachment: '+payload.attachment;
node.appendChild(meta);
}
if(payload.role==='assistant'){
var footer=document.createElement('div');
footer.className='assistant-footer';
footer.innerHTML='<span class=dot style=background:'+_modelColor+'></span>'+document.getElementById('modelName').textContent;
node.appendChild(footer);
}
document.getElementById('log').appendChild(node);
document.getElementById('typingIndicator').style.display='none';
window.scrollTo(0,document.body.scrollHeight);
};
window.bbTypingStart=function(){
if(_streamingNode)return;
var empty=document.querySelector('.empty');
if(empty)empty.remove();
_streamingNode=document.createElement('div');
_streamingNode.className='msg assistant';
_streamingText='';
_streamingNode.innerHTML='<span id=streamContent></span><span class=streaming-cursor></span>';
document.getElementById('log').appendChild(_streamingNode);
document.getElementById('typingIndicator').style.display='none';
window.scrollTo(0,document.body.scrollHeight);
};
window.bbAppendChunk=function(text){
if(!_streamingNode){window.bbTypingStart();}
_streamingText+=text;
var contentEl=_streamingNode.querySelector('#streamContent');
if(contentEl)contentEl.textContent=_streamingText;
window.scrollTo(0,document.body.scrollHeight);
};
window.bbTypingStop=function(){
if(_streamingNode){
var cursor=_streamingNode.querySelector('.streaming-cursor');
if(cursor)cursor.remove();
var footer=document.createElement('div');
footer.className='assistant-footer';
footer.innerHTML='<span class=dot style=background:'+_modelColor+'></span>'+document.getElementById('modelName').textContent;
_streamingNode.appendChild(footer);
_streamingNode=null;
_streamingText='';
}
document.getElementById('typingIndicator').style.display='none';
};
window.bbSetModel=function(name){
document.getElementById('modelName').textContent=name||'AionUI';
var dot=document.querySelector('#header .model-dot');
if(dot)_modelColor=_modelProviders[name]||'#3ba7a4';
if(dot)dot.style.background=_modelColor;
};
window.bbSetStatus=function(raw){
var state=typeof raw==='string'?JSON.parse(raw):raw;
var bridge=document.getElementById('bridgePill');
var mode=document.getElementById('modePill');
var tools=document.getElementById('toolsPill');
if(bridge)bridge.innerHTML='<strong>Bridge</strong> '+esc(state.bridge||'local');
if(mode)mode.innerHTML='<strong>Mode</strong> '+esc(state.mode||'mock')+(state.relayConnected?' · relay':'');
if(tools){
var availability=state.toolAvailability||state.ToolAvailability||{};
var enabled=availability.EnabledTools||availability.enabledTools||0;
var total=availability.TotalTools||availability.totalTools||0;
var search=availability.EnabledSearchTools||availability.enabledSearchTools||0;
tools.innerHTML='<strong>Tools</strong> '+esc(enabled+'/'+total)+' · search '+esc(search);
}
var model=state.activeModel||state.ActiveModel||{};
var modelName=document.getElementById('modelName');
var modelCaps=document.getElementById('modelCaps');
if(modelName&&model.Name){
modelName.textContent=(model.Provider||model.provider||'AI')+' · '+(model.Name||model.name||state.model||'model');
modelName.title=(model.ProviderKind||model.providerKind||'')+' '+(model.BaseUrlAlias||model.baseUrlAlias||'');
}
if(modelCaps){
var caps=[];
if(model.SupportsVision||model.supportsVision)caps.push('vision');
if(model.SupportsTools||model.supportsTools)caps.push('tools');
if(model.SupportsStreaming||model.supportsStreaming)caps.push('stream');
if(model.SupportsJsonMode||model.supportsJsonMode)caps.push('json');
modelCaps.textContent=caps.length?caps.join(' · '):'text';
modelCaps.title='Model capabilities';
}
};
window.bbSetTools=function(raw){
var tools=typeof raw==='string'?JSON.parse(raw):raw;
var rail=document.getElementById('toolRail');
rail.innerHTML='';
if(!tools||!tools.length){
rail.innerHTML='<span class=""toolchip disabled"">No tools</span>';
return;
}
tools.forEach(function(tool){
var chip=document.createElement('span');
var enabled=!!(tool.Enabled||tool.enabled);
chip.className='toolchip '+(enabled?'enabled':'disabled');
chip.title=(tool.Description||tool.description||'')+(enabled?'':' '+(tool.UnavailableReason||tool.unavailableReason||''));
chip.textContent=(tool.DisplayName||tool.displayName||tool.Name||tool.name||'Tool')+(enabled?'':' (off)');
rail.appendChild(chip);
});
};
window.bbSetToolReceipts=function(raw){
var receipts=typeof raw==='string'?JSON.parse(raw):raw;
var grid=document.getElementById('receiptGrid');
if(!grid)return;
grid.innerHTML='';
if(!receipts||!receipts.length){
grid.innerHTML='<div class=""receipt-row""><div><strong>No activity yet</strong><span>tool receipts will appear here</span></div><div class=""receipt-badge"">idle</div></div>';
return;
}
receipts.slice(-4).reverse().forEach(function(receipt){
var id=receipt.ReceiptId||receipt.receiptId||'';
var status=receipt.ResultStatus||receipt.resultStatus||'unknown';
var allowed=!!(receipt.Allowed||receipt.allowed);
var tool=receipt.ToolName||receipt.toolName||'tool';
var risk=receipt.RiskLevel||receipt.riskLevel||'unknown';
var row=document.createElement('div');
row.className='receipt-row';
row.title=(receipt.TraceId||receipt.traceId||'')+' '+(receipt.PolicyCode||receipt.policyCode||'');
row.innerHTML='<div><strong>'+esc(tool)+'</strong><span>'+esc((id?id.substring(0,10):'no receipt')+' · '+risk)+'</span></div><div class=""receipt-badge '+(allowed?'ok':'denied')+'"">'+esc(status)+'</div>';
grid.appendChild(row);
});
};
window.bbSetProductCatalogs=function(raw){
var payload=typeof raw==='string'?JSON.parse(raw):raw;
renderContextCards('integrationGrid',payload.integrations||payload.Integrations||[],function(item){
return {
title:item.Name||item.name||item.Id||item.id||'Integration',
subtitle:item.Status||item.status||'unknown',
state:(item.Status||item.status||'').toLowerCase()==='planned'?'planned':((item.Status||item.status||'').toLowerCase().indexOf('gated')>=0?'gated':'ready'),
tip:item.Summary||item.summary||''
};
});
renderContextCards('documentGrid',payload.documents||payload.Documents||[],function(item){
return {
title:item.Name||item.name||item.Id||item.id||'Document',
subtitle:(item.Implemented||item.implemented)?'implemented':'planned',
state:(item.Implemented||item.implemented)?'ready':'planned',
tip:item.Purpose||item.purpose||''
};
});
};
function renderContextCards(id,items,map){
var grid=document.getElementById(id);
if(!grid)return;
grid.innerHTML='';
if(!items||!items.length){
grid.innerHTML='<div class=""context-card gated""><strong>Unavailable</strong><span>No catalog data</span></div>';
return;
}
items.slice(0,6).forEach(function(item){
var data=map(item);
var card=document.createElement('div');
card.className='context-card '+(data.state||'gated');
card.title=data.tip||'';
card.innerHTML='<strong>'+esc(data.title)+'</strong><span>'+esc(data.subtitle)+'</span>';
grid.appendChild(card);
});
}
window.bbAppendToolResult=function(raw){
var payload=typeof raw==='string'?JSON.parse(raw):raw;
var empty=document.querySelector('.empty');
if(empty)empty.remove();
var node=document.createElement('div');
node.className='tool-result';
var html='<div class=""tool-head""><div class=""tool-title"">'+esc(payload.label||'Tool')+'</div><div class=""tool-status"">'+esc(payload.status||'')+'</div></div>';
html+='<div class=""tool-query"">Query: '+esc(payload.query||'')+'</div>';
html+='<div class=""tool-query"">'+esc(payload.message||'')+'</div>';
var receipt=payload.receipt||payload.Receipt||{};
if(receipt.receiptId||receipt.ReceiptId){
var rid=receipt.receiptId||receipt.ReceiptId||'';
var risk=receipt.riskLevel||receipt.RiskLevel||'unknown';
var policy=receipt.policyCode||receipt.PolicyCode||'';
html+='<div class=""tool-receipt""><div><strong>Receipt '+esc(rid.substring(0,12))+'</strong><span>'+esc(risk+' · '+policy)+'</span></div><div class=""receipt-badge '+((receipt.allowed||receipt.Allowed)?'ok':'denied')+'"">'+esc(receipt.resultStatus||receipt.ResultStatus||payload.status||'')+'</div></div>';
}
var items=payload.items||payload.Items||[];
if(items.length){
html+='<div class=""result-list"">';
items.slice(0,8).forEach(function(item){
var title=item.Title||item.title||'Untitled';
var subtitle=item.Subtitle||item.subtitle||'';
var path=item.Path||item.path||'';
html+='<div class=""result-card""><div class=""result-title"">'+esc(title)+'</div>';
if(subtitle)html+='<div class=""result-subtitle"">'+esc(subtitle)+'</div>';
if(path)html+='<div class=""result-path"">'+esc(path)+'</div>';
html+='</div>';
});
html+='</div>';
}
node.innerHTML=html;
document.getElementById('log').appendChild(node);
window.scrollTo(0,document.body.scrollHeight);
};
window.bbAppendScreenshotArtifact=function(raw){
var artifact=typeof raw==='string'?JSON.parse(raw):raw;
var empty=document.querySelector('.empty');
if(empty)empty.remove();
var node=document.createElement('div');
node.className='screenshot-card';
var dimensions=(artifact.width&&artifact.height)?artifact.width+' x '+artifact.height:'size unknown';
var html='<div class=""screenshot-head""><div class=""screenshot-title"">Screenshot captured</div><div class=""screenshot-chip"">'+esc(dimensions)+'</div></div>';
if(artifact.sourceWindowTitle)html+='<div class=""screenshot-meta"">'+esc(artifact.sourceWindowTitle)+'</div>';
if(artifact.fileName)html+='<div class=""screenshot-meta"">'+esc(artifact.fileName)+'</div>';
if(artifact.captureSource)html+='<div class=""screenshot-meta"">Capture: '+esc(artifact.captureSource)+' · '+esc(artifact.captureTarget||'')+'</div>';
if(artifact.solidWorksDocumentTitle)html+='<div class=""screenshot-meta"">Document: '+esc(artifact.solidWorksDocumentTitle)+'</div>';
html+='<div class=""screenshot-meta"">Annotations: '+esc(artifact.annotationCount||0)+' · Contacts: '+esc(artifact.contactCount||0)+'</div>';
html+='<div class=""screenshot-meta"">Privacy: '+(artifact.sentToModel?'sent to model':'local only')+' · '+esc(artifact.retentionPolicy||'delete_on_session_end')+'</div>';
var annotations=artifact.annotations||[];
if(annotations.length){
html+='<div class=""annotation-list"">';
annotations.slice(0,6).forEach(function(annotation){
var label=annotation.Label||annotation.label||'Annotation';
var severity=annotation.Severity||annotation.severity||'info';
var source=annotation.Source||annotation.source||'unknown';
var x=annotation.X||annotation.x||0;
var y=annotation.Y||annotation.y||0;
var w=annotation.Width||annotation.width||0;
var h=annotation.Height||annotation.height||0;
html+='<div class=""annotation-row""><strong>'+esc(label)+'<span class=""annotation-badge"">'+esc(severity)+'</span></strong><span>'+esc(source)+' · x'+esc(x)+' y'+esc(y)+' · '+esc(w)+' x '+esc(h)+'</span></div>';
});
html+='</div>';
}
var contacts=artifact.contacts||[];
if(contacts.length){
html+='<div class=""contact-list"">';
contacts.slice(0,5).forEach(function(contact){
var name=contact.Name||contact.name||'Contact';
var company=contact.Company||contact.company||'';
var email=contact.Email||contact.email||'';
var phone=contact.Phone||contact.phone||'';
var confidence=contact.Confidence||contact.confidence||0;
var source=contact.SourceAnnotationId||contact.sourceAnnotationId||'';
var reviewStatus=contact.ReviewStatus||contact.reviewStatus||'pending';
var reviewNote=contact.ReviewNote||contact.reviewNote||'';
var extractionSource=contact.ExtractionSource||contact.extractionSource||'local candidate';
var requiresReviewReason=contact.RequiresReviewReason||contact.requiresReviewReason||'';
var sourceText=contact.SourceText||contact.sourceText||'';
html+='<div class=""contact-row""><strong>'+esc(name)+'<span class=""contact-status"">'+esc(reviewStatus)+'</span></strong>';
if(company)html+='<br>'+esc(company);
if(email)html+='<br>'+esc(email);
if(phone)html+=' · '+esc(phone);
html+='<br>Confidence '+esc(Math.round(confidence*100))+'%';
if(source)html+=' · Source '+esc(source);
html+='<div class=""contact-note"">'+esc(extractionSource)+'</div>';
if(requiresReviewReason)html+='<div class=""contact-note"">'+esc(requiresReviewReason)+'</div>';
if(sourceText)html+='<div class=""contact-note"">Source: '+esc(sourceText)+'</div>';
if(reviewNote)html+='<div class=""contact-note"">'+esc(reviewNote)+'</div>';
html+='</div>';
});
html+='</div>';
}
html+='<div class=""screenshot-action""><strong>Review report</strong>Creating a local evidence report with annotations, contacts, privacy state, and receipt.</div>';
node.innerHTML=html;
document.getElementById('log').appendChild(node);
window.scrollTo(0,document.body.scrollHeight);
};
window.bbGetTranscript=function(){
var msgs=[];
document.querySelectorAll('.msg').forEach(function(el){
var role=el.classList.contains('user')?'user':'assistant';
var text=el.textContent||'';
msgs.push({role:role,text:text});
});
return msgs;
};
</script>
</body>
</html>";
        }

        private async void BtnNewSession_Click(object sender, EventArgs e)
        {
            await StartSessionAsync();
        }

        private async void BtnCapture_Click(object sender, EventArgs e)
        {
            await CaptureAsync();
        }

        private void BtnAttach_Click(object sender, EventArgs e)
        {
            Attach_Click(sender, e);
        }

        private async void BtnResetVault_Click(object sender, EventArgs e)
        {
            if (!_initialized) return;
            var confirmed = MessageBox.Show(
                "Reset the local Bluebrick vault index? This removes generated local vault metadata and should not be used during normal assistant chat.",
                "Confirm Local Vault Reset",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (confirmed != DialogResult.Yes) return;
            await CallVaultAsync("/lab/vault/reset", "Vault reset");
        }

        private async void BtnSearchVault_Click(object sender, EventArgs e)
        {
            await SearchSelectedToolAsync();
        }

        private void CmbSearchTool_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateSelectedSearchToolStatus();
        }

        private async void BtnReindex_Click(object sender, EventArgs e)
        {
            await CallVaultAsync("/lab/vault/reindex", "Vault reindexed");
        }

        private void BtnOpenWorking_Click(object sender, EventArgs e)
        {
            OpenWorkingFolder();
        }

        private async void BtnOpenChatGpt_Click(object sender, EventArgs e)
        {
            await OpenChatGptAsync();
        }

        private async void BtnTestConnection_Click(object sender, EventArgs e)
        {
            await TestConnectionAsync();
        }

        private async void BtnToggleMode_Click(object sender, EventArgs e)
        {
            await ToggleModeAsync();
        }

        private async void BtnSend_Click(object sender, EventArgs e)
        {
            await SendAsync();
        }

        private async void CmbModel_SelectionChangeCommitted(object sender, EventArgs e)
        {
            await SelectModelAsync();
        }

        private async void TxtChatInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                await SendAsync();
            }
        }

        private sealed class ModelListItem
        {
            internal ModelListItem(string id, string text)
            {
                Id = id;
                Text = text;
            }

            internal string Id { get; }
            internal string Text { get; }

            public override string ToString()
            {
                return Text;
            }
        }

        internal sealed class AssistantSearchCommand
        {
            internal AssistantSearchCommand(string toolName, string label, string query)
            {
                ToolName = toolName;
                Label = label;
                Query = query;
            }

            internal string ToolName { get; }
            internal string Label { get; }
            internal string Query { get; }
        }

        internal sealed class ToolListItem
        {
            internal ToolListItem(string toolName, string displayText, bool enabled, string unavailableReason)
            {
                ToolName = toolName;
                DisplayText = displayText;
                Enabled = enabled;
                UnavailableReason = unavailableReason;
            }

            internal string ToolName { get; }
            internal string DisplayText { get; }
            internal bool Enabled { get; }
            internal string UnavailableReason { get; }
            internal string ButtonText
            {
                get
                {
                    if (string.Equals(ToolName, "search_pdm", StringComparison.OrdinalIgnoreCase)) return "PDM";
                    if (string.Equals(ToolName, "search_epicor", StringComparison.OrdinalIgnoreCase)) return "Epicor";
                    if (string.Equals(ToolName, "search_salesforce", StringComparison.OrdinalIgnoreCase)) return "SF";
                    if (string.Equals(ToolName, "search_aionui_database", StringComparison.OrdinalIgnoreCase)) return "AionUI";
                    return "Vault";
                }
            }

            public override string ToString()
            {
                if (Enabled) return DisplayText;
                return DisplayText + " (off)";
            }
        }
    }
}
