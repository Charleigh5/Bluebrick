using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace BlueBrick.Audit.Core
{
    /// <summary>
    /// Redacts sensitive material from audit artifacts before user-visible
    /// surface. Per BB-M001 packet section 12, at minimum redacts:
    /// <list type="bullet">
    /// <item>full local paths from user-visible artifacts;</item>
    /// <item>user profile folder names;</item>
    /// <item>API keys, bearer tokens, auth headers, connection strings;</item>
    /// <item><c>.env</c> contents;</item>
    /// <item>PDM credentials.</item>
    /// </list>
    /// Keeps a stable <see cref="PathHash"/> (SHA-256 of the canonicalized
    /// absolute path) and an optional <see cref="Basename"/> (filename only,
    /// no folder).
    /// </summary>
    public static class AuditRedactionService
    {
        private static readonly RegexOptions RxOpts =
            RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase;

        private static readonly Regex ApiKey = new Regex(@"(?<key>api[_-]?key)\s*[=:]\s*""?(?<val>[^\""\s]+)", RxOpts);
        private static readonly Regex Bearer  = new Regex(@"(?<key>bearer)\s+(?<val>[\w\-\.]+)", RxOpts);
        private static readonly Regex Authorization = new Regex(@"(?<key>authorization)\s*[:=]\s*(?<val>[^\s]+)", RxOpts);
        private static readonly Regex ConnStr = new Regex(@"(?<key>conn(ection)?[_-]?string)\s*[=:]\s*""?(?<val>[^\""\s]+)", RxOpts);
        private static readonly Regex Password = new Regex(@"(?<key>(password|pwd|secret))\s*[=:]\s*""?(?<val>[^\""\s]+)", RxOpts);
        private static readonly Regex EnvRef = new Regex(@"(?<key>\.env|env_(file|contents|raw))\s*[=:]\s*""?(?<val>[^\""\s]+)", RxOpts);
        private static readonly Regex PdmCred = new Regex(@"(?<key>(pdm[_-]?credential|pdm[_-]?password|pdm[_-]?token))\s*[=:]\s*""?(?<val>[^\""\s]+)", RxOpts);

        /// <summary>
        /// Redacts any of the secret patterns above in a user-visible string.
        /// Replaces the matched value with the literal marker <c>REDACTED</c>
        /// but preserves the key (for telemetry, not for user display).
        /// </summary>
        public static string RedactSecrets(string input)
        {
            if (string.IsNullOrEmpty(input)) return input ?? string.Empty;
            string s = input;
            s = ApiKey.Replace(s, "${key}=REDACTED");
            s = Bearer.Replace(s, "${key} REDACTED");
            s = Authorization.Replace(s, "${key}: REDACTED");
            s = ConnStr.Replace(s, "${key}=REDACTED");
            s = Password.Replace(s, "${key}=REDACTED");
            s = EnvRef.Replace(s, "${key}=REDACTED");
            s = PdmCred.Replace(s, "${key}=REDACTED");
            return s;
        }

        /// <summary>
        /// Converts an absolute local path to a stable hash + basename pair.
        /// The basename is safe to surface; the hash lets receipts deduplicate
        /// paths without revealing them.
        /// </summary>
        public static (string PathHash, string Basename) RedactPath(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath)) return (string.Empty, string.Empty);

            string basename;
            try
            {
                basename = System.IO.Path.GetFileName(absolutePath);
            }
            catch
            {
                basename = string.Empty;
            }

            string canonical;
            try
            {
                canonical = System.IO.Path.GetFullPath(absolutePath)
                    .TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            }
            catch
            {
                canonical = absolutePath;
            }

            // Strip a Windows user-profile prefix so the hash is stable across
            // machines/test environments even when the username differs.
            canonical = StripUserProfile(canonical);

            string hash;
            using (var sha = SHA256.Create())
            {
                var b = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToLowerInvariant()));
                var sb = new StringBuilder(64);
                for (int i = 0; i < b.Length; i++) sb.Append(b[i].ToString("x2", CultureInfo.InvariantCulture));
                hash = sb.ToString();
            }
            return (hash, basename ?? string.Empty);
        }

        /// <summary>
        /// Strips the leading user profile folder from a Windows-style local
        /// path so it never appears in audit output. e.g.
        /// <c>C:\Users\bob\Documents\part.sldprt</c> becomes
        /// <c>\Documents\part.sldprt</c> before hashing (hash stable across
        /// different usernames); returns the original string if it does not
        /// match the pattern. Designed to be conservative: never throws.
        /// </summary>
        public static string StripUserProfile(string path)
        {
            if (string.IsNullOrEmpty(path)) return path ?? string.Empty;
            // Match both forward and back slashes; case-insensitive; tolerate drive letters.
            var m = Regex.Match(path,
                @"^[a-zA-Z]:[\\/](Users|Documents and Settings)[\\/][^\\/]+",
                RxOpts);
            if (m.Success)
            {
                return path.Substring(m.Length);
            }
            // Also strip a leading UNC user-profile under C:\Users when there is no drive
            var m2 = Regex.Match(path, @"^[\\/](Users|Documents and Settings)[\\/][^\\/]+", RxOpts);
            if (m2.Success)
            {
                return path.Substring(m2.Length);
            }
            return path;
        }
    }
}
