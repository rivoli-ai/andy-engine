using Andy.Engine.SweBench.Model;

namespace Andy.Engine.SweBench.Dataset;

/// <summary>
/// Selects a subset of instances to run, from explicit ids, a subset file
/// (one instance id per line, '#' comments allowed), and/or a max-count cap.
/// Selection preserves dataset order. Explicit ids take precedence; the cap is
/// applied last.
/// </summary>
public sealed class SubsetSelector
{
    public IReadOnlyList<string>? InstanceIds { get; init; }
    public string? SubsetFilePath { get; init; }
    public int? MaxInstances { get; init; }

    public IReadOnlyList<SweBenchInstance> Select(IReadOnlyList<SweBenchInstance> all)
    {
        IEnumerable<SweBenchInstance> selected = all;

        var ids = ResolveIdFilter();
        if (ids is not null)
        {
            var requested = new HashSet<string>(ids, StringComparer.Ordinal);
            selected = all.Where(i => requested.Contains(i.InstanceId));

            // Surface ids that were asked for but not present in the dataset.
            var present = new HashSet<string>(all.Select(i => i.InstanceId), StringComparer.Ordinal);
            var missing = ids.Where(id => !present.Contains(id)).ToList();
            if (missing.Count > 0)
                throw new InvalidOperationException(
                    $"Requested instance ids not found in dataset: {string.Join(", ", missing)}");
        }

        if (MaxInstances is { } cap && cap >= 0)
            selected = selected.Take(cap);

        return selected.ToList();
    }

    private IReadOnlyList<string>? ResolveIdFilter()
    {
        var ids = new List<string>();

        if (InstanceIds is { Count: > 0 })
            ids.AddRange(InstanceIds);

        if (!string.IsNullOrWhiteSpace(SubsetFilePath))
        {
            if (!File.Exists(SubsetFilePath))
                throw new FileNotFoundException($"Subset file not found: {SubsetFilePath}", SubsetFilePath);

            foreach (var raw in File.ReadAllLines(SubsetFilePath))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                    continue;
                ids.Add(line);
            }
        }

        if (ids.Count == 0)
            return null;

        // De-duplicate while preserving first-seen order.
        return ids.Distinct(StringComparer.Ordinal).ToList();
    }
}
