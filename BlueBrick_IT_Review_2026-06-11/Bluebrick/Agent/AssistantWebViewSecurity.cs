using System;

namespace BlueBrick.Agent
{
    internal static class AssistantWebViewSecurity
    {
        internal static bool IsNavigationAllowed(string uri)
        {
            if (string.IsNullOrWhiteSpace(uri)) return true;

            Uri parsed;
            if (!Uri.TryCreate(uri, UriKind.Absolute, out parsed))
            {
                return false;
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
