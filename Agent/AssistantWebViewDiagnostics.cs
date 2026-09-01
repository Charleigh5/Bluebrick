using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json.Linq;

namespace BlueBrick.Agent
{
    internal static class AssistantWebViewDiagnostics
    {
        internal const string BootstrapTelemetryScript = @"
(() => {
    const safeString = value => {
        try {
            const s =
                value && value.message
                    ? String(value.message)
                    : String(value == null ? '' : value);

            return s.length > 500
                ? s.slice(0, 500)
                : s;
        } catch {
            return '<unavailable>';
        }
    };

    const safeUrl = value => {
        try {
            const u = new URL(String(value || ''), document.baseURI);
            return u.protocol + '//' + u.host + u.pathname;
        } catch {
            return String(value || '').slice(0, 500);
        }
    };

    const state = {
        installedAt: Date.now(),
        errors: [],
        resourceErrors: [],
        unhandledRejections: []
    };

    Object.defineProperty(window, '__bbBootTelemetry', {
        value: state,
        configurable: false,
        enumerable: false,
        writable: false
    });

    window.addEventListener(
        'error',
        event => {
            const target = event && event.target;

            if (target &&
                target !== window &&
                (target.tagName === 'SCRIPT' ||
                 target.tagName === 'LINK')) {

                state.resourceErrors.push({
                    tag: String(target.tagName || ''),
                    src: safeUrl(target.src || target.href || ''),
                    type: String(target.type || ''),
                    crossOrigin: String(target.crossOrigin || ''),
                    timestamp: Date.now()
                });

                return;
            }

            state.errors.push({
                message: safeString(event && event.message),
                filename: safeUrl(event && event.filename),
                lineno: Number(event && event.lineno || 0),
                colno: Number(event && event.colno || 0),
                timestamp: Date.now()
            });
        },
        true
    );

    window.addEventListener(
        'unhandledrejection',
        event => {
            state.unhandledRejections.push({
                reason: safeString(event && event.reason),
                timestamp: Date.now()
            });
        },
        true
    );
})();
";

        private const string DomProbeScript = @"
(() => {
    const body = document.body;
    const root = document.getElementById('root');
    const text = body ? (body.innerText || '') : '';
    const rect = root ? root.getBoundingClientRect() : null;

    return {
        readyState: document.readyState,
        visibilityState: document.visibilityState,
        hasFocus: document.hasFocus(),
        titleLength: (document.title || '').length,
        bodyChildCount: body ? body.children.length : 0,
        bodyTextLength: text.length,
        rootPresent: !!root,
        rootChildCount: root ? root.children.length : 0,
        rootRect: rect ? {
            x: rect.x,
            y: rect.y,
            width: rect.width,
            height: rect.height
        } : null,
        blueBrickHeaderPresent: text.indexOf('BlueBrick Assistant') >= 0,
        viewport: {
            width: window.innerWidth,
            height: window.innerHeight,
            devicePixelRatio: window.devicePixelRatio
        }
    };
})()";

        internal static string OutputRoot
        {
            get
            {
                return Path.Combine(
                    Path.GetTempPath(),
                    "BlueBrick",
                    "WebViewDiagnostics");
            }
        }

        private const string BootstrapReadbackScript = @"
(() => {
    let t = null;
    try { t = window.__bbBootTelemetry || null; } catch { t = null; }

    const root = document.getElementById('root');
    const text = document.body ? (document.body.innerText || '') : '';

    return {
        readyState: document.readyState,
        visibilityState: document.visibilityState,
        titleLength: (document.title || '').length,
        bodyTextLength: text.length,
        rootChildCount: root ? root.children.length : -1,
        blueBrickHeaderPresent: text.indexOf('BlueBrick Assistant') >= 0,
        documentUrl: String(document.location || ''),
        bbCallbacks: [
            'bbReset','bbAppend','bbTypingStart','bbAppendChunk','bbTypingStop',
            'bbSetModel','bbSetModels','bbSetScope','bbSetScopes','bbSetStatus',
            'bbSetTools','bbSetToolReceipts','bbSetProductCatalogs',
            'bbAppendToolResult','bbAppendScreenshotArtifact',
            'bbUpdateScreenshotArtifact','bbGetTranscript'
        ].filter(name => typeof window[name] === 'function'),
        transcript: (typeof window.bbGetTranscript === 'function')
            ? window.bbGetTranscript()
            : null,
        scriptTags: Array.from(document.querySelectorAll('script')).map(s => ({
            src: s.src || '(inline)',
            type: s.type || '',
            crossOrigin: s.crossOrigin || ''
        })),
        telemetry: t
    };
})()";

