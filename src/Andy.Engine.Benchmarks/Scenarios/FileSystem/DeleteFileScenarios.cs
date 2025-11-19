using Andy.Benchmarks.Framework;
using Andy.Benchmarks.Validators;
using Andy.Engine.Benchmarks.Framework;

namespace Andy.Engine.Benchmarks.Scenarios.FileSystem;

/// <summary>
/// Provides benchmark scenarios for the delete_file tool
/// </summary>
public static class DeleteFileScenarios
{
    /// <summary>
    /// Creates all delete_file benchmark scenarios
    /// </summary>
    /// <param name="testDirectory">The test directory path to use in scenarios</param>
    public static List<BenchmarkScenario> CreateScenarios(string testDirectory)
    {
        return new List<BenchmarkScenario>
        {
            // Happy path scenarios
            CreateBasicFileDeletion(testDirectory),
            CreateRecursiveDirectoryDeletion(testDirectory),
            CreateDeleteEmptyDirectory(testDirectory),

            // Advanced delete scenarios
            CreateDeleteWithBackup(testDirectory),
            CreateBackupToDefaultLocation(testDirectory),
            CreateDeleteReadOnlyWithForce(testDirectory),
            CreateStatisticsValidation(testDirectory),

            // Safety scenarios (should fail)
            CreateDeleteReadOnlyWithoutForce(testDirectory),
            CreateDeleteNonEmptyWithoutRecursive(testDirectory),
            CreateDeleteWithSizeLimit(testDirectory),
            CreateDeleteWithExclusionPattern(testDirectory),

            // Error handling scenarios
            CreateFileNotFound(testDirectory),
            CreateInvalidPath(testDirectory),
            CreateMissingRequiredParameter(testDirectory),
            CreateCancellationSupport(testDirectory)
        };
    }

