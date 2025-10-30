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

    [Fact]
    public async Task DeleteFile_EmptyDirectory_WithMockedLlm_Success()
    {
        // Arrange
        CreateTestDirectory("empty_dir");
        var scenario = DeleteFileScenarios.CreateDeleteEmptyDirectory(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify directory was deleted
        var emptyDir = Path.Combine(TestDirectory, "empty_dir");
        Assert.False(Directory.Exists(emptyDir));
    }

    [Fact]
    public async Task DeleteFile_EmptyDirectory_WithRealLlm_Success()
    {
        // Arrange
        CreateTestDirectory("empty_dir");
        var scenario = DeleteFileScenarios.CreateDeleteEmptyDirectory(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify directory was deleted
        var emptyDir = Path.Combine(TestDirectory, "empty_dir");
        Assert.False(Directory.Exists(emptyDir));
    }

    [Fact]
    public async Task DeleteFile_WithBackup_WithMockedLlm_CreatesBackup()
    {
        // Arrange
        const string content = "Important content";
        CreateTestFile("important.txt", content);
        var scenario = DeleteFileScenarios.CreateDeleteWithBackup(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("create_backup"));
        Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("backup_location"));

        // Verify file was deleted
        var deletedFile = Path.Combine(TestDirectory, "important.txt");
        Assert.False(File.Exists(deletedFile));

        // Verify backup was created
        var backupDir = Path.Combine(TestDirectory, "backups");
        Assert.True(Directory.Exists(backupDir));
        var backupFiles = Directory.GetFiles(backupDir);
        Assert.NotEmpty(backupFiles);
    }

    [Fact]
    public async Task DeleteFile_WithBackup_WithRealLlm_CreatesBackup()
    {
        // Arrange
        const string content = "Important content";
        CreateTestFile("important.txt", content);
        var scenario = DeleteFileScenarios.CreateDeleteWithBackup(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify file was deleted
        var deletedFile = Path.Combine(TestDirectory, "important.txt");
        Assert.False(File.Exists(deletedFile));

        // Verify backup was created
        var backupDir = Path.Combine(TestDirectory, "backups");
        Assert.True(Directory.Exists(backupDir));
    }

    [Fact]
    public async Task DeleteFile_BackupDefaultLocation_WithMockedLlm_Success()
    {
        // Arrange
        CreateTestFile("important.txt", "Content");
        var scenario = DeleteFileScenarios.CreateBackupToDefaultLocation(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("create_backup"));

        // Verify file was deleted
        var deletedFile = Path.Combine(TestDirectory, "important.txt");
        Assert.False(File.Exists(deletedFile));
    }

    [Fact]
    public async Task DeleteFile_BackupDefaultLocation_WithRealLlm_Success()
    {
        // Arrange
        CreateTestFile("important.txt", "Content");
        var scenario = DeleteFileScenarios.CreateBackupToDefaultLocation(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify file was deleted
        var deletedFile = Path.Combine(TestDirectory, "important.txt");
        Assert.False(File.Exists(deletedFile));
    }

    [Fact]
    public async Task DeleteFile_ReadOnlyWithForce_WithMockedLlm_Success()
    {
        // Arrange
        var readOnlyFile = CreateTestFile("readonly.txt", "Read-only content");
        File.SetAttributes(readOnlyFile, FileAttributes.ReadOnly);
        var scenario = DeleteFileScenarios.CreateDeleteReadOnlyWithForce(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("force"));
        Assert.True((bool)result.ToolInvocations[0].Parameters["force"]);

        // Verify file was deleted
        Assert.False(File.Exists(readOnlyFile));
    }

    [Fact]
    public async Task DeleteFile_ReadOnlyWithForce_WithRealLlm_Success()
    {
        // Arrange
        var readOnlyFile = CreateTestFile("readonly.txt", "Read-only content");
        File.SetAttributes(readOnlyFile, FileAttributes.ReadOnly);
        var scenario = DeleteFileScenarios.CreateDeleteReadOnlyWithForce(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify file was deleted
        Assert.False(File.Exists(readOnlyFile));
    }

    [Fact]
    public async Task DeleteFile_Statistics_WithMockedLlm_ProvidesStats()
    {
        // Arrange
        CreateTestFile("stats_test.txt", "Test content for statistics");
        var scenario = DeleteFileScenarios.CreateStatisticsValidation(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify file was deleted
        var deletedFile = Path.Combine(TestDirectory, "stats_test.txt");
        Assert.False(File.Exists(deletedFile));
    }

    [Fact]
    public async Task DeleteFile_Statistics_WithRealLlm_ProvidesStats()
    {
        // Arrange
        CreateTestFile("stats_test.txt", "Test content for statistics");
        var scenario = DeleteFileScenarios.CreateStatisticsValidation(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify file was deleted
        var deletedFile = Path.Combine(TestDirectory, "stats_test.txt");
        Assert.False(File.Exists(deletedFile));
    }

    [Fact]
    public async Task DeleteFile_ReadOnlyWithoutForce_WithMockedLlm_Fails()
    {
        // Arrange
        var readOnlyFile = CreateTestFile("readonly.txt", "Read-only content");
        File.SetAttributes(readOnlyFile, FileAttributes.ReadOnly);
        var scenario = DeleteFileScenarios.CreateDeleteReadOnlyWithoutForce(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.False((bool)result.ToolInvocations[0].Parameters["force"]);

        // Verify file was NOT deleted
        Assert.True(File.Exists(readOnlyFile));

        // Cleanup readonly attribute
        File.SetAttributes(readOnlyFile, FileAttributes.Normal);
    }

    [Fact]
    public async Task DeleteFile_ReadOnlyWithoutForce_WithRealLlm_Fails()
    {
        // Arrange
        var readOnlyFile = CreateTestFile("readonly.txt", "Read-only content");
        File.SetAttributes(readOnlyFile, FileAttributes.ReadOnly);
        var scenario = DeleteFileScenarios.CreateDeleteReadOnlyWithoutForce(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify file was NOT deleted
        Assert.True(File.Exists(readOnlyFile));

        // Cleanup readonly attribute
        File.SetAttributes(readOnlyFile, FileAttributes.Normal);
    }

    [Fact]
    public async Task DeleteFile_NonEmptyWithoutRecursive_WithMockedLlm_Fails()
    {
        // Arrange
        CreateTestDirectory("nonempty_dir");
        CreateTestFile("nonempty_dir/file.txt", "Content");
        var scenario = DeleteFileScenarios.CreateDeleteNonEmptyWithoutRecursive(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.False((bool)result.ToolInvocations[0].Parameters["recursive"]);

        // Verify directory was NOT deleted
        var nonEmptyDir = Path.Combine(TestDirectory, "nonempty_dir");
        Assert.True(Directory.Exists(nonEmptyDir));
    }

    [Fact]
    public async Task DeleteFile_NonEmptyWithoutRecursive_WithRealLlm_Fails()
    {
        // Arrange
        CreateTestDirectory("nonempty_dir");
        CreateTestFile("nonempty_dir/file.txt", "Content");
        var scenario = DeleteFileScenarios.CreateDeleteNonEmptyWithoutRecursive(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify directory was NOT deleted
        var nonEmptyDir = Path.Combine(TestDirectory, "nonempty_dir");
        Assert.True(Directory.Exists(nonEmptyDir));
    }

    [Fact]
    public async Task DeleteFile_WithSizeLimit_WithMockedLlm_Fails()
    {
        // Arrange
        var largeContent = new string('A', 1000000); // 1MB
        CreateTestFile("large.txt", largeContent);
        var scenario = DeleteFileScenarios.CreateDeleteWithSizeLimit(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("max_size_mb"));

        // Verify file was NOT deleted (too large)
        var largeFile = Path.Combine(TestDirectory, "large.txt");
        Assert.True(File.Exists(largeFile));
    }

    [Fact]
    public async Task DeleteFile_WithSizeLimit_WithRealLlm_Fails()
    {
        // Arrange
        var largeContent = new string('A', 1000000); // 1MB
        CreateTestFile("large.txt", largeContent);
        var scenario = DeleteFileScenarios.CreateDeleteWithSizeLimit(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify file was NOT deleted (too large)
        var largeFile = Path.Combine(TestDirectory, "large.txt");
        Assert.True(File.Exists(largeFile));
    }

    [Fact]
    public async Task DeleteFile_WithExclusionPattern_WithMockedLlm_Fails()
    {
        // Arrange
        CreateTestFile("important.txt", "Keep this");
        var scenario = DeleteFileScenarios.CreateDeleteWithExclusionPattern(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("exclude_patterns"));

        // Verify file was NOT deleted (matched exclusion pattern)
        var importantFile = Path.Combine(TestDirectory, "important.txt");
        Assert.True(File.Exists(importantFile));
    }

    [Fact]
    public async Task DeleteFile_WithExclusionPattern_WithRealLlm_Fails()
    {
        // Arrange
        CreateTestFile("important.txt", "Keep this");
        var scenario = DeleteFileScenarios.CreateDeleteWithExclusionPattern(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify file was NOT deleted (matched exclusion pattern)
        var importantFile = Path.Combine(TestDirectory, "important.txt");
        Assert.True(File.Exists(importantFile));
    }

    [Fact]
    public async Task DeleteFile_FileNotFound_WithMockedLlm_HandlesError()
    {
        // Arrange
        var scenario = DeleteFileScenarios.CreateFileNotFound(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task DeleteFile_FileNotFound_WithRealLlm_HandlesError()
    {
        // Arrange
        var scenario = DeleteFileScenarios.CreateFileNotFound(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task DeleteFile_InvalidPath_WithMockedLlm_HandlesError()
    {
        // Arrange
        var scenario = DeleteFileScenarios.CreateInvalidPath(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task DeleteFile_InvalidPath_WithRealLlm_HandlesError()
    {
        // Arrange
        var scenario = DeleteFileScenarios.CreateInvalidPath(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task DeleteFile_MissingParameter_WithMockedLlm_HandlesError()
    {
        // Arrange
        var scenario = DeleteFileScenarios.CreateMissingRequiredParameter(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task DeleteFile_MissingParameter_WithRealLlm_HandlesError()
    {
        // Arrange
        var scenario = DeleteFileScenarios.CreateMissingRequiredParameter(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task DeleteFile_Cancellation_WithMockedLlm_GracefulHandling()
    {
        // Arrange
        CreateTestDirectory("large_dir");
        for (int i = 0; i < 100; i++)
        {
            CreateTestFile($"large_dir/file{i}.txt", "Content");
        }
        var scenario = DeleteFileScenarios.CreateCancellationSupport(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task DeleteFile_Cancellation_WithRealLlm_GracefulHandling()
    {
        // Arrange
        CreateTestDirectory("large_dir");
        for (int i = 0; i < 100; i++)
        {
            CreateTestFile($"large_dir/file{i}.txt", "Content");
        }
        var scenario = DeleteFileScenarios.CreateCancellationSupport(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }
}
