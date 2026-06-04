using Andy.Engine.SweBench.Model;
using Andy.Engine.SweBench.Orchestration;
using Xunit;

namespace Andy.Benchmarks;

/// <summary>
/// Tests for the parallel agent-stage support: the checkpoint must stay intact under concurrent
/// appends, and the CLI/context must carry --max-parallel.
/// </summary>
public class ParallelExecutionTests
{
    [Fact]
    public async Task Checkpoint_ConcurrentAppend_WritesEveryLineIntact()
    {
        var path = Path.Combine(Path.GetTempPath(), $"swe-ckpt-{Guid.NewGuid():N}.jsonl");
        var checkpoint = new PredictionCheckpoint(path);
        const int n = 300;

        try
        {
            // Hammer Append from many threads at once — the lock must serialize whole lines so
            // none interleave into a corrupt/truncated JSON line.
            await Task.WhenAll(Enumerable.Range(0, n).Select(i => Task.Run(() =>
                checkpoint.Append(new SwePrediction
                {
                    InstanceId = $"repo__inst-{i}",
                    ModelNameOrPath = "m",
                    ModelPatch = $"diff for {i}\nwith a second line",
                }))));

            // Every line must parse (no interleaving) and every instance id must be present once.
            var map = checkpoint.Read();
            Assert.Equal(n, map.Count);
            for (var i = 0; i < n; i++)
                Assert.True(map.ContainsKey($"repo__inst-{i}"), $"missing inst-{i}");

            // Raw line count must match too (no partial/garbage lines were skipped by Read()).
            var lines = (await File.ReadAllLinesAsync(path)).Count(l => l.Trim().Length > 0);
            Assert.Equal(n, lines);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Cli_ParsesMaxParallel()
    {
        var ctx = SweBenchCliOptions.Parse(new[] { "--dataset", "d.jsonl", "--max-parallel", "5" }, "run-x");
        Assert.Equal(5, ctx.MaxParallel);
    }

    [Fact]
    public void Cli_DefaultsToSequential()
    {
        var ctx = SweBenchCliOptions.Parse(new[] { "--dataset", "d.jsonl" }, "run-x");
        Assert.Equal(1, ctx.MaxParallel);
    }

    [Fact]
    public void RunContext_DefaultMaxParallel_IsOne()
    {
        Assert.Equal(1, new RunContext { DatasetPath = "x" }.MaxParallel);
    }
}
