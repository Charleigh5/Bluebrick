using System;
using System.Collections.Generic;
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
        private System.Windows.Forms.Timer _presentationProbeTimer;
        private int _presentationProbeCount;
        private int _presentationProbeAttemptCount;
        private bool _presentationProbeCaptureInFlight;
        private bool _diagnosticsProcessFailedHooked;
        private System.Windows.Forms.Timer _bootstrapReadbackTimer;
        private int _bootstrapReadbackCount;
        private bool _bootstrapReadbackInFlight;

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

                if (!_diagnosticsProcessFailedHooked &&
                    _webView.CoreWebView2 != null)
                {
                    _webView.CoreWebView2.ProcessFailed +=
                        delegate(
                            object sender,
                            Microsoft.Web.WebView2.Core.CoreWebView2ProcessFailedEventArgs args)
                        {
                            AssistantWebViewDiagnostics.RecordProcessFailed(args);
                        };

                    _diagnosticsProcessFailedHooked = true;
                }

                // BB-REACT-VIRTUAL-HOST-REPAIR-001: positive resource-load proof.
                _webView.CoreWebView2.WebResourceResponseReceived +=
                    delegate(object s2, Microsoft.Web.WebView2.Core.CoreWebView2WebResourceResponseReceivedEventArgs args)
                    {
                        try
                        {
                            string uri = args.Request.Uri ?? string.Empty;
                            if (uri.IndexOf(
                                    AssistantWebViewSecurity.ReactVirtualHostName,
                                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                                uri.IndexOf("assistant-", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                uri.EndsWith("/index.html", StringComparison.OrdinalIgnoreCase))
                            {
                                int status = args.Response != null
                                    ? args.Response.StatusCode
                                    : -1;
                                AssistantWebViewDiagnostics.RecordResourceResponse(
                                    uri,
                                    status,
                                    args.Request.Method ?? "GET");
                            }
                        }
                        catch { }
                    };

                // BB-SPA-BOOTSTRAP-TELEMETRY-001: install before any page script runs.
                _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
                    AssistantWebViewDiagnostics.BootstrapTelemetryScript);

                StartBootstrapReadbackTimer();

                // StartPresentationProbe() disabled for bootstrap telemetry isolation
                // (BB-SPA-BOOTSTRAP-TELEMETRY-001 T0). Implementation retained below.
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

                // BB-SPA-BOOTSTRAP-TELEMETRY-001: one-shot read immediately after
                // navigation settles, then a late read for slow/late failures.
                var bootstrapCapture =
                    AssistantWebViewDiagnostics.CaptureBootstrapAsync(
                        _webView,
                        "post_navigation");

                var lateBootstrapCapture = Task.Run(async delegate
                {
                    await Task.Delay(TimeSpan.FromSeconds(8)).ConfigureAwait(false);
                    try
                    {
                        _webView.BeginInvoke((Action)(async delegate
                        {
                            try
                            {
                                await AssistantWebViewDiagnostics.CaptureBootstrapAsync(
                                    _webView,
                                    "late_8s");
                            }
                            catch { }
                        }));
                    }
                    catch { }
                });

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

        private void StartBootstrapReadbackTimer()
        {
            if (_bootstrapReadbackTimer != null)
                return;

            _bootstrapReadbackTimer =
                new System.Windows.Forms.Timer();

            _bootstrapReadbackTimer.Interval = 2000;

            _bootstrapReadbackTimer.Tick +=
                async delegate
                {
                    if (_bootstrapReadbackCount >= 25 ||
                        _webView == null ||
                        _webView.CoreWebView2 == null ||
                        _bootstrapReadbackInFlight)
                    {
                        if (_bootstrapReadbackCount >= 25 &&
                            _bootstrapReadbackTimer != null)
                        {
                            _bootstrapReadbackTimer.Stop();
                            _bootstrapReadbackTimer.Dispose();
                            _bootstrapReadbackTimer = null;
                        }
                        return;
                    }

                    _bootstrapReadbackInFlight = true;
                    try
                    {
                        int candidate = _bootstrapReadbackCount + 1;
                        await AssistantWebViewDiagnostics.CaptureBootstrapAsync(
                            _webView,
                            "periodic_" + candidate.ToString("00"));
                        _bootstrapReadbackCount++;
                    }
                    catch { }
                    finally
                    {
                        _bootstrapReadbackInFlight = false;
                    }
                };

            _bootstrapReadbackTimer.Start();
        }

        private void StartPresentationProbe()
        {
            if (_presentationProbeTimer != null)
                return;

            _presentationProbeTimer =
                new System.Windows.Forms.Timer();

            _presentationProbeTimer.Interval = 1000;

            _presentationProbeTimer.Tick +=
                async delegate
                {
                    if (_presentationProbeCount >= 30 ||
                        _presentationProbeAttemptCount >= 40)
                    {
                        _presentationProbeTimer.Stop();
                        _presentationProbeTimer.Dispose();
                        _presentationProbeTimer = null;
                        return;
                    }

                    if (_webView == null ||
                        !_webView.Visible ||
                        _webView.Width < 100 ||
                        _webView.Height < 100 ||
                        _webView.CoreWebView2 == null ||
                        _presentationProbeCaptureInFlight)
                    {
                        return;
                    }

                    _presentationProbeCaptureInFlight = true;
                    _presentationProbeAttemptCount++;

                    try
                    {
                        int candidate =
                            _presentationProbeCount + 1;

                        var receipt =
                            await AssistantWebViewDiagnostics.CaptureAsync(
                                _webView,
                                "presentation_" +
                                candidate.ToString("00"));

                        string captureResult =
                            receipt == null
                                ? null
                                : (string)receipt["capturePreview"];

                        if (string.Equals(
                            captureResult,
                            "PASS",
                            StringComparison.OrdinalIgnoreCase))
                        {
                            _presentationProbeCount++;
                        }
                    }
                    catch
                    {
                        // Diagnostic instrumentation must never alter host behavior.
                    }
                    finally
                    {
                        _presentationProbeCaptureInFlight = false;
                    }
                };

            _presentationProbeTimer.Start();
        }

        private void ConfigureSecurity()
        {
            if (_webView.CoreWebView2 == null) return;
            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _webView.CoreWebView2.Settings.AreHostObjectsAllowed = false;
            _webView.CoreWebView2.Settings.IsWebMessageEnabled = true;
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

            if (_webView == null || _webView.CoreWebView2 == null)
            {
                LastLoadError = "Assistant WebView core not ready.";
                return false;
            }

            try
            {
                string distRoot = Path.GetDirectoryName(distIndex);
                if (string.IsNullOrWhiteSpace(distRoot) || !Directory.Exists(distRoot))
                {
                    LastLoadError = "AssistantWeb dist root missing.";
                    return false;
                }

                distRoot = Path.GetFullPath(distRoot);

                _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    AssistantWebViewSecurity.ReactVirtualHostName,
                    distRoot,
                    CoreWebView2HostResourceAccessKind.DenyCors);

                _allowedDistRoot = distRoot;
                _webView.CoreWebView2.Navigate(
                    AssistantWebViewSecurity.ReactVirtualEntryUri.AbsoluteUri);
                LoadedReactShell = true;
                return true;
            }
            catch (Exception ex)
            {
                LastLoadError = "Virtual host mapping failed: " + ex.Message;
                LoadedReactShell = false;
                return false;
            }
        }

        private static string FindDistIndex()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var assemblyDir = Path.GetDirectoryName(typeof(AssistantWebViewHost).Assembly.Location);
            var candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(assemblyDir))
            {
                candidates.Add(Path.Combine(assemblyDir, "AssistantWeb", "dist", "index.html"));
            }
            candidates.Add(Path.Combine(baseDir, "AssistantWeb", "dist", "index.html"));
            candidates.Add(Path.Combine(baseDir, "..", "..", "AssistantWeb", "dist", "index.html"));
            candidates.Add(Path.Combine(baseDir, "..", "AssistantWeb", "dist", "index.html"));

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
                if (_presentationProbeTimer != null)
                {
                    _presentationProbeTimer.Stop();
                    _presentationProbeTimer.Dispose();
                    _presentationProbeTimer = null;
                }
                if (_bootstrapReadbackTimer != null)
                {
                    _bootstrapReadbackTimer.Stop();
                    _bootstrapReadbackTimer.Dispose();
                    _bootstrapReadbackTimer = null;
                }
            }
            catch { }
            try
            {
                _webView.CoreWebView2InitializationCompleted -= WebView_CoreWebView2InitializationCompleted;
            }
            catch { }
        }
    }
}
