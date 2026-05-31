using Andy.Engine.SweBench.Model;
using Andy.Engine.SweBench.Orchestration;
using Xunit;

namespace Andy.Benchmarks;

/// <summary>
/// Resume/checkpoint robustness: predictions.jsonl is appended incrementally and a run can be
/// killed mid-write, leaving a truncated or partly-corrupt final line. Reading it must not throw.
/// </summary>
public class PredictionCheckpointTests
{
    [Fact]
    public void Read_Skips_Corrupt_And_Partial_Lines_But_Keeps_Valid_Ones()
    {
        var path = Path.Combine(Path.GetTempPath(), "swebckpt_" + Guid.NewGuid().ToString("N") + ".jsonl");
        try
        {
            File.WriteAllLines(path, new[]
            {
                """{"instance_id":"a__a-1","model_name_or_path":"m","model_patch":"diff1"}""",
                "",                                                   // blank line
                "{not valid json at all",                            // garbage
                """{"model_name_or_path":"m","model_patch":"d"}""",  // missing required instance_id
                """{"instance_id":"b__b-2","model_name_or_path":"m","model_patch":"diff2"}""",
                """{"instance_id":"c__c-3","model_name_or_path":"m","model_patch":"trunc"""  // truncated final line
            });

            var map = new PredictionCheckpoint(path).Read();

            // Only the two well-formed lines survive; nothing throws.
            Assert.Equal(2, map.Count);
            Assert.Equal("diff1", map["a__a-1"].ModelPatch);
            Assert.Equal("diff2", map["b__b-2"].ModelPatch);
            Assert.False(map.ContainsKey("c__c-3"));
        }
        finally
        {
            try { File.Delete(path); } catch { /* best effort */ }
        }
    }

    [Fact]
    public void Read_LastWriteWins_For_Duplicate_InstanceIds()
    {
        var path = Path.Combine(Path.GetTempPath(), "swebckpt_" + Guid.NewGuid().ToString("N") + ".jsonl");
        try
        {
            var cp = new PredictionCheckpoint(path);
            cp.Append(new SwePrediction { InstanceId = "x__x-1", ModelNameOrPath = "m", ModelPatch = "old" });
            cp.Append(new SwePrediction { InstanceId = "x__x-1", ModelNameOrPath = "m", ModelPatch = "new" });

            var map = cp.Read();
            Assert.Single(map);
            Assert.Equal("new", map["x__x-1"].ModelPatch);
        }
        finally
        {
            try { File.Delete(path); } catch { /* best effort */ }
        }
    }
}
