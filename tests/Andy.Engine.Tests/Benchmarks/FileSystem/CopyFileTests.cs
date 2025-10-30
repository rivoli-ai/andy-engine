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

    [Theory]
    [LlmTestData]
    public async Task CopyFile_BasicCopy_Success(LlmMode mode)
    {
        // Arrange
        CreateTestFile("source.txt", "Content to copy");
        var scenario = CopyFileScenarios.CreateBasicFileCopy(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("copy_file", result.ToolInvocations[0].ToolType);
        }

        // Verify destination file exists
        var destFile = Path.Combine(TestDirectory, "destination.txt");
        Assert.True(File.Exists(destFile));

        if (mode == LlmMode.Mock)
        {
            Assert.Equal("Content to copy", File.ReadAllText(destFile));
        }
    }

    [Theory]
    [LlmTestData]
    public async Task CopyFile_WithOverwrite_Success(LlmMode mode)
    {
        // Arrange
        CreateTestFile("source.txt", "New content");
        CreateTestFile("existing_dest.txt", "Old content");
        var scenario = CopyFileScenarios.CreateCopyWithOverwrite(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("overwrite"));
        }

        // Verify file was overwritten
        var destFile = Path.Combine(TestDirectory, "existing_dest.txt");
        Assert.Equal("New content", File.ReadAllText(destFile));
    }

    [Theory]
    [LlmTestData]
    public async Task CopyFile_RecursiveDirectory_Success(LlmMode mode)
    {
        // Arrange
        CreateTestDirectory("source_dir");
        CreateTestFile("source_dir/file1.txt", "File 1");
        CreateTestFile("source_dir/file2.txt", "File 2");
        CreateTestDirectory("source_dir/subdir");
        CreateTestFile("source_dir/subdir/file3.txt", "File 3");
        var scenario = CopyFileScenarios.CreateRecursiveDirectoryCopy(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("recursive"));
        }

        // Verify directory structure was copied
        var destDir = Path.Combine(TestDirectory, "dest_dir");
        Assert.True(Directory.Exists(destDir));
        Assert.True(File.Exists(Path.Combine(destDir, "file1.txt")));

        if (mode == LlmMode.Mock)
        {
            Assert.True(File.Exists(Path.Combine(destDir, "file2.txt")));
            Assert.True(File.Exists(Path.Combine(destDir, "subdir", "file3.txt")));
        }
    }

    [Theory]
    [LlmTestData]
    public async Task CopyFile_ToDirectory_PreservesFilename(LlmMode mode)
    {
        // Arrange
        CreateTestFile("source.txt", "File content");
        CreateTestDirectory("target_dir");
        var scenario = CopyFileScenarios.CreateCopyToDirectory(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify file was copied with preserved filename
        var destFile = Path.Combine(TestDirectory, "target_dir", "source.txt");
        Assert.True(File.Exists(destFile));

        if (mode == LlmMode.Mock)
        {
            Assert.Equal("File content", File.ReadAllText(destFile));
        }
    }

    [Theory]
    [LlmTestData]
    public async Task CopyFile_WithTrailingSeparator_Success(LlmMode mode)
    {
        // Arrange
        CreateTestFile("source.txt", "Content");
        CreateTestDirectory("dest_dir");
        var scenario = CopyFileScenarios.CreateCopyWithTrailingSeparator(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify file was copied into directory
        var destFile = Path.Combine(TestDirectory, "dest_dir", "source.txt");
        Assert.True(File.Exists(destFile));

        if (mode == LlmMode.Mock)
        {
            Assert.Equal("Content", File.ReadAllText(destFile));
        }
    }

    [Theory]
    [LlmTestData]
    public async Task CopyFile_PreserveTimestamps_Success(LlmMode mode)
    {
        // Arrange
        var sourceFile = CreateTestFile("source.txt", "Content");
        var originalTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(sourceFile, originalTime);
        var scenario = CopyFileScenarios.CreatePreserveTimestamps(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("preserve_timestamps"));
        }

        // Verify timestamps were preserved
        var destFile = Path.Combine(TestDirectory, "destination.txt");
        Assert.True(File.Exists(destFile));

        if (mode == LlmMode.Mock)
        {
            var destTime = File.GetLastWriteTimeUtc(destFile);
            Assert.Equal(originalTime, destTime);
        }
    }

    [Theory]
    [LlmTestData]
    public async Task CopyFile_FollowSymlinks_Success(LlmMode mode)
    {
        // Arrange
        CreateTestFile("symlink.txt", "Symlink content");
        var scenario = CopyFileScenarios.CreateFollowSymlinks(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("follow_symlinks"));
            Assert.True((bool)result.ToolInvocations[0].Parameters["follow_symlinks"]);
        }

        // Verify file was copied
        var destFile = Path.Combine(TestDirectory, "symlink_copy.txt");
        Assert.True(File.Exists(destFile));
    }

    [Theory]
    [LlmTestData]
    public async Task CopyFile_EmptyDirectory_Success(LlmMode mode)
    {
        // Arrange
        CreateTestDirectory("empty_source");
        var scenario = CopyFileScenarios.CreateCopyEmptyDirectory(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify empty directory was copied
        var destDir = Path.Combine(TestDirectory, "empty_dest");
        Assert.True(Directory.Exists(destDir));

        if (mode == LlmMode.Mock)
        {
            Assert.Empty(Directory.GetFileSystemEntries(destDir));
        }
    }

    [Theory]
    [LlmTestData]
    public async Task CopyFile_ExcludePatterns_FiltersFiles(LlmMode mode)
    {
        // Arrange
        CreateTestDirectory("source_dir");
        CreateTestFile("source_dir/keep.txt", "Keep this");
        CreateTestFile("source_dir/temp.log", "Exclude this");
        CreateTestFile("source_dir/backup.tmp", "Exclude this too");
        var scenario = CopyFileScenarios.CreateExcludePatterns(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("exclude_patterns"));
        }

        // Verify only .txt was copied, .log and .tmp were excluded
        var destDir = Path.Combine(TestDirectory, "dest_dir");
        Assert.True(File.Exists(Path.Combine(destDir, "keep.txt")));
        Assert.False(File.Exists(Path.Combine(destDir, "temp.log")));
        Assert.False(File.Exists(Path.Combine(destDir, "backup.tmp")));
    }

    [Theory]
    [LlmTestData]
    public async Task CopyFile_ExcludeDirectories_FiltersDirectories(LlmMode mode)
    {
        // Arrange
        CreateTestDirectory("source_dir");
        CreateTestFile("source_dir/file.txt", "Keep this");
        CreateTestDirectory("source_dir/.git");
        CreateTestFile("source_dir/.git/config", "Exclude this");
        CreateTestDirectory("source_dir/node_modules");
        CreateTestFile("source_dir/node_modules/package.json", "Exclude this too");
        var scenario = CopyFileScenarios.CreateExcludeDirectories(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("exclude_patterns"));
        }

        // Verify .git and node_modules were excluded
        var destDir = Path.Combine(TestDirectory, "dest_dir");
        Assert.True(File.Exists(Path.Combine(destDir, "file.txt")));
        Assert.False(Directory.Exists(Path.Combine(destDir, ".git")));
        Assert.False(Directory.Exists(Path.Combine(destDir, "node_modules")));
    }

    [Theory]
    [LlmTestData]
    public async Task CopyFile_CreateDestinationDirectory_CreatesPath(LlmMode mode)
    {
        // Arrange
        CreateTestFile("source.txt", "Content");
        var scenario = CopyFileScenarios.CreateCreateDestinationDirectory(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("create_destination_directory"));
        }

        // Verify nested directory structure was created
        var destFile = Path.Combine(TestDirectory, "deep", "nested", "path", "destination.txt");
        Assert.True(File.Exists(destFile));

        if (mode == LlmMode.Mock)
        {
            Assert.Equal("Content", File.ReadAllText(destFile));
        }
    }

    [Theory]
    [LlmTestData]
    public async Task CopyFile_OverwriteDisabled_Fails(LlmMode mode)
    {
        // Arrange
        CreateTestFile("source.txt", "New content");
        CreateTestFile("existing.txt", "Old content");
        var scenario = CopyFileScenarios.CreateOverwriteDisabled(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.False((bool)result.ToolInvocations[0].Parameters["overwrite"]);
        }

        // Verify original content was preserved (copy should have failed)
        var destFile = Path.Combine(TestDirectory, "existing.txt");
        Assert.Equal("Old content", File.ReadAllText(destFile));
    }

    [Theory]
    [LlmTestData]
    public async Task CopyFile_SourceNotFound_HandlesError(LlmMode mode)
    {
        // Arrange
        var scenario = CopyFileScenarios.CreateSourceNotFound(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Theory]
    [LlmTestData]
    public async Task CopyFile_InvalidSourcePath_HandlesError(LlmMode mode)
    {
        // Arrange
        var scenario = CopyFileScenarios.CreateInvalidSourcePath(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Theory]
    [LlmTestData]
    public async Task CopyFile_InvalidDestinationPath_HandlesError(LlmMode mode)
    {
        // Arrange
        CreateTestFile("source.txt", "Content");
        var scenario = CopyFileScenarios.CreateInvalidDestinationPath(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Theory]
    [LlmTestData]
    public async Task CopyFile_MissingRequiredParameter_HandlesError(LlmMode mode)
    {
        // Arrange
        CreateTestFile("source.txt", "Content");
        var scenario = CopyFileScenarios.CreateMissingRequiredParameter(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Theory]
    [LlmTestData]
    public async Task CopyFile_PathTraversalSecurity_Prevented(LlmMode mode)
    {
        // Arrange
        CreateTestFile("source.txt", "Content");
        var scenario = CopyFileScenarios.CreatePathTraversalSecurity(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify file was NOT copied outside test directory
        var parentDir = Directory.GetParent(TestDirectory)?.FullName;
        if (parentDir != null)
        {
            var dangerousFile = Path.Combine(parentDir, "dangerous.txt");
            Assert.False(File.Exists(dangerousFile));
        }
    }
}
