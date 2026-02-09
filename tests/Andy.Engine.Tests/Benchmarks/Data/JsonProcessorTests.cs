using Andy.Engine.Benchmarks.Scenarios.Data;
using Andy.Engine.Tests.Benchmarks.Common;
using Xunit;
using Xunit.Abstractions;

namespace Andy.Engine.Tests.Benchmarks.Data;

public class JsonProcessorTests : IntegrationTestBase
{
    public JsonProcessorTests(ITestOutputHelper output) : base(output)
    {
    }

    protected override string GetSystemPrompt() =>
        "You are a data processing assistant with access to a JSON processor tool. When users ask about JSON operations, use the json_processor tool. After getting results, summarize them clearly.";

    [Theory]
    [LlmTestData]
    public async Task JsonProcessor_Validate_Success(LlmMode mode)
    {
        var scenario = JsonProcessorScenarios.CreateValidateJson();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("json_processor", result.ToolInvocations[0].ToolType);
        }
    }

    [Theory]
    [LlmTestData]
    public async Task JsonProcessor_Format_Success(LlmMode mode)
    {
        var scenario = JsonProcessorScenarios.CreateFormatJson();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("format", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task JsonProcessor_Minify_Success(LlmMode mode)
    {
        var scenario = JsonProcessorScenarios.CreateMinifyJson();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("minify", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task JsonProcessor_Query_Success(LlmMode mode)
    {
        var scenario = JsonProcessorScenarios.CreateQueryJsonPath();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("query", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task JsonProcessor_Flatten_Success(LlmMode mode)
    {
        var scenario = JsonProcessorScenarios.CreateFlattenJson();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);
    }

    [Theory]
    [LlmTestData]
    public async Task JsonProcessor_Merge_Success(LlmMode mode)
    {
        var scenario = JsonProcessorScenarios.CreateMergeJson();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("merge", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task JsonProcessor_Diff_Success(LlmMode mode)
    {
        var scenario = JsonProcessorScenarios.CreateDiffJson();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("diff", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task JsonProcessor_Extract_Success(LlmMode mode)
    {
        var scenario = JsonProcessorScenarios.CreateExtractJson();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("extract", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task JsonProcessor_Transform_Success(LlmMode mode)
    {
        var scenario = JsonProcessorScenarios.CreateTransformJson();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("transform", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task JsonProcessor_Unflatten_Success(LlmMode mode)
    {
        var scenario = JsonProcessorScenarios.CreateUnflattenJson();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("unflatten", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task JsonProcessor_ToCsv_Success(LlmMode mode)
    {
        var scenario = JsonProcessorScenarios.CreateToCsv();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("to_csv", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task JsonProcessor_FromCsv_Success(LlmMode mode)
    {
        var scenario = JsonProcessorScenarios.CreateFromCsv();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("from_csv", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task JsonProcessor_Count_Success(LlmMode mode)
    {
        var scenario = JsonProcessorScenarios.CreateCountJson();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("count", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task JsonProcessor_Statistics_Success(LlmMode mode)
    {
        var scenario = JsonProcessorScenarios.CreateStatisticsJson();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("statistics", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task JsonProcessor_InvalidJson_HandlesError(LlmMode mode)
    {
        var scenario = JsonProcessorScenarios.CreateInvalidJsonValidation();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);
    }
}