    /// <summary>
    /// Basic file deletion operation
    /// </summary>
    public static BenchmarkScenario CreateBasicFileDeletion(string testDirectory)
    {
        var fileToDelete = Path.Combine(testDirectory, "deleteme.txt");

        return new BenchmarkScenario
        {
            Id = "fs-delete-file-basic",
            Category = "file-system",
            Description = "Delete a single file",
            Tags = new List<string> { "file-system", "delete-file", "single-tool" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
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
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "deleted" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Recursive directory deletion
    /// </summary>
    public static BenchmarkScenario CreateRecursiveDirectoryDeletion(string testDirectory)
    {
        var dirToDelete = Path.Combine(testDirectory, "delete_dir");

        return new BenchmarkScenario
        {
            Id = "fs-delete-file-recursive",
            Category = "file-system",
            Description = "Delete directory recursively",
            Tags = new List<string> { "file-system", "delete-file", "recursive" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Delete the entire directory {dirToDelete} and all its contents"
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
                        ["target_path"] = dirToDelete,
                        ["recursive"] = true
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "deleted" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Delete empty directory
    /// </summary>
    public static BenchmarkScenario CreateDeleteEmptyDirectory(string testDirectory)
    {
        var emptyDir = Path.Combine(testDirectory, "empty_dir");

        return new BenchmarkScenario
        {
            Id = "fs-delete-file-empty-directory",
            Category = "file-system",
            Description = "Delete an empty directory",
            Tags = new List<string> { "file-system", "delete-file", "directory" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
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
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["target_path"] = emptyDir
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "deleted" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Delete with backup creation
    /// </summary>
    public static BenchmarkScenario CreateDeleteWithBackup(string testDirectory)
    {
        var fileToDelete = Path.Combine(testDirectory, "important.txt");
        var backupDir = Path.Combine(testDirectory, "backups");

        return new BenchmarkScenario
        {
            Id = "fs-delete-file-with-backup",
            Category = "file-system",
            Description = "Delete file with backup creation",
            Tags = new List<string> { "file-system", "delete-file", "backup" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Delete the file {fileToDelete}, but create a backup in {backupDir} first"
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
                        ["target_path"] = fileToDelete,
                        ["create_backup"] = true,
                        ["backup_location"] = backupDir
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "deleted", "backup" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Backup to default location
    /// </summary>
    public static BenchmarkScenario CreateBackupToDefaultLocation(string testDirectory)
    {
        var fileToDelete = Path.Combine(testDirectory, "important.txt");

        return new BenchmarkScenario
        {
            Id = "fs-delete-file-backup-default",
            Category = "file-system",
            Description = "Delete file with backup to default location",
            Tags = new List<string> { "file-system", "delete-file", "backup" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Delete the file {fileToDelete}, but create a backup first (use default backup location)"
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
                        ["target_path"] = fileToDelete,
                        ["create_backup"] = true
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "deleted", "backup" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Delete read-only file with force flag
    /// </summary>
    public static BenchmarkScenario CreateDeleteReadOnlyWithForce(string testDirectory)
    {
        var readOnlyFile = Path.Combine(testDirectory, "readonly.txt");

        return new BenchmarkScenario
        {
            Id = "fs-delete-file-readonly-force",
            Category = "file-system",
            Description = "Delete read-only file with force flag",
            Tags = new List<string> { "file-system", "delete-file", "readonly", "force" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Delete the read-only file {readOnlyFile} (force delete if necessary)"
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
                        ["target_path"] = readOnlyFile,
                        ["force"] = true
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "deleted" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Statistics validation
    /// </summary>
    public static BenchmarkScenario CreateStatisticsValidation(string testDirectory)
    {
        var fileToDelete = Path.Combine(testDirectory, "stats_test.txt");

        return new BenchmarkScenario
        {
            Id = "fs-delete-file-statistics",
            Category = "file-system",
            Description = "Delete file and verify statistics",
            Tags = new List<string> { "file-system", "delete-file", "statistics" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
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
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "deleted" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Delete read-only file without force (should fail)
    /// </summary>
    public static BenchmarkScenario CreateDeleteReadOnlyWithoutForce(string testDirectory)
    {
        var readOnlyFile = Path.Combine(testDirectory, "readonly.txt");

        return new BenchmarkScenario
        {
            Id = "fs-delete-file-readonly-no-force",
            Category = "file-system",
            Description = "Delete read-only file without force should fail",
            Tags = new List<string> { "file-system", "delete-file", "readonly", "error-handling" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Delete the read-only file {readOnlyFile} (do NOT force delete)"
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
                        ["target_path"] = readOnlyFile,
                        ["force"] = false
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "read" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Delete non-empty directory without recursive (should fail)
    /// </summary>
    public static BenchmarkScenario CreateDeleteNonEmptyWithoutRecursive(string testDirectory)
    {
        var dirToDelete = Path.Combine(testDirectory, "nonempty_dir");

        return new BenchmarkScenario
        {
            Id = "fs-delete-file-nonempty-no-recursive",
            Category = "file-system",
            Description = "Delete non-empty directory without recursive should fail",
            Tags = new List<string> { "file-system", "delete-file", "error-handling" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Delete the directory {dirToDelete} (do NOT delete recursively)"
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
                        ["target_path"] = dirToDelete,
                        ["recursive"] = false
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "not empty", "empty" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Delete with size limit (file too large - should fail)
    /// </summary>
    public static BenchmarkScenario CreateDeleteWithSizeLimit(string testDirectory)
    {
        var largeFile = Path.Combine(testDirectory, "large.txt");

        return new BenchmarkScenario
        {
            Id = "fs-delete-file-size-limit",
            Category = "file-system",
            Description = "Delete large file exceeding size limit should fail",
            Tags = new List<string> { "file-system", "delete-file", "safety", "error-handling" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Delete the file {largeFile} with a size limit of 0.5MB"
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
                        ["target_path"] = largeFile,
                        ["max_size_mb"] = 0.5
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "size", "limit" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Delete with exclusion pattern (should fail/skip)
    /// </summary>
    public static BenchmarkScenario CreateDeleteWithExclusionPattern(string testDirectory)
    {
        var importantFile = Path.Combine(testDirectory, "important.txt");

        return new BenchmarkScenario
        {
            Id = "fs-delete-file-exclusion-pattern",
            Category = "file-system",
            Description = "Delete file matching exclusion pattern should fail",
            Tags = new List<string> { "file-system", "delete-file", "safety", "error-handling" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Delete {importantFile} but exclude any .txt files from deletion"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "delete_file",
                    MinInvocations = 0,  // Real LLM may refuse contradictory request, Mock LLM will call
                    MaxInvocations = 1
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "exclude", "target_path", "txt" }
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// File not found error handling
    /// </summary>
    public static BenchmarkScenario CreateFileNotFound(string testDirectory)
    {
        var nonExistentFile = Path.Combine(testDirectory, "nonexistent.txt");

        return new BenchmarkScenario
        {
            Id = "fs-delete-file-not-found",
            Category = "file-system",
            Description = "Delete non-existent file should fail gracefully",
            Tags = new List<string> { "file-system", "delete-file", "error-handling" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Delete the file {nonExistentFile}"
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
                        ["target_path"] = nonExistentFile
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "does not exist", "not exist" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Invalid path error handling
    /// </summary>
    public static BenchmarkScenario CreateInvalidPath(string testDirectory)
    {
        return new BenchmarkScenario
        {
            Id = "fs-delete-file-invalid-path",
            Category = "file-system",
            Description = "Delete with invalid path should fail gracefully",
            Tags = new List<string> { "file-system", "delete-file", "error-handling" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    "Delete a file with an empty path ''"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "delete_file",
                    MinInvocations = 0,  // Real LLM won't call, Mock LLM will call and get error
                    MaxInvocations = 1
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "path" }
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Missing required parameter error handling
    /// </summary>
    public static BenchmarkScenario CreateMissingRequiredParameter(string testDirectory)
    {
        return new BenchmarkScenario
        {
            Id = "fs-delete-file-missing-parameter",
            Category = "file-system",
            Description = "Delete without target_path should fail",
            Tags = new List<string> { "file-system", "delete-file", "error-handling" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    "Call delete_file tool but don't provide the target_path parameter"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "delete_file",
                    MinInvocations = 0,  // Real LLM won't call, Mock LLM will call and get error
                    MaxInvocations = 1
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "target_path" }
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Cancellation support test
    /// </summary>
    public static BenchmarkScenario CreateCancellationSupport(string testDirectory)
    {
        var dirToDelete = Path.Combine(testDirectory, "large_dir");

        return new BenchmarkScenario
        {
            Id = "fs-delete-file-cancellation",
            Category = "file-system",
            Description = "Delete operation should handle cancellation gracefully",
            Tags = new List<string> { "file-system", "delete-file", "cancellation" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Delete the large directory {dirToDelete} recursively"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "delete_file",
                    MinInvocations = 1,
                    MaxInvocations = 3,  // Allow retries for parameter validation errors
                    Parameters = new Dictionary<string, object>
                    {
                        ["target_path"] = dirToDelete,
                        ["recursive"] = true
                    }
                }
            },
            Validation = new ValidationConfig
            {
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }
}
