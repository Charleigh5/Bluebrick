using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BlueBrick.Agent;

namespace BlueBrick.UI.Tests.Agent
{
    [TestClass]
    public class AssistantManifestTests
    {
        [TestMethod]
        public void Manifest_ValidSignedManifest_Accepted()
        {
            var manifest = new AssistantManifest
            {
                PluginId = "bluebrick.assistant",
                PluginVersion = "1.0.0",
                Provenance = "local-dev",
                Permissions = new[] { "read", "search" },
                SignerKeyId = "key-2026-07",
                Signature = "valid-signature-placeholder",
                PublicKeyFingerprint = "abc123def456",
                TrustState = ManifestTrustState.Trusted,
                CheckedAtUtc = DateTime.UtcNow
            };
            manifest.ArtifactHashes["canonicalPayload"] = manifest.ComputeCanonicalPayloadHash();

            var (valid, reason, state) = AssistantManifestVerifier.Verify(manifest);
            Assert.IsTrue(valid, "Valid signed manifest should be accepted. Reason: " + reason);
            Assert.AreEqual(ManifestTrustState.Trusted, state);
        }

        [TestMethod]
        public void Manifest_AlteredPayload_Rejected()
        {
            var manifest = new AssistantManifest
            {
                PluginId = "bluebrick.assistant",
                PluginVersion = "1.0.0",
                Provenance = "local-dev",
                Permissions = new[] { "read" },
                SignerKeyId = "key-2026-07",
                Signature = "valid-signature-placeholder",
                PublicKeyFingerprint = "abc123def456",
                TrustState = ManifestTrustState.Trusted,
                CheckedAtUtc = DateTime.UtcNow
            };
            manifest.ArtifactHashes["canonicalPayload"] = manifest.ComputeCanonicalPayloadHash();

            manifest.PluginVersion = "99.99.99";

            var (valid, reason, state) = AssistantManifestVerifier.Verify(manifest);
            Assert.IsFalse(valid, "Altered manifest should be rejected.");
            Assert.IsTrue(reason.Contains("hash mismatch") || reason.Contains("tampered"), "Reason should indicate tampering: " + reason);
        }

        [TestMethod]
        public void Manifest_Revoked_Rejected()
        {
            var manifest = new AssistantManifest
            {
                PluginId = "bluebrick.assistant",
                PluginVersion = "1.0.0",
                Provenance = "local-dev",
                Permissions = new[] { "read" },
                SignerKeyId = "key-2026-07",
                Signature = "valid-signature-placeholder",
                PublicKeyFingerprint = "abc123def456",
                TrustState = ManifestTrustState.Revoked,
                RevocationReason = "Key compromised",
                CheckedAtUtc = DateTime.UtcNow
            };
            manifest.ArtifactHashes["canonicalPayload"] = manifest.ComputeCanonicalPayloadHash();

            var (valid, reason, state) = AssistantManifestVerifier.Verify(manifest);
            Assert.IsFalse(valid, "Revoked manifest should be rejected.");
            Assert.AreEqual(ManifestTrustState.Revoked, state);
        }

        [TestMethod]
        public void Manifest_Unsigned_RejectedByHardenedPolicy()
        {
            var manifest = new AssistantManifest
            {
                PluginId = "bluebrick.assistant",
                PluginVersion = "1.0.0",
                Provenance = "local-dev",
                Permissions = new[] { "read" },
                SignerKeyId = string.Empty,
                Signature = string.Empty,
                PublicKeyFingerprint = string.Empty,
                TrustState = ManifestTrustState.Unknown,
                CheckedAtUtc = DateTime.UtcNow
            };

            var (valid, reason, state) = AssistantManifestVerifier.Verify(manifest);
            Assert.IsFalse(valid, "Unsigned manifest should be rejected by hardened policy.");
            Assert.IsTrue(reason.Contains("signature required") || reason.Contains("unsigned"), "Reason should mention unsigned rejection: " + reason);
        }

        [TestMethod]
        public void Manifest_FailClosedOnInvalid()
        {
            var manifest = new AssistantManifest
            {
                PluginId = string.Empty,
                PluginVersion = string.Empty,
                Signature = string.Empty,
                SignerKeyId = string.Empty,
                TrustState = ManifestTrustState.Unknown
            };

            Assert.ThrowsException<AssistantManifestVerificationException>(() =>
                AssistantManifestVerifier.FailClosedIfInvalid(manifest));
        }

        [TestMethod]
        public void Manifest_IsExpiredWhenCheckedAtIsDefault()
        {
            var manifest = new AssistantManifest
            {
                PluginId = "bluebrick.assistant",
                PluginVersion = "1.0.0",
                CheckedAtUtc = default
            };
            Assert.IsTrue(manifest.IsExpired(), "Manifest with default checked_at should be expired.");
        }

        [TestMethod]
        public void Manifest_IsExpiredWhenCheckedOver30DaysAgo()
        {
            var manifest = new AssistantManifest
            {
                PluginId = "bluebrick.assistant",
                PluginVersion = "1.0.0",
                CheckedAtUtc = DateTime.UtcNow.AddDays(-31)
            };
            Assert.IsTrue(manifest.IsExpired(), "Manifest checked over 30 days ago should be expired.");
        }

        [TestMethod]
        public void Manifest_IsNotExpiredWhenCheckedRecently()
        {
            var manifest = new AssistantManifest
            {
                PluginId = "bluebrick.assistant",
                PluginVersion = "1.0.0",
                CheckedAtUtc = DateTime.UtcNow
            };
            Assert.IsFalse(manifest.IsExpired(), "Manifest checked now should not be expired.");
        }
    }
}