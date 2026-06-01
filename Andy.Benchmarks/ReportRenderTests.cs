using System.Text.Json;
using Andy.Engine.SweBench.Model;
using Andy.Engine.SweBench.Reporting;
using Xunit;

namespace Andy.Benchmarks;

public class ReportRenderTests
{
    private static SweRunReport SampleReport() => new()
    {
        TotalInstances = 4,
        SubmittedInstances = 4,
        CompletedInstances = 4,
        ResolvedInstances = 2,
        UnresolvedInstances = 1,
        EmptyPatchInstances = 1,
        ErrorInstances = 0,
        SubmittedIds = new[] { "django__django-1", "django__django-2", "astropy__astropy-3", "astropy__astropy-4" },
        ResolvedIds = new[] { "django__django-1", "astropy__astropy-3" },
        UnresolvedIds = new[] { "django__django-2" },
        EmptyPatchIds = new[] { "astropy__astropy-4" },
        Metadata = new RunReportMetadata { Model = "xiaomi/mimo-v2.5", RunId = "t", Stage = "All" },
    };

    [Fact]
    public void HtmlReporter_Renders_Self_Contained_Page_With_Stats_And_Repo_Breakdown()
    {
        var html = HtmlReporter.Render(SampleReport());

        Assert.StartsWith("<!doctype html>", html);
        Assert.Contains("</html>", html);
        Assert.Contains("50.0", html);                 // resolve rate 2/4
        Assert.Contains("xiaomi/mimo-v2.5", html);     // model (HTML-encoded slash is fine)
        Assert.Contains("django/django", html);        // per-repo breakdown rows
        Assert.Contains("astropy/astropy", html);
        Assert.Contains("django__django-1", html);     // resolved instance listed
    }

    [Fact]
    public void Consolidator_Aggregates_Run_Dir_Without_Regrading()
    {
        var runDir = Path.Combine(Path.GetTempPath(), "swerpt_" + Guid.NewGuid().ToString("N"));
        var evalDir = Path.Combine(runDir, "logs", "run_evaluation");
        try
        {
            // predictions.jsonl: 2 instances attempted
            Directory.CreateDirectory(runDir);
            File.WriteAllLines(Path.Combine(runDir, "predictions.jsonl"), new[]
            {
                """{"instance_id":"django__django-1","model_name_or_path":"m","model_patch":"diff"}""",
                """{"instance_id":"django__django-2","model_name_or_path":"m","model_patch":"diff"}""",
            });
            // per-instance grade reports: one Full, one No
            WritePerInstance(evalDir, "django__django-1", "Full");
            WritePerInstance(evalDir, "django__django-2", "No");

            var report = ReportConsolidator.Consolidate(runDir);

            Assert.Equal(2, report.TotalInstances);
            Assert.Equal(1, report.ResolvedInstances);
            Assert.Contains("django__django-1", report.ResolvedIds);
            Assert.Contains("django__django-2", report.UnresolvedIds);
            Assert.Equal("m", report.Metadata.Model);
        }
        finally
        {
            try { Directory.Delete(runDir, recursive: true); } catch { /* best effort */ }
        }
    }

    [Fact]
    public void ConsolidateDetailed_Builds_Per_Instance_Rows_With_Failure_Reasons()
    {
        var runDir = Path.Combine(Path.GetTempPath(), "swedet_" + Guid.NewGuid().ToString("N"));
        var evalDir = Path.Combine(runDir, "logs", "run_evaluation");
        try
        {
            Directory.CreateDirectory(runDir);
            File.WriteAllLines(Path.Combine(runDir, "predictions.jsonl"), new[]
            {
                """{"instance_id":"django__django-1","model_name_or_path":"m","model_patch":"diff"}""",
                """{"instance_id":"django__django-2","model_name_or_path":"m","model_patch":""}""",
            });
            WritePerInstance(evalDir, "django__django-1", "No");        // unresolved
            WriteEmptyPatch(evalDir, "django__django-2");               // empty patch

            var detailed = ReportConsolidator.ConsolidateDetailed(runDir);

            Assert.Equal(2, detailed.Instances.Count);
            var unresolved = detailed.Instances.Single(i => i.InstanceId == "django__django-1");
            Assert.Equal(InstanceStatus.Unresolved, unresolved.Status);
            Assert.Equal("django/django", unresolved.Repo);
            Assert.False(string.IsNullOrEmpty(unresolved.FailureSummary));

            var empty = detailed.Instances.Single(i => i.InstanceId == "django__django-2");
            Assert.Equal(InstanceStatus.EmptyPatch, empty.Status);
            Assert.Contains("No patch", empty.FailureSummary);

            // Detailed render contains the Failures section.
            Assert.Contains("Why it failed", HtmlReporter.Render(detailed));
        }
        finally
        {
            try { Directory.Delete(runDir, recursive: true); } catch { /* best effort */ }
        }
    }

    private static void WriteEmptyPatch(string evalDir, string id)
    {
        var dir = Path.Combine(evalDir, id);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "report.json"), JsonSerializer.Serialize(new
        {
            instance_id = id, patch_applied = false, test_patch_applied = false,
            empty_patch = true, resolved = false, resolved_status = "No", error = (string?)null, timed_out = false,
        }));
    }

    private static void WritePerInstance(string evalDir, string id, string resolvedStatus)
    {
        var dir = Path.Combine(evalDir, id);
        Directory.CreateDirectory(dir);
        var obj = new
        {
            instance_id = id,
            patch_applied = true,
            test_patch_applied = true,
            empty_patch = false,
            resolved = resolvedStatus == "Full",
            resolved_status = resolvedStatus,
            error = (string?)null,
            timed_out = false,
        };
        File.WriteAllText(Path.Combine(dir, "report.json"), JsonSerializer.Serialize(obj));
    }
}
