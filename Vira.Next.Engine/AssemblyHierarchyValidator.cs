using Vira.Next.Contracts;

namespace Vira.Next.Engine;

public sealed class AssemblyHierarchyValidator
{
    public AssemblyHierarchyValidation Validate(CadDocumentSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var limitations = new List<string>();
        var components = snapshot.Components;
        var duplicateIds = components
            .GroupBy(item => item.SnapshotId, StringComparer.Ordinal)
            .Where(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateIds.Length > 0)
        {
            limitations.Add("Assembly hierarchy contains duplicate or blank snapshot identifiers.");
        }

        if (components.Any(item => item.Depth > snapshot.AssemblyTraversal.MaxDepth))
        {
            limitations.Add($"Assembly hierarchy exceeds the configured depth limit of {snapshot.AssemblyTraversal.MaxDepth}.");
        }

        if (components.Count > snapshot.AssemblyTraversal.RecordLimit ||
            snapshot.AssemblyTraversal.RecordedCount > snapshot.AssemblyTraversal.RecordLimit ||
            snapshot.AssemblyTraversal.Truncated)
        {
            limitations.Add($"Assembly hierarchy reached or exceeded the configured record limit of {snapshot.AssemblyTraversal.RecordLimit}.");
        }

        var parents = components
            .Where(item => !string.IsNullOrWhiteSpace(item.SnapshotId))
            .GroupBy(item => item.SnapshotId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().ParentSnapshotId, StringComparer.Ordinal);
        var cycleCount = CountCycles(parents);
        if (cycleCount > 0)
        {
            limitations.Add($"Assembly hierarchy contains {cycleCount} parent cycle(s).");
        }

        if (snapshot.AssemblyTraversal.MutationActions != 0 || snapshot.MutationActions != 0)
        {
            limitations.Add("Assembly snapshot reports a non-zero mutation count.");
        }

        if (snapshot.AssemblyTraversal.ExternalSystemsAccessed || snapshot.ExternalSystemsAccessed)
        {
            limitations.Add("Assembly snapshot reports external-system access.");
        }

        return new AssemblyHierarchyValidation
        {
            IsValid = limitations.Count == 0,
            RecordedCount = components.Count,
            CycleCount = cycleCount,
            Limitations = limitations
        };
    }

    private static int CountCycles(IReadOnlyDictionary<string, string> parents)
    {
        var completed = new HashSet<string>(StringComparer.Ordinal);
        var cycles = 0;

        foreach (var start in parents.Keys.OrderBy(item => item, StringComparer.Ordinal))
        {
            if (completed.Contains(start))
            {
                continue;
            }

            var path = new List<string>();
            var positions = new Dictionary<string, int>(StringComparer.Ordinal);
            var current = start;
            while (!string.IsNullOrWhiteSpace(current) && parents.ContainsKey(current) && !completed.Contains(current))
            {
                if (positions.ContainsKey(current))
                {
                    cycles++;
                    break;
                }

                positions[current] = path.Count;
                path.Add(current);
                current = parents[current];
            }

            foreach (var item in path)
            {
                completed.Add(item);
            }
        }

        return cycles;
    }
}
