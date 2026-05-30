using Andy.Engine.SweBench.Dataset;
using Andy.Engine.SweBench.Orchestration;
using Andy.Engine.SweBench.Reporting;
using Xunit;

namespace Andy.Benchmarks;

/// <summary>
/// M0 smoke tests. The dataset/selection/reporting paths need neither Docker nor an LLM,
/// so they run everywhere. Docker/LLM-dependent tests are added in M1/M2 and are skipped
/// when their prerequisites are absent (mirroring the "Real LLM" skip idiom).
/// </summary>
public class SweBenchSmokeTests
{
    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "TestData", "swebench_fixture.jsonl");

    [Fact]
    public void DatasetLoader_Parses_And_JsonDecodes_TestLists()
    {
        var instances = new SweBenchDatasetLoader().LoadFromFile(FixturePath);

        Assert.Equal(2, instances.Count);

        var widget = instances[0];
        Assert.Equal("acme__widget-101", widget.InstanceId);
        Assert.Equal("acme/widget", widget.Repo);
        Assert.Equal("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", widget.BaseCommit);
        Assert.Equal("1.0", widget.Version);
        Assert.Equal("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", widget.EnvironmentSetupCommit);

        // FAIL_TO_PASS / PASS_TO_PASS were JSON-ENCODED STRINGS -> must decode to lists.
        Assert.Equal(new[] { "tests/test_widget.py::test_parse_empty" }, widget.FailToPass);
        Assert.Equal(
            new[] { "tests/test_widget.py::test_parse_basic", "tests/test_widget.py::test_parse_nested" },
            widget.PassToPass);

        // Answer key is loaded but must be present only for the grader.
        Assert.Contains("return None", widget.GoldPatch);
        Assert.Contains("test_parse_empty", widget.TestPatch);

        // Second row encodes the lists as real JSON arrays; both shapes must work.
        var gadget = instances[1];
        Assert.Equal("acme__gadget-202", gadget.InstanceId);
        Assert.Null(gadget.EnvironmentSetupCommit);
        Assert.Equal(new[] { "tests/test_gadget.py::test_range_upper_bound" }, gadget.FailToPass);
        Assert.Equal(new[] { "tests/test_gadget.py::test_range_zero" }, gadget.PassToPass);
    }

    [Fact]
    public void SubsetSelector_Caps_And_Filters_By_Id()
    {
        var instances = new SweBenchDatasetLoader().LoadFromFile(FixturePath);

        var capped = new SubsetSelector { MaxInstances = 1 }.Select(instances);
        Assert.Single(capped);
        Assert.Equal("acme__widget-101", capped[0].InstanceId);

        var byId = new SubsetSelector { InstanceIds = new[] { "acme__gadget-202" } }.Select(instances);
        Assert.Single(byId);
        Assert.Equal("acme__gadget-202", byId[0].InstanceId);

        Assert.Throws<InvalidOperationException>(() =>
            new SubsetSelector { InstanceIds = new[] { "does-not-exist" } }.Select(instances));
    }

    [Fact]
    public async Task DryRun_Stage_None_Writes_WellFormed_Report()
    {
        var runDirRoot = Path.Combine(Path.GetTempPath(), "swebench-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var ctx = new RunContext
            {
                DatasetPath = FixturePath,
                Subset = new SubsetSelector { MaxInstances = 2 },
                Stage = RunStage.None,
                WorkDir = runDirRoot,
                RunId = "dry",
                Reporters = new[] { "json" },
            };

            var sw = new StringWriter();
            var exit = await new SweBenchRunner(ctx, sw).RunAsync();

            Assert.Equal(0, exit);

            var reportPath = Path.Combine(ctx.RunDir, "report.json");
            Assert.True(File.Exists(reportPath), "report.json should be written");

            var report = new SweBenchDatasetLoaderReportProbe(reportPath);
            Assert.Equal(2, report.TotalInstances);
            Assert.Equal(0, report.SubmittedInstances);
        }
        finally
        {
            if (Directory.Exists(runDirRoot))
                Directory.Delete(runDirRoot, recursive: true);
        }
    }

    /// <summary>Tiny helper to read back a couple of fields from report.json without coupling to internals.</summary>
    private sealed class SweBenchDatasetLoaderReportProbe
    {
        public int TotalInstances { get; }
        public int SubmittedInstances { get; }

        public SweBenchDatasetLoaderReportProbe(string path)
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            TotalInstances = doc.RootElement.GetProperty("total_instances").GetInt32();
            SubmittedInstances = doc.RootElement.GetProperty("submitted_instances").GetInt32();
        }
    }
}
