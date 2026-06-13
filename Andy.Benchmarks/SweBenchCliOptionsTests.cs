using Xunit;

namespace Andy.Benchmarks;

public class SweBenchCliOptionsTests
{
    [Fact]
    public void Limit_Defaults_Are_Generous_For_Large_Context_Models()
    {
        var ctx = SweBenchCliOptions.Parse(new[] { "--dataset", "x.jsonl" }, "run");
        Assert.Equal(50, ctx.MaxTurns);
        Assert.Equal(32_768, ctx.MaxOutputTokens);
        Assert.Equal(1_000_000, ctx.MaxContextTokens);
        Assert.Equal(100_000, ctx.MaxToolResultChars);
    }

    [Fact]
    public void Limits_Can_Be_Tightened_For_Constrained_Models()
    {
        var ctx = SweBenchCliOptions.Parse(
            new[] { "--dataset", "x.jsonl", "--max-context-tokens", "32000", "--max-tool-result-chars", "8000" }, "run");
        Assert.Equal(32000, ctx.MaxContextTokens);
        Assert.Equal(8000, ctx.MaxToolResultChars);
    }

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
