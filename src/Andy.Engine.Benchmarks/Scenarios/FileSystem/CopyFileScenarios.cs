using Andy.Benchmarks.Framework;
using Andy.Benchmarks.Validators;
using Andy.Engine.Benchmarks.Framework;

namespace Andy.Engine.Benchmarks.Scenarios.FileSystem;

/// <summary>
/// Provides benchmark scenarios for the copy_file tool
/// </summary>
public static class CopyFileScenarios
{
    /// <summary>
    /// Creates all copy_file benchmark scenarios
    /// </summary>
    /// <param name="testDirectory">The test directory path to use in scenarios</param>
    public static List<BenchmarkScenario> CreateScenarios(string testDirectory)
    {
        return new List<BenchmarkScenario>
        {
            // Happy path scenarios
            CreateBasicFileCopy(testDirectory),
            CreateCopyWithOverwrite(testDirectory),
            CreateRecursiveDirectoryCopy(testDirectory),

            // Advanced copy scenarios
            CreateCopyToDirectory(testDirectory),
            CreateCopyWithTrailingSeparator(testDirectory),
            CreatePreserveTimestamps(testDirectory),
            CreateFollowSymlinks(testDirectory),
            CreateCopyEmptyDirectory(testDirectory),
            CreateExcludePatterns(testDirectory),
            CreateExcludeDirectories(testDirectory),
            CreateCreateDestinationDirectory(testDirectory),

            // Error handling scenarios
            CreateOverwriteDisabled(testDirectory),
            CreateSourceNotFound(testDirectory),
            CreateInvalidSourcePath(testDirectory),
            CreateInvalidDestinationPath(testDirectory),
            CreateMissingRequiredParameter(testDirectory),
            CreatePathTraversalSecurity(testDirectory)
        };
    }

