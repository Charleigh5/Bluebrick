using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using BlueBrick.Audit.Core;

namespace BlueBrick.Agent
{
    public sealed class AssistantChainedReceipt
    {
        public string ReceiptId { get; set; } = Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture);
        public string PreviousReceiptHash { get; set; } = string.Empty;
        public string CanonicalPayloadHash { get; set; } = string.Empty;
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        public string ActorId { get; set; } = string.Empty;
        public string ActorName { get; set; } = string.Empty;
        public string ToolIdentity { get; set; } = string.Empty;
        public string ActionTier { get; set; } = "read";
        public string[] EvidenceIds { get; set; } = Array.Empty<string>();
        public string VerificationState { get; set; } = "pending";
        public string Status { get; set; } = "ok";
        public string Message { get; set; } = string.Empty;
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();

        public string ComputeCanonicalHash()
        {
            var payload = new
            {
                ReceiptId,
                PreviousReceiptHash,
                TimestampUtc = TimestampUtc.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                ActorId,
                ActorName,
                ToolIdentity,
                ActionTier,
                EvidenceIds = EvidenceIds.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                VerificationState,
                Status,
                Message,
                Metadata = Metadata.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToDictionary(kv => kv.Key, kv => kv.Value)
            };
            return AuditCanonicalSerializer.ToCanonicalJson(payload);
        }

        public void Seal()
        {
            // Flip VerificationState *before* hashing so the canonical hash covers
            // the final state; otherwise the first receipt (empty PreviousReceiptHash)
            // is hashed as "pending" but re-verified as "sealed" and the chain breaks.
            if (string.IsNullOrEmpty(PreviousReceiptHash) && VerificationState == "pending")
            {
                VerificationState = "sealed";
            }

            CanonicalPayloadHash = AssistantIntegrityScanner.ComputeSha256String(ComputeCanonicalHash());
        }
    }

    public sealed class AssistantChainedReceiptChain
    {
        private readonly List<AssistantChainedReceipt> _receipts = new List<AssistantChainedReceipt>();

        public IReadOnlyList<AssistantChainedReceipt> Receipts => _receipts;

        public AssistantChainedReceipt Head => _receipts.Count > 0 ? _receipts[_receipts.Count - 1] : null;

        public AssistantChainedReceipt Append(AssistantChainedReceipt receipt)
        {
            if (receipt == null) throw new ArgumentNullException(nameof(receipt));

            if (_receipts.Count > 0)
            {
                var previous = _receipts[_receipts.Count - 1];
                receipt.PreviousReceiptHash = previous.CanonicalPayloadHash;
            }

            receipt.Seal();
            _receipts.Add(receipt);
            return receipt;
        }

        public bool IsChainValid()
        {
            for (int i = 0; i < _receipts.Count; i++)
            {
                var receipt = _receipts[i];
                if (receipt.CanonicalPayloadHash != AssistantIntegrityScanner.ComputeSha256String(receipt.ComputeCanonicalHash()))
                {
                    return false;
                }
                if (i > 0 && !string.Equals(receipt.PreviousReceiptHash, _receipts[i - 1].CanonicalPayloadHash, StringComparison.Ordinal))
                {
                    return false;
                }
            }
            return true;
        }

        public AssistantChainedReceipt CreateCorrection(string originalReceiptId, string actorId, string actorName, string reason)
        {
            var original = _receipts.FirstOrDefault(r => r.ReceiptId == originalReceiptId);
            if (original == null) throw new ArgumentException("Original receipt not found: " + originalReceiptId, nameof(originalReceiptId));

            var correction = new AssistantChainedReceipt
            {
                ActorId = actorId,
                ActorName = actorName,
                ToolIdentity = original.ToolIdentity,
                ActionTier = original.ActionTier,
                EvidenceIds = original.EvidenceIds,
                Status = "corrected",
                Message = "Correction for " + originalReceiptId + ": " + (reason ?? "no reason provided"),
                VerificationState = "sealed"
            };
            correction.Metadata["corrects_receipt_id"] = originalReceiptId;
            correction.Metadata["correction_reason"] = reason ?? "unspecified";

            return Append(correction);
        }
    }
}
