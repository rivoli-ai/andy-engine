using Andy.Engine.SweBench.Model;

namespace Andy.Engine.SweBench.Reporting;

public enum InstanceStatus { Resolved, Unresolved, EmptyPatch, Error }

/// <summary>Per-instance row for the detailed HTML report.</summary>
public sealed record InstanceDetail
{
    public required string InstanceId { get; init; }
    public required string Repo { get; init; }
    public required InstanceStatus Status { get; init; }

    /// <summary>One-line gist of the task (from the dataset problem statement), if available.</summary>
    public string? TaskSummary { get; init; }

    /// <summary>Short "why it failed" summary for non-resolved instances.</summary>
    public string? FailureSummary { get; init; }

    /// <summary>Relative href to the full per-instance details (test log / patch), if on disk.</summary>
    public string? DetailsHref { get; init; }
}

/// <summary>The aggregate summary plus per-instance detail rows.</summary>
public sealed record DetailedRunReport(SweRunReport Summary, IReadOnlyList<InstanceDetail> Instances);
