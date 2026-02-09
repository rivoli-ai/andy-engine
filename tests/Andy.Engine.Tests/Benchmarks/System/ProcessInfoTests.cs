using Andy.Engine.Benchmarks.Scenarios.SystemTools;
using Andy.Engine.Tests.Benchmarks.Common;
using Xunit;
using Xunit.Abstractions;

namespace Andy.Engine.Tests.Benchmarks.SystemTools;

public class ProcessInfoTests : IntegrationTestBase
{
    public ProcessInfoTests(ITestOutputHelper output) : base(output)
    {
    }

    protected override string GetSystemPrompt() =>
        "You are a system information assistant. When users ask about processes, use the process_info tool. After getting results, summarize them clearly.";

    [Theory]
    [LlmTestData]
    public async Task ProcessInfo_GetCurrent_Success(LlmMode mode)
    {
        var scenario = ProcessInfoScenarios.CreateGetCurrentProcess();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("process_info", result.ToolInvocations[0].ToolType);
        }
    }

    [Theory]
    [LlmTestData]
    public async Task ProcessInfo_ListProcesses_Success(LlmMode mode)
    {
        var scenario = ProcessInfoScenarios.CreateListProcesses();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);
    }

    [Theory]
    [LlmTestData]
    public async Task ProcessInfo_FindByName_Success(LlmMode mode)
    {
        var scenario = ProcessInfoScenarios.CreateFindProcessByName();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);
    }

    [Theory]
    [LlmTestData]
    public async Task ProcessInfo_SortByMemory_Success(LlmMode mode)
    {
        var scenario = ProcessInfoScenarios.CreateSortByMemory();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);
    }

    [Theory]
    [LlmTestData]
    public async Task ProcessInfo_SortByCpu_Success(LlmMode mode)
    {
        var scenario = ProcessInfoScenarios.CreateSortByCpu();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);
    }
}
