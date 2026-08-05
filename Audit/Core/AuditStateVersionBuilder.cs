using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace BlueBrick.Audit.Core
{
    /// <summary>
    /// SHA-256 state version builder. Per BB-M001 packet section 11,
    /// produces a deterministic SHA-256 hex digest over canonical JSON
    /// (see <see cref="AuditCanonicalSerializer"/>) of a snapshot POCO.
    /// The same snapshot input MUST produce the same hash across
    /// repeated runs and across process restarts. Timestamps and
    /// caller-side capture metadata must NOT be present in the POCO
    /// that the caller passes here — the caller is responsible for
    /// excluding those fields from the snapshot bundle before hashing.
    /// </summary>
    public static class AuditStateVersionBuilder
    {
        /// <summary>Compute a SHA-256 hex state version over a canonicalized snapshot POCO.</summary>
        /// <param name="snapshot">A serializable POCO. Must contain only stable, ordered-by-canonical-serializer fields. Must not contain timestamps or full local paths.</param>
        /// <returns>Lowercase SHA-256 hex digest string; never null.</returns>
        public static string BuildStateVersion(object snapshot)
        {
            if (snapshot == null)
            {
                // Hash a canonical null marker so that "no snapshot" is itself a stable state.
                return HashBytes(AuditCanonicalSerializer.ToCanonicalBytes(null));
            }
            return HashBytes(AuditCanonicalSerializer.ToCanonicalBytes(snapshot));
        }

        /// <summary>
        /// Two-level hash that combines multiple snapshot fragments into one
        /// state version. Used when the caller hashes a document-scope and a
        /// configuration-scope snapshot separately and wants a single digest.
        /// </summary>
        public static string BuildCombinedStateVersion(params object[] snapshots)
        {
            // Canonicalize each fragment independently + concatenate the hex
            // strings + hash the concatenation. This ensures that per-fragment
            // canonicalization is identical to the standalone call, while the
            // combination itself is also deterministic and order-dependent.
            var sb = new StringBuilder();
            foreach (var s in snapshots ?? Array.Empty<object>())
            {
                sb.Append(BuildStateVersion(s));
            }
            return HashBytes(System.Text.Encoding.UTF8.GetBytes(sb.ToString()));
        }

        private static string HashBytes(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(bytes);
                var sb = new StringBuilder(64);
                for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }
    }
}