    /// <summary>
    /// Basic file copy operation
    /// </summary>
    public static BenchmarkScenario CreateBasicFileCopy(string testDirectory)
    {
        var sourceFile = Path.Combine(testDirectory, "source.txt");
        var destFile = Path.Combine(testDirectory, "destination.txt");

        return new BenchmarkScenario
        {
            Id = "fs-copy-file-basic",
            Category = "file-system",
            Description = "Copy file from source to destination",
            Tags = new List<string> { "file-system", "copy-file", "single-tool" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Copy the file {sourceFile} to {destFile}"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "copy_file",
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
                ResponseMustContain = new List<string> { "copied" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Copy file with overwrite enabled
    /// </summary>
    public static BenchmarkScenario CreateCopyWithOverwrite(string testDirectory)
    {
        var sourceFile = Path.Combine(testDirectory, "source.txt");
        var destFile = Path.Combine(testDirectory, "existing_dest.txt");

        return new BenchmarkScenario
        {
            Id = "fs-copy-file-overwrite",
            Category = "file-system",
            Description = "Copy file with overwrite enabled",
            Tags = new List<string> { "file-system", "copy-file", "overwrite" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Copy {sourceFile} to {destFile} and overwrite if it exists"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "copy_file",
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
                ResponseMustContain = new List<string> { "copied" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Copy with overwrite disabled (should fail)
    /// </summary>
    public static BenchmarkScenario CreateOverwriteDisabled(string testDirectory)
    {
        var sourceFile = Path.Combine(testDirectory, "source.txt");
        var destFile = Path.Combine(testDirectory, "existing_dest.txt");

        return new BenchmarkScenario
        {
            Id = "fs-copy-file-no-overwrite",
            Category = "file-system",
            Description = "Copy file with overwrite disabled should fail when destination exists",
            Tags = new List<string> { "file-system", "copy-file", "error-handling" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Copy {sourceFile} to {destFile} but do NOT overwrite if the file already exists"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "copy_file",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["source_path"] = sourceFile,
                        ["destination_path"] = destFile,
                        ["overwrite"] = false
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "already exists", "error" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Recursive directory copy
    /// </summary>
    public static BenchmarkScenario CreateRecursiveDirectoryCopy(string testDirectory)
    {
        var sourceDir = Path.Combine(testDirectory, "source_dir");
        var destDir = Path.Combine(testDirectory, "dest_dir");

        return new BenchmarkScenario
        {
            Id = "fs-copy-file-recursive",
            Category = "file-system",
            Description = "Copy directory recursively",
            Tags = new List<string> { "file-system", "copy-file", "recursive" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Copy the entire directory {sourceDir} to {destDir} including all subdirectories"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "copy_file",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["source_path"] = sourceDir,
                        ["destination_path"] = destDir,
                        ["recursive"] = true
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "copied", "directory" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Copy file to existing directory (should preserve filename)
    /// </summary>
    public static BenchmarkScenario CreateCopyToDirectory(string testDirectory)
    {
        var sourceFile = Path.Combine(testDirectory, "document.pdf");
        var destDir = Path.Combine(testDirectory, "documents");

        return new BenchmarkScenario
        {
            Id = "fs-copy-file-to-directory",
            Category = "file-system",
            Description = "Copy file to directory preserving filename",
            Tags = new List<string> { "file-system", "copy-file", "directory-dest" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Copy {sourceFile} into the {destDir} directory"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "copy_file",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["source_path"] = sourceFile,
                        ["destination_path"] = destDir
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "copied" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Copy with trailing directory separator
    /// </summary>
    public static BenchmarkScenario CreateCopyWithTrailingSeparator(string testDirectory)
    {
        var sourceFile = Path.Combine(testDirectory, "source.txt");
        var destDir = Path.Combine(testDirectory, "dest_dir") + Path.DirectorySeparatorChar;

        return new BenchmarkScenario
        {
            Id = "fs-copy-file-trailing-separator",
            Category = "file-system",
            Description = "Copy file with trailing directory separator",
            Tags = new List<string> { "file-system", "copy-file", "edge-case" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Copy {sourceFile} to {destDir} and create the directory if needed"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "copy_file",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["source_path"] = sourceFile,
                        ["destination_path"] = destDir,
                        ["create_destination_directory"] = true
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "copied" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Copy with timestamp preservation
    /// </summary>
    public static BenchmarkScenario CreatePreserveTimestamps(string testDirectory)
    {
        var sourceFile = Path.Combine(testDirectory, "timestamped.txt");
        var destFile = Path.Combine(testDirectory, "copy.txt");

        return new BenchmarkScenario
        {
            Id = "fs-copy-file-timestamps",
            Category = "file-system",
            Description = "Copy file preserving timestamps",
            Tags = new List<string> { "file-system", "copy-file", "metadata" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Copy {sourceFile} to {destFile} and preserve the original timestamps"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "copy_file",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["source_path"] = sourceFile,
                        ["destination_path"] = destFile,
                        ["preserve_timestamps"] = true
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "copied" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Copy with symbolic links handling
    /// </summary>
    public static BenchmarkScenario CreateFollowSymlinks(string testDirectory)
    {
        var sourceFile = Path.Combine(testDirectory, "symlink.txt");
        var destFile = Path.Combine(testDirectory, "symlink_copy.txt");

        return new BenchmarkScenario
        {
            Id = "fs-copy-file-follow-symlinks",
            Category = "file-system",
            Description = "Copy file following symbolic links",
            Tags = new List<string> { "file-system", "copy-file", "symlinks" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Copy {sourceFile} to {destFile}, following symbolic links"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "copy_file",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["source_path"] = sourceFile,
                        ["destination_path"] = destFile,
                        ["follow_symlinks"] = true
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "copied" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Copy empty directory
    /// </summary>
    public static BenchmarkScenario CreateCopyEmptyDirectory(string testDirectory)
    {
        var sourceDir = Path.Combine(testDirectory, "empty_source");
        var destDir = Path.Combine(testDirectory, "empty_dest");

        return new BenchmarkScenario
        {
            Id = "fs-copy-file-empty-dir",
            Category = "file-system",
            Description = "Copy empty directory",
            Tags = new List<string> { "file-system", "copy-file", "edge-case" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Copy the empty directory {sourceDir} to {destDir}"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "copy_file",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["source_path"] = sourceDir,
                        ["destination_path"] = destDir,
                        ["recursive"] = true
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "copied" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Copy with exclude patterns
    /// </summary>
    public static BenchmarkScenario CreateExcludePatterns(string testDirectory)
    {
        var sourceDir = Path.Combine(testDirectory, "source_with_logs");
        var destDir = Path.Combine(testDirectory, "dest_no_logs");

        return new BenchmarkScenario
        {
            Id = "fs-copy-file-exclude-patterns",
            Category = "file-system",
            Description = "Copy directory excluding log files",
            Tags = new List<string> { "file-system", "copy-file", "pattern-exclusion" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Copy {sourceDir} to {destDir} but exclude all .log and .tmp files"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "copy_file",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["source_path"] = sourceDir,
                        ["destination_path"] = destDir,
                        ["recursive"] = true,
                        ["exclude_patterns"] = new[] { "*.log", "*.tmp" }
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "copied", "excluded" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Copy with directory exclusions
    /// </summary>
    public static BenchmarkScenario CreateExcludeDirectories(string testDirectory)
    {
        var sourceDir = Path.Combine(testDirectory, "project");
        var destDir = Path.Combine(testDirectory, "project_backup");

        return new BenchmarkScenario
        {
            Id = "fs-copy-file-exclude-dirs",
            Category = "file-system",
            Description = "Copy directory excluding .git and node_modules",
            Tags = new List<string> { "file-system", "copy-file", "directory-exclusion" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Copy {sourceDir} to {destDir} but exclude .git and node_modules directories"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "copy_file",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["source_path"] = sourceDir,
                        ["destination_path"] = destDir,
                        ["recursive"] = true,
                        ["exclude_patterns"] = new[] { ".git", "node_modules" }
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "copied" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Create destination directory automatically
    /// </summary>
    public static BenchmarkScenario CreateCreateDestinationDirectory(string testDirectory)
    {
        var sourceFile = Path.Combine(testDirectory, "source.txt");
        var destFile = Path.Combine(testDirectory, "new_dir", "subdir", "destination.txt");

        return new BenchmarkScenario
        {
            Id = "fs-copy-file-create-dest-dir",
            Category = "file-system",
            Description = "Copy file creating destination directories",
            Tags = new List<string> { "file-system", "copy-file", "create-directory" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Copy {sourceFile} to {destFile} and create any necessary parent directories"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "copy_file",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["source_path"] = sourceFile,
                        ["destination_path"] = destFile,
                        ["create_destination_directory"] = true
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "copied" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    // Error handling scenarios

    /// <summary>
    /// Source file not found
    /// </summary>
    public static BenchmarkScenario CreateSourceNotFound(string testDirectory)
    {
        var nonExistentFile = Path.Combine(testDirectory, "nonexistent.txt");
        var destFile = Path.Combine(testDirectory, "destination.txt");

        return new BenchmarkScenario
        {
            Id = "fs-copy-file-source-not-found",
            Category = "file-system",
            Description = "Copy non-existent file should fail gracefully",
            Tags = new List<string> { "file-system", "copy-file", "error-handling" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Copy the file {nonExistentFile} to {destFile}"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "copy_file",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["source_path"] = nonExistentFile,
                        ["destination_path"] = destFile
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "not found", "does not exist", "error" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Invalid/empty source path
    /// </summary>
    public static BenchmarkScenario CreateInvalidSourcePath(string testDirectory)
    {
        var destFile = Path.Combine(testDirectory, "destination.txt");

        return new BenchmarkScenario
        {
            Id = "fs-copy-file-invalid-source",
            Category = "file-system",
            Description = "Copy with invalid source path should fail gracefully",
            Tags = new List<string> { "file-system", "copy-file", "error-handling" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Copy an empty path '' to {destFile}"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "copy_file",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["source_path"] = "",
                        ["destination_path"] = destFile
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "invalid", "error" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Invalid/empty destination path
    /// </summary>
    public static BenchmarkScenario CreateInvalidDestinationPath(string testDirectory)
    {
        var sourceFile = Path.Combine(testDirectory, "source.txt");

        return new BenchmarkScenario
        {
            Id = "fs-copy-file-invalid-destination",
            Category = "file-system",
            Description = "Copy with invalid destination path should fail gracefully",
            Tags = new List<string> { "file-system", "copy-file", "error-handling" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Copy {sourceFile} to an empty path ''"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "copy_file",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["source_path"] = sourceFile,
                        ["destination_path"] = ""
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "invalid", "error" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Missing required parameter
    /// </summary>
    public static BenchmarkScenario CreateMissingRequiredParameter(string testDirectory)
    {
        var sourceFile = Path.Combine(testDirectory, "source.txt");

        return new BenchmarkScenario
        {
            Id = "fs-copy-file-missing-param",
            Category = "file-system",
            Description = "Copy without destination should fail gracefully",
            Tags = new List<string> { "file-system", "copy-file", "error-handling" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Copy the file {sourceFile} but don't specify where to copy it to"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "copy_file",
                    MinInvocations = 0,  // Might not even invoke the tool
                    MaxInvocations = 1
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "destination", "required", "error" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Path traversal security
    /// </summary>
    public static BenchmarkScenario CreatePathTraversalSecurity(string testDirectory)
    {
        var sourceFile = Path.Combine(testDirectory, "source.txt");
        var dangerousPath = Path.Combine(testDirectory, "..", "..", "dangerous.txt");

        return new BenchmarkScenario
        {
            Id = "fs-copy-file-path-traversal",
            Category = "file-system",
            Description = "Copy with path traversal should be blocked",
            Tags = new List<string> { "file-system", "copy-file", "security" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Copy {sourceFile} to {dangerousPath}"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "copy_file",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["source_path"] = sourceFile,
                        ["destination_path"] = dangerousPath
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "outside", "allowed", "error" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }
}
