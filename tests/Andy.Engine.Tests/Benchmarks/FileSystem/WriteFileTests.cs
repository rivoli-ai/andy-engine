using Andy.Benchmarks.Framework;
using Andy.Benchmarks.Validators;
using Andy.Engine.Benchmarks.Framework;
using Andy.Engine.Benchmarks.Scenarios.FileSystem;
using Xunit;
using Xunit.Abstractions;

namespace Andy.Engine.Tests.Benchmarks.FileSystem;

/// <summary>
/// Integration tests for write_file tool via the engine
/// Executes scenarios through the Agent with both mocked and real LLM
/// </summary>
public class WriteFileTests : FileSystemIntegrationTestBase
{
    public WriteFileTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public async Task WriteFile_BasicWrite_WithMockedLlm_Success()
    {
        // Arrange
        var scenario = WriteFileScenarios.CreateBasicFileWrite(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.Single(result.ToolInvocations);
        Assert.Equal("write_file", result.ToolInvocations[0].ToolType);

        // Verify file was created
        var newFile = Path.Combine(TestDirectory, "newfile.txt");
        Assert.True(File.Exists(newFile));
        Assert.Equal("Hello, World!", File.ReadAllText(newFile));
    }

    [Fact]
    public async Task WriteFile_BasicWrite_WithRealLlm_Success()
    {
        // Arrange
        var scenario = WriteFileScenarios.CreateBasicFileWrite(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify file was created
        var newFile = Path.Combine(TestDirectory, "newfile.txt");
        Assert.True(File.Exists(newFile));
    }

    [Fact]
    public async Task WriteFile_AppendMode_WithMockedLlm_Success()
    {
        // Arrange
        CreateTestFile("append.txt", "Initial content\n");
        var scenario = WriteFileScenarios.CreateAppendToFile(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("mode"));

        // Verify content was appended
        var appendFile = Path.Combine(TestDirectory, "append.txt");
        var content = File.ReadAllText(appendFile);
        Assert.Contains("Initial content", content);
        Assert.Contains("Additional line", content);
    }

    [Fact]
    public async Task WriteFile_AppendMode_WithRealLlm_Success()
    {
        // Arrange
        CreateTestFile("append.txt", "Initial content\n");
        var scenario = WriteFileScenarios.CreateAppendToFile(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify content was appended
        var appendFile = Path.Combine(TestDirectory, "append.txt");
        var content = File.ReadAllText(appendFile);
        Assert.Contains("Initial content", content);
    }
}
