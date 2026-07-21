using System.Reflection;
using Xunit;

namespace Andy.Engine.Tests.Benchmarks.Common;

/// <summary>
/// Verifies that live-LLM cases are strictly opt-in: a plain run (no <c>ANDY_LIVE_LLM_TESTS</c>)
/// yields only the deterministic Mock case, even when API keys are present.
/// </summary>
[Collection("LlmTestDataEnv")] // serialize: these mutate a process-wide environment variable
public class LlmTestDataAttributeTests
{
    private static readonly MethodInfo AnyMethod = typeof(LlmTestDataAttributeTests)
        .GetMethod(nameof(Placeholder), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private void Placeholder(LlmMode mode) { }

    private static IReadOnlyList<LlmMode> Modes(LlmTestDataAttribute attr) =>
        attr.GetData(AnyMethod).Select(row => (LlmMode)row[0]).ToList();

    [Fact]
    public void WithoutOptIn_YieldsMockOnly_EvenWhenKeyPresent()
    {
        WithEnv(LlmTestDataAttribute.LiveTestsEnvVar, null, () =>
        WithEnv("OPENAI_API_KEY", "sk-test", () =>
        {
            var modes = Modes(new LlmTestDataAttribute());
            Assert.Equal(new[] { LlmMode.Mock }, modes);
            Assert.False(LlmTestDataAttribute.IsLiveOptIn());
        }));
    }

    [Fact]
    public void OptInWithoutKey_StillYieldsMockOnly()
    {
        WithEnv(LlmTestDataAttribute.LiveTestsEnvVar, "1", () =>
        WithEnv("OPENAI_API_KEY", null, () =>
        WithEnv("ANTHROPIC_API_KEY", null, () =>
        {
            Assert.True(LlmTestDataAttribute.IsLiveOptIn());
            Assert.Equal(new[] { LlmMode.Mock }, Modes(new LlmTestDataAttribute()));
        })));
    }

    [Fact]
    public void OptInWithKey_YieldsMockAndReal()
    {
        WithEnv(LlmTestDataAttribute.LiveTestsEnvVar, "true", () =>
        WithEnv("OPENAI_API_KEY", "sk-test", () =>
        {
            var modes = Modes(new LlmTestDataAttribute());
            Assert.Contains(LlmMode.Mock, modes);
            Assert.Contains(LlmMode.Real, modes);
        }));
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("true", true)]
    [InlineData("YES", true)]
    [InlineData("0", false)]
    [InlineData("off", false)]
    [InlineData("", false)]
    public void IsLiveOptIn_ParsesTruthyValues(string value, bool expected)
    {
        WithEnv(LlmTestDataAttribute.LiveTestsEnvVar, value, () =>
            Assert.Equal(expected, LlmTestDataAttribute.IsLiveOptIn()));
    }

    private static void WithEnv(string name, string? value, Action body)
    {
        var previous = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
        try { body(); }
        finally { Environment.SetEnvironmentVariable(name, previous); }
    }
}
