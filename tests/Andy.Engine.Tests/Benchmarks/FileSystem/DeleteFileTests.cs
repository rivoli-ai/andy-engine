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

    [Theory]
    [LlmTestData]
    public async Task DeleteFile_BasicDeletion_Success(LlmMode mode)
    {
        // Arrange
        CreateTestFile("deleteme.txt", "Content to delete");
        var scenario = DeleteFileScenarios.CreateBasicFileDeletion(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("delete_file", result.ToolInvocations[0].ToolType);
        }

        // Verify file was deleted
        var deletedFile = Path.Combine(TestDirectory, "deleteme.txt");
        Assert.False(File.Exists(deletedFile));
    }

    [Theory]
    [LlmTestData]
    public async Task DeleteFile_RecursiveDirectory_Success(LlmMode mode)
    {
        // Arrange
        CreateTestDirectory("delete_dir");
        CreateTestFile("delete_dir/file1.txt", "File 1");
        CreateTestFile("delete_dir/file2.txt", "File 2");
        CreateTestDirectory("delete_dir/subdir");
        CreateTestFile("delete_dir/subdir/file3.txt", "File 3");
        var scenario = DeleteFileScenarios.CreateRecursiveDirectoryDeletion(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        if (mode == LlmMode.Mock)
        {
            Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("recursive"));
        }

        // Verify directory was deleted
        var deletedDir = Path.Combine(TestDirectory, "delete_dir");
        Assert.False(Directory.Exists(deletedDir));
    }

    [Theory]
    [LlmTestData]
    public async Task DeleteFile_EmptyDirectory_Success(LlmMode mode)
    {
        // Arrange
        CreateTestDirectory("empty_dir");
        var scenario = DeleteFileScenarios.CreateDeleteEmptyDirectory(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify directory was deleted
        var emptyDir = Path.Combine(TestDirectory, "empty_dir");
        Assert.False(Directory.Exists(emptyDir));
    }

    [Theory]
    [LlmTestData]
    public async Task DeleteFile_WithBackup_CreatesBackup(LlmMode mode)
    {
        // Arrange
        const string content = "Important content";
        CreateTestFile("important.txt", content);
        var scenario = DeleteFileScenarios.CreateDeleteWithBackup(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        if (mode == LlmMode.Mock)
        {
            Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("create_backup"));
            Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("backup_location"));
        }

        // Verify file was deleted
        var deletedFile = Path.Combine(TestDirectory, "important.txt");
        Assert.False(File.Exists(deletedFile));

        // Verify backup was created
        var backupDir = Path.Combine(TestDirectory, "backups");
        Assert.True(Directory.Exists(backupDir));
        if (mode == LlmMode.Mock)
        {
            var backupFiles = Directory.GetFiles(backupDir);
            Assert.NotEmpty(backupFiles);
        }
    }

    [Theory]
    [LlmTestData]
    public async Task DeleteFile_BackupDefaultLocation_Success(LlmMode mode)
    {
        // Arrange
        CreateTestFile("important.txt", "Content");
        var scenario = DeleteFileScenarios.CreateBackupToDefaultLocation(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        if (mode == LlmMode.Mock)
        {
            Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("create_backup"));
        }

        // Verify file was deleted
        var deletedFile = Path.Combine(TestDirectory, "important.txt");
        Assert.False(File.Exists(deletedFile));
    }

    [Theory]
    [LlmTestData]
    public async Task DeleteFile_ReadOnlyWithForce_Success(LlmMode mode)
    {
        // Arrange
        var readOnlyFile = CreateTestFile("readonly.txt", "Read-only content");
        File.SetAttributes(readOnlyFile, FileAttributes.ReadOnly);
        var scenario = DeleteFileScenarios.CreateDeleteReadOnlyWithForce(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        if (mode == LlmMode.Mock)
        {
            Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("force"));
            Assert.True((bool)result.ToolInvocations[0].Parameters["force"]);
        }

        // Verify file was deleted
        Assert.False(File.Exists(readOnlyFile));
    }

    [Theory]
    [LlmTestData]
    public async Task DeleteFile_Statistics_ProvidesStats(LlmMode mode)
    {
        // Arrange
        CreateTestFile("stats_test.txt", "Test content for statistics");
        var scenario = DeleteFileScenarios.CreateStatisticsValidation(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify file was deleted
        var deletedFile = Path.Combine(TestDirectory, "stats_test.txt");
        Assert.False(File.Exists(deletedFile));
    }

    [Theory]
    [LlmTestData]
    public async Task DeleteFile_ReadOnlyWithoutForce_Fails(LlmMode mode)
    {
        // Arrange
        var readOnlyFile = CreateTestFile("readonly.txt", "Read-only content");
        File.SetAttributes(readOnlyFile, FileAttributes.ReadOnly);
        var scenario = DeleteFileScenarios.CreateDeleteReadOnlyWithoutForce(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        if (mode == LlmMode.Mock)
        {
            Assert.False((bool)result.ToolInvocations[0].Parameters["force"]);
        }

        // Verify file was NOT deleted
        Assert.True(File.Exists(readOnlyFile));

        // Cleanup readonly attribute
        File.SetAttributes(readOnlyFile, FileAttributes.Normal);
    }

    [Theory]
    [LlmTestData]
    public async Task DeleteFile_NonEmptyWithoutRecursive_Fails(LlmMode mode)
    {
        // Arrange
        CreateTestDirectory("nonempty_dir");
        CreateTestFile("nonempty_dir/file.txt", "Content");
        var scenario = DeleteFileScenarios.CreateDeleteNonEmptyWithoutRecursive(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        if (mode == LlmMode.Mock)
        {
            Assert.False((bool)result.ToolInvocations[0].Parameters["recursive"]);
        }

        // Verify directory was NOT deleted
        var nonEmptyDir = Path.Combine(TestDirectory, "nonempty_dir");
        Assert.True(Directory.Exists(nonEmptyDir));
    }

    [Theory]
    [LlmTestData]
    public async Task DeleteFile_WithSizeLimit_Fails(LlmMode mode)
    {
        // Arrange
        var largeContent = new string('A', 1000000); // 1MB
        CreateTestFile("large.txt", largeContent);
        var scenario = DeleteFileScenarios.CreateDeleteWithSizeLimit(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        if (mode == LlmMode.Mock)
        {
            Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("max_size_mb"));
        }

        // Verify file was NOT deleted (too large)
        var largeFile = Path.Combine(TestDirectory, "large.txt");
        Assert.True(File.Exists(largeFile));
    }

    [Theory]
    [LlmTestData]
    public async Task DeleteFile_WithExclusionPattern_Fails(LlmMode mode)
    {
        // Arrange
        CreateTestFile("important.txt", "Keep this");
        var scenario = DeleteFileScenarios.CreateDeleteWithExclusionPattern(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        if (mode == LlmMode.Mock)
        {
            Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("exclude_patterns"));
        }

        // Verify file was NOT deleted (matched exclusion pattern)
        var importantFile = Path.Combine(TestDirectory, "important.txt");
        Assert.True(File.Exists(importantFile));
    }

    [Theory]
    [LlmTestData]
    public async Task DeleteFile_FileNotFound_HandlesError(LlmMode mode)
    {
        // Arrange
        var scenario = DeleteFileScenarios.CreateFileNotFound(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Theory]
    [LlmTestData]
    public async Task DeleteFile_InvalidPath_HandlesError(LlmMode mode)
    {
        // Arrange
        var scenario = DeleteFileScenarios.CreateInvalidPath(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Theory]
    [LlmTestData]
    public async Task DeleteFile_MissingParameter_HandlesError(LlmMode mode)
    {
        // Arrange
        var scenario = DeleteFileScenarios.CreateMissingRequiredParameter(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Theory]
    [LlmTestData]
    public async Task DeleteFile_Cancellation_GracefulHandling(LlmMode mode)
    {
        // Arrange
        CreateTestDirectory("large_dir");
        for (int i = 0; i < 100; i++)
        {
            CreateTestFile($"large_dir/file{i}.txt", "Content");
        }
        var scenario = DeleteFileScenarios.CreateCancellationSupport(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }
}
