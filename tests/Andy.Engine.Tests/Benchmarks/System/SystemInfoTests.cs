using Andy.Engine.Benchmarks.Scenarios.SystemTools;
using Andy.Engine.Tests.Benchmarks.Common;
using Xunit;
using Xunit.Abstractions;

namespace Andy.Engine.Tests.Benchmarks.SystemTools;

public class SystemInfoTests : IntegrationTestBase
{
    public SystemInfoTests(ITestOutputHelper output) : base(output)
    {
    }

    protected override string GetSystemPrompt() =>
        "You are a system information assistant. When users ask about system details, use the system_info tool. After getting results, summarize them clearly.";

    [Theory]
    [LlmTestData]
    public async Task SystemInfo_GetAll_Success(LlmMode mode)
    {
        var scenario = SystemInfoScenarios.CreateGetAllInfo();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("system_info", result.ToolInvocations[0].ToolType);
        }
    }

    [Theory]
    [LlmTestData]
    public async Task SystemInfo_GetOs_Success(LlmMode mode)
    {
        var scenario = SystemInfoScenarios.CreateGetOsInfo();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);
    }

    [Theory]
    [LlmTestData]
    public async Task SystemInfo_GetMemory_Success(LlmMode mode)
    {
        var scenario = SystemInfoScenarios.CreateGetMemoryInfo();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);
    }

    [Theory]
    [LlmTestData]
    public async Task SystemInfo_GetCpu_Success(LlmMode mode)
    {
        var scenario = SystemInfoScenarios.CreateGetCpuInfo();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);
    }

    [Theory]
    [LlmTestData]
    public async Task SystemInfo_GetRuntime_Success(LlmMode mode)
    {
        var scenario = SystemInfoScenarios.CreateGetRuntimeInfo();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);
    }

    [Theory]
    [LlmTestData]
    public async Task SystemInfo_GetStorage_Success(LlmMode mode)
    {
        var scenario = SystemInfoScenarios.CreateGetStorageInfo();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);
    }

    [Theory]
    [LlmTestData]
    public async Task SystemInfo_GetDetailed_Success(LlmMode mode)
    {
        var scenario = SystemInfoScenarios.CreateGetDetailedInfo();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);
    }

    [Theory]
    [LlmTestData]
    public async Task SystemInfo_GetMultiCategory_Success(LlmMode mode)
    {
        var scenario = SystemInfoScenarios.CreateGetMultiCategoryInfo();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);
    }
}
