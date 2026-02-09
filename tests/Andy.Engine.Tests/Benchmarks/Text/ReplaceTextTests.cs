using Andy.Engine.Benchmarks.Scenarios.Text;
using Andy.Engine.Tests.Benchmarks.Common;
using Xunit;
using Xunit.Abstractions;

namespace Andy.Engine.Tests.Benchmarks.Text;

public class ReplaceTextTests : TextIntegrationTestBase
{
    public ReplaceTextTests(ITestOutputHelper output) : base(output)
    {
    }

    [Theory]
    [LlmTestData]
    public async Task ReplaceText_SimpleReplace_Success(LlmMode mode)
    {
        CreateTextTestFiles();
        var scenario = ReplaceTextScenarios.CreateSimpleReplace(TestDirectory);
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("replace_text", result.ToolInvocations[0].ToolType);
        }
    }

    [Theory]
    [LlmTestData]
    public async Task ReplaceText_RegexReplace_Success(LlmMode mode)
    {
        CreateTextTestFiles();
        var scenario = ReplaceTextScenarios.CreateRegexReplace(TestDirectory);
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("regex", result.ToolInvocations[0].Parameters["search_type"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task ReplaceText_DryRun_Success(LlmMode mode)
    {
        CreateTextTestFiles();
        var scenario = ReplaceTextScenarios.CreateDryRunReplace(TestDirectory);
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);
    }

    [Theory]
    [LlmTestData]
    public async Task ReplaceText_WholeWords_Success(LlmMode mode)
    {
        CreateTextTestFiles();
        var scenario = ReplaceTextScenarios.CreateWholeWordsReplace(TestDirectory);
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("true", result.ToolInvocations[0].Parameters["whole_words_only"]?.ToString().ToLower());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task ReplaceText_ExactMatch_Success(LlmMode mode)
    {
        CreateTextTestFiles();
        var scenario = ReplaceTextScenarios.CreateExactReplace(TestDirectory);
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("exact", result.ToolInvocations[0].Parameters["search_type"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task ReplaceText_StartsWith_Success(LlmMode mode)
    {
        CreateTextTestFiles();
        var scenario = ReplaceTextScenarios.CreateStartsWithReplace(TestDirectory);
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("starts_with", result.ToolInvocations[0].Parameters["search_type"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task ReplaceText_EndsWith_Success(LlmMode mode)
    {
        CreateTextTestFiles();
        var scenario = ReplaceTextScenarios.CreateEndsWithReplace(TestDirectory);
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("ends_with", result.ToolInvocations[0].Parameters["search_type"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task ReplaceText_FilePatterns_Success(LlmMode mode)
    {
        CreateTextTestFiles();
        var scenario = ReplaceTextScenarios.CreateFilePatternsReplace(TestDirectory);
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);
    }

    [Theory]
    [LlmTestData]
    public async Task ReplaceText_Backup_Success(LlmMode mode)
    {
        CreateTextTestFiles();
        var scenario = ReplaceTextScenarios.CreateBackupReplace(TestDirectory);
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("true", result.ToolInvocations[0].Parameters["create_backup"]?.ToString().ToLower());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task ReplaceText_MissingPattern_HandlesError(LlmMode mode)
    {
        var scenario = ReplaceTextScenarios.CreateReplaceMissingPattern();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);
    }
}