        internal static async Task<JObject> CaptureBootstrapAsync(
            WebView2 webView,
            string reason)
        {
            if (webView == null)
                throw new ArgumentNullException("webView");

            if (webView.InvokeRequired)
                throw new InvalidOperationException(
                    "Bootstrap diagnostics must execute on the WebView UI thread.");

            Directory.CreateDirectory(OutputRoot);

            string baseName =
                DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") +
                "_bootstrap_" +
                Sanitize(reason);

            string jsonPath = Path.Combine(OutputRoot, baseName + ".json");

            var receipt = new JObject
            {
                ["capturedUtc"] = DateTime.UtcNow.ToString("o"),
                ["reason"] = reason ?? string.Empty,
                ["coreReady"] = webView.CoreWebView2 != null
            };

            try
            {
                string raw =
                    await webView.CoreWebView2.ExecuteScriptAsync(
                        BootstrapReadbackScript);

                JToken parsed;

                try
                {
                    parsed = JToken.Parse(
                        string.IsNullOrWhiteSpace(raw) ? "null" : raw);
                }
                catch
                {
                    parsed = JValue.CreateString("<READBACK_PARSE_FAILED>");
                }

                receipt["readback"] = parsed;
            }
            catch (Exception ex)
            {
                receipt["readback"] = "FAIL";
                receipt["readbackErrorType"] = ex.GetType().FullName;
                receipt["readbackError"] = SafeMessage(ex.Message);
            }

            receipt["result"] = "BOOTSTRAP_RECEIPT_COMPLETE";

            File.WriteAllText(
                jsonPath,
                receipt.ToString(),
                Encoding.UTF8);

            return receipt;
        }

        internal static bool IsReactBootstrapReady(
            JObject receipt,
            out string failureReason)
        {
            failureReason = null;
            var readback = receipt == null ? null : receipt["readback"] as JObject;
            if (readback == null)
            {
                failureReason = "bootstrap readback was unavailable.";
                return false;
            }

            if ((readback.Value<int?>("rootChildCount") ?? 0) < 1)
            {
                failureReason = "React root did not mount content.";
                return false;
            }

            if (readback.Value<bool?>("blueBrickHeaderPresent") != true)
            {
                failureReason = "BlueBrick React shell header was not present.";
                return false;
            }

            var callbacks = readback["bbCallbacks"] as JArray;
            if (callbacks == null || callbacks.Count != 17)
            {
                failureReason = "17 callback bridge was not mounted.";
                return false;
            }

            var telemetry = readback["telemetry"] as JObject;
            if (telemetry != null &&
                ((telemetry["errors"] as JArray)?.Count > 0 ||
                 (telemetry["resourceErrors"] as JArray)?.Count > 0 ||
                 (telemetry["unhandledRejections"] as JArray)?.Count > 0))
            {
                failureReason = "React bootstrap telemetry reported a script or resource failure.";
                return false;
            }

            return true;
        }

