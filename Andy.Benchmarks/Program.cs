using System.Text.Json;
using Andy.Benchmarks;
using Andy.Engine.SweBench.Model;
using Andy.Engine.SweBench.Orchestration;
using Andy.Engine.SweBench.Reporting;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || SweBenchCliOptions.WantsHelp(args))
        {
            Console.WriteLine(SweBenchCliOptions.Usage);
            return args.Length == 0 ? 1 : 0;
        }

        // Render-only mode: turn an existing report.json into report.html. No dataset, Docker,
        // or LLM — so a finished run can be presented without re-running anything.
        var renderIdx = Array.IndexOf(args, "--render-report");
        if (renderIdx >= 0)
        {
            if (renderIdx + 1 >= args.Length)
            {
                Console.Error.WriteLine("--render-report requires a path to a report.json");
                return 64;
            }
            var reportPath = args[renderIdx + 1];
            var outIdx = Array.IndexOf(args, "--out");
            try
            {
                // A directory => consolidate the whole run from its on-disk artifacts (handles
                // batched/--resume runs whose top-level report.json is only the last batch).
                // A file => render that report.json directly.
                if (Directory.Exists(reportPath))
                {
                    // Run dir: consolidate all batches + enrich per-instance from the dataset
                    // (task gist, which target tests failed) when --dataset is given.
                    var dsIdx = Array.IndexOf(args, "--dataset");
                    var datasetPath = dsIdx >= 0 && dsIdx + 1 < args.Length ? args[dsIdx + 1] : null;
                    var detailed = ReportConsolidator.ConsolidateDetailed(reportPath, datasetPath);
                    var outPath = outIdx >= 0 && outIdx + 1 < args.Length
                        ? args[outIdx + 1]
                        : Path.Combine(reportPath, "report.html");
                    Console.WriteLine($"Wrote {HtmlReporter.Write(detailed, outPath)}  "
                                    + $"({detailed.Summary.ResolvedInstances}/{detailed.Summary.TotalInstances} resolved)");
                }
                else
                {
                    var json = await File.ReadAllTextAsync(reportPath);
                    var report = JsonSerializer.Deserialize<SweRunReport>(json)
                        ?? throw new InvalidOperationException("report.json deserialized to null");
                    var dir = Path.GetDirectoryName(Path.GetFullPath(reportPath)) ?? ".";
                    var outPath = outIdx >= 0 && outIdx + 1 < args.Length ? args[outIdx + 1] : Path.Combine(dir, "report.html");
                    Console.WriteLine($"Wrote {HtmlReporter.Write(report, outPath)}  "
                                    + $"({report.ResolvedInstances}/{report.TotalInstances} resolved)");
                }
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Render failed: {ex.Message}");
                return 1;
            }
        }

        RunContext ctx;
        try
        {
            var defaultRunId = $"run-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
            ctx = SweBenchCliOptions.Parse(args, defaultRunId);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Argument error: {ex.Message}");
            Console.Error.WriteLine();
            Console.Error.WriteLine(SweBenchCliOptions.Usage);
            return 64; // EX_USAGE
        }

        try
        {
            var runner = new SweBenchRunner(ctx);
            return await runner.RunAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Run failed: {ex.Message}");
            return 1;
        }
    }
}
