using Andy.Engine.Benchmarks.Scenarios.Network;
using Andy.Engine.Tests.Benchmarks.Common;
using Xunit;
using Xunit.Abstractions;

namespace Andy.Engine.Tests.Benchmarks.Network;

public class HttpRequestTests : IntegrationTestBase
{
    public HttpRequestTests(ITestOutputHelper output) : base(output)
    {
    }

    protected override string GetSystemPrompt() =>
        "You are a network assistant with access to HTTP request tools. When users ask you to make HTTP requests, use the http_request tool. After getting results, summarize the response clearly.";

    [Theory]
    [LlmTestData]
    public async Task HttpRequest_BasicGet_Success(LlmMode mode)
    {
        var scenario = HttpRequestScenarios.CreateBasicGet();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("http_request", result.ToolInvocations[0].ToolType);
            Assert.Equal("GET", result.ToolInvocations[0].Parameters["method"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task HttpRequest_PostWithBody_Success(LlmMode mode)
    {
        var scenario = HttpRequestScenarios.CreatePostWithBody();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("POST", result.ToolInvocations[0].Parameters["method"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task HttpRequest_CustomHeaders_Success(LlmMode mode)
    {
        var scenario = HttpRequestScenarios.CreateCustomHeaders();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("GET", result.ToolInvocations[0].Parameters["method"]?.ToString());
        }
    }
}
