using System;
using System.Text;

namespace BlueBrick.Agent
{
    /// <summary>
    /// A bounded native receipt for a React-shell failure. Once active it is
    /// intentionally independent from ordinary provider/session status.
    /// </summary>
    internal sealed class AssistantFallbackDiagnostic
    {
        internal const int MaximumDisplayLength = 280;
        private const string Prefix = "React fallback active: ";

        internal bool IsActive { get; private set; }
        internal string DisplayText { get; private set; }

        internal void Activate(string reason)
        {
            IsActive = true;
            DisplayText = Prefix + Sanitize(reason, MaximumDisplayLength - Prefix.Length);
        }

        internal bool TryRestore(string currentText, bool currentVisible, out string text, out bool visible)
        {
            text = DisplayText;
            visible = IsActive;
            return IsActive && (!string.Equals(currentText, DisplayText, StringComparison.Ordinal) || !currentVisible);
        }

        private static string Sanitize(string value, int maximumLength)
        {
            if (string.IsNullOrWhiteSpace(value)) return "React shell was unavailable.";

            var builder = new StringBuilder();
            bool previousWhitespace = false;
            foreach (var character in value)
            {
                if (char.IsControl(character))
                {
                    if (!previousWhitespace)
                    {
                        builder.Append(' ');
                        previousWhitespace = true;
                    }
                    continue;
                }

                builder.Append(character);
                previousWhitespace = char.IsWhiteSpace(character);
                if (builder.Length >= maximumLength) break;
            }

            var sanitized = builder.ToString().Trim();
            return string.IsNullOrWhiteSpace(sanitized) ? "React shell was unavailable." : sanitized;
        }
    }
}
