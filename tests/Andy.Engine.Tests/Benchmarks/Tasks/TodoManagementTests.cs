using Andy.Engine.Benchmarks.Scenarios.Tasks;
using Andy.Engine.Tests.Benchmarks.Common;
using Xunit;
using Xunit.Abstractions;

namespace Andy.Engine.Tests.Benchmarks.Tasks;

public class TodoManagementTests : IntegrationTestBase
{
    public TodoManagementTests(ITestOutputHelper output) : base(output)
    {
    }

    protected override string GetSystemPrompt() =>
        "You are a task management assistant. When users ask about todos or tasks, use the todo_management tool. After getting results, summarize them clearly.";

    [Theory]
    [LlmTestData]
    public async Task TodoManagement_AddTodo_Success(LlmMode mode)
    {
        var scenario = TodoScenarios.CreateAddTodo();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("todo_management", result.ToolInvocations[0].ToolType);
        }
    }

    [Theory]
    [LlmTestData]
    public async Task TodoManagement_ListTodos_Success(LlmMode mode)
    {
        var scenario = TodoScenarios.CreateListTodos();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);
    }

    [Theory]
    [LlmTestData]
    public async Task TodoManagement_CompleteTodo_Success(LlmMode mode)
    {
        var scenario = TodoScenarios.CreateCompleteTodo();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);
    }

    [Theory]
    [LlmTestData]
    public async Task TodoManagement_SearchTodos_Success(LlmMode mode)
    {
        var scenario = TodoScenarios.CreateSearchTodos();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);
    }

    [Theory]
    [LlmTestData]
    public async Task TodoManagement_RemoveTodo_Success(LlmMode mode)
    {
        var scenario = TodoScenarios.CreateRemoveTodo();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);
    }

    [Theory]
    [LlmTestData]
    public async Task TodoManagement_BatchAdd_Success(LlmMode mode)
    {
        var scenario = TodoScenarios.CreateAddBatchTodos();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);
    }

    [Theory]
    [LlmTestData]
    public async Task TodoManagement_ClearCompleted_Success(LlmMode mode)
    {
        var scenario = TodoScenarios.CreateClearCompletedTodos();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);
    }

    [Theory]
    [LlmTestData]
    public async Task TodoManagement_UpdateProgress_Success(LlmMode mode)
    {
        var scenario = TodoScenarios.CreateUpdateProgress();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("update_progress", result.ToolInvocations[0].Parameters["action"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task TodoManagement_MissingAction_HandlesError(LlmMode mode)
    {
        var scenario = TodoScenarios.CreateMissingAction();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);
    }
}
