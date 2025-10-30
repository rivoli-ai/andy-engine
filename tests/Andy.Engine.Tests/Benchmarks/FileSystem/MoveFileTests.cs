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

    [Theory]
    [LlmTestData]
    public async Task MoveFile_BasicMove_Success(LlmMode mode)
    {
        // Arrange
        CreateTestFile("moveme.txt", "Content to move");
        var scenario = MoveFileScenarios.CreateBasicFileMove(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("move_file", result.ToolInvocations[0].ToolType);
        }

        // Verify file was moved
        var sourceFile = Path.Combine(TestDirectory, "moveme.txt");
        var destFile = Path.Combine(TestDirectory, "moved.txt");
        Assert.False(File.Exists(sourceFile));
        Assert.True(File.Exists(destFile));

        if (mode == LlmMode.Mock)
        {
            Assert.Equal("Content to move", File.ReadAllText(destFile));
        }
    }

    [Theory]
    [LlmTestData]
    public async Task MoveFile_WithOverwrite_Success(LlmMode mode)
    {
        // Arrange
        CreateTestFile("moveme.txt", "New content");
        CreateTestFile("existing.txt", "Old content");
        var scenario = MoveFileScenarios.CreateMoveWithOverwrite(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("overwrite"));
        }

        // Verify file was moved and overwritten
        var sourceFile = Path.Combine(TestDirectory, "moveme.txt");
        var destFile = Path.Combine(TestDirectory, "existing.txt");
        Assert.False(File.Exists(sourceFile));

        if (mode == LlmMode.Mock)
        {
            Assert.Equal("New content", File.ReadAllText(destFile));
        }
    }

    [Theory]
    [LlmTestData]
    public async Task MoveFile_Rename_Success(LlmMode mode)
    {
        // Arrange
        CreateTestFile("oldname.txt", "Content");
        var scenario = MoveFileScenarios.CreateRenameFile(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        var oldFile = Path.Combine(TestDirectory, "oldname.txt");
        var newFile = Path.Combine(TestDirectory, "newname.txt");
        Assert.False(File.Exists(oldFile));
        Assert.True(File.Exists(newFile));
    }

    [Theory]
    [LlmTestData]
    public async Task MoveFile_Directory_Success(LlmMode mode)
    {
        // Arrange
        CreateTestDirectory("source_dir");
        CreateTestFile("source_dir/file1.txt", "File 1");
        CreateTestFile("source_dir/file2.txt", "File 2");
        var scenario = MoveFileScenarios.CreateMoveDirectory(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        var sourceDir = Path.Combine(TestDirectory, "source_dir");
        var destDir = Path.Combine(TestDirectory, "dest_dir");
        Assert.False(Directory.Exists(sourceDir));
        Assert.True(Directory.Exists(destDir));

        if (mode == LlmMode.Mock)
        {
            Assert.True(File.Exists(Path.Combine(destDir, "file1.txt")));
        }
    }

    [Theory]
    [LlmTestData]
    public async Task MoveFile_EmptyDirectory_Success(LlmMode mode)
    {
        // Arrange
        CreateTestDirectory("empty_source");
        var scenario = MoveFileScenarios.CreateMoveEmptyDirectory(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        var sourceDir = Path.Combine(TestDirectory, "empty_source");
        var destDir = Path.Combine(TestDirectory, "empty_dest");
        Assert.False(Directory.Exists(sourceDir));
        Assert.True(Directory.Exists(destDir));
    }

    [Theory]
    [LlmTestData]
    public async Task MoveFile_WithBackup_CreatesBackup(LlmMode mode)
    {
        // Arrange
        CreateTestFile("source.txt", "New content");
        CreateTestFile("existing.txt", "Old content to backup");
        var scenario = MoveFileScenarios.CreateMoveWithBackup(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("backup_existing"));
        }

        var sourceFile = Path.Combine(TestDirectory, "source.txt");
        Assert.False(File.Exists(sourceFile));
    }

    [Theory]
    [LlmTestData]
    public async Task MoveFile_ReadOnly_PreservesAttributes(LlmMode mode)
    {
        // Arrange
        var readOnlyFile = CreateTestFile("readonly.txt", "Read-only content");
        File.SetAttributes(readOnlyFile, FileAttributes.ReadOnly);
        var scenario = MoveFileScenarios.CreateMoveReadOnlyFile(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.False(File.Exists(readOnlyFile));
        var destFile = Path.Combine(TestDirectory, "moved_readonly.txt");
        Assert.True(File.Exists(destFile));

        // Cleanup readonly attribute
        File.SetAttributes(destFile, FileAttributes.Normal);
    }

    [Theory]
    [LlmTestData]
    public async Task MoveFile_CreateDestinationDirectory_CreatesPath(LlmMode mode)
    {
        // Arrange
        CreateTestFile("source.txt", "Content");
        var scenario = MoveFileScenarios.CreateCreateDestinationDirectory(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("create_destination_directory"));
        }

        var sourceFile = Path.Combine(TestDirectory, "source.txt");
        var destFile = Path.Combine(TestDirectory, "nested", "deep", "path", "destination.txt");
        Assert.False(File.Exists(sourceFile));
        Assert.True(File.Exists(destFile));
    }

    [Theory]
    [LlmTestData]
    public async Task MoveFile_CrossVolume_Success(LlmMode mode)
    {
        // Arrange
        CreateTestFile("source.txt", "Content");
        var scenario = MoveFileScenarios.CreateCrossVolumeMove(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        var sourceFile = Path.Combine(TestDirectory, "source.txt");
        Assert.False(File.Exists(sourceFile));

        // Cleanup cross-volume destination
        var destFile = Path.Combine(Path.GetTempPath(), "cross_volume_test", "destination.txt");
        if (File.Exists(destFile)) File.Delete(destFile);
        var destDir = Path.GetDirectoryName(destFile);
        if (destDir != null && Directory.Exists(destDir)) Directory.Delete(destDir, true);
    }

    [Theory]
    [LlmTestData]
    public async Task MoveFile_OverwriteDisabled_Fails(LlmMode mode)
    {
        // Arrange
        CreateTestFile("source.txt", "New content");
        CreateTestFile("existing.txt", "Old content");
        var scenario = MoveFileScenarios.CreateOverwriteDisabled(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.False((bool)result.ToolInvocations[0].Parameters["overwrite"]);
        }

        // Verify source was NOT moved (operation failed)
        var sourceFile = Path.Combine(TestDirectory, "source.txt");
        Assert.True(File.Exists(sourceFile));
        var destFile = Path.Combine(TestDirectory, "existing.txt");
        Assert.Equal("Old content", File.ReadAllText(destFile));
    }

    [Theory]
    [LlmTestData]
    public async Task MoveFile_ToSubdirectory_Fails(LlmMode mode)
    {
        // Arrange
        CreateTestDirectory("source_dir");
        CreateTestDirectory("source_dir/subdir");
        var scenario = MoveFileScenarios.CreateMoveToSubdirectory(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify directory was NOT moved (circular dependency)
        var sourceDir = Path.Combine(TestDirectory, "source_dir");
        Assert.True(Directory.Exists(sourceDir));
    }

    [Theory]
    [LlmTestData]
    public async Task MoveFile_SameSourceAndDestination_Fails(LlmMode mode)
    {
        // Arrange
        CreateTestFile("same.txt", "Content");
        var scenario = MoveFileScenarios.CreateSameSourceAndDestination(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify file still exists (operation failed)
        var sameFile = Path.Combine(TestDirectory, "same.txt");
        Assert.True(File.Exists(sameFile));
    }

    [Theory]
    [LlmTestData]
    public async Task MoveFile_SourceNotFound_HandlesError(LlmMode mode)
    {
        // Arrange
        var scenario = MoveFileScenarios.CreateSourceNotFound(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Theory]
    [LlmTestData]
    public async Task MoveFile_InvalidSourcePath_HandlesError(LlmMode mode)
    {
        // Arrange
        var scenario = MoveFileScenarios.CreateInvalidSourcePath(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Theory]
    [LlmTestData]
    public async Task MoveFile_InvalidDestinationPath_HandlesError(LlmMode mode)
    {
        // Arrange
        CreateTestFile("source.txt", "Content");
        var scenario = MoveFileScenarios.CreateInvalidDestinationPath(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Theory]
    [LlmTestData]
    public async Task MoveFile_MissingRequiredParameter_HandlesError(LlmMode mode)
    {
        // Arrange
        CreateTestFile("source.txt", "Content");
        var scenario = MoveFileScenarios.CreateMissingRequiredParameter(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Theory]
    [LlmTestData]
    public async Task MoveFile_Cancellation_GracefulHandling(LlmMode mode)
    {
        // Arrange
        var largeContent = new string('A', 1000000); // 1MB
        CreateTestFile("large.txt", largeContent);
        var scenario = MoveFileScenarios.CreateCancellationSupport(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Theory]
    [LlmTestData]
    public async Task MoveFile_Statistics_ProvidesStats(LlmMode mode)
    {
        // Arrange
        CreateTestFile("stats_test.txt", "Test content");
        var scenario = MoveFileScenarios.CreateStatisticsValidation(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        var sourceFile = Path.Combine(TestDirectory, "stats_test.txt");
        var destFile = Path.Combine(TestDirectory, "stats_moved.txt");
        Assert.False(File.Exists(sourceFile));
        Assert.True(File.Exists(destFile));
    }
}
