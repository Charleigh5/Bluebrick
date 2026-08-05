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
    public class FrmAssistantWindow : Form
    {
        private readonly WebView2 _webView;
        private readonly TextBox _input;
        private readonly Button _send;
        private readonly Button _newSession;
        private readonly Button _capture;
        private readonly Button _attach;
        private readonly Button _reindex;
        private readonly Button _resetVault;
        private readonly Button _openWorking;
        private readonly Button _openChatGpt;
        private readonly Button _testConnection;
        private readonly Button _toggleMode;
        private readonly Label _status;
        private readonly TextBox _previewInfo;
        private readonly TaskCompletionSource<bool> _pageReady = new TaskCompletionSource<bool>();
        private string _sessionId;
        private string _pendingAttachment;
        private string _assistantMode;
        private string _activeModel = "AionUI";
        private bool _isStreaming;
        private CancellationTokenSource _streamCts;
        private bool _initialized;
        private bool _initFailed;
        private readonly object _errorLogLock = new object();

        public FrmAssistantWindow()
        {
            Text = AppIdentity.ProductName + " Assistant Preview";
            Width = 1080;
            Height = 820;
            StartPosition = FormStartPosition.CenterScreen;

            var top = new Panel { Dock = DockStyle.Top, Height = 102 };
            _newSession = new Button { Text = "New Session", Width = 100, Left = 10, Top = 8 };
            _capture = new Button { Text = "Capture Window", Width = 112, Left = 116, Top = 8 };
            _attach = new Button { Text = "Attach Image/PDF", Width = 118, Left = 234, Top = 8 };
            _reindex = new Button { Text = "Reindex Vault", Width = 100, Left = 358, Top = 8 };
            _resetVault = new Button { Text = "Reset Vault", Width = 96, Left = 464, Top = 8 };
            _openWorking = new Button { Text = "Open Working", Width = 104, Left = 566, Top = 8 };
            _openChatGpt = new Button { Text = "Open in ChatGPT", Width = 126, Left = 676, Top = 8 };
            _testConnection = new Button { Text = "Test Assistant", Width = 108, Left = 808, Top = 8 };
            _toggleMode = new Button { Text = "Use Mock", Width = 92, Left = 922, Top = 8 };
            _status = new Label { Text = "Connecting...", AutoSize = true, Left = 10, Top = 78 };
            _previewInfo = new TextBox
            {
                Left = 10,
                Top = 42,
                Width = 1042,
                Height = 48,
                Multiline = true,
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            top.Controls.AddRange(new Control[]
            {
                _newSession, _capture, _attach, _reindex, _resetVault, _openWorking, _openChatGpt, _testConnection, _toggleMode,
                _status, _previewInfo
            });

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 54 };
        _input = new TextBox
        {
            Left = 10,
            Top = 14,
            Width = 920,
            Text = "Chat",
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
        };
        _send = new Button { Text = "Send", Width = 100, Left = 940, Top = 12, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        bottom.Controls.AddRange(new Control[] { _input, _send });
        _input.GotFocus += (s, e) => { if (_input.Text == "Chat") _input.Text = ""; };
        _input.LostFocus += (s, e) => { if (string.IsNullOrWhiteSpace(_input.Text)) _input.Text = "Chat"; };

            _webView = new WebView2 { Dock = DockStyle.Fill };

            Controls.Add(_webView);
            Controls.Add(bottom);
            Controls.Add(top);

            _newSession.Click += async (s, e) => await StartSessionAsync();
            _capture.Click += async (s, e) => await CaptureAsync();
            _attach.Click += Attach_Click;
            _reindex.Click += async (s, e) => await CallVaultAsync("/lab/vault/reindex", "Vault reindexed");
            _resetVault.Click += async (s, e) => await CallVaultAsync("/lab/vault/reset", "Vault reset");
            _openWorking.Click += (s, e) => OpenWorkingFolder();
            _openChatGpt.Click += async (s, e) => await OpenChatGptAsync();
            _testConnection.Click += async (s, e) => await TestConnectionAsync();
            _toggleMode.Click += async (s, e) => await ToggleModeAsync();
            _send.Click += async (s, e) => await SendAsync();
            Shown += async (s, e) => await InitializeAsync();
            _webView.NavigationCompleted += (s, e) => _pageReady.TrySetResult(true);
        }

    private async Task InitializeAsync()
    {
        try
        {
            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: Path.GetTempPath());
            await _webView.EnsureCoreWebView2Async(env);

            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _webView.CoreWebView2.Settings.AreHostObjectsAllowed = false;
            _webView.CoreWebView2.Settings.IsWebMessageEnabled = false;
            _webView.CoreWebView2.Settings.IsScriptEnabled = true;

            _webView.CoreWebView2.NavigationStarting += (s, e) =>
            {
                if (!AssistantWebViewSecurity.IsNavigationAllowed(e.Uri))
                {
                    e.Cancel = true;
                }
            };
            _webView.CoreWebView2.NewWindowRequested += (s, e) =>
            {
                e.Handled = true;
            };
            _webView.NavigateToString(BuildShellHtml());
            await _pageReady.Task;

            _initialized = true;
            await RefreshStatusAsync();
            await StartSessionAsync();
        }
        catch (Exception)
        {
            _initFailed = true;
            _status.Text = "WebView2 unavailable";
        }
    }

        private async Task StartSessionAsync()
        {
            var result = await AgentPanelClient.PostJsonAsync("/assistant/session", new JObject());
            if (!result.Ok)
            {
                _status.Text = result.Error;
                return;
            }

            _sessionId = result.Data.Value<string>("sessionId");
            _pendingAttachment = null;
            _status.Text = "Session ready";
            await ResetTranscriptAsync();
            await RefreshStatusAsync();
        }

        private async Task RefreshStatusAsync()
        {
            var result = await AgentPanelClient.GetJsonAsync("/assistant/status");
            if (!result.Ok)
            {
                _status.Text = result.Error;
                _previewInfo.Text = "Preview status unavailable. " + result.Error;
                return;
            }

            var data = result.Data;
            _assistantMode = data.Value<string>("AssistantMode") ?? data.Value<string>("assistant_mode") ?? "mock";
            var configured = data.Value<bool?>("Configured") ?? data.Value<bool?>("configured") ?? false;
            var keySource = data.Value<string>("KeySource") ?? data.Value<string>("key_source") ?? "missing";
            var bridgeUrl = data.Value<string>("BridgeUrl") ?? data.Value<string>("bridge_url") ?? ("http://127.0.0.1:" + AppIdentity.BridgePort);
            var localVaultRoot = data.Value<string>("LocalVaultRoot") ?? data.Value<string>("local_vault_root") ?? AppIdentity.LocalVaultRoot;
            var sampleSeedRoot = data.Value<string>("SampleSeedRoot") ?? data.Value<string>("sample_seed_root") ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "samples");
            var relayConfigured = data.Value<bool?>("RelayConfigured") ?? data.Value<bool?>("relay_configured") ?? false;
            var relayConnected = data.Value<bool?>("RelayConnected") ?? data.Value<bool?>("relay_connected") ?? false;
            var relayBaseUrl = data.Value<string>("RelayBaseUrl") ?? data.Value<string>("relay_base_url") ?? string.Empty;
            var chatWorkspaceUrl = data.Value<string>("ChatWorkspaceUrl") ?? data.Value<string>("chat_workspace_url") ?? string.Empty;
            var checklist = data["Checklist"] as JArray;

            _activeModel = data.Value<string>("Model") ?? data.Value<string>("model") ?? "AionUI";

            _toggleMode.Text = string.Equals(_assistantMode, "real", StringComparison.OrdinalIgnoreCase) ? "Use Mock" : "Use Real";
            _openChatGpt.Enabled = true;
            _previewInfo.Text =
            $"Mode: {(_assistantMode ?? "mock").ToUpperInvariant()} | Key: {(configured ? "Configured" : "Missing")} ({keySource}) | Bridge: {bridgeUrl}{Environment.NewLine}" +
            $"Vault: {localVaultRoot} | Samples: {sampleSeedRoot}{Environment.NewLine}" +
            $"Relay: {(relayConfigured ? (relayConnected ? "Connected" : "Configured") : "Not Configured")} | Relay URL: {relayBaseUrl} | ChatGPT: {chatWorkspaceUrl}";

            if (checklist != null && checklist.Count > 0)
            {
                _status.Text = checklist[checklist.Count - 1]?.ToString() ?? "Preview ready";
            }
            else if (configured && string.Equals(_assistantMode, "real", StringComparison.OrdinalIgnoreCase))
            {
                _status.Text = "Real assistant ready";
            }
            else
            {
                _status.Text = "Mock preview ready";
            }

            try
            {
                await _webView.ExecuteScriptAsync("if(window.bbSetModel)window.bbSetModel(" + JsonConvert.SerializeObject(_activeModel) + ");");
            }
            catch { }
        }

        private async Task CaptureAsync()
        {
            if (string.IsNullOrWhiteSpace(_sessionId)) await StartSessionAsync();
            var payload = new JObject { ["sessionId"] = _sessionId };
            var result = await AgentPanelClient.PostJsonAsync("/assistant/screenshot", payload);
            if (!result.Ok)
            {
                _status.Text = result.Error;
                return;
            }

            _pendingAttachment = result.Data.Value<string>("path");
            _status.Text = "Captured screenshot attached";
        }

        private void Attach_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "Images and PDFs|*.png;*.jpg;*.jpeg;*.bmp;*.pdf";
                if (dialog.ShowDialog() != DialogResult.OK) return;
                _pendingAttachment = dialog.FileName;
                _status.Text = "Attached " + Path.GetFileName(dialog.FileName);
            }
        }

        private async Task SendAsync()
        {
            var text = _input.Text.Trim();
            if ((text.Length == 0 || text == "Chat") && string.IsNullOrWhiteSpace(_pendingAttachment)) return;
            if (text == "Chat") text = "";
            if (string.IsNullOrWhiteSpace(_sessionId)) await StartSessionAsync();

            var userText = text;
            await AppendMessageAsync("user", userText, _pendingAttachment);
            _input.Text = "";

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
            _streamCts = new CancellationTokenSource();

            _status.Text = _activeModel;
            await _webView.ExecuteScriptAsync("window.bbTypingStart();");

            var fullResponse = new StringBuilder();
            try
            {
                await AgentPanelClient.PostStreamingAsync("/assistant/message", payload, chunk =>
                {
                    try
                    {
                        var jObj = JObject.Parse(chunk);
                        var textChunk = jObj["text"]?.ToString() ?? jObj["content"]?.ToString() ?? jObj["delta"]?["content"]?.ToString();
                        if (textChunk != null)
                        {
                            fullResponse.Append(textChunk);
                            _webView.ExecuteScriptAsync("window.bbAppendChunk(" + JsonConvert.SerializeObject(textChunk) + ");");
                        }
                    }
                    catch
                    {
                        fullResponse.Append(chunk);
                        _webView.ExecuteScriptAsync("window.bbAppendChunk(" + JsonConvert.SerializeObject(chunk) + ");");
                    }
                }, _streamCts.Token);

                var finalText = fullResponse.ToString();
                if (string.IsNullOrWhiteSpace(finalText))
                {
                    var fallback = await AgentPanelClient.PostJsonAsync("/assistant/message", payload);
                    if (fallback.Ok)
                    {
                        var error = fallback.Data.Value<string>("error");
                        if (!string.IsNullOrWhiteSpace(error))
                        {
                            finalText = error;
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
                        finalText = "Request failed: " + fallback.Error;
                        await LogErrorAsync(fallback.Error, "", "SendAsync-fallback-fail");
                    }
                }

                await _webView.ExecuteScriptAsync("window.bbTypingStop();");
                await AppendMessageAsync("assistant", finalText, null);
            }
            catch (OperationCanceledException)
            {
                await _webView.ExecuteScriptAsync("window.bbTypingStop();");
            }
            catch (Exception ex)
            {
                await _webView.ExecuteScriptAsync("window.bbTypingStop();");
                await AppendMessageAsync("assistant", "Request failed: " + ex.Message, null);
                await LogErrorAsync(ex.Message, ex.StackTrace, "SendAsync-streaming");
            }
            finally
            {
                _isStreaming = false;
                _streamCts?.Dispose();
                _streamCts = null;
                _status.Text = "Ready";
                await RefreshStatusAsync();
                await SaveSessionAsync();
            }
        }

        private async Task TestConnectionAsync()
        {
            _status.Text = "Testing assistant...";
            var result = await AgentPanelClient.PostJsonAsync("/assistant/test", new JObject());
            if (!result.Ok)
            {
                _status.Text = result.Error;
                return;
            }

            var success = result.Data.Value<bool?>("success") ?? false;
            var mode = result.Data.Value<string>("mode") ?? _assistantMode ?? "mock";
            var message = result.Data.Value<string>("message") ?? string.Empty;
            var latency = result.Data.Value<double?>("latencyMs") ?? 0d;
            _status.Text = success
                ? $"Assistant {mode} test ok ({latency:0} ms)"
                : $"Assistant {mode} test failed";
            await AppendMessageAsync("assistant", message, null);
            await RefreshStatusAsync();
        }

        private async Task ToggleModeAsync()
        {
            var nextMode = string.Equals(_assistantMode, "real", StringComparison.OrdinalIgnoreCase) ? "mock" : "real";
            var result = await AgentPanelClient.PostJsonAsync("/assistant/mode", new JObject { ["mode"] = nextMode });
            if (!result.Ok)
            {
                _status.Text = result.Error;
                return;
            }

            await RefreshStatusAsync();
        }

        private async Task OpenChatGptAsync()
        {
            if (string.IsNullOrWhiteSpace(_sessionId))
            {
                await StartSessionAsync();
            }

            _status.Text = "Preparing ChatGPT handoff...";
            var payload = new JObject();
            if (!string.IsNullOrWhiteSpace(_pendingAttachment))
            {
                payload["lastScreenshotPath"] = _pendingAttachment;
            }

            var result = await AgentPanelClient.PostJsonAsync("/chatgpt/session/create", payload);
            if (!result.Ok)
            {
                _status.Text = result.Error;
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
                _status.Text = "ChatGPT handoff unavailable";
                await AppendMessageAsync("assistant", string.IsNullOrWhiteSpace(message) ? "Relay is not configured yet." : message, null);
                return;
            }

            try
            {
                Process.Start(targetUrl);
                _status.Text = relayConfigured ? "Opened ChatGPT handoff" : "Opened ChatGPT workspace";
                if (!string.IsNullOrWhiteSpace(message))
                {
                    await AppendMessageAsync("assistant", message, null);
                }
            }
            catch (Exception ex)
            {
                _status.Text = "Failed to open ChatGPT";
                await AppendMessageAsync("assistant", "ChatGPT handoff URL could not be opened: " + ex.Message, null);
            }
        }

        private async Task CallVaultAsync(string path, string statusText)
        {
            var result = await AgentPanelClient.PostJsonAsync(path, new JObject());
            _status.Text = result.Ok ? statusText : result.Error;
            await RefreshStatusAsync();
        }

        private void OpenWorkingFolder()
        {
            Directory.CreateDirectory(AppIdentity.DefaultWorkingFolder);
            Process.Start("explorer.exe", AppIdentity.DefaultWorkingFolder);
        }

        private async Task ResetTranscriptAsync()
        {
            await _webView.ExecuteScriptAsync("window.bbReset();");
        }

        private async Task AppendMessageAsync(string role, string text, string attachmentPath)
        {
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
body{font-family:Segoe UI,Arial,sans-serif;background:#0f1216;color:#f5f7fa;margin:0;padding:18px;}
#header{display:flex;align-items:center;justify-content:space-between;padding:4px 0 12px;border-bottom:1px solid #2b333b;margin-bottom:12px;}
#header .title{font-size:15px;font-weight:600;color:#d9ff5a;}
#header .model{font-size:11px;color:#8b949e;display:flex;align-items:center;gap:5px;}
#header .model-dot{width:7px;height:7px;border-radius:50%;background:#3ba7a4;}
#log{display:flex;flex-direction:column;gap:12px;min-height:60px;}
.msg{padding:12px 14px;border-radius:12px;max-width:82%;white-space:pre-wrap;line-height:1.45;}
.user{align-self:flex-end;background:#d9ff5a;color:#101410;}
.assistant{align-self:flex-start;background:#1b2128;color:#f5f7fa;border:1px solid #2b333b;}
.assistant-footer{display:flex;align-items:center;gap:5px;font-size:10px;color:#8b949e;margin-top:6px;}
.assistant-footer .dot{width:6px;height:6px;border-radius:50%;background:#3ba7a4;}
.meta{display:block;font-size:11px;opacity:.72;margin-top:8px;}
.typing-indicator{align-self:flex-start;background:#1b2128;border:1px solid #2b333b;padding:10px 16px;border-radius:12px;display:none;align-items:center;gap:5px;}
.typing-indicator.active{display:flex;}
.typing-indicator span{width:7px;height:7px;border-radius:50%;background:#d9ff5a;animation:bbBounce 1.2s infinite ease-in-out;}
.typing-indicator span:nth-child(2){animation-delay:.2s;}
.typing-indicator span:nth-child(3){animation-delay:.4s;}
@keyframes bbBounce{0%,80%,100%{transform:scale(.4);opacity:.4;}40%{transform:scale(1);opacity:1;}}
.streaming-cursor{display:inline-block;width:2px;height:14px;background:#d9ff5a;margin-left:2px;vertical-align:middle;animation:bbCursor .8s infinite;}
@keyframes bbCursor{0%,100%{opacity:1;}50%{opacity:0;}}
</style>
</head>
<body>
<div id='header'>
<div class='title'>Chat</div>
<div class='model'><span class='model-dot'></span><span id='modelName'>AionUI</span></div>
</div>
<div id='log'></div>
<div id='typingIndicator' class='typing-indicator'><span></span><span></span><span></span></div>
<script>
var _streamingNode=null;
var _streamingText='';
var _modelColor='#3ba7a4';
var _modelProviders={'OpenAI':'#10a37f','Gemini':'#4285f4','Claude':'#d97757','Codex':'#1da1f2','OpenRouter':'#6366f1','NVIDIA':'#76b900','Local':'#8b949e','AionUI':'#3ba7a4'};
window.bbReset=function(){
document.getElementById('log').innerHTML='';
_streamingNode=null;
_streamingText='';
};
window.bbAppend=function(raw){
var payload=JSON.parse(raw);
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
    }
}
