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

    // ---- Agent selection ----
    /// <summary>
    /// Which agent drives each instance: "andy" (the in-process <c>SimpleAgent</c>, default) or
    /// "external" (an out-of-process CLI agent — opencode, aider, ... — via <see cref="AgentCommand"/>).
    /// The harness captures the patch by diffing the workspace either way, so both grade identically.
    /// </summary>
    public string Agent { get; init; } = "andy";

    /// <summary>
    /// For <c>Agent == "external"</c>: the command template to launch, whitespace-tokenized.
    /// Whole-token placeholders {model}/{workspace}/{prompt}/{prompt_file} are substituted (never
    /// shell-interpolated). Example: <c>opencode run --model {model} {prompt}</c>.
    /// </summary>
    public string? AgentCommand { get; init; }

    // ---- Model / provider (agent stage) ----
    // Free, tool-capable OpenRouter model (Qwen free was retired). Kimi
    // (moonshotai/kimi-k2.6:free) is a stronger coding alternative via --model.
    public string Model { get; init; } = "openai/gpt-oss-20b:free";
    public string ProviderBaseUrl { get; init; } = "https://openrouter.ai/api/v1";
    // ---- Agent limits ----
    // Defaults are tuned for LARGE-CONTEXT coding models (the norm for capable agents — mimo has a
    // 1M window). A re-baseline showed that tight limits (200k context / 8k output / 40 turns)
    // REGRESS such models: the compaction + output-truncation hide information and burn turns,
    // producing empty patches. So defaults are generous; tighten with the flags below for
    // token-constrained models. See src/Andy.Engine.SweBench/README.md.
    public int MaxTurns { get; init; } = 50;

    /// <summary>
    /// Per-response output-token cap sent to the model. Must fit a reasoning model's hidden
    /// reasoning PLUS a complete tool call; too low truncates turns (FinishReason "length"),
    /// wasting turns on "continue" round-trips and yielding empty patches.
    /// </summary>
    public int MaxOutputTokens { get; init; } = 16_384;

    /// <summary>
    /// Per-request input-token budget. The full conversation log is retained; only the per-request
    /// VIEW is compressed (Andy.Context SmartCompressor) once it exceeds this. Default is high so
    /// compaction does NOT kick in for large-context models (which need to see full files to edit
    /// them); lower it for models with a small context window.
    /// </summary>
    public int MaxContextTokens { get; init; } = 1_000_000;

    /// <summary>
    /// Per-tool-result character cap before truncate-with-guidance. Generous by default so the
    /// agent sees whole files; lower it to save tokens on token-constrained models.
    /// </summary>
    public int MaxToolResultChars { get; init; } = 100_000;

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

    /// <summary>
    /// Wall-clock cap per agent instance (seconds). If the agent exceeds it, the instance is
    /// cancelled, recorded as a timed-out (empty) prediction, and the run continues. Bounds
    /// runaway instances (a hung LLM call or a model that never stops). 0 disables. Relies on
    /// SimpleAgent propagating cancellation; the longest healthy instance observed was ~16 min.
    /// </summary>
    public int AgentTimeoutSeconds { get; init; } = 1800;

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
