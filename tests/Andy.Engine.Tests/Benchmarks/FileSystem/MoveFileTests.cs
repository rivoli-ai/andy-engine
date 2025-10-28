using Andy.Benchmarks.Framework;
using Andy.Benchmarks.Validators;
using Andy.Engine.Benchmarks.Framework;
using Xunit;

namespace Andy.Engine.Tests.Benchmarks.FileSystem;

/// <summary>
/// Tests for move_file tool via the engine
/// Validates file moving and renaming on the same filesystem
/// </summary>
public class MoveFileTests : FileSystemTestBase
{
    [Fact]
    public void MoveFile_SimpleMove_RelocatesFile()
    {
        // Arrange
        var sourceFile = CreateTestFile("source.txt", "Content to move");
        var destFile = GetFullPath("moved.txt");

        var scenario = new BenchmarkScenario
        {
            Id = "fs-move-file-simple",
            Category = "file-system",
            Description = "Move/rename file on same filesystem",
            Tags = new List<string> { "file-system", "move-file", "single-tool" },
            Workspace = CreateWorkspaceConfig(),
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Move the file {sourceFile} to {destFile}"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "move_file",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["source_path"] = sourceFile,
                        ["destination_path"] = destFile
                    }
                }
            },
            Validation = CreateValidationConfig(
                mustContain: new List<string> { "moved", "successfully" }
            ),
            Timeout = TimeSpan.FromMinutes(1)
        };

        // Act & Assert
        Assert.NotNull(scenario);
        Assert.Equal("move_file", scenario.ExpectedTools[0].Type);
    }

    [Fact]
    public void MoveFile_Rename_ChangesFilename()
    {
        // Arrange
        var oldName = CreateTestFile("old_name.txt", "File content");
        var newName = GetFullPath("new_name.txt");

        var scenario = new BenchmarkScenario
        {
            Id = "fs-move-file-rename",
            Category = "file-system",
            Description = "Rename file in same directory",
            Tags = new List<string> { "file-system", "move-file", "rename" },
            Workspace = CreateWorkspaceConfig(),
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Rename {oldName} to {newName}"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "move_file",
                    MinInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["source_path"] = oldName,
                        ["destination_path"] = newName
                    }
                }
            },
            Validation = CreateValidationConfig(
                mustContain: new List<string> { "renamed", "moved" }
            ),
            Timeout = TimeSpan.FromMinutes(1)
        };

        // Act & Assert
        Assert.NotNull(scenario);
        Assert.Contains("rename", scenario.Description.ToLower());
    }

    [Fact]
    public void MoveFile_ToSubdirectory_MovesToNewLocation()
    {
        // Arrange
        var sourceFile = CreateTestFile("file.txt", "Moving to subdirectory");
        var subdir = CreateTestDirectory("archive");
        var destFile = GetFullPath("archive/file.txt");

        var scenario = new BenchmarkScenario
        {
            Id = "fs-move-file-subdirectory",
            Category = "file-system",
            Description = "Move file to subdirectory",
            Tags = new List<string> { "file-system", "move-file", "subdirectory" },
            Workspace = CreateWorkspaceConfig(),
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Move {sourceFile} into the {subdir} directory"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "move_file",
                    MinInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["source_path"] = sourceFile,
                        ["destination_path"] = destFile
                    }
                }
            },
            Validation = CreateValidationConfig(
                mustContain: new List<string> { "moved" }
            ),
            Timeout = TimeSpan.FromMinutes(1)
        };

        // Act & Assert
        Assert.NotNull(scenario);
        Assert.Contains("archive", destFile);
    }

    [Fact]
    public void MoveFile_WithOverwrite_ReplacesExistingFile()
    {
        // Arrange
        var sourceFile = CreateTestFile("source.txt", "New content");
        var destFile = CreateTestFile("existing.txt", "Old content");

        var scenario = new BenchmarkScenario
        {
            Id = "fs-move-file-overwrite",
            Category = "file-system",
            Description = "Move file with overwrite enabled",
            Tags = new List<string> { "file-system", "move-file", "overwrite" },
            Workspace = CreateWorkspaceConfig(),
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Move {sourceFile} to {destFile} and overwrite the existing file"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "move_file",
                    MinInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["source_path"] = sourceFile,
                        ["destination_path"] = destFile,
                        ["overwrite"] = true
                    }
                }
            },
            Validation = CreateValidationConfig(
                mustContain: new List<string> { "moved", "overwritten" }
            ),
            Timeout = TimeSpan.FromMinutes(1)
        };

        // Act & Assert
        Assert.NotNull(scenario);
        Assert.True(scenario.ExpectedTools[0].Parameters.ContainsKey("overwrite"));
    }

    [Fact]
    public void MoveFile_WithBackup_CreatesBackupBeforeOverwrite()
    {
        // Arrange
        var sourceFile = CreateTestFile("new_version.txt", "Version 2");
        var destFile = CreateTestFile("current.txt", "Version 1");

        var scenario = new BenchmarkScenario
        {
            Id = "fs-move-file-backup",
            Category = "file-system",
            Description = "Move file with backup of existing destination",
            Tags = new List<string> { "file-system", "move-file", "backup" },
            Workspace = CreateWorkspaceConfig(),
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Move {sourceFile} to {destFile}, create a backup of the existing file first"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "move_file",
                    MinInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["source_path"] = sourceFile,
                        ["destination_path"] = destFile,
                        ["backup_existing"] = true
                    }
                }
            },
            Validation = CreateValidationConfig(
                mustContain: new List<string> { "backup", "moved" }
            ),
            Timeout = TimeSpan.FromMinutes(1)
        };

        // Act & Assert
        Assert.NotNull(scenario);
        Assert.True(scenario.ExpectedTools[0].Parameters.ContainsKey("backup_existing"));
    }

    [Fact]
    public void MoveFile_Directory_MovesEntireDirectory()
    {
        // Arrange
        CreateTestDirectory("source_folder");
        CreateTestFile("source_folder/file1.txt", "File 1");
        CreateTestFile("source_folder/file2.txt", "File 2");

        var sourceDir = GetFullPath("source_folder");
        var destDir = GetFullPath("moved_folder");

        var scenario = new BenchmarkScenario
        {
            Id = "fs-move-file-directory",
            Category = "file-system",
            Description = "Move entire directory",
            Tags = new List<string> { "file-system", "move-file", "directory" },
            Workspace = CreateWorkspaceConfig(),
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Move the directory {sourceDir} to {destDir}"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "move_file",
                    MinInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["source_path"] = sourceDir,
                        ["destination_path"] = destDir
                    }
                }
            },
            Validation = CreateValidationConfig(
                mustContain: new List<string> { "moved", "directory" }
            ),
            Timeout = TimeSpan.FromMinutes(1)
        };

        // Act & Assert
        Assert.NotNull(scenario);
        Assert.Contains("directory", scenario.Description.ToLower());
    }

    [Fact]
    public void MoveFile_CreateDestinationDirectory_AutoCreatesPath()
    {
        // Arrange
        var sourceFile = CreateTestFile("data.txt", "Data content");
        var destFile = GetFullPath("new/path/that/doesnt/exist/data.txt");

        var scenario = new BenchmarkScenario
        {
            Id = "fs-move-file-create-path",
            Category = "file-system",
            Description = "Move file to non-existent directory path",
            Tags = new List<string> { "file-system", "move-file", "auto-create" },
            Workspace = CreateWorkspaceConfig(),
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Move {sourceFile} to {destFile}, creating any necessary directories"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "move_file",
                    MinInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["source_path"] = sourceFile,
                        ["destination_path"] = destFile,
                        ["create_destination_directory"] = true
                    }
                }
            },
            Validation = CreateValidationConfig(
                mustContain: new List<string> { "moved", "created" }
            ),
            Timeout = TimeSpan.FromMinutes(1)
        };

        // Act & Assert
        Assert.NotNull(scenario);
        Assert.True(scenario.ExpectedTools[0].Parameters.ContainsKey("create_destination_directory"));
    }
}
