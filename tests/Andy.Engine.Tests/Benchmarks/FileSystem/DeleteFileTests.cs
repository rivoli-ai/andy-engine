using Andy.Benchmarks.Framework;
using Andy.Benchmarks.Validators;
using Andy.Engine.Benchmarks.Framework;
using Andy.Engine.Benchmarks.Scenarios.FileSystem;
using Xunit;
using Xunit.Abstractions;

namespace Andy.Engine.Tests.Benchmarks.FileSystem;

/// <summary>
/// Integration tests for delete_file tool via the engine
/// Executes scenarios through the Agent with both mocked and real LLM
/// </summary>
public class DeleteFileTests : FileSystemIntegrationTestBase
{
    public DeleteFileTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public async Task DeleteFile_BasicDeletion_WithMockedLlm_Success()
    {
        // Arrange
        CreateTestFile("deleteme.txt", "Content to delete");
        var scenario = DeleteFileScenarios.CreateBasicFileDeletion(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.Single(result.ToolInvocations);
        Assert.Equal("delete_file", result.ToolInvocations[0].ToolType);

        // Verify file was deleted
        var deletedFile = Path.Combine(TestDirectory, "deleteme.txt");
        Assert.False(File.Exists(deletedFile));
    }

    [Fact]
    public async Task DeleteFile_BasicDeletion_WithRealLlm_Success()
    {
        // Arrange
        CreateTestFile("deleteme.txt", "Content to delete");
        var scenario = DeleteFileScenarios.CreateBasicFileDeletion(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify file was deleted
        var deletedFile = Path.Combine(TestDirectory, "deleteme.txt");
        Assert.False(File.Exists(deletedFile));
    }

    [Fact]
    public async Task DeleteFile_RecursiveDirectory_WithMockedLlm_Success()
    {
        // Arrange
        CreateTestDirectory("delete_dir");
        CreateTestFile("delete_dir/file1.txt", "File 1");
        CreateTestFile("delete_dir/file2.txt", "File 2");
        CreateTestDirectory("delete_dir/subdir");
        CreateTestFile("delete_dir/subdir/file3.txt", "File 3");
        var scenario = DeleteFileScenarios.CreateRecursiveDirectoryDeletion(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("recursive"));

        // Verify directory was deleted
        var deletedDir = Path.Combine(TestDirectory, "delete_dir");
        Assert.False(Directory.Exists(deletedDir));
    }

    [Fact]
    public async Task DeleteFile_RecursiveDirectory_WithRealLlm_Success()
    {
        // Arrange
        CreateTestDirectory("delete_dir");
        CreateTestFile("delete_dir/file1.txt", "File 1");
        CreateTestFile("delete_dir/file2.txt", "File 2");
        CreateTestDirectory("delete_dir/subdir");
        CreateTestFile("delete_dir/subdir/file3.txt", "File 3");
        var scenario = DeleteFileScenarios.CreateRecursiveDirectoryDeletion(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify directory was deleted
        var deletedDir = Path.Combine(TestDirectory, "delete_dir");
        Assert.False(Directory.Exists(deletedDir));
    }
}
