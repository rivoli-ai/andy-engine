using Andy.Engine.SweBench.Agent;
using Andy.Engine.SweBench.Dataset;
using Andy.Engine.SweBench.Grading;
using Andy.Engine.SweBench.Model;
using Andy.Engine.SweBench.Reporting;

namespace Andy.Engine.SweBench.Orchestration;

/// <summary>
/// Top-level orchestrator: select a subset, run the requested stage(s), aggregate a
/// report, and emit it. The same entry point is used by both the CLI and the xUnit harness.
///
/// Milestone status:
///  - M0: <see cref="RunStage.None"/> (dry run).
///  - M1: <see cref="RunStage.Grade"/> + gold validation.
///  - M2: <see cref="RunStage.Agent"/> / <see cref="RunStage.All"/>.
/// </summary>
public sealed class SweBenchRunner
{
    private readonly RunContext _ctx;
    private readonly TextWriter _log;
    private readonly IDockerClient _docker;

    public SweBenchRunner(RunContext ctx, TextWriter? log = null, IDockerClient? docker = null)
    {
        _ctx = ctx;
        _log = log ?? Console.Out;
        _docker = docker ?? new DockerClient(log: _log);
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;

        var loader = new SweBenchDatasetLoader();
        var all = loader.LoadFromFile(_ctx.DatasetPath);
        var subset = _ctx.Subset.Select(all);

        if (subset.Count == 0)
        {
            _log.WriteLine("No instances selected after applying subset filters; nothing to do.");
            return 2;
        }

        Directory.CreateDirectory(_ctx.RunDir);
        _log.WriteLine($"Loaded {all.Count} instances from {_ctx.DatasetPath}; selected {subset.Count}.");
        _log.WriteLine($"Stage: {_ctx.Stage}{(_ctx.IsGoldValidation ? " (gold validation)" : string.Empty)}");
        _log.WriteLine($"Run dir: {_ctx.RunDir}");
        PrintSubset(subset);

        var predictions = new List<SwePrediction>();
        var grades = new Dictionary<string, InstanceGradeResult>(StringComparer.Ordinal);
        var abortReason = (string?)null;

        switch (_ctx.Stage)
        {
            case RunStage.None:
                break;

            case RunStage.Grade:
                {
                    var byId = ResolvePredictions(subset);
                    abortReason = await RunGradeStageAsync(subset, byId, predictions, grades, cancellationToken);
                    break;
                }

            case RunStage.Agent:
                abortReason = await RunAgentStageAsync(subset, predictions, cancellationToken);
                break;

            case RunStage.All:
                {
                    abortReason = await RunAgentStageAsync(subset, predictions, cancellationToken);
                    if (abortReason is null)
                    {
                        var byId = predictions.ToDictionary(p => p.InstanceId, p => p, StringComparer.Ordinal);
                        abortReason = await RunGradeStageAsync(subset, byId, new List<SwePrediction>(), grades, cancellationToken);
                    }
                    break;
                }
        }

        var completedAt = DateTimeOffset.UtcNow;
        var metadata = new RunReportMetadata
        {
            Model = _ctx.IsGoldValidation ? "gold" : (_ctx.Stage is RunStage.Agent or RunStage.All ? _ctx.Model : null),
            Dataset = _ctx.DatasetPath,
            Subset = DescribeSubset(subset.Count),
            RunId = _ctx.RunId,
            Stage = _ctx.Stage.ToString(),
            StartedAt = startedAt,
            CompletedAt = completedAt,
            DurationSeconds = (completedAt - startedAt).TotalSeconds,
            Aborted = abortReason is not null,
            AbortReason = abortReason,
        };

        var report = new RunReportBuilder().Build(subset.Count, predictions, grades, metadata);
        EmitReports(report);

        return abortReason is null ? 0 : 1;
    }

