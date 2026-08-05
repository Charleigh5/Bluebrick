using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BlueBrick.Agent;

namespace BlueBrick.UI.Tests.Agent
{
    [TestClass]
    public class AssistantProviderExpiryTests
    {
        private static ProviderExpiryRecord ValidRecord(string providerId)
        {
            return new ProviderExpiryRecord
            {
                ProviderId = providerId,
                ProviderName = providerId + "-provider",
                OfficialSource = "https://example.com/" + providerId,
                IsFree = true,
                FreeStatus = ProviderFreeStatus.Free,
                PrivacyClass = ProviderPrivacyClass.Public,
                ValidationState = ProviderValidationState.Valid,
                CheckedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(30)
            };
        }

        private static ProviderExpiryRecord ExpiredRecord(string providerId)
        {
            return new ProviderExpiryRecord
            {
                ProviderId = providerId,
                ProviderName = providerId + "-provider",
                OfficialSource = "https://example.com/" + providerId,
                IsFree = false,
                FreeStatus = ProviderFreeStatus.Paid,
                PrivacyClass = ProviderPrivacyClass.Public,
                ValidationState = ProviderValidationState.Valid,
                CheckedAtUtc = DateTime.UtcNow.AddDays(-60),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(-1)
            };
        }

        private static ProviderExpiryRecord RevokedRecord(string providerId)
        {
            return new ProviderExpiryRecord
            {
                ProviderId = providerId,
                ProviderName = providerId + "-provider",
                OfficialSource = "https://example.com/" + providerId,
                IsFree = false,
                FreeStatus = ProviderFreeStatus.Paid,
                PrivacyClass = ProviderPrivacyClass.Public,
                ValidationState = ProviderValidationState.Revoked,
                CheckedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(30),
                ValidationEvidenceId = "REVOKED-001"
            };
        }

        private static ProviderExpiryRecord UnknownRecord(string providerId)
        {
            return new ProviderExpiryRecord
            {
                ProviderId = providerId,
                ProviderName = providerId + "-provider",
                OfficialSource = "https://example.com/" + providerId,
                IsFree = false,
                FreeStatus = ProviderFreeStatus.Unknown,
                PrivacyClass = ProviderPrivacyClass.Public,
                ValidationState = ProviderValidationState.Unknown,
                CheckedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(30)
            };
        }

        [TestMethod]
        public void ProviderExpiry_ValidRecord_IsEligible()
        {
            var checker = new ProviderExpiryChecker(new[] { ValidRecord("nvidia") });
            Assert.IsTrue(checker.IsProviderEligible("nvidia"), "Valid provider should be eligible.");
        }

        [TestMethod]
        public void ProviderExpiry_ExpiredRecord_IsNotEligible()
        {
            var checker = new ProviderExpiryChecker(new[] { ExpiredRecord("openai") });
            Assert.IsFalse(checker.IsProviderEligible("openai"), "Expired provider should not be eligible.");
        }

        [TestMethod]
        public void ProviderExpiry_RevokedRecord_IsNotEligible()
        {
            var checker = new ProviderExpiryChecker(new[] { RevokedRecord("evil-provider") });
            Assert.IsFalse(checker.IsProviderEligible("evil-provider"), "Revoked provider should not be eligible.");
        }

        [TestMethod]
        public void ProviderExpiry_UnknownRecord_IsNotEligible()
        {
            var checker = new ProviderExpiryChecker(new[] { UnknownRecord("unknown-provider") });
            Assert.IsFalse(checker.IsProviderEligible("unknown-provider"), "Unknown provider should not be eligible.");
        }

        [TestMethod]
        public void ProviderExpiry_MissingRecord_IsNotEligible()
        {
            var checker = new ProviderExpiryChecker(new[] { ValidRecord("nvidia") });
            Assert.IsFalse(checker.IsProviderEligible("nonexistent"), "Missing provider should not be eligible.");
        }

        [TestMethod]
        public void ProviderExpiry_ExpiredProviderExcludedWithNoPaidFallback()
        {
            var checker = new ProviderExpiryChecker(new[] { ExpiredRecord("paid-provider") });
            Assert.IsTrue(checker.IsProviderExpiredOrUnknown("paid-provider"), "Expired paid provider should be excluded.");
        }

        [TestMethod]
        public void ProviderExpiry_RevalidateAll_MarksExpiredAsStale()
        {
            var expired = ExpiredRecord("stale-provider");
            var checker = new ProviderExpiryChecker(new[] { expired });
            checker.RevalidateAll();
            Assert.AreEqual(ProviderValidationState.Stale, expired.ValidationState, "Expired provider should be marked stale after revalidation.");
        }

        [TestMethod]
        public void ProviderExpiry_GetExpiredRecords_ReturnsOnlyExpired()
        {
            var valid = ValidRecord("nvidia");
            var expired = ExpiredRecord("openai");
            var checker = new ProviderExpiryChecker(new[] { valid, expired });
            var expiredRecords = checker.GetExpiredRecords();
            Assert.AreEqual(1, expiredRecords.Count, "Should return exactly one expired record.");
            Assert.AreEqual("openai", expiredRecords[0].ProviderId);
        }

        [TestMethod]
        public void ProviderExpiry_FreeStatusFree_IsEligible()
        {
            var record = ValidRecord("free-provider");
            record.FreeStatus = ProviderFreeStatus.Free;
            record.IsFree = true;
            var checker = new ProviderExpiryChecker(new[] { record });
            Assert.IsTrue(checker.IsProviderEligible("free-provider"), "Free provider should be eligible.");
        }

        [TestMethod]
        public void ProviderExpiry_RestrictedPrivacy_IsNotEligible()
        {
            var record = ValidRecord("restricted-provider");
            record.PrivacyClass = ProviderPrivacyClass.Restricted;
            var checker = new ProviderExpiryChecker(new[] { record });
            Assert.IsFalse(checker.IsProviderEligible("restricted-provider"), "Restricted privacy provider should not be eligible.");
        }
    }
}