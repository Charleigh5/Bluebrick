using System;

namespace BlueBrick.SolidWorks.Runtime
{
    /// <summary>
    /// SOLIDWORKS version classification. Per BB-M001 packet §16, the
    /// classification forces read-only limited status when the version
    /// cannot be proven; never claims a service pack above what
    /// <c>ISldWorks.RevisionNumber()</c> actually proves.
    /// </summary>
    [Serializable]
    public enum SolidWorksRuntimeClassification
    {
        /// <summary>SOLIDWORKS 2024 SP5 regression target. Confirmed by live RevisionNumber or by interop SHA-256 family.</summary>
        Sw2024Sp5Regression = 0,

        /// <summary>SOLIDWORKS 2025 target. Confirmed by live RevisionNumber or by install registry capture (Slice 0).</summary>
        Sw2025Target = 1,

        /// <summary>SOLIDWORKS 2026 forward, unverified. Forces read-only limited status, never mutation.</summary>
        Sw2026ForwardUnverified = 2,

        /// <summary>Could not classify the runtime. Forces read-only limited status, never mutation.</summary>
        UnknownReadOnly = 100
    }
}
