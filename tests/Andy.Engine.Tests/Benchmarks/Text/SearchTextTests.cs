using Andy.Engine.Benchmarks.Scenarios.Text;
using Andy.Engine.Tests.Benchmarks.Common;
using Xunit;
using Xunit.Abstractions;

namespace Andy.Engine.Tests.Benchmarks.Text;

public class SearchTextTests : TextIntegrationTestBase
{
    public SearchTextTests(ITestOutputHelper output) : base(output)
    {
    }

    [Theory]
    [LlmTestData]
    public async Task SearchText_BasicSearch_Success(LlmMode mode)
    {
        CreateTextTestFiles();
        var scenario = SearchTextScenarios.CreateBasicSearch(TestDirectory);
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("search_text", result.ToolInvocations[0].ToolType);
        }
    }

    [Theory]
    [LlmTestData]
    public async Task SearchText_RegexSearch_Success(LlmMode mode)
    {
        CreateTextTestFiles();
        var scenario = SearchTextScenarios.CreateRegexSearch(TestDirectory);
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
    public async Task SearchText_CaseInsensitive_Success(LlmMode mode)
    {
        CreateTextTestFiles();
        var scenario = SearchTextScenarios.CreateCaseInsensitiveSearch(TestDirectory);
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);
    }

    [Theory]
    [LlmTestData]
    public async Task SearchText_MaxResults_Success(LlmMode mode)
    {
        CreateTextTestFiles();
        var scenario = SearchTextScenarios.CreateMaxResultsSearch(TestDirectory);
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("2", result.ToolInvocations[0].Parameters["max_results"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task SearchText_ContextLines_Success(LlmMode mode)
    {
        CreateTextTestFiles();
        var scenario = SearchTextScenarios.CreateContextLinesSearch(TestDirectory);
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("1", result.ToolInvocations[0].Parameters["context_lines"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task SearchText_WholeWords_Success(LlmMode mode)
    {
        CreateTextTestFiles();
        var scenario = SearchTextScenarios.CreateWholeWordsSearch(TestDirectory);
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
    public async Task SearchText_StartsWith_Success(LlmMode mode)
    {
        CreateTextTestFiles();
        var scenario = SearchTextScenarios.CreateStartsWithSearch(TestDirectory);
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
    public async Task SearchText_EndsWith_Success(LlmMode mode)
    {
        CreateTextTestFiles();
        var scenario = SearchTextScenarios.CreateEndsWithSearch(TestDirectory);
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
    public async Task SearchText_FilePatterns_Success(LlmMode mode)
    {
        CreateTextTestFiles();
        var scenario = SearchTextScenarios.CreateFilePatternsSearch(TestDirectory);
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);
    }

    [Theory]
    [LlmTestData]
    public async Task SearchText_NoMatches_Success(LlmMode mode)
    {
        CreateTextTestFiles();
        var scenario = SearchTextScenarios.CreateSearchNoMatches(TestDirectory);
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);
    }

    [Theory]
    [LlmTestData]
    public async Task SearchText_MissingPattern_HandlesError(LlmMode mode)
    {
        var scenario = SearchTextScenarios.CreateSearchMissingPattern();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);
    }
}
