using System;

namespace BlueBrick.Audit.Contracts
{
    /// <summary>
    /// Operation mode for a VIRA audit run.
    /// Per BB-M001 packet section 10, only MOCK and READ_ONLY_ANALYST are
    /// usable by this packet; PREVIEW_ONLY and HUMAN_APPROVED_MUTATION are
    /// reserved for later packets and are not constructed by Slice 1/2 code.
    /// </summary>
    [Serializable]
    public enum AuditOperationMode
    {
        /// <summary>Synthetic evidence; no SOLIDWORKS access; produces receipts only.</summary>
        MOCK = 0,

        /// <summary>Read-only property snapshot against a live SOLIDWORKS document; zero mutation.</summary>
        READ_ONLY_ANALYST = 1,

        /// <summary>Reserved for later packets. NOT constructed by Slice 1/2.</summary>
        PREVIEW_ONLY = 2,

        /// <summary>Reserved for later packets. NOT constructed by Slice 1/2.</summary>
        HUMAN_APPROVED_MUTATION = 3
    }
}
