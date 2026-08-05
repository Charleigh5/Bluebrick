using System;

namespace BlueBrick.SolidWorks.Runtime
{
    /// <summary>
    /// Source the runtime info was captured from. Per BB-M001 packet
    /// §16, when only the install registry was consulted (no live
    /// RevisionNumber call), the classification is forced to
    /// <see cref="SolidWorksRuntimeClassification.UnknownReadOnly"/>.
    /// </summary>
    [Serializable]
    public enum RuntimeInfoCaptureSource
    {
        /// <summary>Captured from a live <c>ISldWorks.RevisionNumber()</c> call with main-thread access proven.</summary>
        FromLiveInstance = 0,

        /// <summary>Captured from the install registry only; classification forced to <see cref="SolidWorksRuntimeClassification.UnknownReadOnly"/>.</summary>
        FromInstallRegistry = 1,

        /// <summary>Synthetic; no live instance and no install present (used by MOCK mode).</summary>
        Mock = 2
    }

    /// <summary>
    /// Runtime info POCO. Per BB-M001 packet §16, carries the SOLIDWORKS
    /// version, adapter classification, and the capture timestamp. The
    /// timestamp is EXCLUDED from state-hash inputs (see
    /// <see cref="BlueBrick.Audit.Core.AuditStateVersionBuilder"/>); only
    /// the version + classification contribute to the canonical snapshot.
    /// </summary>
    [Serializable]
    public sealed class SolidWorksRuntimeInfo
    {
        /// <summary>Version details. Never null. Default is an empty <see cref="SolidWorksVersion"/>.</summary>
        public SolidWorksVersion Version { get; set; } = new SolidWorksVersion();

        /// <summary>Adapter classification; forces read-only limited when not provable.</summary>
        public SolidWorksRuntimeClassification Classification { get; set; } = SolidWorksRuntimeClassification.UnknownReadOnly;

        /// <summary>Where the info was captured from. Used by receipts to honestly describe the runtime claim.</summary>
        public RuntimeInfoCaptureSource CaptureSource { get; set; } = RuntimeInfoCaptureSource.FromInstallRegistry;

        /// <summary>Capture time (UTC). EXCLUDED from state hash (caller must omit this field before hashing).</summary>
        public DateTime CaptureTimestampUtc { get; set; }

        /// <summary>True when this instance was synthesized by MOCK mode; never had a live instance or install.</summary>
        public bool IsMock => CaptureSource == RuntimeInfoCaptureSource.Mock;
    }
}
