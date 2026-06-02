using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace BlueBrick.Agent
{
    internal sealed class AssistantWebViewHost : IDisposable
    {
        private readonly WebView2 _webView;
        private readonly AgentConfig _config;
        private bool _disposed;
        private string _allowedDistRoot;

        internal AssistantWebViewHost(WebView2 webView, AgentConfig config)
        {
            _webView = webView ?? throw new ArgumentNullException(nameof(webView));
            _config = config ?? new AgentConfig();
        }

        internal string LastLoadError { get; private set; }
        internal bool LoadedReactShell { get; private set; }

        internal async Task<bool> InitializeAsync(Func<string> fallbackShellHtml)
        {
            try
            {
                var env = await CoreWebView2Environment.CreateAsync(userDataFolder: Path.GetTempPath()).ConfigureAwait(true);
                _webView.CoreWebView2InitializationCompleted += WebView_CoreWebView2InitializationCompleted;
                await _webView.EnsureCoreWebView2Async(env).ConfigureAwait(true);
                ConfigureSecurity();

                var navigationReady = new TaskCompletionSource<bool>();
                EventHandler<CoreWebView2NavigationCompletedEventArgs> completed = null;
                completed = (s, e) =>
                {
                    _webView.CoreWebView2.NavigationCompleted -= completed;
                    navigationReady.TrySetResult(e.IsSuccess);
                    if (!e.IsSuccess)
                    {
                        LastLoadError = "Assistant WebView navigation failed: " + e.WebErrorStatus;
                    }
                };
                _webView.CoreWebView2.NavigationCompleted += completed;

                var loaded = LoadReactDist();
                if (!loaded)
                {
                    LoadedReactShell = false;
                    _webView.NavigateToString(fallbackShellHtml == null ? MinimalFallbackHtml() : fallbackShellHtml());
                }

                var finished = await Task.WhenAny(navigationReady.Task, Task.Delay(TimeSpan.FromSeconds(10))).ConfigureAwait(true);
                if (finished != navigationReady.Task)
                {
                    LastLoadError = "Assistant WebView navigation timed out.";
                }

                return true;
            }
            catch (Exception ex)
            {
                LastLoadError = ex.Message;
                try
                {
                    _webView.NavigateToString(MinimalFallbackHtml());
                }
                catch { }
                return false;
            }
        }

        private void WebView_CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (!e.IsSuccess)
            {
                LastLoadError = e.InitializationException?.Message ?? "WebView2 initialization failed.";
            }
        }

        private void ConfigureSecurity()
        {
            if (_webView.CoreWebView2 == null) return;
            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _webView.CoreWebView2.Settings.AreHostObjectsAllowed = false;
            _webView.CoreWebView2.Settings.IsWebMessageEnabled = false;
            _webView.CoreWebView2.Settings.IsScriptEnabled = true;
            _webView.CoreWebView2.NavigationStarting += (s, e) =>
            {
                if (!IsNavigationAllowed(e.Uri))
                {
                    e.Cancel = true;
                    LastLoadError = "Blocked WebView navigation: " + e.Uri;
                }
            };
            _webView.CoreWebView2.NewWindowRequested += (s, e) => { e.Handled = true; };
        }

        private bool LoadReactDist()
        {
            if ((_config.Assistant?.UseReactWebView ?? false) != true) return false;
            var distIndex = FindDistIndex();
            if (string.IsNullOrWhiteSpace(distIndex) || !File.Exists(distIndex))
            {
                LastLoadError = "AssistantWeb dist not found.";
                return false;
            }

            _allowedDistRoot = Path.GetDirectoryName(distIndex);
            _webView.Source = new Uri(distIndex);
            LoadedReactShell = true;
            return true;
        }

        private static string FindDistIndex()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(baseDir, "AssistantWeb", "dist", "index.html"),
                Path.Combine(baseDir, "..", "..", "AssistantWeb", "dist", "index.html"),
                Path.Combine(baseDir, "..", "AssistantWeb", "dist", "index.html")
            };

            foreach (var candidate in candidates)
            {
                try
                {
                    var full = Path.GetFullPath(candidate);
                    if (File.Exists(full)) return full;
                }
                catch { }
            }
            return null;
        }

        private bool IsNavigationAllowed(string uri)
        {
            if (AssistantWebViewSecurity.IsNavigationAllowed(uri)) return true;
            if (string.IsNullOrWhiteSpace(uri)) return false;
            if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed)) return false;
            if (parsed.IsFile && !string.IsNullOrWhiteSpace(_allowedDistRoot))
            {
                try
                {
                    var localPath = Path.GetFullPath(parsed.LocalPath);
                    var root = Path.GetFullPath(_allowedDistRoot);
                    return localPath.StartsWith(root, StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    return false;
                }
            }
            return false;
        }

        private static string MinimalFallbackHtml()
        {
            return "<!doctype html><html><body style='font-family:Segoe UI,Arial,sans-serif;background:#f8fafc;color:#111827'><strong>BlueBrick Assistant</strong><br/>Assistant WebView fallback loaded.</body></html>";
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                _webView.CoreWebView2InitializationCompleted -= WebView_CoreWebView2InitializationCompleted;
            }
            catch { }
        }
    }
}