        internal static async Task<JObject> CaptureAsync(
            WebView2 webView,
            string reason)
        {
            if (webView == null)
                throw new ArgumentNullException("webView");

            if (webView.InvokeRequired)
                throw new InvalidOperationException(
                    "WebView diagnostics must execute on the WebView UI thread.");

            Directory.CreateDirectory(OutputRoot);

            string timestamp =
                DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");

            string safeReason = Sanitize(reason);

            string baseName =
                timestamp + "_" + safeReason;

            string pngPath =
                Path.Combine(OutputRoot, baseName + ".png");

            string jsonPath =
                Path.Combine(OutputRoot, baseName + ".json");

            var receipt = new JObject
            {
                ["capturedUtc"] = DateTime.UtcNow.ToString("o"),
                ["reason"] = reason ?? string.Empty,
                ["webViewVisible"] = webView.Visible,
                ["webViewEnabled"] = webView.Enabled,
                ["webViewWidth"] = webView.ClientSize.Width,
                ["webViewHeight"] = webView.ClientSize.Height,
                ["coreReady"] = webView.CoreWebView2 != null
            };

            var core = webView.CoreWebView2;

            if (core == null)
            {
                receipt["result"] = "CORE_NOT_READY";
                File.WriteAllText(
                    jsonPath,
                    receipt.ToString(),
                    Encoding.UTF8);

                return receipt;
            }

            try
            {
                using (var stream = new FileStream(
                    pngPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.Read))
                {
                    await core.CapturePreviewAsync(
                        CoreWebView2CapturePreviewImageFormat.Png,
                        stream);
                }

                receipt["capturePreview"] = "PASS";
                receipt["pngPath"] = pngPath;
            }
            catch (Exception ex)
            {
                receipt["capturePreview"] = "FAIL";
                receipt["captureErrorType"] =
                    ex.GetType().FullName;
                receipt["captureError"] =
                    SafeMessage(ex.Message);
            }

            try
            {
                string raw =
                    await core.ExecuteScriptAsync(DomProbeScript);

                JToken dom;

                try
                {
                    dom = JToken.Parse(
                        string.IsNullOrWhiteSpace(raw)
                            ? "null"
                            : raw);
                }
                catch
                {
                    dom = JValue.CreateString(
                        "<DOM_PROBE_PARSE_FAILED>");
                }

                receipt["dom"] = dom;
            }
            catch (Exception ex)
            {
                receipt["domProbe"] = "FAIL";
                receipt["domProbeErrorType"] =
                    ex.GetType().FullName;
                receipt["domProbeError"] =
                    SafeMessage(ex.Message);
            }

            receipt["result"] = "CAPTURE_COMPLETE";

            File.WriteAllText(
                jsonPath,
                receipt.ToString(),
                Encoding.UTF8);

            return receipt;
        }

        internal static void RecordProcessFailed(
            CoreWebView2ProcessFailedEventArgs args)
        {            try
            {
                Directory.CreateDirectory(OutputRoot);

                var receipt = new JObject
                {
                    ["capturedUtc"] = DateTime.UtcNow.ToString("o"),
                    ["event"] = "ProcessFailed",
                    ["processFailedKind"] =
                        args.ProcessFailedKind.ToString(),
                    ["reason"] =
                        args.Reason.ToString(),
                    ["exitCode"] =
                        args.ExitCode,
                    ["processDescription"] =
                        args.ProcessDescription ?? string.Empty
                };

                string path = Path.Combine(
                    OutputRoot,
                    DateTime.UtcNow.ToString(
                        "yyyyMMdd_HHmmss_fff") +
                    "_process_failed.json");

                File.WriteAllText(
                    path,
                    receipt.ToString(),
                    Encoding.UTF8);
            }
            catch
            {
                // Diagnostic logging must never destabilize the host.
            }
        }

        internal static void RecordResourceResponse(
            string uri,
            int statusCode,
            string method)
        {
            try
            {
                Directory.CreateDirectory(OutputRoot);

                var receipt = new JObject
                {
                    ["capturedUtc"] = DateTime.UtcNow.ToString("o"),
                    ["uri"] = uri ?? string.Empty,
                    ["statusCode"] = statusCode,
                    ["method"] = method ?? string.Empty
                };

                string line = receipt.ToString(
                    Newtonsoft.Json.Formatting.None);

                File.AppendAllText(
                    Path.Combine(OutputRoot, "webresource_log.jsonl"),
                    line + Environment.NewLine,
                    Encoding.UTF8);
            }
            catch
            {
                // Diagnostic logging must never destabilize the host.
            }
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "probe";

            var builder = new StringBuilder();

            foreach (char c in value)
            {
                if (char.IsLetterOrDigit(c) ||
                    c == '-' ||
                    c == '_')
                {
                    builder.Append(c);
                }
            }

            return builder.Length == 0
                ? "probe"
                : builder.ToString();
        }

        private static string SafeMessage(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string result = value;

            if (result.Length > 500)
                result = result.Substring(0, 500);

            return result;
        }
    }
}
