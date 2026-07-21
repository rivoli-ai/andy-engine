using System.Reflection;
using Xunit.Sdk;

namespace Andy.Engine.Tests.Benchmarks.Common;

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
///
/// The Mock case is the default deterministic suite and always runs. The Real (live-LLM) case is
/// OPT-IN: it is emitted only when the <see cref="LiveTestsEnvVar"/> environment variable is set
/// (in addition to a configured API key). Presence of an API key alone never authorizes live
/// tests, so a plain <c>dotnet test</c> — even on a machine that happens to have credentials —
/// makes no LLM network calls and cannot fail stochastically on model-output variation.
///
/// To run the live suite intentionally (this makes paid network calls and its outputs vary):
/// <code>ANDY_LIVE_LLM_TESTS=1 OPENAI_API_KEY=sk-... dotnet test</code>
/// </summary>
public class LlmTestDataAttribute : DataAttribute
{
    /// <summary>
    /// Environment variable that opts a run in to the live-LLM cases. Accepted truthy values:
    /// <c>1</c>, <c>true</c>, <c>yes</c> (case-insensitive). Anything else (including unset) keeps
    /// the run on the Mock-only deterministic suite.
    /// </summary>
    public const string LiveTestsEnvVar = "ANDY_LIVE_LLM_TESTS";

    private readonly bool _includeMock;
    private readonly bool _includeReal;

    /// <summary>
    /// Provides test cases for both Mock and Real LLM
    /// </summary>
    /// <param name="includeMock">Include Mock LLM test case (default: true)</param>
    /// <param name="includeReal">Include Real LLM test case when opted in (default: true)</param>
    public LlmTestDataAttribute(bool includeMock = true, bool includeReal = true)
    {
        _includeMock = includeMock;
        _includeReal = includeReal;
    }

    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        if (_includeMock)
            yield return new object[] { LlmMode.Mock };

        // Live cases require BOTH an explicit opt-in and a configured key. The opt-in is what keeps
        // credentialed-but-not-opted-in runs (CI, local dev) fully offline.
        if (_includeReal && IsLiveOptIn() && IsRealLlmAvailable())
            yield return new object[] { LlmMode.Real };
    }

    /// <summary>
    /// True when the run has explicitly opted in to live-LLM tests via <see cref="LiveTestsEnvVar"/>.
    /// </summary>
    public static bool IsLiveOptIn()
    {
        var value = Environment.GetEnvironmentVariable(LiveTestsEnvVar);
        return !string.IsNullOrWhiteSpace(value)
            && (value.Equals("1", StringComparison.Ordinal)
                || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks if real LLM credentials are available (API keys configured). Availability alone does
    /// NOT authorize live tests — see <see cref="IsLiveOptIn"/>.
    /// </summary>
    private static bool IsRealLlmAvailable()
    {
        var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrEmpty(openAiKey))
            return true;

        var anthropicKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (!string.IsNullOrEmpty(anthropicKey))
            return true;

        return false;
    }
}
