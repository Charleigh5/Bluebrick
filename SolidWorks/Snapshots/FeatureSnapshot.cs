using System;
using System.Collections.Generic;

namespace BlueBrick.SolidWorks.Snapshots
{
    [Serializable]
    public sealed class FeatureSnapshot
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int Depth { get; set; }
        public string Parent { get; set; } = string.Empty;
        public string Suppression { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public List<string> Limitations { get; set; } = new List<string>();
    }

    [Serializable]
    public sealed class FeatureTreeSnapshot
    {
        public const int MaxNodes = 500;
        public const int MaxDepth = 20;
        public const string LimitReachedCode = "FEATURE_LIMIT_REACHED";

        public List<FeatureSnapshot> Features { get; set; } = new List<FeatureSnapshot>();
        public string Status { get; set; } = "ok";
        public List<string> Limitations { get; set; } = new List<string>();
        public string DocumentIdentityHash { get; set; } = string.Empty;
        public int TotalCount { get; set; }
        public bool Truncated { get; set; }
    }
}
