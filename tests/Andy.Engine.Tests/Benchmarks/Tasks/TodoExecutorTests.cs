using Andy.Engine.Benchmarks.Scenarios.Tasks;
using Andy.Engine.Tests.Benchmarks.Common;
using Xunit;
using Xunit.Abstractions;

namespace Andy.Engine.Tests.Benchmarks.Tasks;

public class TodoExecutorTests : IntegrationTestBase
{
    public TodoExecutorTests(ITestOutputHelper output) : base(output)
    {
    }

    protected override string GetSystemPrompt() =>
        "You are a task execution assistant. When users ask about executing or automating todos, use the todo_executor tool. After getting results, summarize them clearly.";

    [Theory]
    [LlmTestData]
    public async Task TodoExecutor_Analyze_Success(LlmMode mode)
    {
        var scenario = TodoScenarios.CreateAnalyzeTodos();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("todo_executor", result.ToolInvocations[0].ToolType);
        }
    }

    [Theory]
    [LlmTestData]
    public async Task TodoExecutor_DryRun_Success(LlmMode mode)
    {
        var scenario = TodoScenarios.CreateDryRunTodos();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);
    }

    [Theory]
    [LlmTestData]
    public async Task TodoExecutor_ExecuteSingle_Success(LlmMode mode)
    {
        var scenario = TodoScenarios.CreateExecuteSingle();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);
    }

    [Theory]
    [LlmTestData]
    public async Task TodoExecutor_ExecuteAll_Success(LlmMode mode)
    {
        var scenario = TodoScenarios.CreateExecuteAll();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("execute_all", result.ToolInvocations[0].Parameters["action"]?.ToString());
        }
    }
}
