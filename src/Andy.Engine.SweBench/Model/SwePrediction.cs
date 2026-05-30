using System.Text.Json.Serialization;

namespace Andy.Engine.SweBench.Model;

/// <summary>
/// A model/agent prediction for a single instance, in the official SWE-bench
/// prediction format. Serialized one-per-line to predictions.jsonl.
/// </summary>
public sealed class SwePrediction
{
    [JsonPropertyName("instance_id")]
    public required string InstanceId { get; init; }

    [JsonPropertyName("model_name_or_path")]
    public required string ModelNameOrPath { get; init; }

    /// <summary>Unified git diff produced by the agent (what `git apply` consumes).</summary>
    [JsonPropertyName("model_patch")]
    public string ModelPatch { get; init; } = string.Empty;

    [JsonIgnore]
    public bool IsEmptyPatch => string.IsNullOrWhiteSpace(ModelPatch);
}
