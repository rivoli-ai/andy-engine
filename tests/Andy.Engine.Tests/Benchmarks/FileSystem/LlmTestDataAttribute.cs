using System.Reflection;
using Xunit.Sdk;

namespace Andy.Engine.Tests.Benchmarks.FileSystem;

/// <summary>
/// Specifies which LLM mode to use for testing
/// </summary>
public enum LlmMode
{
    /// <summary>
    /// Use mocked LLM (fast, deterministic)
    /// </summary>
    Mock,

    /// <summary>
    /// Use real LLM from configuration (comprehensive)
    /// </summary>
    Real
}

/// <summary>
/// Provides test data for both Mocked and Real LLM modes.
/// Tests will appear as separate cases in test runners.
/// </summary>
public class LlmTestDataAttribute : DataAttribute
{
    private readonly bool _includeMock;
    private readonly bool _includeReal;

    /// <summary>
    /// Provides test cases for both Mock and Real LLM
    /// </summary>
    /// <param name="includeMock">Include Mock LLM test case (default: true)</param>
    /// <param name="includeReal">Include Real LLM test case (default: true)</param>
    public LlmTestDataAttribute(bool includeMock = true, bool includeReal = true)
    {
        _includeMock = includeMock;
        _includeReal = includeReal;
    }

    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        if (_includeMock)
            yield return new object[] { LlmMode.Mock };

        if (_includeReal)
            yield return new object[] { LlmMode.Real };
    }
}
