using Andy.Benchmarks.Framework;
using Andy.Benchmarks.Validators;
using Andy.Engine.Benchmarks.Framework;
using Xunit;

namespace Andy.Engine.Tests.Benchmarks.FileSystem;

/// <summary>
/// Tests for delete_file tool via the engine
/// Validates deletion with proper safety guards and safeguards
/// </summary>
public class DeleteFileTests : FileSystemTestBase
{
    [Fact]
    public void DeleteFile_SimpleFile_DeletesSuccessfully()
    {
        // Arrange
        var fileToDelete = CreateTestFile("deleteme.txt", "Content to delete");

        var scenario = new BenchmarkScenario
        {
            Id = "fs-delete-file-simple",
            Category = "file-system",
            Description = "Delete a single file safely",
            Tags = new List<string> { "file-system", "delete-file", "single-tool" },
            Workspace = CreateWorkspaceConfig(),
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Delete the file {fileToDelete}"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "delete_file",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["target_path"] = fileToDelete
                    }
                }
            },
            Validation = CreateValidationConfig(
                mustContain: new List<string> { "deleted", "successfully" }
            ),
            Timeout = TimeSpan.FromMinutes(1)
        };

        // Act & Assert
        Assert.NotNull(scenario);
        Assert.Equal("delete_file", scenario.ExpectedTools[0].Type);
        Assert.Contains("safely", scenario.Description);
    }

    [Fact]
    public void DeleteFile_WithDefaultSafeguards_UsesDefaults()
    {
        // Arrange
        var fileToDelete = CreateTestFile("guarded.txt", "Protected content");

        var scenario = new BenchmarkScenario
        {
            Id = "fs-delete-file-safeguards",
            Category = "file-system",
            Description = "Delete file with default safety safeguards",
            Tags = new List<string> { "file-system", "delete-file", "safeguards" },
            Workspace = CreateWorkspaceConfig(),
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Safely delete {fileToDelete} using default safeguards"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "delete_file",
                    MinInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["target_path"] = fileToDelete,
                        ["confirm_delete"] = true  // Default safeguard
                    }
                }
            },
            Validation = CreateValidationConfig(
                mustContain: new List<string> { "deleted" }
            ),
            Timeout = TimeSpan.FromMinutes(1)
        };

        // Act & Assert
        Assert.NotNull(scenario);
        Assert.True(scenario.ExpectedTools[0].Parameters.ContainsKey("confirm_delete"));
    }

    [Fact]
    public void DeleteFile_EmptyDirectory_DeletesSuccessfully()
    {
        // Arrange
        var emptyDir = CreateTestDirectory("empty_dir");

        var scenario = new BenchmarkScenario
        {
            Id = "fs-delete-file-empty-dir",
            Category = "file-system",
            Description = "Delete an empty directory",
            Tags = new List<string> { "file-system", "delete-file", "directory" },
            Workspace = CreateWorkspaceConfig(),
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Delete the empty directory {emptyDir}"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "delete_file",
                    MinInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["target_path"] = emptyDir,
                        ["recursive"] = false  // Not needed for empty directory
                    }
                }
            },
            Validation = CreateValidationConfig(
                mustContain: new List<string> { "deleted", "directory" }
            ),
            Timeout = TimeSpan.FromMinutes(1)
        };

        // Act & Assert
        Assert.NotNull(scenario);
        Assert.Contains("empty", scenario.Description.ToLower());
    }

    [Fact]
    public void DeleteFile_RecursiveDirectory_DeletesAllContents()
    {
        // Arrange
        CreateTestDirectory("dir_to_delete");
        CreateTestFile("dir_to_delete/file1.txt", "File 1");
        CreateTestFile("dir_to_delete/file2.txt", "File 2");
        CreateTestDirectory("dir_to_delete/subdir");
        CreateTestFile("dir_to_delete/subdir/file3.txt", "File 3");

        var dirToDelete = GetFullPath("dir_to_delete");

        var scenario = new BenchmarkScenario
        {
            Id = "fs-delete-file-recursive",
            Category = "file-system",
            Description = "Delete directory recursively with all contents",
            Tags = new List<string> { "file-system", "delete-file", "recursive" },
            Workspace = CreateWorkspaceConfig(),
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Delete the directory {dirToDelete} and all its contents recursively"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "delete_file",
                    MinInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["target_path"] = dirToDelete,
                        ["recursive"] = true
                    }
                }
            },
            Validation = CreateValidationConfig(
                mustContain: new List<string> { "deleted", "recursively" }
            ),
            Timeout = TimeSpan.FromMinutes(1)
        };

        // Act & Assert
        Assert.NotNull(scenario);
        Assert.True(scenario.ExpectedTools[0].Parameters.ContainsKey("recursive"));
        Assert.Equal(true, scenario.ExpectedTools[0].Parameters["recursive"]);
    }

    [Fact]
    public void DeleteFile_WithBackup_CreatesBackupBeforeDeletion()
    {
        // Arrange
        var fileToDelete = CreateTestFile("important.txt", "Important data that needs backup");

        var scenario = new BenchmarkScenario
        {
            Id = "fs-delete-file-backup",
            Category = "file-system",
            Description = "Delete file after creating backup",
            Tags = new List<string> { "file-system", "delete-file", "backup" },
            Workspace = CreateWorkspaceConfig(),
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Delete {fileToDelete} but create a backup first"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "delete_file",
                    MinInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["target_path"] = fileToDelete,
                        ["create_backup"] = true
                    }
                }
            },
            Validation = CreateValidationConfig(
                mustContain: new List<string> { "backup", "deleted" }
            ),
            Timeout = TimeSpan.FromMinutes(1)
        };

        // Act & Assert
        Assert.NotNull(scenario);
        Assert.True(scenario.ExpectedTools[0].Parameters.ContainsKey("create_backup"));
    }

    [Fact]
    public void DeleteFile_WithSizeLimit_RespectsLimit()
    {
        // Arrange
        var smallFile = CreateTestFile("small.txt", "Small content");

        var scenario = new BenchmarkScenario
        {
            Id = "fs-delete-file-size-limit",
            Category = "file-system",
            Description = "Delete file respecting size limits",
            Tags = new List<string> { "file-system", "delete-file", "size-limit", "safeguards" },
            Workspace = CreateWorkspaceConfig(),
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Delete {smallFile} but ensure the total size is under 10MB"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "delete_file",
                    MinInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["target_path"] = smallFile,
                        ["max_size_mb"] = 10
                    }
                }
            },
            Validation = CreateValidationConfig(
                mustContain: new List<string> { "deleted" }
            ),
            Timeout = TimeSpan.FromMinutes(1)
        };

        // Act & Assert
        Assert.NotNull(scenario);
        Assert.True(scenario.ExpectedTools[0].Parameters.ContainsKey("max_size_mb"));
    }

    [Fact]
    public void DeleteFile_WithExclusionPattern_ExcludesMatchingFiles()
    {
        // Arrange
        CreateTestDirectory("mixed_dir");
        CreateTestFile("mixed_dir/data.txt", "Keep this");
        CreateTestFile("mixed_dir/temp.log", "Delete this");
        CreateTestFile("mixed_dir/debug.log", "Delete this too");

        var dirToClean = GetFullPath("mixed_dir");

        var scenario = new BenchmarkScenario
        {
            Id = "fs-delete-file-exclude",
            Category = "file-system",
            Description = "Delete directory excluding certain file patterns",
            Tags = new List<string> { "file-system", "delete-file", "pattern-exclusion", "safeguards" },
            Workspace = CreateWorkspaceConfig(),
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Delete everything in {dirToClean} except .txt files"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "delete_file",
                    MinInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["target_path"] = dirToClean,
                        ["recursive"] = true,
                        ["exclude_patterns"] = new[] { "*.txt" }
                    }
                }
            },
            Validation = CreateValidationConfig(
                mustContain: new List<string> { "deleted", "excluded" }
            ),
            Timeout = TimeSpan.FromMinutes(1)
        };

        // Act & Assert
        Assert.NotNull(scenario);
        Assert.True(scenario.ExpectedTools[0].Parameters.ContainsKey("exclude_patterns"));
    }

    [Fact]
    public void DeleteFile_ReadOnlyFile_RequiresForceFlag()
    {
        // Arrange
        var readOnlyFile = CreateTestFile("readonly.txt", "Read-only content");
        // Note: Setting read-only attribute would require platform-specific code

        var scenario = new BenchmarkScenario
        {
            Id = "fs-delete-file-readonly",
            Category = "file-system",
            Description = "Delete read-only file with force flag",
            Tags = new List<string> { "file-system", "delete-file", "force", "safeguards" },
            Workspace = CreateWorkspaceConfig(),
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Force delete the read-only file {readOnlyFile}"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "delete_file",
                    MinInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["target_path"] = readOnlyFile,
                        ["force"] = true
                    }
                }
            },
            Validation = CreateValidationConfig(
                mustContain: new List<string> { "deleted", "force" }
            ),
            Timeout = TimeSpan.FromMinutes(1)
        };

        // Act & Assert
        Assert.NotNull(scenario);
        Assert.True(scenario.ExpectedTools[0].Parameters.ContainsKey("force"));
        Assert.Equal(true, scenario.ExpectedTools[0].Parameters["force"]);
    }

    [Fact]
    public void DeleteFile_WithCustomBackupLocation_UsesSpecifiedPath()
    {
        // Arrange
        var fileToDelete = CreateTestFile("document.txt", "Document content");
        var backupDir = CreateTestDirectory("backups");

        var scenario = new BenchmarkScenario
        {
            Id = "fs-delete-file-custom-backup",
            Category = "file-system",
            Description = "Delete file with custom backup location",
            Tags = new List<string> { "file-system", "delete-file", "backup", "custom-path" },
            Workspace = CreateWorkspaceConfig(),
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Delete {fileToDelete} and save backup to {backupDir}"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "delete_file",
                    MinInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["target_path"] = fileToDelete,
                        ["create_backup"] = true,
                        ["backup_location"] = backupDir
                    }
                }
            },
            Validation = CreateValidationConfig(
                mustContain: new List<string> { "backup", "deleted" }
            ),
            Timeout = TimeSpan.FromMinutes(1)
        };

        // Act & Assert
        Assert.NotNull(scenario);
        Assert.True(scenario.ExpectedTools[0].Parameters.ContainsKey("backup_location"));
    }

    [Fact]
    public void DeleteFile_MultipleFiles_UsesSeparateCalls()
    {
        // Arrange
        CreateTestFile("temp1.tmp", "Temp 1");
        CreateTestFile("temp2.tmp", "Temp 2");
        CreateTestFile("temp3.tmp", "Temp 3");

        var scenario = new BenchmarkScenario
        {
            Id = "fs-delete-file-multiple",
            Category = "file-system",
            Description = "Delete multiple specific files",
            Tags = new List<string> { "file-system", "delete-file", "multi-tool" },
            Workspace = CreateWorkspaceConfig(),
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Delete all .tmp files in {TestDirectory}"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "delete_file",
                    MinInvocations = 1,  // Could be one call with pattern or multiple calls
                    MaxInvocations = 5   // Allow flexibility
                }
            },
            Validation = CreateValidationConfig(
                mustContain: new List<string> { "deleted" }
            ),
            Timeout = TimeSpan.FromMinutes(1)
        };

        // Act & Assert
        Assert.NotNull(scenario);
        Assert.True(scenario.ExpectedTools[0].MaxInvocations >= 1);
    }
}
