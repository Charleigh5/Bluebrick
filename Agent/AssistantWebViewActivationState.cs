namespace BlueBrick.Agent
{
    /// <summary>
    /// Pure state model for the WebView React shell. Navigation dispatch is not
    /// activation: React becomes active only after both navigation and the
    /// deterministic bootstrap probe succeed.
    /// </summary>
    internal sealed class AssistantWebViewActivationState
    {
        internal bool LoadedReactShell { get; private set; }
        /// <summary>
        /// The control has rendered either a verified React shell or a
        /// fail-visible fallback. This is intentionally not equivalent to
        /// <see cref="LoadedReactShell"/>.
        /// </summary>
        internal bool WebViewUsable { get; private set; }
        internal bool FallbackRequired { get; private set; }
        internal string LastLoadError { get; private set; }

        internal bool BeginReactLoad(
            bool reactEnabled,
            bool hasIndex,
            bool hasCss,
            bool hasJavaScript)
        {
            LoadedReactShell = false;
            WebViewUsable = false;
            FallbackRequired = false;
            LastLoadError = null;

            if (!reactEnabled)
            {
                Fail("Assistant React WebView disabled by configuration.");
                return false;
            }
            if (!hasIndex)
            {
                Fail("AssistantWeb dist missing required asset: index.html.");
                return false;
            }
            if (!hasCss)
            {
                Fail("AssistantWeb dist missing required asset: assistant-index.css.");
                return false;
            }
            if (!hasJavaScript)
            {
                Fail("AssistantWeb dist missing required asset: assistant-web.js.");
                return false;
            }
            return true;
        }

        internal void RecordNavigationSuccess()
        {
            // Deliberately do not set LoadedReactShell here. A completed
            // navigation can still be an empty/malformed React shell.
        }

        internal void RecordNavigationFailure(string status)
        {
            Fail("Assistant React WebView navigation failed: " + (status ?? "Unknown") + ".");
        }

        internal void RecordNavigationTimeout()
        {
            Fail("Assistant React WebView navigation timed out.");
        }

        internal void RecordBootstrapFailure(string detail)
        {
            Fail("Assistant React WebView bootstrap failed: " + (detail ?? "unknown readiness failure"));
        }

        internal void RecordBootstrapSuccess()
        {
            LoadedReactShell = true;
            WebViewUsable = true;
            FallbackRequired = false;
            LastLoadError = null;
        }

        internal void RecordFallbackShown()
        {
            // Fallback dispatch is only a pending presentation state. It must
            // not claim that the WebView is usable until NavigationCompleted
            // reports success and RecordFallbackNavigationSuccess is called.
            LoadedReactShell = false;
            WebViewUsable = false;
            FallbackRequired = true;
        }

        internal void RecordFallbackNavigationSuccess()
        {
            // Keep the precise React failure reason observable while making
            // the successfully navigated legacy shell a usable outcome.
            LoadedReactShell = false;
            WebViewUsable = true;
            FallbackRequired = true;
        }

        internal void RecordFallbackNavigationFailure(string detail)
        {
            WebViewUsable = false;
            FallbackRequired = true;
            var fallbackFailure = "Assistant WebView fallback navigation failed: " +
                (detail ?? "unknown failure") + ".";
            LastLoadError = string.IsNullOrWhiteSpace(LastLoadError)
                ? fallbackFailure
                : LastLoadError + " " + fallbackFailure;
        }

        internal void RecordFallbackNavigationTimeout()
        {
            RecordFallbackNavigationFailure("timed out");
        }

        internal void RecordHostFailure(string detail)
        {
            if (FallbackRequired)
                return;

            Fail("Assistant React WebView host initialization failed: " + (detail ?? "unknown failure"));
        }

        internal void RecordObservedError(string detail)
        {
            LastLoadError = detail ?? string.Empty;
        }

        private void Fail(string reason)
        {
            LoadedReactShell = false;
            WebViewUsable = false;
            FallbackRequired = true;
            LastLoadError = reason;
        }
    }
}
