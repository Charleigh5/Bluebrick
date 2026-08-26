using System;

namespace BlueBrick.SolidWorks.Runtime
{
    /// <summary>
    /// SOLIDWORKS version details read from the live application object
    /// (when proven) or synthesized from the install registry (when only
    /// registry capture is available — Slice 0 default).
    /// Per BB-M001 packet §16, NEVER claims a service pack that cannot
    /// be proven: when only the registry is consulted, <see cref="ServicePack"/>
    /// is left empty and <see cref="Classification"/> is set to
    /// <see cref="SolidWorksRuntimeClassification.UnknownReadOnly"/>.
    /// </summary>
    [Serializable]
    public sealed class SolidWorksVersion
    {
        /// <summary>Dotted major.version label (e.g. "33.5.0.53" from the install registry); empty when not available.</summary>
        public string DisplayVersion { get; set; } = string.Empty;

        /// <summary>SOLIDWORKS major release number (2024, 2025, 2026); 0 when unknown.</summary>
        public int MajorVersion { get; set; }

        /// <summary>Human-readable service pack label (e.g. "SP5.0"); empty when not proven live.</summary>
        public string ServicePack { get; set; } = string.Empty;

        /// <summary>Raw revision string from <c>ISldWorks.RevisionNumber()</c>; empty when not queried live.</summary>
        public string RawRevisionString { get; set; } = string.Empty;

        /// <summary>Build number when available; empty otherwise.</summary>
        public string BuildNumber { get; set; } = string.Empty;
    }

    /// <summary>
    /// Helpers to build <see cref="SolidWorksRuntimeInfo"/> instances for
    /// the read-only audit snapshot. Per BB-M001 packet §16, runtime
    /// detection reads from the live application object when safely
    /// available; captured info falls back to install-registry capture
    /// when no live instance is safely available.
    /// </summary>
    public static class SolidWorksRuntimeInfoFactory
    {
        /// <summary>
        /// Create a runtime info from a live <c>ISldWorks.RevisionNumber()</c>
        /// string (called from the proven UI thread only).
        /// </summary>
        public static SolidWorksRuntimeInfo FromLiveRevisionNumber(string revisionString)
        {
            var info = new SolidWorksRuntimeInfo
            {
                CaptureSource = RuntimeInfoCaptureSource.FromLiveInstance,
                CaptureTimestampUtc = DateTime.UtcNow,
                Version = Parse(revisionString),
                Classification = ClassifyLive(revisionString)
            };
            if (string.IsNullOrEmpty(revisionString))
            {
                info.Classification = SolidWorksRuntimeClassification.UnknownReadOnly;
            }
            return info;
        }

        /// <summary>
        /// Create a runtime info from the install registry (Slice 0
        /// evidence). Per packet §16, classification is forced to
        /// UnknownReadOnly when no live RevisionNumber has been observed.
        /// </summary>
        public static SolidWorksRuntimeInfo FromInstallRegistry(SolidWorksVersion version)
        {
            var sanitized = version == null ? new SolidWorksVersion() : new SolidWorksVersion
            {
                DisplayVersion = version.DisplayVersion ?? string.Empty,
                MajorVersion = version.MajorVersion,
                ServicePack = string.Empty,
                BuildNumber = string.Empty,
                RawRevisionString = string.Empty
            };
            return new SolidWorksRuntimeInfo
            {
                CaptureSource = RuntimeInfoCaptureSource.FromInstallRegistry,
                CaptureTimestampUtc = DateTime.UtcNow,
                Version = sanitized,
                Classification = ClassifyInstall(version)
            };
        }

        /// <summary>MOCK-mode factory: returns a synthetic runtime info with no live install.</summary>
        public static SolidWorksRuntimeInfo ForMock()
        {
            return new SolidWorksRuntimeInfo
            {
                CaptureSource = RuntimeInfoCaptureSource.Mock,
                CaptureTimestampUtc = DateTime.UtcNow,
                Version = new SolidWorksVersion { DisplayVersion = "MOCK", MajorVersion = 0, ServicePack = string.Empty, RawRevisionString = "MOCK" },
                Classification = SolidWorksRuntimeClassification.UnknownReadOnly
            };
        }

        private static SolidWorksVersion Parse(string revisionString)
        {
            if (string.IsNullOrEmpty(revisionString))
            {
                return new SolidWorksVersion { RawRevisionString = revisionString ?? string.Empty };
            }
            var v = new SolidWorksVersion { RawRevisionString = revisionString, DisplayVersion = revisionString };
            if (revisionString.Contains("2024")) v.MajorVersion = 2024;
            else if (revisionString.Contains("2025")) v.MajorVersion = 2025;
            else if (revisionString.Contains("2026")) v.MajorVersion = 2026;
            return v;
        }

        private static SolidWorksRuntimeClassification ClassifyLive(string revisionString)
        {
            if (string.IsNullOrEmpty(revisionString)) return SolidWorksRuntimeClassification.UnknownReadOnly;
            if (revisionString.Contains("2026")) return SolidWorksRuntimeClassification.Sw2026ForwardUnverified;
            if (revisionString.Contains("2025")) return SolidWorksRuntimeClassification.Sw2025Target;
            if (revisionString.Contains("2024")) return SolidWorksRuntimeClassification.Sw2024Sp5Regression;
            return SolidWorksRuntimeClassification.UnknownReadOnly;
        }

        private static SolidWorksRuntimeClassification ClassifyInstall(SolidWorksVersion version)
        {
            // Per packet §16 "Do not claim a service pack that cannot be proven."
            return SolidWorksRuntimeClassification.UnknownReadOnly;
        }
    }
}
