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
            CreateBasicFileCopy(testDirectory),
            CreateCopyWithOverwrite(testDirectory),
            CreateRecursiveDirectoryCopy(testDirectory)
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
}
