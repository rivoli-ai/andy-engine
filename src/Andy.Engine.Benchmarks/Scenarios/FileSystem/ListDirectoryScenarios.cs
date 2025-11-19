using Andy.Benchmarks.Framework;
using Andy.Benchmarks.Validators;
using Andy.Engine.Benchmarks.Framework;

namespace Andy.Engine.Benchmarks.Scenarios.FileSystem;

/// <summary>
/// Provides benchmark scenarios for the list_directory tool
/// </summary>
public static class ListDirectoryScenarios
{
    /// <summary>
    /// Creates all list_directory benchmark scenarios
    /// </summary>
    /// <param name="testDirectory">The test directory path to use in scenarios</param>
    public static List<BenchmarkScenario> CreateScenarios(string testDirectory)
    {
        return new List<BenchmarkScenario>
        {
            CreateBasicListing(testDirectory),
            CreateRecursiveListing(testDirectory),
            CreatePatternFiltering(testDirectory),
            CreateHiddenFileInclusion(testDirectory),
            CreateSortedListing(testDirectory),
            CreateEmptyDirectory(testDirectory),
            CreateSortBySize(testDirectory),
            CreateSortDescending(testDirectory),
            CreateMaxDepth(testDirectory),
            CreateDirectoryNotFound(testDirectory),
            CreateInvalidPath(testDirectory)
        };
    }

    /// <summary>
    /// Basic directory listing
    /// </summary>
    public static BenchmarkScenario CreateBasicListing(string testDirectory)
    {
        return new BenchmarkScenario
        {
            Id = "fs-list-directory-basic",
            Category = "file-system",
            Description = "List contents of a directory",
            Tags = new List<string> { "file-system", "list-directory", "single-tool" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"List all files and directories in {testDirectory}"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "list_directory",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["directory_path"] = testDirectory
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "readme.txt", "documents" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Recursive directory listing
    /// </summary>
    public static BenchmarkScenario CreateRecursiveListing(string testDirectory)
    {
        return new BenchmarkScenario
        {
            Id = "fs-list-directory-recursive",
            Category = "file-system",
            Description = "List directory contents recursively",
            Tags = new List<string> { "file-system", "list-directory", "recursive" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"List all files in {testDirectory} and all subdirectories recursively"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "list_directory",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["directory_path"] = testDirectory,
                        ["recursive"] = true
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "report.txt", "nested", "subdirector" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Pattern-based filtering (*.txt files)
    /// </summary>
    public static BenchmarkScenario CreatePatternFiltering(string testDirectory)
    {
        return new BenchmarkScenario
        {
            Id = "fs-list-directory-pattern",
            Category = "file-system",
            Description = "List directory with pattern filter",
            Tags = new List<string> { "file-system", "list-directory", "filtering" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"List only .txt files in {testDirectory}"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "list_directory",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["directory_path"] = testDirectory,
                        ["pattern"] = "*.txt"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "readme.txt" },
                ResponseMustNotContain = new List<string> { "data.json", "script.sh" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Include hidden files
    /// </summary>
    public static BenchmarkScenario CreateHiddenFileInclusion(string testDirectory)
    {
        return new BenchmarkScenario
        {
            Id = "fs-list-directory-hidden",
            Category = "file-system",
            Description = "List directory including hidden files",
            Tags = new List<string> { "file-system", "list-directory", "hidden-files" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"List all files in {testDirectory}, including hidden files"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "list_directory",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["directory_path"] = testDirectory,
                        ["include_hidden"] = true
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { ".hidden" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Sorted directory listing
    /// </summary>
    public static BenchmarkScenario CreateSortedListing(string testDirectory)
    {
        return new BenchmarkScenario
        {
            Id = "fs-list-directory-sorted",
            Category = "file-system",
            Description = "List directory with sorted output",
            Tags = new List<string> { "file-system", "list-directory", "sorting" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"List all files in {testDirectory}, sorted by name"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "list_directory",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["directory_path"] = testDirectory,
                        ["sort_by"] = "name"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "readme.txt", "data.json" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// List empty directory
    /// </summary>
    public static BenchmarkScenario CreateEmptyDirectory(string testDirectory)
    {
        var emptyDir = Path.Combine(testDirectory, "empty_dir");

        return new BenchmarkScenario
        {
            Id = "fs-list-directory-empty",
            Category = "file-system",
            Description = "List empty directory",
            Tags = new List<string> { "file-system", "list-directory", "edge-case" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"List all files in the empty directory {emptyDir}"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "list_directory",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["directory_path"] = emptyDir
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "empty", "no files", "directory", "count", "0", "[]" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Sort by file size
    /// </summary>
    public static BenchmarkScenario CreateSortBySize(string testDirectory)
    {
        return new BenchmarkScenario
        {
            Id = "fs-list-directory-sort-size",
            Category = "file-system",
            Description = "List directory sorted by file size",
            Tags = new List<string> { "file-system", "list-directory", "sorting" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"List all files in {testDirectory}, sorted by size"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "list_directory",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["directory_path"] = testDirectory,
                        ["sort_by"] = "size"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "size" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Sort descending by name
    /// </summary>
    public static BenchmarkScenario CreateSortDescending(string testDirectory)
    {
        return new BenchmarkScenario
        {
            Id = "fs-list-directory-sort-descending",
            Category = "file-system",
            Description = "List directory sorted in descending order",
            Tags = new List<string> { "file-system", "list-directory", "sorting" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"List all files in {testDirectory}, sorted by name in descending order (Z to A)"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "list_directory",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["directory_path"] = testDirectory,
                        ["sort_by"] = "name",
                        ["sort_descending"] = true
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "descending", "reverse", "sorted", "Z to A" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// List with max depth limit
    /// </summary>
    public static BenchmarkScenario CreateMaxDepth(string testDirectory)
    {
        return new BenchmarkScenario
        {
            Id = "fs-list-directory-max-depth",
            Category = "file-system",
            Description = "List directory with depth limit",
            Tags = new List<string> { "file-system", "list-directory", "depth-limit" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"List files in {testDirectory} recursively but only go 1 level deep"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "list_directory",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["directory_path"] = testDirectory,
                        ["recursive"] = true,
                        ["max_depth"] = 1
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

    /// <summary>
    /// Directory not found error
    /// </summary>
    public static BenchmarkScenario CreateDirectoryNotFound(string testDirectory)
    {
        var nonExistentDir = Path.Combine(testDirectory, "nonexistent_directory");

        return new BenchmarkScenario
        {
            Id = "fs-list-directory-not-found",
            Category = "file-system",
            Description = "List non-existent directory should fail gracefully",
            Tags = new List<string> { "file-system", "list-directory", "error-handling" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"List all files in {nonExistentDir}"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "list_directory",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["directory_path"] = nonExistentDir
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "does not exist", "not exist", "not found" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Invalid path error
    /// </summary>
    public static BenchmarkScenario CreateInvalidPath(string testDirectory)
    {
        return new BenchmarkScenario
        {
            Id = "fs-list-directory-invalid-path",
            Category = "file-system",
            Description = "List directory with invalid path should fail gracefully",
            Tags = new List<string> { "file-system", "list-directory", "error-handling" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    "List all files in an empty path ''"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "list_directory",
                    MinInvocations = 0,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["directory_path"] = ""
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "empty", "path", "whitespace" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }
}
