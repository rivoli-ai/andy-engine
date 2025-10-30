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

    [Fact]
    public async Task MoveFile_Rename_WithMockedLlm_Success()
    {
        // Arrange
        CreateTestFile("oldname.txt", "Content");
        var scenario = MoveFileScenarios.CreateRenameFile(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        var oldFile = Path.Combine(TestDirectory, "oldname.txt");
        var newFile = Path.Combine(TestDirectory, "newname.txt");
        Assert.False(File.Exists(oldFile));
        Assert.True(File.Exists(newFile));
    }

    [Fact]
    public async Task MoveFile_Rename_WithRealLlm_Success()
    {
        // Arrange
        CreateTestFile("oldname.txt", "Content");
        var scenario = MoveFileScenarios.CreateRenameFile(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        var oldFile = Path.Combine(TestDirectory, "oldname.txt");
        var newFile = Path.Combine(TestDirectory, "newname.txt");
        Assert.False(File.Exists(oldFile));
        Assert.True(File.Exists(newFile));
    }

    [Fact]
    public async Task MoveFile_Directory_WithMockedLlm_Success()
    {
        // Arrange
        CreateTestDirectory("source_dir");
        CreateTestFile("source_dir/file1.txt", "File 1");
        CreateTestFile("source_dir/file2.txt", "File 2");
        var scenario = MoveFileScenarios.CreateMoveDirectory(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        var sourceDir = Path.Combine(TestDirectory, "source_dir");
        var destDir = Path.Combine(TestDirectory, "dest_dir");
        Assert.False(Directory.Exists(sourceDir));
        Assert.True(Directory.Exists(destDir));
        Assert.True(File.Exists(Path.Combine(destDir, "file1.txt")));
    }

    [Fact]
    public async Task MoveFile_Directory_WithRealLlm_Success()
    {
        // Arrange
        CreateTestDirectory("source_dir");
        CreateTestFile("source_dir/file1.txt", "File 1");
        CreateTestFile("source_dir/file2.txt", "File 2");
        var scenario = MoveFileScenarios.CreateMoveDirectory(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        var sourceDir = Path.Combine(TestDirectory, "source_dir");
        var destDir = Path.Combine(TestDirectory, "dest_dir");
        Assert.False(Directory.Exists(sourceDir));
        Assert.True(Directory.Exists(destDir));
    }

    [Fact]
    public async Task MoveFile_EmptyDirectory_WithMockedLlm_Success()
    {
        // Arrange
        CreateTestDirectory("empty_source");
        var scenario = MoveFileScenarios.CreateMoveEmptyDirectory(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        var sourceDir = Path.Combine(TestDirectory, "empty_source");
        var destDir = Path.Combine(TestDirectory, "empty_dest");
        Assert.False(Directory.Exists(sourceDir));
        Assert.True(Directory.Exists(destDir));
    }

    [Fact]
    public async Task MoveFile_EmptyDirectory_WithRealLlm_Success()
    {
        // Arrange
        CreateTestDirectory("empty_source");
        var scenario = MoveFileScenarios.CreateMoveEmptyDirectory(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        var sourceDir = Path.Combine(TestDirectory, "empty_source");
        var destDir = Path.Combine(TestDirectory, "empty_dest");
        Assert.False(Directory.Exists(sourceDir));
        Assert.True(Directory.Exists(destDir));
    }

    [Fact]
    public async Task MoveFile_WithBackup_WithMockedLlm_CreatesBackup()
    {
        // Arrange
        CreateTestFile("source.txt", "New content");
        CreateTestFile("existing.txt", "Old content to backup");
        var scenario = MoveFileScenarios.CreateMoveWithBackup(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("backup_existing"));
        var sourceFile = Path.Combine(TestDirectory, "source.txt");
        Assert.False(File.Exists(sourceFile));
    }

    [Fact]
    public async Task MoveFile_WithBackup_WithRealLlm_CreatesBackup()
    {
        // Arrange
        CreateTestFile("source.txt", "New content");
        CreateTestFile("existing.txt", "Old content to backup");
        var scenario = MoveFileScenarios.CreateMoveWithBackup(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        var sourceFile = Path.Combine(TestDirectory, "source.txt");
        Assert.False(File.Exists(sourceFile));
    }

    [Fact]
    public async Task MoveFile_ReadOnly_WithMockedLlm_PreservesAttributes()
    {
        // Arrange
        var readOnlyFile = CreateTestFile("readonly.txt", "Read-only content");
        File.SetAttributes(readOnlyFile, FileAttributes.ReadOnly);
        var scenario = MoveFileScenarios.CreateMoveReadOnlyFile(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.False(File.Exists(readOnlyFile));
        var destFile = Path.Combine(TestDirectory, "moved_readonly.txt");
        Assert.True(File.Exists(destFile));

        // Cleanup readonly attribute
        File.SetAttributes(destFile, FileAttributes.Normal);
    }

    [Fact]
    public async Task MoveFile_ReadOnly_WithRealLlm_PreservesAttributes()
    {
        // Arrange
        var readOnlyFile = CreateTestFile("readonly.txt", "Read-only content");
        File.SetAttributes(readOnlyFile, FileAttributes.ReadOnly);
        var scenario = MoveFileScenarios.CreateMoveReadOnlyFile(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.False(File.Exists(readOnlyFile));
        var destFile = Path.Combine(TestDirectory, "moved_readonly.txt");
        Assert.True(File.Exists(destFile));

        // Cleanup readonly attribute
        File.SetAttributes(destFile, FileAttributes.Normal);
    }

    [Fact]
    public async Task MoveFile_CreateDestinationDirectory_WithMockedLlm_CreatesPath()
    {
        // Arrange
        CreateTestFile("source.txt", "Content");
        var scenario = MoveFileScenarios.CreateCreateDestinationDirectory(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("create_destination_directory"));
        var sourceFile = Path.Combine(TestDirectory, "source.txt");
        var destFile = Path.Combine(TestDirectory, "nested", "deep", "path", "destination.txt");
        Assert.False(File.Exists(sourceFile));
        Assert.True(File.Exists(destFile));
    }

    [Fact]
    public async Task MoveFile_CreateDestinationDirectory_WithRealLlm_CreatesPath()
    {
        // Arrange
        CreateTestFile("source.txt", "Content");
        var scenario = MoveFileScenarios.CreateCreateDestinationDirectory(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        var sourceFile = Path.Combine(TestDirectory, "source.txt");
        var destFile = Path.Combine(TestDirectory, "nested", "deep", "path", "destination.txt");
        Assert.False(File.Exists(sourceFile));
        Assert.True(File.Exists(destFile));
    }

    [Fact]
    public async Task MoveFile_CrossVolume_WithMockedLlm_Success()
    {
        // Arrange
        CreateTestFile("source.txt", "Content");
        var scenario = MoveFileScenarios.CreateCrossVolumeMove(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

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

    [Fact]
    public async Task MoveFile_CrossVolume_WithRealLlm_Success()
    {
        // Arrange
        CreateTestFile("source.txt", "Content");
        var scenario = MoveFileScenarios.CreateCrossVolumeMove(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

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

    [Fact]
    public async Task MoveFile_OverwriteDisabled_WithMockedLlm_Fails()
    {
        // Arrange
        CreateTestFile("source.txt", "New content");
        CreateTestFile("existing.txt", "Old content");
        var scenario = MoveFileScenarios.CreateOverwriteDisabled(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.False((bool)result.ToolInvocations[0].Parameters["overwrite"]);

        // Verify source was NOT moved (operation failed)
        var sourceFile = Path.Combine(TestDirectory, "source.txt");
        Assert.True(File.Exists(sourceFile));
        var destFile = Path.Combine(TestDirectory, "existing.txt");
        Assert.Equal("Old content", File.ReadAllText(destFile));
    }

    [Fact]
    public async Task MoveFile_OverwriteDisabled_WithRealLlm_Fails()
    {
        // Arrange
        CreateTestFile("source.txt", "New content");
        CreateTestFile("existing.txt", "Old content");
        var scenario = MoveFileScenarios.CreateOverwriteDisabled(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify source was NOT moved (operation failed)
        var sourceFile = Path.Combine(TestDirectory, "source.txt");
        Assert.True(File.Exists(sourceFile));
        var destFile = Path.Combine(TestDirectory, "existing.txt");
        Assert.Equal("Old content", File.ReadAllText(destFile));
    }

    [Fact]
    public async Task MoveFile_ToSubdirectory_WithMockedLlm_Fails()
    {
        // Arrange
        CreateTestDirectory("source_dir");
        CreateTestDirectory("source_dir/subdir");
        var scenario = MoveFileScenarios.CreateMoveToSubdirectory(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify directory was NOT moved (circular dependency)
        var sourceDir = Path.Combine(TestDirectory, "source_dir");
        Assert.True(Directory.Exists(sourceDir));
    }

    [Fact]
    public async Task MoveFile_ToSubdirectory_WithRealLlm_Fails()
    {
        // Arrange
        CreateTestDirectory("source_dir");
        CreateTestDirectory("source_dir/subdir");
        var scenario = MoveFileScenarios.CreateMoveToSubdirectory(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify directory was NOT moved (circular dependency)
        var sourceDir = Path.Combine(TestDirectory, "source_dir");
        Assert.True(Directory.Exists(sourceDir));
    }

    [Fact]
    public async Task MoveFile_SameSourceAndDestination_WithMockedLlm_Fails()
    {
        // Arrange
        CreateTestFile("same.txt", "Content");
        var scenario = MoveFileScenarios.CreateSameSourceAndDestination(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify file still exists (operation failed)
        var sameFile = Path.Combine(TestDirectory, "same.txt");
        Assert.True(File.Exists(sameFile));
    }

    [Fact]
    public async Task MoveFile_SameSourceAndDestination_WithRealLlm_Fails()
    {
        // Arrange
        CreateTestFile("same.txt", "Content");
        var scenario = MoveFileScenarios.CreateSameSourceAndDestination(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify file still exists (operation failed)
        var sameFile = Path.Combine(TestDirectory, "same.txt");
        Assert.True(File.Exists(sameFile));
    }

    [Fact]
    public async Task MoveFile_SourceNotFound_WithMockedLlm_HandlesError()
    {
        // Arrange
        var scenario = MoveFileScenarios.CreateSourceNotFound(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task MoveFile_SourceNotFound_WithRealLlm_HandlesError()
    {
        // Arrange
        var scenario = MoveFileScenarios.CreateSourceNotFound(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task MoveFile_InvalidSourcePath_WithMockedLlm_HandlesError()
    {
        // Arrange
        var scenario = MoveFileScenarios.CreateInvalidSourcePath(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task MoveFile_InvalidSourcePath_WithRealLlm_HandlesError()
    {
        // Arrange
        var scenario = MoveFileScenarios.CreateInvalidSourcePath(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task MoveFile_InvalidDestinationPath_WithMockedLlm_HandlesError()
    {
        // Arrange
        CreateTestFile("source.txt", "Content");
        var scenario = MoveFileScenarios.CreateInvalidDestinationPath(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task MoveFile_InvalidDestinationPath_WithRealLlm_HandlesError()
    {
        // Arrange
        CreateTestFile("source.txt", "Content");
        var scenario = MoveFileScenarios.CreateInvalidDestinationPath(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task MoveFile_MissingRequiredParameter_WithMockedLlm_HandlesError()
    {
        // Arrange
        CreateTestFile("source.txt", "Content");
        var scenario = MoveFileScenarios.CreateMissingRequiredParameter(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task MoveFile_MissingRequiredParameter_WithRealLlm_HandlesError()
    {
        // Arrange
        CreateTestFile("source.txt", "Content");
        var scenario = MoveFileScenarios.CreateMissingRequiredParameter(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task MoveFile_Cancellation_WithMockedLlm_GracefulHandling()
    {
        // Arrange
        var largeContent = new string('A', 1000000); // 1MB
        CreateTestFile("large.txt", largeContent);
        var scenario = MoveFileScenarios.CreateCancellationSupport(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task MoveFile_Cancellation_WithRealLlm_GracefulHandling()
    {
        // Arrange
        var largeContent = new string('A', 1000000); // 1MB
        CreateTestFile("large.txt", largeContent);
        var scenario = MoveFileScenarios.CreateCancellationSupport(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task MoveFile_Statistics_WithMockedLlm_ProvidesStats()
    {
        // Arrange
        CreateTestFile("stats_test.txt", "Test content");
        var scenario = MoveFileScenarios.CreateStatisticsValidation(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        var sourceFile = Path.Combine(TestDirectory, "stats_test.txt");
        var destFile = Path.Combine(TestDirectory, "stats_moved.txt");
        Assert.False(File.Exists(sourceFile));
        Assert.True(File.Exists(destFile));
    }

    [Fact]
    public async Task MoveFile_Statistics_WithRealLlm_ProvidesStats()
    {
        // Arrange
        CreateTestFile("stats_test.txt", "Test content");
        var scenario = MoveFileScenarios.CreateStatisticsValidation(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        var sourceFile = Path.Combine(TestDirectory, "stats_test.txt");
        var destFile = Path.Combine(TestDirectory, "stats_moved.txt");
        Assert.False(File.Exists(sourceFile));
        Assert.True(File.Exists(destFile));
    }
}
