using System.Text.Json;
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
}
