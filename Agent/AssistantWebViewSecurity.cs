using System;

namespace BlueBrick.Agent
{
    internal static class AssistantWebViewSecurity
    {
        internal const string ReactVirtualHostName = "bluebrick-ui.invalid";

        internal static readonly Uri ReactVirtualEntryUri =
            new Uri(
                "https://" + ReactVirtualHostName + "/index.html",
                UriKind.Absolute);

        internal static bool IsNavigationAllowed(string uri)
        {
            if (string.IsNullOrWhiteSpace(uri)) return true;

            Uri parsed;
            if (!Uri.TryCreate(uri, UriKind.Absolute, out parsed))
            {
                return false;
            }

            if (string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(parsed.Host, ReactVirtualHostName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(parsed.Scheme, "about", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(parsed.AbsoluteUri, "about:blank", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(parsed.Scheme, "data", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(parsed.Scheme, "https", StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(parsed.Host, "chatgpt.com", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(parsed.Host, "chat.openai.com", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return false;
        }

        internal static bool IsPrivilegedDocumentUri(string uri)
        {
            if (string.IsNullOrWhiteSpace(uri)) return false;

            Uri parsed;
            if (!Uri.TryCreate(uri, UriKind.Absolute, out parsed)) return false;

            return string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(parsed.Host, ReactVirtualHostName, StringComparison.OrdinalIgnoreCase) &&
                   (string.Equals(parsed.AbsolutePath, "/index.html", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(parsed.AbsolutePath, "/", StringComparison.OrdinalIgnoreCase)) &&
                   string.IsNullOrEmpty(parsed.UserInfo);
        }

        internal static bool IsTrustedPrivilegedMessage(string sourceUri, string currentUri, string messageNonce, string expectedNonce)
        {
            if (string.IsNullOrWhiteSpace(messageNonce) ||
                string.IsNullOrWhiteSpace(expectedNonce) ||
                !string.Equals(messageNonce, expectedNonce, StringComparison.Ordinal))
            {
                return false;
            }

            if (!IsPrivilegedDocumentUri(sourceUri) || !IsPrivilegedDocumentUri(currentUri)) return false;

            Uri source;
            Uri current;
            if (!Uri.TryCreate(sourceUri, UriKind.Absolute, out source) ||
                !Uri.TryCreate(currentUri, UriKind.Absolute, out current)) return false;

            return string.Equals(source.AbsoluteUri, current.AbsoluteUri, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool ContainsSensitiveTokenText(string html)
        {
            if (string.IsNullOrEmpty(html)) return false;
            return html.IndexOf("X-Agent-Auth", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   html.IndexOf(".agent_token", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   html.IndexOf("OPENAI_API_KEY", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   html.IndexOf("NVIDIA_API_KEY", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   html.IndexOf("AssistantApiKey", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
