using Andy.Benchmarks.Framework;
using Andy.Benchmarks.Validators;
using Andy.Engine.Benchmarks.Framework;

namespace Andy.Engine.Benchmarks.Scenarios.FileSystem;

/// <summary>
/// Provides benchmark scenarios for the move_file tool
/// </summary>
public static class MoveFileScenarios
{
    /// <summary>
    /// Creates all move_file benchmark scenarios
    /// </summary>
    /// <param name="testDirectory">The test directory path to use in scenarios</param>
    public static List<BenchmarkScenario> CreateScenarios(string testDirectory)
    {
        return new List<BenchmarkScenario>
        {
            // Happy path scenarios
            CreateBasicFileMove(testDirectory),
            CreateMoveWithOverwrite(testDirectory),
            CreateRenameFile(testDirectory),
            CreateMoveDirectory(testDirectory),
            CreateMoveEmptyDirectory(testDirectory),
            CreateMoveWithBackup(testDirectory),
            CreateMoveReadOnlyFile(testDirectory),
            CreateCreateDestinationDirectory(testDirectory),
            CreateCrossVolumeMove(testDirectory),

            // Safety scenarios (should fail)
            CreateOverwriteDisabled(testDirectory),
            CreateMoveToSubdirectory(testDirectory),
            CreateSameSourceAndDestination(testDirectory),

            // Error handling scenarios
            CreateSourceNotFound(testDirectory),
            CreateInvalidSourcePath(testDirectory),
            CreateInvalidDestinationPath(testDirectory),
            CreateMissingRequiredParameter(testDirectory),
            CreateCancellationSupport(testDirectory),

            // Statistics
            CreateStatisticsValidation(testDirectory)
        };
    }

