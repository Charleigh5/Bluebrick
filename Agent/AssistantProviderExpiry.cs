using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace BlueBrick.Agent
{
    public enum ProviderFreeStatus { Unknown = 0, Free = 1, Paid = 2 }
    public enum ProviderPrivacyClass { Public = 0, Internal = 1, Confidential = 2, Restricted = 3 }
    public enum ProviderValidationState { Unknown = 0, Valid = 1, Stale = 2, Missing = 3, Revoked = 4 }

    public sealed class ProviderExpiryRecord
    {
        public string ProviderId { get; set; } = string.Empty;
        public string ProviderName { get; set; } = string.Empty;
        public string OfficialSource { get; set; } = string.Empty;
        public bool IsFree { get; set; }
        public ProviderFreeStatus FreeStatus { get; set; } = ProviderFreeStatus.Unknown;
        public ProviderPrivacyClass PrivacyClass { get; set; } = ProviderPrivacyClass.Public;
        public ProviderValidationState ValidationState { get; set; } = ProviderValidationState.Unknown;
        public DateTime CheckedAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public string ValidationEvidenceId { get; set; } = string.Empty;

        public bool IsExpired()
        {
            return ExpiresAtUtc == default || DateTime.UtcNow >= ExpiresAtUtc;
        }

        public bool IsEligible()
        {
            return ValidationState == ProviderValidationState.Valid
                && !IsExpired()
                && PrivacyClass != ProviderPrivacyClass.Restricted;
        }
    }

    public sealed class ProviderExpiryChecker
    {
        private readonly Dictionary<string, ProviderExpiryRecord> _records;

        public ProviderExpiryChecker(IEnumerable<ProviderExpiryRecord> records)
        {
            _records = (records ?? Enumerable.Empty<ProviderExpiryRecord>())
                .Where(r => r != null && !string.IsNullOrEmpty(r.ProviderId))
                .ToDictionary(r => r.ProviderId, StringComparer.OrdinalIgnoreCase);
        }

        public ProviderExpiryChecker() : this(Array.Empty<ProviderExpiryRecord>()) { }

        public ProviderExpiryRecord GetRecord(string providerId)
        {
            if (string.IsNullOrEmpty(providerId)) return null;
            _records.TryGetValue(providerId, out var record);
            return record;
        }

        public bool IsProviderEligible(string providerId)
        {
            var record = GetRecord(providerId);
            return record != null && record.IsEligible();
        }

        public bool IsProviderExpiredOrUnknown(string providerId)
        {
            var record = GetRecord(providerId);
            return record == null || record.IsExpired() || record.ValidationState != ProviderValidationState.Valid;
        }

        public IReadOnlyList<ProviderExpiryRecord> GetExpiredRecords()
        {
            return _records.Values.Where(r => r.IsExpired()).ToList();
        }

        public IReadOnlyList<ProviderExpiryRecord> GetUnknownRecords()
        {
            return _records.Values.Where(r => r.ValidationState == ProviderValidationState.Unknown).ToList();
        }

        public void RevalidateAll()
        {
            foreach (var record in _records.Values)
            {
                if (record.IsExpired())
                {
                    record.ValidationState = ProviderValidationState.Stale;
                }
            }
        }
    }
}
