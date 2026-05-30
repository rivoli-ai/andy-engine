using System.Text.Json.Serialization;

namespace Andy.Engine.SweBench.Model;

/// <summary>
/// Aggregate run report, modeled on the official swebench run report
/// (reporting.py make_run_report). Counts plus the id lists, plus run metadata.
/// </summary>
public sealed class SweRunReport
{
    [JsonPropertyName("total_instances")]
    public int TotalInstances { get; init; }

    [JsonPropertyName("submitted_instances")]
    public int SubmittedInstances { get; init; }

    [JsonPropertyName("completed_instances")]
    public int CompletedInstances { get; init; }

    [JsonPropertyName("resolved_instances")]
    public int ResolvedInstances { get; init; }

    [JsonPropertyName("unresolved_instances")]
    public int UnresolvedInstances { get; init; }

    [JsonPropertyName("empty_patch_instances")]
    public int EmptyPatchInstances { get; init; }

    [JsonPropertyName("error_instances")]
    public int ErrorInstances { get; init; }

    [JsonPropertyName("submitted_ids")]
    public IReadOnlyList<string> SubmittedIds { get; init; } = Array.Empty<string>();

    [JsonPropertyName("completed_ids")]
    public IReadOnlyList<string> CompletedIds { get; init; } = Array.Empty<string>();

    [JsonPropertyName("resolved_ids")]
    public IReadOnlyList<string> ResolvedIds { get; init; } = Array.Empty<string>();

    [JsonPropertyName("unresolved_ids")]
    public IReadOnlyList<string> UnresolvedIds { get; init; } = Array.Empty<string>();

    [JsonPropertyName("empty_patch_ids")]
    public IReadOnlyList<string> EmptyPatchIds { get; init; } = Array.Empty<string>();

    [JsonPropertyName("error_ids")]
    public IReadOnlyList<string> ErrorIds { get; init; } = Array.Empty<string>();

    [JsonPropertyName("schema_version")]
    public int SchemaVersion { get; init; } = 2;

    /// <summary>Run metadata (model, dataset, subset, timestamps, duration, tokens).</summary>
    [JsonPropertyName("metadata")]
    public RunReportMetadata Metadata { get; init; } = new();

    /// <summary>Resolve rate = resolved / total (0 when total is 0).</summary>
    [JsonPropertyName("resolve_rate")]
    public double ResolveRate => TotalInstances == 0 ? 0d : (double)ResolvedInstances / TotalInstances;
}

public sealed class RunReportMetadata
{
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("dataset")]
    public string? Dataset { get; init; }

    [JsonPropertyName("subset")]
    public string? Subset { get; init; }

    [JsonPropertyName("run_id")]
    public string? RunId { get; init; }

    [JsonPropertyName("stage")]
    public string? Stage { get; init; }

    [JsonPropertyName("started_at")]
    public DateTimeOffset? StartedAt { get; init; }

    [JsonPropertyName("completed_at")]
    public DateTimeOffset? CompletedAt { get; init; }

    [JsonPropertyName("duration_seconds")]
    public double? DurationSeconds { get; init; }

    [JsonPropertyName("total_tokens")]
    public long? TotalTokens { get; init; }

    /// <summary>True if the run was aborted early by the fail-fast gate.</summary>
    [JsonPropertyName("aborted")]
    public bool Aborted { get; init; }

    [JsonPropertyName("abort_reason")]
    public string? AbortReason { get; init; }
}
