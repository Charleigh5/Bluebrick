using System;
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
        private JArray _integrationCatalog = new JArray();
        private JArray _documentCatalog = new JArray();
        private bool _initialized;
        private bool _initFailed;
        private bool _isStreaming;
        private bool _loadingModels;
        private readonly object _initLock = new object();
        private readonly object _errorLogLock = new object();
        private CancellationTokenSource _streamCts;
        private AssistantWebViewHost _webHost;

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
                lblChatStatus.Visible = false;

                _initialized = true;

                await RefreshStatusAsync();
                await LoadModelsAsync();
                await LoadToolsAsync();
                await LoadToolAuditAsync();
                await LoadProductCatalogsAsync();
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

        internal async Task StartSessionAsync()
        {
            if (!_initialized) return;

            var result = await AgentPanelClient.PostJsonAsync("/assistant/session", new JObject());
            if (!result.Ok)
            {
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
                try
                {
                    await _webView.ExecuteScriptAsync("if(window.bbSetModel)window.bbSetModel(" + JsonConvert.SerializeObject(_activeModel) + ");");
                    var uiState = new JObject
                    {
                        ["mode"] = _assistantMode,
                        ["model"] = _activeModel,
                        ["configured"] = configured,
                        ["bridge"] = "127.0.0.1:" + bridgePort,
                        ["relayConnected"] = relayConnected,
                        ["activeModel"] = activeModel,
                        ["toolAvailability"] = toolAvailability,
                        ["status"] = lblChatStatus.Text
                    };
                    await _webView.ExecuteScriptAsync("if(window.bbSetStatus)window.bbSetStatus(" + uiState.ToString(Formatting.None) + ");");
                }
                catch { }
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
                try
                {
                    await _webView.ExecuteScriptAsync("if(window.bbSetTools)window.bbSetTools(" + _toolCatalog.ToString(Formatting.None) + ");");
                }
                catch { }
            }
        }

        private async Task LoadToolAuditAsync()
        {
            if (!_initialized) return;

            _toolReceipts = await AgentPanelClient.FetchToolAuditAsync(6).ConfigureAwait(true);
            if (_webView.CoreWebView2 != null)
            {
                try
                {
                    await _webView.ExecuteScriptAsync("if(window.bbSetToolReceipts)window.bbSetToolReceipts(" + _toolReceipts.ToString(Formatting.None) + ");");
                }
                catch { }
            }
        }

        private async Task LoadProductCatalogsAsync()
        {
            if (!_initialized) return;

            _integrationCatalog = await AgentPanelClient.FetchIntegrationsAsync().ConfigureAwait(true);
            _documentCatalog = await AgentPanelClient.FetchDocumentCatalogAsync().ConfigureAwait(true);
            if (_webView.CoreWebView2 != null)
            {
                try
                {
                    var payload = new JObject
                    {
                        ["integrations"] = _integrationCatalog,
                        ["documents"] = _documentCatalog
                    };
                    await _webView.ExecuteScriptAsync("if(window.bbSetProductCatalogs)window.bbSetProductCatalogs(" + payload.ToString(Formatting.None) + ");");
                }
                catch { }
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

            var result = await AgentPanelClient.PostJsonAsync("/assistant/model", new JObject { ["modelId"] = item.Id });
            if (!result.Ok)
            {
                lblChatStatus.Text = result.Error;
                lblChatStatus.Visible = true;
                return;
            }

            _activeModel = result.Data.Value<string>("model") ?? item.Text;
            lblChatStatus.Text = "Model set: " + _activeModel;
            lblChatStatus.Visible = false;
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
                   GetToolDescriptor("search_epicor") != null;
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
                if (name != "search_local_vault" && name != "search_pdm" && name != "search_epicor") continue;
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
            var command = ResolveSearchCommand(query, selected?.ToolName);
            query = command.Query;
            if (string.IsNullOrWhiteSpace(query))
            {
                await AppendMessageAsync("assistant", "Type a search query, optionally prefixed with /vault, /pdm, or /epicor, then click Search.", null);
                return;
            }

            var descriptor = GetToolDescriptor(command.ToolName);
            var label = descriptor?.Value<string>("DisplayName") ?? descriptor?.Value<string>("displayName") ?? selected?.DisplayText ?? command.Label;
            var enabled = descriptor?.Value<bool?>("Enabled") ?? descriptor?.Value<bool?>("enabled") ?? selected?.Enabled ?? false;

            lblChatStatus.Text = enabled ? "Searching " + label + "..." : label + " unavailable";
            lblChatStatus.Visible = true;

            var result = await AgentPanelClient.ExecuteToolAsync(command.ToolName, query, 8);
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
            return new AssistantSearchCommand("search_local_vault", "Local vault", query);
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

        private async Task SendAsync()
        {
            if (!_initialized) return;

            var text = txtChatInput.Text.Trim();
            if ((text.Length == 0 || text == "Chat") && string.IsNullOrWhiteSpace(_pendingAttachment)) return;
            if (text == "Chat") text = "";
            if (string.IsNullOrWhiteSpace(_sessionId)) await StartSessionAsync();

            var userText = text;
            await AppendMessageAsync("user", userText, _pendingAttachment);
            txtChatInput.Text = "";

            var payload = new JObject
            {
                ["sessionId"] = _sessionId,
                ["message"] = userText,
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

            lblChatStatus.Text = _activeModel;
            lblChatStatus.Visible = true;
            await _webView.ExecuteScriptAsync("window.bbTypingStart();");

            var fullResponse = new StringBuilder();
            try
            {
            await AgentPanelClient.PostStreamingAsync("/assistant/message/stream", payload, chunk =>
            {
                try
                {
                    var jObj = JObject.Parse(chunk);
                    var chunkType = jObj.Value<string>("type");
                    if (chunkType == "text_delta")
                    {
                        var textChunk = jObj.Value<string>("text") ?? string.Empty;
                        if (textChunk.Length > 0)
                        {
                            fullResponse.Append(textChunk);
                            _webView.ExecuteScriptAsync("window.bbAppendChunk(" + JsonConvert.SerializeObject(textChunk) + ");");
                        }
                    }
                    else if (chunkType == "error")
                    {
                        var errorCode = jObj.Value<string>("errorCode") ?? "unknown";
                        var errorMessage = jObj.Value<string>("errorMessage") ?? "Unknown error";
                        fullResponse.Clear();
                        fullResponse.Append(BuildUserFacingError(errorCode, errorMessage));
                    }
                    else if (chunkType == "tool_call")
                    {
                        var toolName = jObj.Value<string>("toolName") ?? "unknown";
                        var toolCallId = jObj.Value<string>("toolCallId") ?? "";
                        var toolLabel = string.IsNullOrWhiteSpace(toolName) ? "tool" : toolName;
                        _webView.ExecuteScriptAsync("window.bbAppendChunk(" + JsonConvert.SerializeObject("[Calling " + toolLabel + "...]") + ");");
                    }
                    else if (chunkType == "tool_result")
                    {
                        var resultContent = jObj.Value<string>("toolResultContent") ?? "";
                        string summaryText;
                        try
                        {
                            var resultObj = JObject.Parse(resultContent);
                            summaryText = resultObj.Value<string>("message") ?? "Tool completed.";
                        }
                        catch
                        {
                            summaryText = "Tool completed.";
                        }
                        _webView.ExecuteScriptAsync("window.bbAppendChunk(" + JsonConvert.SerializeObject("[" + summaryText + "]") + ");");
                    }
                }
                catch
                {
                    fullResponse.Append(chunk);
                    _webView.ExecuteScriptAsync("window.bbAppendChunk(" + JsonConvert.SerializeObject(chunk) + ");");
                }
            }, requestCts.Token);

                var finalText = fullResponse.ToString();
                if (string.IsNullOrWhiteSpace(finalText))
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

                await _webView.ExecuteScriptAsync("window.bbTypingStop();");
                await AppendMessageAsync("assistant", finalText, null);
            }
            catch (OperationCanceledException)
            {
                await _webView.ExecuteScriptAsync("window.bbTypingStop();");
                await AppendMessageAsync("assistant", "Request canceled.", null);
            }
            catch (Exception ex)
            {
                await _webView.ExecuteScriptAsync("window.bbTypingStop();");
                var classified = AssistantErrorClassifier.FromException(ex);
                await AppendMessageAsync("assistant", BuildUserFacingError(classified.Code, classified.Message), null);
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
            await _webView.ExecuteScriptAsync("window.bbReset();");
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
html+='<div class=""contact-row""><strong>'+esc(name)+'<span class=""contact-status"">'+esc(reviewStatus)+'</span></strong>';
if(company)html+='<br>'+esc(company);
if(email)html+='<br>'+esc(email);
if(phone)html+=' · '+esc(phone);
html+='<br>Confidence '+esc(Math.round(confidence*100))+'%';
if(source)html+=' · Source '+esc(source);
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
