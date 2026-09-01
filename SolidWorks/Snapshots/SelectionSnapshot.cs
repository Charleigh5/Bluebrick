using System;
using System.Collections.Generic;

namespace BlueBrick.SolidWorks.Snapshots
{
    [Serializable]
    public sealed class SelectionEntry
    {
        public int Index { get; set; }
        public string SelectionType { get; set; }
        public string SafeName { get; set; }
        public int SelectionMark { get; set; }
    }

    [Serializable]
    public sealed class SelectionSnapshot
    {
        public const int MaxSelectionCount = 100;
        public const string LimitReachedCode = "SELECTION_LIMIT_REACHED";
        public int Count { get; set; }
        public string SelectionType { get; set; }
        public string SafeName { get; set; }
        public int SelectionMark { get; set; }
        public string DocumentIdentityHash { get; set; }
        public List<string> Limitations { get; set; } = new List<string>();
        public string Status { get; set; }
        public List<SelectionEntry> Items { get; set; } = new List<SelectionEntry>();
    }
}
