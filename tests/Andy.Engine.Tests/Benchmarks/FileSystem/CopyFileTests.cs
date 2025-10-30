using Andy.Benchmarks.Framework;
using Andy.Benchmarks.Validators;
using Andy.Engine.Benchmarks.Framework;
using Andy.Engine.Benchmarks.Scenarios.FileSystem;
using Xunit;
using Xunit.Abstractions;

namespace Andy.Engine.Tests.Benchmarks.FileSystem;

/// <summary>
/// Integration tests for copy_file tool via the engine
/// Executes scenarios through the Agent with both mocked and real LLM
/// </summary>
public class CopyFileTests : FileSystemIntegrationTestBase
{
    public CopyFileTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public async Task CopyFile_BasicCopy_WithMockedLlm_Success()
    {
        // Arrange
        CreateTestFile("source.txt", "Content to copy");
        var scenario = CopyFileScenarios.CreateBasicFileCopy(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.Single(result.ToolInvocations);
        Assert.Equal("copy_file", result.ToolInvocations[0].ToolType);

        // Verify destination file exists
        var destFile = Path.Combine(TestDirectory, "destination.txt");
        Assert.True(File.Exists(destFile));
        Assert.Equal("Content to copy", File.ReadAllText(destFile));
    }

    [Fact]
    public async Task CopyFile_BasicCopy_WithRealLlm_Success()
    {
        // Arrange
        CreateTestFile("source.txt", "Content to copy");
        var scenario = CopyFileScenarios.CreateBasicFileCopy(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify destination file exists
        var destFile = Path.Combine(TestDirectory, "destination.txt");
        Assert.True(File.Exists(destFile));
    }

    [Fact]
    public async Task CopyFile_WithOverwrite_WithMockedLlm_Success()
    {
        // Arrange
        CreateTestFile("source.txt", "New content");
        CreateTestFile("existing_dest.txt", "Old content");
        var scenario = CopyFileScenarios.CreateCopyWithOverwrite(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("overwrite"));

        // Verify file was overwritten
        var destFile = Path.Combine(TestDirectory, "existing_dest.txt");
        Assert.Equal("New content", File.ReadAllText(destFile));
    }

    [Fact]
    public async Task CopyFile_WithOverwrite_WithRealLlm_Success()
    {
        // Arrange
        CreateTestFile("source.txt", "New content");
        CreateTestFile("existing_dest.txt", "Old content");
        var scenario = CopyFileScenarios.CreateCopyWithOverwrite(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify file was overwritten
        var destFile = Path.Combine(TestDirectory, "existing_dest.txt");
        Assert.Equal("New content", File.ReadAllText(destFile));
    }

    [Fact]
    public async Task CopyFile_RecursiveDirectory_WithMockedLlm_Success()
    {
        // Arrange
        CreateTestDirectory("source_dir");
        CreateTestFile("source_dir/file1.txt", "File 1");
        CreateTestFile("source_dir/file2.txt", "File 2");
        CreateTestDirectory("source_dir/subdir");
        CreateTestFile("source_dir/subdir/file3.txt", "File 3");
        var scenario = CopyFileScenarios.CreateRecursiveDirectoryCopy(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("recursive"));

        // Verify directory structure was copied
        var destDir = Path.Combine(TestDirectory, "dest_dir");
        Assert.True(Directory.Exists(destDir));
        Assert.True(File.Exists(Path.Combine(destDir, "file1.txt")));
        Assert.True(File.Exists(Path.Combine(destDir, "file2.txt")));
        Assert.True(File.Exists(Path.Combine(destDir, "subdir", "file3.txt")));
    }

    [Fact]
    public async Task CopyFile_RecursiveDirectory_WithRealLlm_Success()
    {
        // Arrange
        CreateTestDirectory("source_dir");
        CreateTestFile("source_dir/file1.txt", "File 1");
        CreateTestFile("source_dir/file2.txt", "File 2");
        CreateTestDirectory("source_dir/subdir");
        CreateTestFile("source_dir/subdir/file3.txt", "File 3");
        var scenario = CopyFileScenarios.CreateRecursiveDirectoryCopy(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify directory structure was copied
        var destDir = Path.Combine(TestDirectory, "dest_dir");
        Assert.True(Directory.Exists(destDir));
        Assert.True(File.Exists(Path.Combine(destDir, "file1.txt")));
    }
}
