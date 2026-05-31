using Xunit;

namespace Andy.Benchmarks;

public class SweBenchCliOptionsTests
{
    [Fact]
    public void AgentTimeout_Defaults_To_1800()
    {
        var ctx = SweBenchCliOptions.Parse(new[] { "--dataset", "x.jsonl" }, "run");
        Assert.Equal(1800, ctx.AgentTimeoutSeconds);
    }

    [Theory]
    [InlineData("60", 60)]
    [InlineData("0", 0)] // disabled
    public void AgentTimeout_Parses_Explicit_Value(string arg, int expected)
    {
        var ctx = SweBenchCliOptions.Parse(new[] { "--dataset", "x.jsonl", "--agent-timeout-seconds", arg }, "run");
        Assert.Equal(expected, ctx.AgentTimeoutSeconds);
    }
}
