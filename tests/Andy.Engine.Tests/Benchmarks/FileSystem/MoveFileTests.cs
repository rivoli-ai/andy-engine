using Andy.Benchmarks.Framework;
using Andy.Benchmarks.Validators;
using Andy.Engine.Benchmarks.Framework;
using Andy.Engine.Benchmarks.Scenarios.FileSystem;
using Xunit;
using Xunit.Abstractions;

namespace Andy.Engine.Tests.Benchmarks.FileSystem;

/// <summary>
/// Integration tests for move_file tool via the engine
/// Executes scenarios through the Agent with both mocked and real LLM
/// </summary>
public class MoveFileTests : FileSystemIntegrationTestBase
{
    public MoveFileTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public async Task MoveFile_BasicMove_WithMockedLlm_Success()
    {
        // Arrange
        CreateTestFile("moveme.txt", "Content to move");
        var scenario = MoveFileScenarios.CreateBasicFileMove(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.Single(result.ToolInvocations);
        Assert.Equal("move_file", result.ToolInvocations[0].ToolType);

        // Verify file was moved
        var sourceFile = Path.Combine(TestDirectory, "moveme.txt");
        var destFile = Path.Combine(TestDirectory, "moved.txt");
        Assert.False(File.Exists(sourceFile));
        Assert.True(File.Exists(destFile));
        Assert.Equal("Content to move", File.ReadAllText(destFile));
    }

    [Fact]
    public async Task MoveFile_BasicMove_WithRealLlm_Success()
    {
        // Arrange
        CreateTestFile("moveme.txt", "Content to move");
        var scenario = MoveFileScenarios.CreateBasicFileMove(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify file was moved
        var sourceFile = Path.Combine(TestDirectory, "moveme.txt");
        var destFile = Path.Combine(TestDirectory, "moved.txt");
        Assert.False(File.Exists(sourceFile));
        Assert.True(File.Exists(destFile));
    }

    [Fact]
    public async Task MoveFile_WithOverwrite_WithMockedLlm_Success()
    {
        // Arrange
        CreateTestFile("moveme.txt", "New content");
        CreateTestFile("existing.txt", "Old content");
        var scenario = MoveFileScenarios.CreateMoveWithOverwrite(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("overwrite"));

        // Verify file was moved and overwritten
        var sourceFile = Path.Combine(TestDirectory, "moveme.txt");
        var destFile = Path.Combine(TestDirectory, "existing.txt");
        Assert.False(File.Exists(sourceFile));
        Assert.Equal("New content", File.ReadAllText(destFile));
    }

    [Fact]
    public async Task MoveFile_WithOverwrite_WithRealLlm_Success()
    {
        // Arrange
        CreateTestFile("moveme.txt", "New content");
        CreateTestFile("existing.txt", "Old content");
        var scenario = MoveFileScenarios.CreateMoveWithOverwrite(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify file was moved and overwritten
        var sourceFile = Path.Combine(TestDirectory, "moveme.txt");
        var destFile = Path.Combine(TestDirectory, "existing.txt");
        Assert.False(File.Exists(sourceFile));
    }
}
