using Andy.Engine.SweBench.Model;

namespace Andy.Engine.SweBench.Reporting;

/// <summary>
/// Aggregates per-instance grade results (and predictions) into a <see cref="SweRunReport"/>,
/// following the official counting semantics.
/// </summary>
public sealed class RunReportBuilder
{
    /// <param name="totalInstances">Size of the selected subset (the denominator).</param>
    /// <param name="predictions">Predictions that were submitted (by instance id).</param>
    /// <param name="grades">Grade results keyed by instance id (may be empty for a dry run).</param>
    public SweRunReport Build(
        int totalInstances,
        IReadOnlyCollection<SwePrediction> predictions,
        IReadOnlyDictionary<string, InstanceGradeResult> grades,
        RunReportMetadata metadata)
    {
        var submittedIds = predictions.Select(p => p.InstanceId).OrderBy(x => x, StringComparer.Ordinal).ToList();

        var completedIds = new List<string>();
        var resolvedIds = new List<string>();
        var unresolvedIds = new List<string>();
        var emptyPatchIds = new List<string>();
        var errorIds = new List<string>();

        foreach (var (id, grade) in grades)
        {
            if (grade.EmptyPatch)
                emptyPatchIds.Add(id);

            if (grade.Error is not null)
            {
                errorIds.Add(id);
                continue;
            }

            completedIds.Add(id);
            if (grade.Resolved)
                resolvedIds.Add(id);
            else
                unresolvedIds.Add(id);
        }

        Sort(completedIds); Sort(resolvedIds); Sort(unresolvedIds); Sort(emptyPatchIds); Sort(errorIds);

        return new SweRunReport
        {
            TotalInstances = totalInstances,
            SubmittedInstances = submittedIds.Count,
            CompletedInstances = completedIds.Count,
            ResolvedInstances = resolvedIds.Count,
            UnresolvedInstances = unresolvedIds.Count,
            EmptyPatchInstances = emptyPatchIds.Count,
            ErrorInstances = errorIds.Count,
            SubmittedIds = submittedIds,
            CompletedIds = completedIds,
            ResolvedIds = resolvedIds,
            UnresolvedIds = unresolvedIds,
            EmptyPatchIds = emptyPatchIds,
            ErrorIds = errorIds,
            Metadata = metadata,
        };
    }

    private static void Sort(List<string> ids) => ids.Sort(StringComparer.Ordinal);
}
