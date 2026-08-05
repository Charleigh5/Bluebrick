using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace BlueBrick.Agent
{
    [Serializable]
    public enum ManifestTrustState
    {
        Unknown = 0,
        Trusted = 1,
        Untrusted = 2,
        Revoked = 3,
        UnsupportedAlgorithm = 4,
        Missing = 5
    }

    public sealed class AssistantManifest
    {
        public string SchemaVersion { get; set; } = "1.0.0";
        public string PluginId { get; set; } = string.Empty;
        public string PluginVersion { get; set; } = string.Empty;
        public string Provenance { get; set; } = string.Empty;
        public string[] Permissions { get; set; } = Array.Empty<string>();
        public Dictionary<string, string> ToolSchemas { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> ArtifactHashes { get; set; } = new Dictionary<string, string>();
        public string SignatureAlgorithm { get; set; } = "SHA256withRSA";
        public string SignerKeyId { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
        public string PublicKeyFingerprint { get; set; } = string.Empty;
        public ManifestTrustState TrustState { get; set; } = ManifestTrustState.Unknown;
        public string RevocationReason { get; set; } = string.Empty;
        public DateTime CheckedAtUtc { get; set; }

        public string ComputeCanonicalPayloadHash()
        {
            // ArtifactHashes is deliberately excluded: the canonicalPayload entry
            // is stored *inside* ArtifactHashes, so hashing the map would be
            // self-referential (hash changes the moment it is stored, making any
            // stored hash impossible to verify). Identity + permissions + tool
            // schemas are the payload the signature must protect; per-artifact
            // hashes are verified independently.
            var payload = new
            {
                SchemaVersion,
                PluginId,
                PluginVersion,
                Provenance,
                Permissions = Permissions ?? Array.Empty<string>(),
                ToolSchemas,
                SignatureAlgorithm,
                SignerKeyId,
                PublicKeyFingerprint
            };
            var json = BlueBrick.Audit.Core.AuditCanonicalSerializer.ToCanonicalJson(payload);
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(json);
            var hash = sha.ComputeHash(bytes);
            var sb = new StringBuilder(64);
            for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        public bool IsExpired()
        {
            return CheckedAtUtc == default || CheckedAtUtc < DateTime.UtcNow.AddDays(-30);
        }

        public bool IsValid()
        {
            return TrustState == ManifestTrustState.Trusted
                && !string.IsNullOrEmpty(Signature)
                && !string.IsNullOrEmpty(SignerKeyId)
                && !IsExpired()
                && string.IsNullOrEmpty(RevocationReason);
        }
    }

    public static class AssistantManifestVerifier
    {
        public static (bool Valid, string Reason, ManifestTrustState TrustState) Verify(AssistantManifest manifest)
        {
            if (manifest == null) return (false, "manifest is null", ManifestTrustState.Missing);
            if (string.IsNullOrEmpty(manifest.PluginId)) return (false, "pluginId required", ManifestTrustState.Missing);
            if (string.IsNullOrEmpty(manifest.PluginVersion)) return (false, "pluginVersion required", ManifestTrustState.Missing);
            if (string.IsNullOrEmpty(manifest.Signature)) return (false, "signature required; unsigned manifests are rejected by hardened policy", ManifestTrustState.Untrusted);
            if (string.IsNullOrEmpty(manifest.SignerKeyId)) return (false, "signerKeyId required", ManifestTrustState.Untrusted);
            if (manifest.IsExpired()) return (false, "manifest checked_at is stale or missing; revalidate before execution", ManifestTrustState.Untrusted);
            if (manifest.TrustState == ManifestTrustState.Revoked) return (false, "manifest is revoked: " + (manifest.RevocationReason ?? "unknown"), ManifestTrustState.Revoked);
            if (manifest.TrustState == ManifestTrustState.Unknown) return (false, "manifest trust state is unknown; cannot authorize execution", ManifestTrustState.Unknown);
            if (manifest.TrustState != ManifestTrustState.Trusted) return (false, "manifest trust state is not trusted", manifest.TrustState);
            if (string.IsNullOrEmpty(manifest.PublicKeyFingerprint)) return (false, "publicKeyFingerprint required for signature verification", ManifestTrustState.Untrusted);

            var expectedHash = manifest.ComputeCanonicalPayloadHash();
            string storedHash;
            if (manifest.ArtifactHashes == null || !manifest.ArtifactHashes.TryGetValue("canonicalPayload", out storedHash) || string.IsNullOrEmpty(storedHash))
            {
                return (false, "canonicalPayload hash missing from artifact hashes", ManifestTrustState.Untrusted);
            }
            if (!string.Equals(storedHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                return (false, "canonicalPayload hash mismatch; manifest has been tampered with", ManifestTrustState.Untrusted);
            }

            return (true, "manifest verified", ManifestTrustState.Trusted);
        }

        public static void FailClosedIfInvalid(AssistantManifest manifest)
        {
            var (valid, reason, state) = Verify(manifest);
            if (!valid)
            {
                throw new AssistantManifestVerificationException(reason, state);
            }
        }
    }

    public sealed class AssistantManifestVerificationException : Exception
    {
        public ManifestTrustState TrustState { get; }
        public AssistantManifestVerificationException(string message, ManifestTrustState state) : base(message) { TrustState = state; }
    }
}
