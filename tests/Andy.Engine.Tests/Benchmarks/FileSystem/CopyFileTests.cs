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

    [Fact]
    public async Task CopyFile_ToDirectory_WithMockedLlm_PreservesFilename()
    {
        // Arrange
        CreateTestFile("source.txt", "File content");
        CreateTestDirectory("target_dir");
        var scenario = CopyFileScenarios.CreateCopyToDirectory(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify file was copied with preserved filename
        var destFile = Path.Combine(TestDirectory, "target_dir", "source.txt");
        Assert.True(File.Exists(destFile));
        Assert.Equal("File content", File.ReadAllText(destFile));
    }

    [Fact]
    public async Task CopyFile_ToDirectory_WithRealLlm_PreservesFilename()
    {
        // Arrange
        CreateTestFile("source.txt", "File content");
        CreateTestDirectory("target_dir");
        var scenario = CopyFileScenarios.CreateCopyToDirectory(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify file was copied with preserved filename
        var destFile = Path.Combine(TestDirectory, "target_dir", "source.txt");
        Assert.True(File.Exists(destFile));
    }

    [Fact]
    public async Task CopyFile_WithTrailingSeparator_WithMockedLlm_Success()
    {
        // Arrange
        CreateTestFile("source.txt", "Content");
        CreateTestDirectory("dest_dir");
        var scenario = CopyFileScenarios.CreateCopyWithTrailingSeparator(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify file was copied into directory
        var destFile = Path.Combine(TestDirectory, "dest_dir", "source.txt");
        Assert.True(File.Exists(destFile));
        Assert.Equal("Content", File.ReadAllText(destFile));
    }

    [Fact]
    public async Task CopyFile_WithTrailingSeparator_WithRealLlm_Success()
    {
        // Arrange
        CreateTestFile("source.txt", "Content");
        CreateTestDirectory("dest_dir");
        var scenario = CopyFileScenarios.CreateCopyWithTrailingSeparator(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify file was copied into directory
        var destFile = Path.Combine(TestDirectory, "dest_dir", "source.txt");
        Assert.True(File.Exists(destFile));
    }

    [Fact]
    public async Task CopyFile_PreserveTimestamps_WithMockedLlm_Success()
    {
        // Arrange
        var sourceFile = CreateTestFile("source.txt", "Content");
        var originalTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(sourceFile, originalTime);
        var scenario = CopyFileScenarios.CreatePreserveTimestamps(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("preserve_timestamps"));

        // Verify timestamps were preserved
        var destFile = Path.Combine(TestDirectory, "destination.txt");
        Assert.True(File.Exists(destFile));
        var destTime = File.GetLastWriteTimeUtc(destFile);
        Assert.Equal(originalTime, destTime);
    }

    [Fact]
    public async Task CopyFile_PreserveTimestamps_WithRealLlm_Success()
    {
        // Arrange
        var sourceFile = CreateTestFile("source.txt", "Content");
        var originalTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(sourceFile, originalTime);
        var scenario = CopyFileScenarios.CreatePreserveTimestamps(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify timestamps were preserved
        var destFile = Path.Combine(TestDirectory, "destination.txt");
        Assert.True(File.Exists(destFile));
    }

    [Fact]
    public async Task CopyFile_FollowSymlinks_WithMockedLlm_Success()
    {
        // Arrange
        CreateTestFile("symlink.txt", "Symlink content");
        var scenario = CopyFileScenarios.CreateFollowSymlinks(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("follow_symlinks"));
        Assert.True((bool)result.ToolInvocations[0].Parameters["follow_symlinks"]);

        // Verify file was copied
        var destFile = Path.Combine(TestDirectory, "symlink_copy.txt");
        Assert.True(File.Exists(destFile));
    }

    [Fact]
    public async Task CopyFile_FollowSymlinks_WithRealLlm_Success()
    {
        // Arrange
        CreateTestFile("symlink.txt", "Symlink content");
        var scenario = CopyFileScenarios.CreateFollowSymlinks(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify file was copied
        var destFile = Path.Combine(TestDirectory, "symlink_copy.txt");
        Assert.True(File.Exists(destFile));
    }

    [Fact]
    public async Task CopyFile_EmptyDirectory_WithMockedLlm_Success()
    {
        // Arrange
        CreateTestDirectory("empty_source");
        var scenario = CopyFileScenarios.CreateCopyEmptyDirectory(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify empty directory was copied
        var destDir = Path.Combine(TestDirectory, "empty_dest");
        Assert.True(Directory.Exists(destDir));
        Assert.Empty(Directory.GetFileSystemEntries(destDir));
    }

    [Fact]
    public async Task CopyFile_EmptyDirectory_WithRealLlm_Success()
    {
        // Arrange
        CreateTestDirectory("empty_source");
        var scenario = CopyFileScenarios.CreateCopyEmptyDirectory(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify empty directory was copied
        var destDir = Path.Combine(TestDirectory, "empty_dest");
        Assert.True(Directory.Exists(destDir));
    }

    [Fact]
    public async Task CopyFile_ExcludePatterns_WithMockedLlm_FiltersFiles()
    {
        // Arrange
        CreateTestDirectory("source_dir");
        CreateTestFile("source_dir/keep.txt", "Keep this");
        CreateTestFile("source_dir/temp.log", "Exclude this");
        CreateTestFile("source_dir/backup.tmp", "Exclude this too");
        var scenario = CopyFileScenarios.CreateExcludePatterns(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("exclude_patterns"));

        // Verify only .txt was copied, .log and .tmp were excluded
        var destDir = Path.Combine(TestDirectory, "dest_dir");
        Assert.True(File.Exists(Path.Combine(destDir, "keep.txt")));
        Assert.False(File.Exists(Path.Combine(destDir, "temp.log")));
        Assert.False(File.Exists(Path.Combine(destDir, "backup.tmp")));
    }

    [Fact]
    public async Task CopyFile_ExcludePatterns_WithRealLlm_FiltersFiles()
    {
        // Arrange
        CreateTestDirectory("source_dir");
        CreateTestFile("source_dir/keep.txt", "Keep this");
        CreateTestFile("source_dir/temp.log", "Exclude this");
        CreateTestFile("source_dir/backup.tmp", "Exclude this too");
        var scenario = CopyFileScenarios.CreateExcludePatterns(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify only .txt was copied, .log and .tmp were excluded
        var destDir = Path.Combine(TestDirectory, "dest_dir");
        Assert.True(File.Exists(Path.Combine(destDir, "keep.txt")));
        Assert.False(File.Exists(Path.Combine(destDir, "temp.log")));
        Assert.False(File.Exists(Path.Combine(destDir, "backup.tmp")));
    }

    [Fact]
    public async Task CopyFile_ExcludeDirectories_WithMockedLlm_FiltersDirectories()
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
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("exclude_patterns"));

        // Verify .git and node_modules were excluded
        var destDir = Path.Combine(TestDirectory, "dest_dir");
        Assert.True(File.Exists(Path.Combine(destDir, "file.txt")));
        Assert.False(Directory.Exists(Path.Combine(destDir, ".git")));
        Assert.False(Directory.Exists(Path.Combine(destDir, "node_modules")));
    }

    [Fact]
    public async Task CopyFile_ExcludeDirectories_WithRealLlm_FiltersDirectories()
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
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify .git and node_modules were excluded
        var destDir = Path.Combine(TestDirectory, "dest_dir");
        Assert.True(File.Exists(Path.Combine(destDir, "file.txt")));
        Assert.False(Directory.Exists(Path.Combine(destDir, ".git")));
        Assert.False(Directory.Exists(Path.Combine(destDir, "node_modules")));
    }

    [Fact]
    public async Task CopyFile_CreateDestinationDirectory_WithMockedLlm_CreatesPath()
    {
        // Arrange
        CreateTestFile("source.txt", "Content");
        var scenario = CopyFileScenarios.CreateCreateDestinationDirectory(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("create_destination_directory"));

        // Verify nested directory structure was created
        var destFile = Path.Combine(TestDirectory, "deep", "nested", "path", "destination.txt");
        Assert.True(File.Exists(destFile));
        Assert.Equal("Content", File.ReadAllText(destFile));
    }

    [Fact]
    public async Task CopyFile_CreateDestinationDirectory_WithRealLlm_CreatesPath()
    {
        // Arrange
        CreateTestFile("source.txt", "Content");
        var scenario = CopyFileScenarios.CreateCreateDestinationDirectory(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify nested directory structure was created
        var destFile = Path.Combine(TestDirectory, "deep", "nested", "path", "destination.txt");
        Assert.True(File.Exists(destFile));
    }

    [Fact]
    public async Task CopyFile_OverwriteDisabled_WithMockedLlm_Fails()
    {
        // Arrange
        CreateTestFile("source.txt", "New content");
        CreateTestFile("existing.txt", "Old content");
        var scenario = CopyFileScenarios.CreateOverwriteDisabled(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.False((bool)result.ToolInvocations[0].Parameters["overwrite"]);

        // Verify original content was preserved (copy should have failed)
        var destFile = Path.Combine(TestDirectory, "existing.txt");
        Assert.Equal("Old content", File.ReadAllText(destFile));
    }

    [Fact]
    public async Task CopyFile_OverwriteDisabled_WithRealLlm_Fails()
    {
        // Arrange
        CreateTestFile("source.txt", "New content");
        CreateTestFile("existing.txt", "Old content");
        var scenario = CopyFileScenarios.CreateOverwriteDisabled(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify original content was preserved (copy should have failed)
        var destFile = Path.Combine(TestDirectory, "existing.txt");
        Assert.Equal("Old content", File.ReadAllText(destFile));
    }

    [Fact]
    public async Task CopyFile_SourceNotFound_WithMockedLlm_HandlesError()
    {
        // Arrange
        var scenario = CopyFileScenarios.CreateSourceNotFound(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task CopyFile_SourceNotFound_WithRealLlm_HandlesError()
    {
        // Arrange
        var scenario = CopyFileScenarios.CreateSourceNotFound(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task CopyFile_InvalidSourcePath_WithMockedLlm_HandlesError()
    {
        // Arrange
        var scenario = CopyFileScenarios.CreateInvalidSourcePath(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task CopyFile_InvalidSourcePath_WithRealLlm_HandlesError()
    {
        // Arrange
        var scenario = CopyFileScenarios.CreateInvalidSourcePath(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task CopyFile_InvalidDestinationPath_WithMockedLlm_HandlesError()
    {
        // Arrange
        CreateTestFile("source.txt", "Content");
        var scenario = CopyFileScenarios.CreateInvalidDestinationPath(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task CopyFile_InvalidDestinationPath_WithRealLlm_HandlesError()
    {
        // Arrange
        CreateTestFile("source.txt", "Content");
        var scenario = CopyFileScenarios.CreateInvalidDestinationPath(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task CopyFile_MissingRequiredParameter_WithMockedLlm_HandlesError()
    {
        // Arrange
        CreateTestFile("source.txt", "Content");
        var scenario = CopyFileScenarios.CreateMissingRequiredParameter(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task CopyFile_MissingRequiredParameter_WithRealLlm_HandlesError()
    {
        // Arrange
        CreateTestFile("source.txt", "Content");
        var scenario = CopyFileScenarios.CreateMissingRequiredParameter(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task CopyFile_PathTraversalSecurity_WithMockedLlm_Prevented()
    {
        // Arrange
        CreateTestFile("source.txt", "Content");
        var scenario = CopyFileScenarios.CreatePathTraversalSecurity(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

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

    [Fact]
    public async Task CopyFile_PathTraversalSecurity_WithRealLlm_Prevented()
    {
        // Arrange
        CreateTestFile("source.txt", "Content");
        var scenario = CopyFileScenarios.CreatePathTraversalSecurity(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

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
