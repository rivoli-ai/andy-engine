using Andy.Benchmarks.Framework;
using Andy.Engine.Benchmarks.Framework;

namespace Andy.Engine.Benchmarks.Scenarios.Text;

/// <summary>
/// Provides benchmark scenarios for the replace_text tool
/// </summary>
public static class ReplaceTextScenarios
{
    public static List<BenchmarkScenario> CreateScenarios(string testDirectory)
    {
        return new List<BenchmarkScenario>
        {
            CreateSimpleReplace(testDirectory),
            CreateRegexReplace(testDirectory),
            CreateDryRunReplace(testDirectory),
            CreateWholeWordsReplace(testDirectory),
            CreateExactReplace(testDirectory),
            CreateStartsWithReplace(testDirectory),
            CreateEndsWithReplace(testDirectory),
            CreateFilePatternsReplace(testDirectory),
            CreateBackupReplace(testDirectory),
            CreateReplaceMissingPattern()
        };
    }

    public static BenchmarkScenario CreateSimpleReplace(string testDirectory)
    {
        var targetFile = Path.Combine(testDirectory, "readme.txt");

        return new BenchmarkScenario
        {
            Id = "text-replace-simple",
            Category = "text",
            Description = "Simple text replacement in a file",
            Tags = new List<string> { "text", "replace", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection
            {
                Prompts = new List<string> { $"Replace 'readme' with 'README' in {targetFile}" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "replace_text",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["search_pattern"] = "readme",
                        ["replacement_text"] = "README",
                        ["target_path"] = targetFile
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "replace", "README", "modified", "changed" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateRegexReplace(string testDirectory)
    {
        var targetFile = Path.Combine(testDirectory, "readme.txt");

        return new BenchmarkScenario
        {
            Id = "text-replace-regex",
            Category = "text",
            Description = "Regex-based text replacement",
            Tags = new List<string> { "text", "replace", "regex" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection
            {
                Prompts = new List<string> { $"Replace all occurrences matching regex 'Line \\d+' with 'LINE' in {targetFile}" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "replace_text",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["search_pattern"] = "Line \\d+",
                        ["replacement_text"] = "LINE",
                        ["target_path"] = targetFile,
                        ["search_type"] = "regex"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "replace", "LINE", "modified", "regex" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateDryRunReplace(string testDirectory)
    {
        return new BenchmarkScenario
        {
            Id = "text-replace-dry-run",
            Category = "text",
            Description = "Dry run replacement to preview changes",
            Tags = new List<string> { "text", "replace", "dry-run" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection
            {
                Prompts = new List<string> { $"Do a dry run replacement of 'test' with 'TEST' in {testDirectory}" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "replace_text",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["search_pattern"] = "test",
                        ["replacement_text"] = "TEST",
                        ["target_path"] = testDirectory,
                        ["dry_run"] = true
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "dry run", "preview", "would", "match" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateWholeWordsReplace(string testDirectory)
    {
        var targetFile = Path.Combine(testDirectory, "readme.txt");

        return new BenchmarkScenario
        {
            Id = "text-replace-whole-words",
            Category = "text",
            Description = "Replace whole words only",
            Tags = new List<string> { "text", "replace", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection
            {
                Prompts = new List<string> { $"Replace the whole word 'test' with 'demo' in {targetFile} (whole words only)" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "replace_text",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["search_pattern"] = "test",
                        ["replacement_text"] = "demo",
                        ["target_path"] = targetFile,
                        ["whole_words_only"] = true
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "replace", "demo", "whole", "word", "modified", "\"count\"" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateExactReplace(string testDirectory)
    {
        var targetFile = Path.Combine(testDirectory, "readme.txt");

        return new BenchmarkScenario
        {
            Id = "text-replace-exact",
            Category = "text",
            Description = "Exact match replacement",
            Tags = new List<string> { "text", "replace", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection
            {
                Prompts = new List<string> { $"Replace the exact text 'Line 3 here.' with 'Line 3 done.' in {targetFile}" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "replace_text",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["search_pattern"] = "Line 3 here.",
                        ["replacement_text"] = "Line 3 done.",
                        ["target_path"] = targetFile,
                        ["search_type"] = "exact"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "replace", "done", "exact", "modified", "\"count\"" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateStartsWithReplace(string testDirectory)
    {
        var targetFile = Path.Combine(testDirectory, "readme.txt");

        return new BenchmarkScenario
        {
            Id = "text-replace-starts-with",
            Category = "text",
            Description = "Replace text at start of lines",
            Tags = new List<string> { "text", "replace", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection
            {
                Prompts = new List<string> { $"Replace lines starting with 'It' to start with 'The file' in {targetFile}" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "replace_text",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["search_pattern"] = "It",
                        ["replacement_text"] = "The file",
                        ["target_path"] = targetFile,
                        ["search_type"] = "starts_with"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "replace", "start", "modified", "\"count\"" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateEndsWithReplace(string testDirectory)
    {
        var targetFile = Path.Combine(testDirectory, "readme.txt");

        return new BenchmarkScenario
        {
            Id = "text-replace-ends-with",
            Category = "text",
            Description = "Replace text at end of lines",
            Tags = new List<string> { "text", "replace", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection
            {
                Prompts = new List<string> { $"Replace lines ending with 'here.' to end with 'HERE!' in {targetFile}" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "replace_text",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["search_pattern"] = "here.",
                        ["replacement_text"] = "HERE!",
                        ["target_path"] = targetFile,
                        ["search_type"] = "ends_with"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "replace", "end", "modified", "HERE", "\"count\"" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateFilePatternsReplace(string testDirectory)
    {
        return new BenchmarkScenario
        {
            Id = "text-replace-file-patterns",
            Category = "text",
            Description = "Replace with file pattern filtering",
            Tags = new List<string> { "text", "replace", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection
            {
                Prompts = new List<string> { $"Replace 'Item' with 'Entry' only in .md files in {testDirectory}" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "replace_text",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["search_pattern"] = "Item",
                        ["replacement_text"] = "Entry",
                        ["target_path"] = testDirectory,
                        ["file_patterns"] = new[] { "*.md" }
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "replace", "Entry", "modified", ".md", "\"count\"" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateBackupReplace(string testDirectory)
    {
        var targetFile = Path.Combine(testDirectory, "readme.txt");

        return new BenchmarkScenario
        {
            Id = "text-replace-backup",
            Category = "text",
            Description = "Replace with explicit backup creation",
            Tags = new List<string> { "text", "replace", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection
            {
                Prompts = new List<string> { $"Replace 'readme' with 'README' in {targetFile} and create a backup" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "replace_text",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["search_pattern"] = "readme",
                        ["replacement_text"] = "README",
                        ["target_path"] = targetFile,
                        ["create_backup"] = true
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "replace", "README", "backup", "modified", "\"count\"" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateReplaceMissingPattern()
    {
        return new BenchmarkScenario
        {
            Id = "text-replace-missing-pattern",
            Category = "text",
            Description = "Replace without providing a search pattern",
            Tags = new List<string> { "text", "replace", "error-handling" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Use the replace text tool but don't specify what to search for" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "replace_text",
                    MinInvocations = 0,
                    MaxInvocations = 1
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "pattern", "search", "required", "specify" }
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }
}
