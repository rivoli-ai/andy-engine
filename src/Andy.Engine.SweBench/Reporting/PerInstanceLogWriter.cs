using System.Text.Json;
using Andy.Engine.SweBench.Model;

namespace Andy.Engine.SweBench.Reporting;

/// <summary>
/// Writes per-instance artifacts under &lt;runDir&gt;/logs/&lt;instance_id&gt;/:
/// patch.diff (the model patch), test_output.txt (raw log), report.json (the grade).
/// Mirrors the official harness per-instance logs.
/// </summary>
public sealed class PerInstanceLogWriter
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _logsRoot;

    public PerInstanceLogWriter(string runDir) =>
        _logsRoot = Path.Combine(runDir, "logs", "run_evaluation");

    public void Write(SwePrediction prediction, InstanceGradeResult grade)
    {
        var dir = Path.Combine(_logsRoot, Sanitize(grade.InstanceId));
        Directory.CreateDirectory(dir);

        File.WriteAllText(Path.Combine(dir, "patch.diff"), prediction.ModelPatch);
        File.WriteAllText(Path.Combine(dir, "test_output.txt"), grade.RawTestLog);

        var report = new
        {
            instance_id = grade.InstanceId,
            patch_applied = grade.PatchApplied,
            test_patch_applied = grade.TestPatchApplied,
            empty_patch = grade.EmptyPatch,
            resolved = grade.Resolved,
            resolved_status = grade.ResolvedStatus.ToString(),
            error = grade.Error,
            timed_out = grade.TimedOut,
            status_map = grade.StatusMap.ToDictionary(kv => kv.Key, kv => kv.Value.ToString()),
        };
        File.WriteAllText(Path.Combine(dir, "report.json"), JsonSerializer.Serialize(report, Options));
    }

    private static string Sanitize(string id) =>
        string.Join("_", id.Split(Path.GetInvalidFileNameChars()));
}