    /// <summary>
    /// Runs the agent stage: produce a model patch per instance and checkpoint predictions.
    /// Returns an abort reason if the fail-fast gate tripped, else null.
    /// </summary>
    private async Task<string?> RunAgentStageAsync(
        IReadOnlyList<SweBenchInstance> subset,
        List<SwePrediction> predictions,
        CancellationToken cancellationToken)
    {
        // ---- Pre-flight: the in-process andy agent reaches OpenRouter directly, so it needs a
        // key. An external CLI agent carries its own auth, so we don't gate on the key there. ----
        var usingAndyAgent = string.Equals(_ctx.Agent, "andy", StringComparison.OrdinalIgnoreCase);
        if (usingAndyAgent && string.IsNullOrWhiteSpace(SweAgentFactory.ApiKey))
            return "OPENROUTER_API_KEY is not set (agent stage cannot reach the model)";

        var checkpoint = new PredictionCheckpoint(_ctx.PredictionsFilePath);
        var done = _ctx.Resume ? checkpoint.Read() : new Dictionary<string, SwePrediction>(StringComparer.Ordinal);

        var workspaces = new SweWorkspaceManager(_ctx.RunDir, _log);
        var runner = new SweInstanceRunner(_ctx, workspaces, _log);
        var gate = new FailFastGate(
            _ctx.FailFastWindow, _ctx.FailFastThreshold, _ctx.MaxConsecutiveErrors, goldMode: false);

        _log.WriteLine($"Model: {_ctx.Model} via {_ctx.ProviderBaseUrl} (max {_ctx.MaxTurns} turns/instance)");

        foreach (var instance in subset)
        {
            if (_ctx.Resume && done.TryGetValue(instance.InstanceId, out var existing))
            {
                _log.WriteLine($"  [resume] {instance.InstanceId}: already in checkpoint");
                predictions.Add(existing);
                continue;
            }

            _log.WriteLine($"  [agent] {instance.InstanceId} ...");

            // Per-instance wall-clock cap: cancel a runaway agent and move on, without aborting
            // the whole run. The outer token still cancels everything.
            using var instanceCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (_ctx.AgentTimeoutSeconds > 0)
                instanceCts.CancelAfter(TimeSpan.FromSeconds(_ctx.AgentTimeoutSeconds));

            AgentRunResult result;
            try
            {
                result = await runner.RunAsync(instance, instanceCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Timed out (not an outer cancellation): record an empty prediction and continue.
                _log.WriteLine($"           -> TIMED OUT after {_ctx.AgentTimeoutSeconds}s; skipping instance");
                var timedOut = new SwePrediction
                {
                    InstanceId = instance.InstanceId,
                    ModelNameOrPath = _ctx.Model,
                    ModelPatch = string.Empty,
                };
                predictions.Add(timedOut);
                checkpoint.Append(timedOut);
                if (gate.Observe(InstanceOutcome.Soft, instance.InstanceId))
                    break;
                continue;
            }

            predictions.Add(result.Prediction);
            checkpoint.Append(result.Prediction);

            _log.WriteLine($"           -> {result.Detail} ({result.TurnCount} turns, {result.Duration.TotalSeconds:0.0}s)");

            if (gate.Observe(result.Outcome, instance.InstanceId))
                break;
        }

        if (gate.Tripped)
            _log.WriteLine($"FAIL-FAST: {gate.Reason}");

        return gate.Reason;
    }

    /// <summary>Runs the grade stage over a resolved prediction map. Returns an abort reason if tripped.</summary>
    private async Task<string?> RunGradeStageAsync(
        IReadOnlyList<SweBenchInstance> subset,
        IReadOnlyDictionary<string, SwePrediction> byId,
        List<SwePrediction> submitted,
        Dictionary<string, InstanceGradeResult> grades,
        CancellationToken cancellationToken)
    {
        // ---- Pre-flight ----
        if (!await _docker.IsAvailableAsync(cancellationToken))
            return "docker daemon not reachable (pre-flight failed)";

        var gate = new FailFastGate(
            _ctx.FailFastWindow, _ctx.FailFastThreshold, _ctx.MaxConsecutiveErrors,
            goldMode: _ctx.IsGoldValidation && !_ctx.GoldSurvey);

        var grader = new DockerGrader(_docker, timeout: TimeSpan.FromSeconds(_ctx.DockerTimeoutSeconds));
        var logWriter = new PerInstanceLogWriter(_ctx.RunDir);
        var specBuilder = new TestSpecBuilder();

        foreach (var instance in subset)
        {
            if (!byId.TryGetValue(instance.InstanceId, out var prediction))
            {
                _log.WriteLine($"  [skip] {instance.InstanceId}: no prediction submitted");
                continue;
            }

            submitted.Add(prediction);

            // Ensure the image is present (missing image == broken setup -> hard error).
            var image = specBuilder.GetImageTag(instance.InstanceId);
            if (!await _docker.EnsureImageAsync(image, cancellationToken))
            {
                var miss = new InstanceGradeResult
                {
                    InstanceId = instance.InstanceId,
                    Error = $"docker image unavailable: {image}",
                    ResolvedStatus = ResolvedStatus.No,
                };
                grades[instance.InstanceId] = miss;
                logWriter.Write(prediction, miss);
                _log.WriteLine($"  [error] {instance.InstanceId}: image unavailable {image}");
                if (gate.Observe(InstanceOutcome.HardError, instance.InstanceId)) break;
                continue;
            }

            _log.WriteLine($"  [grade] {instance.InstanceId} ...");
            var grade = await grader.GradeAsync(instance, prediction, cancellationToken);
            grades[instance.InstanceId] = grade;
            logWriter.Write(prediction, grade);

            _log.WriteLine($"           -> {Describe(grade)}");

            var outcome = grade.Error is not null ? InstanceOutcome.HardError : InstanceOutcome.Soft;
            if (gate.Observe(outcome, instance.InstanceId, goldResolved: grade.Resolved))
                break;
        }

        if (gate.Tripped)
            _log.WriteLine($"FAIL-FAST: {gate.Reason}");

        return gate.Reason;
    }

    private IReadOnlyDictionary<string, SwePrediction> ResolvePredictions(IReadOnlyList<SweBenchInstance> subset)
    {
        if (_ctx.IsGoldValidation)
        {
            return subset.ToDictionary(
                i => i.InstanceId,
                i => new SwePrediction
                {
                    InstanceId = i.InstanceId,
                    ModelNameOrPath = "gold",
                    ModelPatch = i.GoldPatch,
                },
                StringComparer.Ordinal);
        }

        var checkpoint = new PredictionCheckpoint(_ctx.PredictionsFilePath);
        if (!checkpoint.Exists)
            throw new FileNotFoundException($"Predictions file not found: {_ctx.PredictionsFilePath}");
        return checkpoint.Read();
    }

    private static string Describe(InstanceGradeResult g)
    {
        if (g.EmptyPatch) return "empty patch (unresolved)";
        if (g.Error is not null) return $"ERROR: {g.Error}";
        if (!g.PatchApplied) return "patch did not apply (unresolved)";
        return g.Resolved ? "RESOLVED" : $"unresolved ({g.ResolvedStatus})";
    }

    private void EmitReports(SweRunReport report)
    {
        if (_ctx.Reporters.Contains("json", StringComparer.OrdinalIgnoreCase))
        {
            var path = new JsonReporter().Write(report, _ctx.RunDir);
            _log.WriteLine($"Wrote {path}");
        }

        if (_ctx.Reporters.Contains("console", StringComparer.OrdinalIgnoreCase))
            _log.WriteLine(ConsoleReporter.Render(report));

        if (_ctx.Reporters.Contains("html", StringComparer.OrdinalIgnoreCase))
        {
            var path = HtmlReporter.Write(report, Path.Combine(_ctx.RunDir, "report.html"));
            _log.WriteLine($"Wrote {path}");
        }
    }

    private void PrintSubset(IReadOnlyList<SweBenchInstance> subset)
    {
        _log.WriteLine("Selected instances:");
        foreach (var group in subset.GroupBy(i => i.Repo).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            _log.WriteLine($"  {group.Key}:");
            foreach (var inst in group)
                _log.WriteLine($"    - {inst.InstanceId} (version {inst.Version})");
        }
    }

    private string DescribeSubset(int count)
    {
        if (_ctx.Subset.InstanceIds is { Count: > 0 })
            return $"ids[{_ctx.Subset.InstanceIds.Count}]";
        if (!string.IsNullOrWhiteSpace(_ctx.Subset.SubsetFilePath))
            return $"file:{Path.GetFileName(_ctx.Subset.SubsetFilePath)}";
        if (_ctx.Subset.MaxInstances is { } cap)
            return $"first {Math.Min(cap, count)}";
        return $"all ({count})";
    }
}
