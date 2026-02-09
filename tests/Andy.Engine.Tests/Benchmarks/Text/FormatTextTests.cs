using Andy.Engine.Benchmarks.Scenarios.Text;
using Andy.Engine.Tests.Benchmarks.Common;
using Xunit;
using Xunit.Abstractions;

namespace Andy.Engine.Tests.Benchmarks.Text;

public class FormatTextTests : IntegrationTestBase
{
    public FormatTextTests(ITestOutputHelper output) : base(output)
    {
    }

    protected override string GetSystemPrompt() =>
        "You are a text formatting assistant. When users ask you to format or transform text, use the format_text tool. After getting results, summarize them clearly.";

    [Theory]
    [LlmTestData]
    public async Task FormatText_Trim_Success(LlmMode mode)
    {
        var scenario = FormatTextScenarios.CreateTrimWhitespace();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("format_text", result.ToolInvocations[0].ToolType);
        }
    }

    [Theory]
    [LlmTestData]
    public async Task FormatText_Upper_Success(LlmMode mode)
    {
        var scenario = FormatTextScenarios.CreateUpperCase();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("upper", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task FormatText_Lower_Success(LlmMode mode)
    {
        var scenario = FormatTextScenarios.CreateLowerCase();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("lower", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task FormatText_SnakeCase_Success(LlmMode mode)
    {
        var scenario = FormatTextScenarios.CreateSnakeCase();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("snake", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task FormatText_TitleCase_Success(LlmMode mode)
    {
        var scenario = FormatTextScenarios.CreateTitleCase();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("title", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task FormatText_CamelCase_Success(LlmMode mode)
    {
        var scenario = FormatTextScenarios.CreateCamelCase();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("camel", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task FormatText_KebabCase_Success(LlmMode mode)
    {
        var scenario = FormatTextScenarios.CreateKebabCase();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("kebab", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task FormatText_Reverse_Success(LlmMode mode)
    {
        var scenario = FormatTextScenarios.CreateReverse();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("reverse", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task FormatText_CountWords_Success(LlmMode mode)
    {
        var scenario = FormatTextScenarios.CreateCountWords();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("count_words", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task FormatText_PascalCase_Success(LlmMode mode)
    {
        var scenario = FormatTextScenarios.CreatePascalCase();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("pascal", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task FormatText_CountChars_Success(LlmMode mode)
    {
        var scenario = FormatTextScenarios.CreateCountChars();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("count_chars", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task FormatText_SortLines_Success(LlmMode mode)
    {
        var scenario = FormatTextScenarios.CreateSortLines();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("sort_lines", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task FormatText_RemoveDuplicates_Success(LlmMode mode)
    {
        var scenario = FormatTextScenarios.CreateRemoveDuplicates();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("remove_duplicates", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task FormatText_RemoveEmptyLines_Success(LlmMode mode)
    {
        var scenario = FormatTextScenarios.CreateRemoveEmptyLines();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("remove_empty_lines", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task FormatText_NormalizeWhitespace_Success(LlmMode mode)
    {
        var scenario = FormatTextScenarios.CreateNormalizeWhitespace();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("normalize_whitespace", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task FormatText_WordWrap_Success(LlmMode mode)
    {
        var scenario = FormatTextScenarios.CreateWordWrap();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("word_wrap", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task FormatText_ExtractNumbers_Success(LlmMode mode)
    {
        var scenario = FormatTextScenarios.CreateExtractNumbers();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("extract_numbers", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task FormatText_ExtractEmails_Success(LlmMode mode)
    {
        var scenario = FormatTextScenarios.CreateExtractEmails();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("extract_emails", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task FormatText_ExtractUrls_Success(LlmMode mode)
    {
        var scenario = FormatTextScenarios.CreateExtractUrls();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("extract_urls", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task FormatText_MissingInput_HandlesError(LlmMode mode)
    {
        var scenario = FormatTextScenarios.CreateMissingInput();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);
    }
}
