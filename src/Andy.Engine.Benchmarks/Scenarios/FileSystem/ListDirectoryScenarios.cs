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
            CreateSortedListing(testDirectory)
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
                ResponseMustContain = new List<string> { "readme.txt", "documents", "report.txt" },
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
}
