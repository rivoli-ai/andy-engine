using System.Text.Json;
using Andy.Engine.SweBench.Dataset;
using Andy.Engine.SweBench.Model;

namespace Andy.Engine.SweBench.Reporting;

/// <summary>
/// Rebuilds a full <see cref="SweRunReport"/> from a run directory's on-disk artifacts
/// (predictions.jsonl + the per-instance grade reports under logs/run_evaluation/*/report.json),
/// with NO Docker re-grading. This lets a batched/--resume run — whose top-level report.json only
/// reflects the last batch — be presented in full.
/// </summary>
public static class ReportConsolidator
{
    public static SweRunReport Consolidate(string runDir)
    {
        // Predictions (the set attempted); last write wins per instance, like the checkpoint.
        var predMap = new Dictionary<string, SwePrediction>(StringComparer.Ordinal);
        var predFile = Path.Combine(runDir, "predictions.jsonl");
        if (File.Exists(predFile))
        {
            foreach (var line in File.ReadLines(predFile))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var p = JsonSerializer.Deserialize<SwePrediction>(line);
                    if (p is not null) predMap[p.InstanceId] = p;
                }
                catch (JsonException) { /* skip corrupt line */ }
            }
        }

        // Per-instance grade results from logs/run_evaluation/<id>/report.json.
        var grades = new Dictionary<string, InstanceGradeResult>(StringComparer.Ordinal);
        var evalDir = Path.Combine(runDir, "logs", "run_evaluation");
        if (Directory.Exists(evalDir))
        {
            foreach (var rf in Directory.EnumerateFiles(evalDir, "report.json", SearchOption.AllDirectories))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(rf));
                    var root = doc.RootElement;
                    if (!root.TryGetProperty("instance_id", out var idEl) || idEl.GetString() is not { } id)
                        continue;
                    grades[id] = new InstanceGradeResult
                    {
                        InstanceId = id,
                        ResolvedStatus = root.TryGetProperty("resolved_status", out var rs)
                            && Enum.TryParse<ResolvedStatus>(rs.GetString(), ignoreCase: true, out var v)
                            ? v : ResolvedStatus.No,
                        EmptyPatch = GetBool(root, "empty_patch"),
                        PatchApplied = GetBool(root, "patch_applied"),
                        TestPatchApplied = GetBool(root, "test_patch_applied"),
                        TimedOut = GetBool(root, "timed_out"),
                        Error = root.TryGetProperty("error", out var er) && er.ValueKind == JsonValueKind.String
                            ? er.GetString() : null,
                    };
                }
                catch (JsonException) { /* skip unreadable per-instance report */ }
            }
        }

        var metadata = new RunReportMetadata
        {
            Model = predMap.Values.Select(p => p.ModelNameOrPath).FirstOrDefault(s => s != "gold" && s != "error")
                    ?? predMap.Values.Select(p => p.ModelNameOrPath).FirstOrDefault(),
            RunId = Path.GetFileName(runDir.TrimEnd(Path.DirectorySeparatorChar, '/')),
            Stage = "consolidated",
        };

        return new RunReportBuilder().Build(predMap.Count, predMap.Values.ToList(), grades, metadata);
    }

    private static bool GetBool(JsonElement root, string name) =>
        root.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.True;

    /// <summary>
    /// Like <see cref="Consolidate"/>, but also produces per-instance detail rows: a one-line task
    /// gist (from the dataset problem statement, if <paramref name="datasetPath"/> is given), a
    /// short failure reason, and a relative link to the full per-instance log on disk.
    /// </summary>
    public static DetailedRunReport ConsolidateDetailed(string runDir, string? datasetPath = null)
    {
        var summary = Consolidate(runDir);

        // Optional dataset join: instance id -> (problem statement, FAIL_TO_PASS).
        var ds = new Dictionary<string, SweBenchInstance>(StringComparer.Ordinal);
        if (!string.IsNullOrEmpty(datasetPath) && File.Exists(datasetPath))
        {
            try
            {
                foreach (var inst in new SweBenchDatasetLoader().LoadFromFile(datasetPath))
                    ds[inst.InstanceId] = inst;
            }
            catch { /* dataset is best-effort enrichment */ }
        }

        var evalDir = Path.Combine(runDir, "logs", "run_evaluation");
        var rows = new List<InstanceDetail>();
        var allIds = summary.SubmittedIds.Count > 0
            ? summary.SubmittedIds
            : summary.ResolvedIds.Concat(summary.UnresolvedIds).Concat(summary.EmptyPatchIds).Concat(summary.ErrorIds);

        foreach (var id in allIds)
        {
            var status = ClassifyFromReport(summary, id);
            ds.TryGetValue(id, out var inst);
            var instDir = Path.Combine(evalDir, id);
            rows.Add(new InstanceDetail
            {
                InstanceId = id,
                Repo = inst?.Repo ?? RepoOf(id),
                Status = status,
                TaskSummary = Gist(inst?.ProblemStatement),
                FailureSummary = status == InstanceStatus.Resolved ? null : FailureReason(status, instDir, inst),
                DetailsHref = BestDetailsHref(runDir, evalDir, id),
            });
        }

        // Order: failures first (most interesting), then resolved; stable by id within a group.
        rows = rows
            .OrderBy(r => r.Status == InstanceStatus.Resolved ? 1 : 0)
            .ThenBy(r => r.InstanceId, StringComparer.Ordinal)
            .ToList();

        return new DetailedRunReport(summary, rows);
    }

    private static InstanceStatus ClassifyFromReport(SweRunReport r, string id)
    {
        if (r.ResolvedIds.Contains(id)) return InstanceStatus.Resolved;
        if (r.EmptyPatchIds.Contains(id)) return InstanceStatus.EmptyPatch;
        if (r.ErrorIds.Contains(id)) return InstanceStatus.Error;
        return InstanceStatus.Unresolved;
    }

    private static string? FailureReason(InstanceStatus status, string instDir, SweBenchInstance? inst)
    {
        if (status == InstanceStatus.EmptyPatch)
            return "No patch produced — the agent made no file edits (gave up or ran out of turns).";

        // Read the per-instance grade report for error / status_map.
        var rf = Path.Combine(instDir, "report.json");
        if (!File.Exists(rf))
            return status == InstanceStatus.Error ? "Run error (no grade report)." : "Did not resolve.";

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(rf));
            var root = doc.RootElement;

            if (status == InstanceStatus.Error)
            {
                var err = root.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;
                if (GetBool(root, "timed_out")) return "Timed out.";
                return Truncate("Run error: " + (err ?? "unknown"), 200);
            }

            // Unresolved: which FAIL_TO_PASS are still failing?
            var statusMap = new Dictionary<string, string>(StringComparer.Ordinal);
            if (root.TryGetProperty("status_map", out var sm) && sm.ValueKind == JsonValueKind.Object)
                foreach (var p in sm.EnumerateObject())
                    statusMap[p.Name] = p.Value.GetString() ?? "";

            if (!GetBool(root, "patch_applied"))
                return "The model patch did not apply to the repo.";

            if (inst is { FailToPass.Count: > 0 })
            {
                bool Passed(string t) => statusMap.TryGetValue(t, out var s) && (s is "Passed" or "XFail");
                var failing = inst.FailToPass.Where(t => !Passed(t)).ToList();
                if (failing.Count > 0)
                {
                    var shown = string.Join(", ", failing.Take(3).Select(ShortTest));
                    var more = failing.Count > 3 ? $" (+{failing.Count - 3} more)" : "";
                    return Truncate($"{failing.Count}/{inst.FailToPass.Count} target tests still failing: {shown}{more}", 240);
                }
                return "PASS_TO_PASS regression (a previously-passing test broke).";
            }

            return "Patch applied but the target tests did not pass.";
        }
        catch
        {
            return "Did not resolve.";
        }
    }

    /// <summary>"path/to/test_file.py::ClassName::test_method[param]" -> "test_method[param]".</summary>
    private static string ShortTest(string testId)
    {
        var afterColons = testId.Contains("::", StringComparison.Ordinal)
            ? testId[(testId.LastIndexOf("::", StringComparison.Ordinal) + 2)..]
            : testId;
        return afterColons.Length > 60 ? afterColons[..60] + "…" : afterColons;
    }

    private static string? BestDetailsHref(string runDir, string evalDir, string id)
    {
        // Relative to where report.html is written (the run dir, by default).
        foreach (var name in new[] { "test_output.txt", "report.json", "patch.diff" })
            if (File.Exists(Path.Combine(evalDir, id, name)))
                return $"logs/run_evaluation/{id}/{name}";
        return null;
    }

    private static string? Gist(string? problemStatement)
    {
        if (string.IsNullOrWhiteSpace(problemStatement)) return null;
        var firstLine = problemStatement
            .Split('\n')
            .Select(l => l.Trim())
            .FirstOrDefault(l => l.Length > 0) ?? problemStatement.Trim();
        return Truncate(firstLine, 180);
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max].TrimEnd() + "…";

    private static string RepoOf(string instanceId)
    {
        var us = instanceId.IndexOf("__", StringComparison.Ordinal);
        if (us < 0) return "(unknown)";
        var owner = instanceId[..us];
        var rest = instanceId[(us + 2)..];
        var dash = rest.LastIndexOf('-');
        return $"{owner}/{(dash > 0 ? rest[..dash] : rest)}";
    }
}
