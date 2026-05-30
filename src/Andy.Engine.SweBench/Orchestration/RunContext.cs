using Andy.Engine.SweBench.Dataset;

namespace Andy.Engine.SweBench.Orchestration;

/// <summary>
/// Immutable configuration for a single benchmark run. Built from CLI options (or
/// directly in tests) and consumed by <c>SweBenchRunner</c>.
/// </summary>
public sealed class RunContext
{
    // ---- Dataset / selection ----
    public required string DatasetPath { get; init; }
    public SubsetSelector Subset { get; init; } = new();

    // ---- Stage ----
    public RunStage Stage { get; init; } = RunStage.All;

    /// <summary>
    /// For the grade stage: path to an existing predictions.jsonl, or the literal
    /// "gold" to grade the dataset's own gold patches (the validation gate).
    /// </summary>
    public string? PredictionsPath { get; init; }

    public bool IsGoldValidation =>
        string.Equals(PredictionsPath, "gold", StringComparison.OrdinalIgnoreCase);

    // ---- Model / provider (agent stage) ----
    // Free, tool-capable OpenRouter model (Qwen free was retired). Kimi
    // (moonshotai/kimi-k2.6:free) is a stronger coding alternative via --model.
    public string Model { get; init; } = "openai/gpt-oss-20b:free";
    public string ProviderBaseUrl { get; init; } = "https://openrouter.ai/api/v1";
    public int MaxTurns { get; init; } = 40;

    /// <summary>
    /// Per-response output-token cap sent to the model. Must be large enough to fit a reasoning
    /// model's hidden reasoning plus a complete tool call; too low truncates turns (FinishReason
    /// "length") and yields empty patches.
    /// </summary>
    public int MaxOutputTokens { get; init; } = 8192;

    // ---- Rate-limit policy ----
    public int MaxRetries { get; init; } = 6;
    public int MaxDelaySeconds { get; init; } = 60;

    // ---- Fail-fast ----
    public int FailFastWindow { get; init; } = 5;
    public double FailFastThreshold { get; init; } = 0.6;
    public int MaxConsecutiveErrors { get; init; } = 3;

    /// <summary>
    /// Gold survey mode: in gold validation, grade ALL instances and report the full pass/fail
    /// breakdown instead of aborting on the first non-resolving gold instance. Useful for
    /// triaging a new/large subset (genuine grader bugs vs. swebench dataset/image drift).
    /// </summary>
    public bool GoldSurvey { get; init; } = false;

    // ---- Grading ----
    public int DockerTimeoutSeconds { get; init; } = 1800;

    // ---- Output / behavior ----
    public string WorkDir { get; init; } = Path.Combine(Directory.GetCurrentDirectory(), "swebench-runs");
    public string RunId { get; init; } = "run";
    public IReadOnlyList<string> Reporters { get; init; } = new[] { "console", "json" };
    public bool Resume { get; init; }
    public bool KeepWorkspaces { get; init; }

    /// <summary>The output directory for this run: WorkDir/RunId.</summary>
    public string RunDir => Path.Combine(WorkDir, RunId);

    /// <summary>Path where predictions are written (agent stage) or read (grade stage).</summary>
    public string PredictionsFilePath =>
        Stage == RunStage.Grade && !IsGoldValidation && !string.IsNullOrEmpty(PredictionsPath)
            ? PredictionsPath!
            : Path.Combine(RunDir, "predictions.jsonl");
}
