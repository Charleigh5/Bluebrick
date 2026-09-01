using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace BlueBrick.Agent
{
    internal sealed class AssistantWebViewHost : IDisposable
    {
        private readonly WebView2 _webView;
        private readonly AgentConfig _config;
        private readonly AssistantWebViewActivationState _activationState = new AssistantWebViewActivationState();
        private bool _disposed;
        private string _allowedDistRoot;
        private System.Windows.Forms.Timer _presentationProbeTimer;
        private int _presentationProbeCount;
        private int _presentationProbeAttemptCount;
        private bool _presentationProbeCaptureInFlight;
        private bool _diagnosticsProcessFailedHooked;
        private readonly string _documentNonce = Guid.NewGuid().ToString("N");
        private bool _trustedDocument;
        private System.Windows.Forms.Timer _bootstrapReadbackTimer;
        private int _bootstrapReadbackCount;
        private bool _bootstrapReadbackInFlight;

        internal AssistantWebViewHost(WebView2 webView, AgentConfig config)
        {
            _webView = webView ?? throw new ArgumentNullException(nameof(webView));
            _config = config ?? new AgentConfig();
        }

        internal string LastLoadError => _activationState.LastLoadError;
        internal bool LoadedReactShell => _activationState.LoadedReactShell;
        internal bool WebViewUsable => _activationState.WebViewUsable;

        internal bool IsTrustedDocumentMessage(string sourceUri, string currentUri, string messageNonce)
        {
            return !_disposed &&
                   _trustedDocument &&
                   AssistantWebViewSecurity.IsTrustedPrivilegedMessage(
                       sourceUri,
                       currentUri,
                       messageNonce,
                       _documentNonce);
        }

        internal async Task<bool> InitializeAsync(Func<string> fallbackShellHtml)
        {
            var fallbackHtml = fallbackShellHtml == null
                ? MinimalFallbackHtml()
                : fallbackShellHtml();
            try
            {
                Directory.CreateDirectory(AppIdentity.WebViewUserDataRoot);
                var env = await CoreWebView2Environment.CreateAsync(userDataFolder: AppIdentity.WebViewUserDataRoot).ConfigureAwait(true);
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
                await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
                    BuildDocumentBindingScript() + Environment.NewLine + AssistantWebViewDiagnostics.BootstrapTelemetryScript).ConfigureAwait(true);

                var navigationReady = new TaskCompletionSource<CoreWebView2NavigationCompletedEventArgs>();
                EventHandler<CoreWebView2NavigationCompletedEventArgs> completed = null;
                completed = (s, e) =>
                {
                    _webView.CoreWebView2.NavigationCompleted -= completed;
                    navigationReady.TrySetResult(e);
                };
                _webView.CoreWebView2.NavigationCompleted += completed;

                if (!StartReactNavigation())
                {
                    _webView.CoreWebView2.NavigationCompleted -= completed;
                    return await NavigateFallbackAsync(fallbackHtml).ConfigureAwait(true);
                }

                var finished = await Task.WhenAny(navigationReady.Task, Task.Delay(TimeSpan.FromSeconds(10))).ConfigureAwait(true);
                if (finished != navigationReady.Task)
                {
                    _webView.CoreWebView2.NavigationCompleted -= completed;
                    _activationState.RecordNavigationTimeout();
                    return await NavigateFallbackAsync(fallbackHtml).ConfigureAwait(true);
                }

                var navigation = await navigationReady.Task.ConfigureAwait(true);
                if (!navigation.IsSuccess)
                {
                    _activationState.RecordNavigationFailure(navigation.WebErrorStatus.ToString());
                    return await NavigateFallbackAsync(fallbackHtml).ConfigureAwait(true);
                }

                _activationState.RecordNavigationSuccess();
                _trustedDocument = AssistantWebViewSecurity.IsPrivilegedDocumentUri(_webView.Source == null ? null : _webView.Source.AbsoluteUri);
                var bootstrapFailure = await WaitForReactBootstrapAsync().ConfigureAwait(true);
                if (!string.IsNullOrWhiteSpace(bootstrapFailure))
                {
                    _activationState.RecordBootstrapFailure(bootstrapFailure);
                    return await NavigateFallbackAsync(fallbackHtml).ConfigureAwait(true);
                }

                _activationState.RecordBootstrapSuccess();
                StartBootstrapReadbackTimer();
                return true;
            }
            catch (Exception ex)
            {
                _activationState.RecordHostFailure(ex.Message);
                return await NavigateFallbackAsync(fallbackHtml).ConfigureAwait(true);
            }
        }

        private void WebView_CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (!e.IsSuccess)
            {
                _activationState.RecordHostFailure(e.InitializationException?.Message ?? "WebView2 initialization failed.");
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
                _trustedDocument = false;
                if (!IsNavigationAllowed(e.Uri))
                {
                    e.Cancel = true;
                    _activationState.RecordObservedError("Blocked WebView navigation: " + e.Uri);
                }
            };
            _webView.CoreWebView2.NewWindowRequested += (s, e) => { e.Handled = true; };
        }

        private bool StartReactNavigation()
        {
            var distIndex = FindDistIndex();
            var distRoot = string.IsNullOrWhiteSpace(distIndex)
                ? null
                : Path.GetDirectoryName(distIndex);
            var hasIndex = !string.IsNullOrWhiteSpace(distIndex) && File.Exists(distIndex);
            var hasCss = !string.IsNullOrWhiteSpace(distRoot) &&
                File.Exists(Path.Combine(distRoot, "assistant-index.css"));
            var hasJavaScript = !string.IsNullOrWhiteSpace(distRoot) &&
                File.Exists(Path.Combine(distRoot, "assistant-web.js"));

            if (!_activationState.BeginReactLoad(
                    _config.Assistant?.UseReactWebView ?? false,
                    hasIndex,
                    hasCss,
                    hasJavaScript))
            {
                return false;
            }

            if (_webView == null || _webView.CoreWebView2 == null)
            {
                _activationState.RecordBootstrapFailure("WebView core was not ready.");
                return false;
            }

            try
            {
                if (string.IsNullOrWhiteSpace(distRoot) || !Directory.Exists(distRoot))
                {
                    _activationState.RecordBootstrapFailure("AssistantWeb dist root was missing.");
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
                return true;
            }
            catch (Exception ex)
            {
                _activationState.RecordBootstrapFailure("Virtual host mapping failed: " + ex.Message);
                return false;
            }
        }

        private async Task<string> WaitForReactBootstrapAsync()
        {
            string lastFailure = "bootstrap readiness did not complete.";
            for (int attempt = 0; attempt < 40; attempt++)
            {
                try
                {
                    var receipt = await AssistantWebViewDiagnostics.CaptureBootstrapAsync(
                        _webView,
                        "activation_" + (attempt + 1).ToString("00")).ConfigureAwait(true);
                    if (AssistantWebViewDiagnostics.IsReactBootstrapReady(receipt, out var failureReason))
                        return null;

                    lastFailure = failureReason ?? lastFailure;
                }
                catch (Exception ex)
                {
                    lastFailure = "bootstrap probe threw " + ex.GetType().Name + ".";
                }

                await Task.Delay(250).ConfigureAwait(true);
            }

            return lastFailure;
        }

        private async Task<bool> NavigateFallbackAsync(string fallbackHtml)
        {
            if (_webView == null || _webView.CoreWebView2 == null)
            {
                _activationState.RecordFallbackNavigationFailure("WebView core was not ready");
                return false;
            }

            var coordinator = new AssistantWebViewFallbackNavigationCoordinator();
            EventHandler<CoreWebView2NavigationCompletedEventArgs> completed = null;
            completed = (sender, args) =>
            {
                _webView.CoreWebView2.NavigationCompleted -= completed;
                coordinator.RecordCompleted(
                    args.IsSuccess,
                    args.IsSuccess ? "Success" : args.WebErrorStatus.ToString());
            };

            try
            {
                _trustedDocument = false;
                _webView.CoreWebView2.NavigationCompleted += completed;
                _activationState.RecordFallbackShown();
                _webView.NavigateToString(string.IsNullOrWhiteSpace(fallbackHtml)
                    ? MinimalFallbackHtml()
                    : fallbackHtml);

                var completedTask = await Task.WhenAny(
                    coordinator.Completion,
                    Task.Delay(TimeSpan.FromSeconds(10))).ConfigureAwait(true);
                if (completedTask != coordinator.Completion)
                {
                    _webView.CoreWebView2.NavigationCompleted -= completed;
                    coordinator.RecordTimeout();
                }

                var outcome = await coordinator.Completion.ConfigureAwait(true);
                if (outcome == AssistantWebViewFallbackNavigationOutcome.Success)
                {
                    _activationState.RecordFallbackNavigationSuccess();
                    return true;
                }

                if (outcome == AssistantWebViewFallbackNavigationOutcome.Timeout)
                    _activationState.RecordFallbackNavigationTimeout();
                else
                    _activationState.RecordFallbackNavigationFailure("WebView2 reported navigation failure");
                return false;
            }
            catch (Exception ex)
            {
                try { _webView.CoreWebView2.NavigationCompleted -= completed; } catch { }
                _activationState.RecordFallbackNavigationFailure(ex.Message);
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

        private string BuildDocumentBindingScript()
        {
            var nonce = JsonConvert.SerializeObject(_documentNonce);
            return "(() => { try { Object.defineProperty(window, '__blueBrickDocumentNonce', { value: " + nonce + ", writable: false, configurable: false, enumerable: false }); } catch (_) {} })();";
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _trustedDocument = false;
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
