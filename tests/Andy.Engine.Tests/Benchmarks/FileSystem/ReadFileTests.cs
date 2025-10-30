using Andy.Benchmarks.Framework;
using Andy.Benchmarks.Validators;
using Andy.Engine.Benchmarks.Framework;
using Andy.Engine.Benchmarks.Scenarios.FileSystem;
using Xunit;
using Xunit.Abstractions;

namespace Andy.Engine.Tests.Benchmarks.FileSystem;

/// <summary>
/// Integration tests for read_file tool via the engine
/// Executes scenarios through the Agent with both mocked and real LLM
/// </summary>
public class ReadFileTests : FileSystemIntegrationTestBase
{
    public ReadFileTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public async Task ReadFile_BasicRead_WithMockedLlm_Success()
    {
        // Arrange
        CreateTestFileStructure();
        var scenario = ReadFileScenarios.CreateBasicFileRead(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.Single(result.ToolInvocations);
        Assert.Equal("read_file", result.ToolInvocations[0].ToolType);
    }

    [Fact]
    public async Task ReadFile_BasicRead_WithRealLlm_Success()
    {
        // Arrange
        CreateTestFileStructure();
        var scenario = ReadFileScenarios.CreateBasicFileRead(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task ReadFile_JsonFile_WithMockedLlm_Success()
    {
        // Arrange
        CreateTestFileStructure();
        var scenario = ReadFileScenarios.CreateBinaryFileRead(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.Single(result.ToolInvocations);
    }

    [Fact]
    public async Task ReadFile_JsonFile_WithRealLlm_Success()
    {
        // Arrange
        CreateTestFileStructure();
        var scenario = ReadFileScenarios.CreateBinaryFileRead(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }
}