    /// <summary>
    /// Basic file move operation
    /// </summary>
    public static BenchmarkScenario CreateBasicFileMove(string testDirectory)
    {
        var sourceFile = Path.Combine(testDirectory, "moveme.txt");
        var destFile = Path.Combine(testDirectory, "moved.txt");

        return new BenchmarkScenario
        {
            Id = "fs-move-file-basic",
            Category = "file-system",
            Description = "Move file from source to destination",
            Tags = new List<string> { "file-system", "move-file", "single-tool" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
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
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "moved" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Move file with overwrite enabled
    /// </summary>
    public static BenchmarkScenario CreateMoveWithOverwrite(string testDirectory)
    {
        var sourceFile = Path.Combine(testDirectory, "moveme.txt");
        var destFile = Path.Combine(testDirectory, "existing.txt");

        return new BenchmarkScenario
        {
            Id = "fs-move-file-overwrite",
            Category = "file-system",
            Description = "Move file with overwrite enabled",
            Tags = new List<string> { "file-system", "move-file", "overwrite" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Move {sourceFile} to {destFile} and overwrite if it exists"
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
                        ["destination_path"] = destFile,
                        ["overwrite"] = true
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "moved" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateRenameFile(string testDirectory)
    {
        var sourceFile = Path.Combine(testDirectory, "oldname.txt");
        var destFile = Path.Combine(testDirectory, "newname.txt");

        return new BenchmarkScenario
        {
            Id = "fs-move-file-rename",
            Category = "file-system",
            Description = "Rename file in same directory",
            Tags = new List<string> { "file-system", "move-file", "rename" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection
            {
                Prompts = new List<string> { $"Rename {sourceFile} to {destFile}" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "move_file",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object> { ["source_path"] = sourceFile, ["destination_path"] = destFile }
                }
            },
            Validation = new ValidationConfig { ResponseMustContain = new List<string> { "moved", "renamed" }, MustNotAskUser = true },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateMoveDirectory(string testDirectory)
    {
        var sourceDir = Path.Combine(testDirectory, "source_dir");
        var destDir = Path.Combine(testDirectory, "dest_dir");

        return new BenchmarkScenario
        {
            Id = "fs-move-file-directory",
            Category = "file-system",
            Description = "Move directory with all contents",
            Tags = new List<string> { "file-system", "move-file", "directory" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection
            {
                Prompts = new List<string> { $"Move the directory {sourceDir} to {destDir}" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "move_file",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object> { ["source_path"] = sourceDir, ["destination_path"] = destDir }
                }
            },
            Validation = new ValidationConfig { ResponseMustContain = new List<string> { "moved" }, MustNotAskUser = true },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateMoveEmptyDirectory(string testDirectory)
    {
        var sourceDir = Path.Combine(testDirectory, "empty_source");
        var destDir = Path.Combine(testDirectory, "empty_dest");

        return new BenchmarkScenario
        {
            Id = "fs-move-file-empty-directory",
            Category = "file-system",
            Description = "Move empty directory",
            Tags = new List<string> { "file-system", "move-file", "directory" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection { Prompts = new List<string> { $"Move the empty directory {sourceDir} to {destDir}" } },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation { Type = "move_file", MinInvocations = 1, MaxInvocations = 1, Parameters = new Dictionary<string, object> { ["source_path"] = sourceDir, ["destination_path"] = destDir } }
            },
            Validation = new ValidationConfig { ResponseMustContain = new List<string> { "moved" }, MustNotAskUser = true },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateMoveWithBackup(string testDirectory)
    {
        var sourceFile = Path.Combine(testDirectory, "source.txt");
        var destFile = Path.Combine(testDirectory, "existing.txt");

        return new BenchmarkScenario
        {
            Id = "fs-move-file-with-backup",
            Category = "file-system",
            Description = "Move file with backup of existing destination",
            Tags = new List<string> { "file-system", "move-file", "backup" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection { Prompts = new List<string> { $"Move {sourceFile} to {destFile}, backup the existing file first" } },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation { Type = "move_file", MinInvocations = 1, MaxInvocations = 1, Parameters = new Dictionary<string, object> { ["source_path"] = sourceFile, ["destination_path"] = destFile, ["overwrite"] = true, ["backup_existing"] = true } }
            },
            Validation = new ValidationConfig { ResponseMustContain = new List<string> { "moved", "backup" }, MustNotAskUser = true },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateMoveReadOnlyFile(string testDirectory)
    {
        var sourceFile = Path.Combine(testDirectory, "readonly.txt");
        var destFile = Path.Combine(testDirectory, "moved_readonly.txt");

        return new BenchmarkScenario
        {
            Id = "fs-move-file-readonly",
            Category = "file-system",
            Description = "Move read-only file preserving attributes",
            Tags = new List<string> { "file-system", "move-file", "readonly" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection { Prompts = new List<string> { $"Move the read-only file {sourceFile} to {destFile}" } },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation { Type = "move_file", MinInvocations = 1, MaxInvocations = 1, Parameters = new Dictionary<string, object> { ["source_path"] = sourceFile, ["destination_path"] = destFile } }
            },
            Validation = new ValidationConfig { ResponseMustContain = new List<string> { "moved" }, MustNotAskUser = true },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateCreateDestinationDirectory(string testDirectory)
    {
        var sourceFile = Path.Combine(testDirectory, "source.txt");
        var destFile = Path.Combine(testDirectory, "nested", "deep", "path", "destination.txt");

        return new BenchmarkScenario
        {
            Id = "fs-move-file-create-dest-dir",
            Category = "file-system",
            Description = "Move file creating nested destination directories",
            Tags = new List<string> { "file-system", "move-file" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection { Prompts = new List<string> { $"Move {sourceFile} to {destFile}, create directories if needed" } },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation { Type = "move_file", MinInvocations = 1, MaxInvocations = 1, Parameters = new Dictionary<string, object> { ["source_path"] = sourceFile, ["destination_path"] = destFile, ["create_destination_directory"] = true } }
            },
            Validation = new ValidationConfig { ResponseMustContain = new List<string> { "moved" }, MustNotAskUser = true },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateCrossVolumeMove(string testDirectory)
    {
        var sourceFile = Path.Combine(testDirectory, "source.txt");
        var destFile = Path.Combine(Path.GetTempPath(), "cross_volume_test", "destination.txt");

        return new BenchmarkScenario
        {
            Id = "fs-move-file-cross-volume",
            Category = "file-system",
            Description = "Move file across volumes (copy + delete)",
            Tags = new List<string> { "file-system", "move-file", "cross-volume" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection { Prompts = new List<string> { $"Move {sourceFile} to {destFile}" } },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation { Type = "move_file", MinInvocations = 1, MaxInvocations = 1, Parameters = new Dictionary<string, object> { ["source_path"] = sourceFile, ["destination_path"] = destFile } }
            },
            Validation = new ValidationConfig { ResponseMustContain = new List<string> { "moved" }, MustNotAskUser = true },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateOverwriteDisabled(string testDirectory)
    {
        var sourceFile = Path.Combine(testDirectory, "source.txt");
        var destFile = Path.Combine(testDirectory, "existing.txt");

        return new BenchmarkScenario
        {
            Id = "fs-move-file-no-overwrite",
            Category = "file-system",
            Description = "Move file without overwrite should fail when destination exists",
            Tags = new List<string> { "file-system", "move-file", "error-handling" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection { Prompts = new List<string> { $"Move {sourceFile} to {destFile}, do NOT overwrite if it exists" } },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation { Type = "move_file", MinInvocations = 1, MaxInvocations = 1, Parameters = new Dictionary<string, object> { ["source_path"] = sourceFile, ["destination_path"] = destFile, ["overwrite"] = false } }
            },
            Validation = new ValidationConfig { ResponseMustContainAny = new List<string> { "exists", "already" }, MustNotAskUser = true },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateMoveToSubdirectory(string testDirectory)
    {
        var sourceDir = Path.Combine(testDirectory, "source_dir");
        var destDir = Path.Combine(testDirectory, "source_dir", "subdir");

        return new BenchmarkScenario
        {
            Id = "fs-move-file-to-subdirectory",
            Category = "file-system",
            Description = "Move directory into its own subdirectory should fail",
            Tags = new List<string> { "file-system", "move-file", "error-handling" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection { Prompts = new List<string> { $"Move {sourceDir} to {destDir}" } },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation { Type = "move_file", MinInvocations = 1, MaxInvocations = 1, Parameters = new Dictionary<string, object> { ["source_path"] = sourceDir, ["destination_path"] = destDir } }
            },
            Validation = new ValidationConfig { ResponseMustContainAny = new List<string> { "subdirectory", "circular", "into itself" }, MustNotAskUser = true },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateSameSourceAndDestination(string testDirectory)
    {
        var sourceFile = Path.Combine(testDirectory, "same.txt");

        return new BenchmarkScenario
        {
            Id = "fs-move-file-same-paths",
            Category = "file-system",
            Description = "Move to same location should fail",
            Tags = new List<string> { "file-system", "move-file", "error-handling" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection { Prompts = new List<string> { $"Move {sourceFile} to itself {sourceFile}" } },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation { Type = "move_file", MinInvocations = 1, MaxInvocations = 1, Parameters = new Dictionary<string, object> { ["source_path"] = sourceFile, ["destination_path"] = sourceFile } }
            },
            Validation = new ValidationConfig { ResponseMustContainAny = new List<string> { "same", "identical" }, MustNotAskUser = true },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateSourceNotFound(string testDirectory)
    {
        var nonExistentFile = Path.Combine(testDirectory, "nonexistent.txt");
        var destFile = Path.Combine(testDirectory, "dest.txt");

        return new BenchmarkScenario
        {
            Id = "fs-move-file-not-found",
            Category = "file-system",
            Description = "Move non-existent file should fail",
            Tags = new List<string> { "file-system", "move-file", "error-handling" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection { Prompts = new List<string> { $"Move {nonExistentFile} to {destFile}" } },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation { Type = "move_file", MinInvocations = 1, MaxInvocations = 1, Parameters = new Dictionary<string, object> { ["source_path"] = nonExistentFile, ["destination_path"] = destFile } }
            },
            Validation = new ValidationConfig { ResponseMustContainAny = new List<string> { "does not exist", "not exist", "not found" }, MustNotAskUser = true },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateInvalidSourcePath(string testDirectory)
    {
        var destFile = Path.Combine(testDirectory, "dest.txt");

        return new BenchmarkScenario
        {
            Id = "fs-move-file-invalid-source",
            Category = "file-system",
            Description = "Move with invalid source path should fail",
            Tags = new List<string> { "file-system", "move-file", "error-handling" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection { Prompts = new List<string> { $"Move a file with empty source path '' to {destFile}" } },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation { Type = "move_file", MinInvocations = 0, MaxInvocations = 1, Parameters = new Dictionary<string, object> { ["source_path"] = "", ["destination_path"] = destFile } }
            },
            Validation = new ValidationConfig { ResponseMustContainAny = new List<string> { "source", "path" }, MustNotAskUser = true },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateInvalidDestinationPath(string testDirectory)
    {
        var sourceFile = Path.Combine(testDirectory, "source.txt");

        return new BenchmarkScenario
        {
            Id = "fs-move-file-invalid-destination",
            Category = "file-system",
            Description = "Move with invalid destination path should fail",
            Tags = new List<string> { "file-system", "move-file", "error-handling" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection { Prompts = new List<string> { $"Move {sourceFile} to an empty destination path ''" } },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation { Type = "move_file", MinInvocations = 0, MaxInvocations = 1, Parameters = new Dictionary<string, object> { ["source_path"] = sourceFile, ["destination_path"] = "" } }
            },
            Validation = new ValidationConfig { ResponseMustContainAny = new List<string> { "destination", "path" }, MustNotAskUser = true },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateMissingRequiredParameter(string testDirectory)
    {
        var sourceFile = Path.Combine(testDirectory, "source.txt");

        return new BenchmarkScenario
        {
            Id = "fs-move-file-missing-parameter",
            Category = "file-system",
            Description = "Move without destination_path should fail",
            Tags = new List<string> { "file-system", "move-file", "error-handling" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection { Prompts = new List<string> { $"Call move_file with source {sourceFile} but don't provide destination_path" } },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation { Type = "move_file", MinInvocations = 0, MaxInvocations = 1, Parameters = new Dictionary<string, object> { ["source_path"] = sourceFile } }
            },
            Validation = new ValidationConfig { ResponseMustContainAny = new List<string> { "destination", "parameter" }, MustNotAskUser = true },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateCancellationSupport(string testDirectory)
    {
        var sourceFile = Path.Combine(testDirectory, "large.txt");
        var destFile = Path.Combine(testDirectory, "moved_large.txt");

        return new BenchmarkScenario
        {
            Id = "fs-move-file-cancellation",
            Category = "file-system",
            Description = "Move operation should handle cancellation gracefully",
            Tags = new List<string> { "file-system", "move-file", "cancellation" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection { Prompts = new List<string> { $"Move the large file {sourceFile} to {destFile}" } },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation { Type = "move_file", MinInvocations = 1, MaxInvocations = 1, Parameters = new Dictionary<string, object> { ["source_path"] = sourceFile, ["destination_path"] = destFile } }
            },
            Validation = new ValidationConfig { MustNotAskUser = true },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateStatisticsValidation(string testDirectory)
    {
        var sourceFile = Path.Combine(testDirectory, "stats_test.txt");
        var destFile = Path.Combine(testDirectory, "stats_moved.txt");

        return new BenchmarkScenario
        {
            Id = "fs-move-file-statistics",
            Category = "file-system",
            Description = "Move file and verify statistics",
            Tags = new List<string> { "file-system", "move-file", "statistics" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection { Prompts = new List<string> { $"Move {sourceFile} to {destFile}" } },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation { Type = "move_file", MinInvocations = 1, MaxInvocations = 1, Parameters = new Dictionary<string, object> { ["source_path"] = sourceFile, ["destination_path"] = destFile } }
            },
            Validation = new ValidationConfig { ResponseMustContain = new List<string> { "moved" }, MustNotAskUser = true },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }
}
