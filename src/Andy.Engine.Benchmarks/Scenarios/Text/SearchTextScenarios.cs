using Andy.Benchmarks.Framework;
using Andy.Engine.Benchmarks.Framework;

namespace Andy.Engine.Benchmarks.Scenarios.Text;

/// <summary>
/// Provides benchmark scenarios for the search_text tool
/// </summary>
public static class SearchTextScenarios
{
    public static List<BenchmarkScenario> CreateScenarios(string testDirectory)
    {
        return new List<BenchmarkScenario>
        {
            CreateBasicSearch(testDirectory),
            CreateRegexSearch(testDirectory),
            CreateCaseInsensitiveSearch(testDirectory),
            CreateMaxResultsSearch(testDirectory),
            CreateContextLinesSearch(testDirectory),
            CreateWholeWordsSearch(testDirectory),
            CreateStartsWithSearch(testDirectory),
            CreateEndsWithSearch(testDirectory),
            CreateFilePatternsSearch(testDirectory),
            CreateSearchNoMatches(testDirectory),
            CreateSearchMissingPattern()
        };
    }

    public static BenchmarkScenario CreateBasicSearch(string testDirectory)
    {
        return new BenchmarkScenario
        {
            Id = "text-search-basic",
            Category = "text",
            Description = "Search for text in a directory",
            Tags = new List<string> { "text", "search", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection
            {
                Prompts = new List<string> { $"Search for the text 'readme' in {testDirectory}" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "search_text",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["search_pattern"] = "readme",
                        ["target_path"] = testDirectory
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "readme", "found", "match" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateRegexSearch(string testDirectory)
    {
        return new BenchmarkScenario
        {
            Id = "text-search-regex",
            Category = "text",
            Description = "Search using a regex pattern",
            Tags = new List<string> { "text", "search", "regex" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection
            {
                Prompts = new List<string> { $"Search for the regex pattern 'Line \\d+' in {testDirectory}" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "search_text",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["search_pattern"] = "Line \\d+",
                        ["target_path"] = testDirectory,
                        ["search_type"] = "regex"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "Line", "match", "found" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateCaseInsensitiveSearch(string testDirectory)
    {
        return new BenchmarkScenario
        {
            Id = "text-search-case-insensitive",
            Category = "text",
            Description = "Case-insensitive text search",
            Tags = new List<string> { "text", "search", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection
            {
                Prompts = new List<string> { $"Search for 'HELLO' (case insensitive) in {testDirectory}" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "search_text",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["search_pattern"] = "HELLO",
                        ["target_path"] = testDirectory,
                        ["case_sensitive"] = false
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "Hello", "hello", "found", "match" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateMaxResultsSearch(string testDirectory)
    {
        return new BenchmarkScenario
        {
            Id = "text-search-max-results",
            Category = "text",
            Description = "Search with max results limit",
            Tags = new List<string> { "text", "search", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection
            {
                Prompts = new List<string> { $"Search for 'Line' in {testDirectory} but limit to 2 results" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "search_text",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["search_pattern"] = "Line",
                        ["target_path"] = testDirectory,
                        ["max_results"] = 2
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "Line", "found", "match", "result" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateContextLinesSearch(string testDirectory)
    {
        return new BenchmarkScenario
        {
            Id = "text-search-context-lines",
            Category = "text",
            Description = "Search with context lines around matches",
            Tags = new List<string> { "text", "search", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection
            {
                Prompts = new List<string> { $"Search for 'multiple' in {testDirectory} and show 1 context line around each match" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "search_text",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["search_pattern"] = "multiple",
                        ["target_path"] = testDirectory,
                        ["context_lines"] = 1
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "multiple", "found", "match", "line" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateWholeWordsSearch(string testDirectory)
    {
        return new BenchmarkScenario
        {
            Id = "text-search-whole-words",
            Category = "text",
            Description = "Search for whole words only",
            Tags = new List<string> { "text", "search", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection
            {
                Prompts = new List<string> { $"Search for the whole word 'test' in {testDirectory} (whole words only)" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "search_text",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["search_pattern"] = "test",
                        ["target_path"] = testDirectory,
                        ["whole_words_only"] = true
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "test", "found", "match", "word" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateStartsWithSearch(string testDirectory)
    {
        return new BenchmarkScenario
        {
            Id = "text-search-starts-with",
            Category = "text",
            Description = "Search for lines starting with a pattern",
            Tags = new List<string> { "text", "search", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection
            {
                Prompts = new List<string> { $"Search for lines starting with 'This' in {testDirectory}" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "search_text",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["search_pattern"] = "This",
                        ["target_path"] = testDirectory,
                        ["search_type"] = "starts_with"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "This", "found", "match", "start" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateEndsWithSearch(string testDirectory)
    {
        return new BenchmarkScenario
        {
            Id = "text-search-ends-with",
            Category = "text",
            Description = "Search for lines ending with a pattern",
            Tags = new List<string> { "text", "search", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection
            {
                Prompts = new List<string> { $"Search for lines ending with 'here.' in {testDirectory}" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "search_text",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["search_pattern"] = "here.",
                        ["target_path"] = testDirectory,
                        ["search_type"] = "ends_with"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "here", "found", "match", "end" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateFilePatternsSearch(string testDirectory)
    {
        return new BenchmarkScenario
        {
            Id = "text-search-file-patterns",
            Category = "text",
            Description = "Search with file pattern filtering",
            Tags = new List<string> { "text", "search", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection
            {
                Prompts = new List<string> { $"Search for 'Item' only in .md files in {testDirectory}" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "search_text",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["search_pattern"] = "Item",
                        ["target_path"] = testDirectory,
                        ["file_patterns"] = new[] { "*.md" }
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "Item", "found", "match", ".md" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateSearchNoMatches(string testDirectory)
    {
        return new BenchmarkScenario
        {
            Id = "text-search-no-matches",
            Category = "text",
            Description = "Search for text that doesn't exist",
            Tags = new List<string> { "text", "search", "error-handling" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection
            {
                Prompts = new List<string> { $"Search for 'xyznonexistent123' in {testDirectory}" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "search_text",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["search_pattern"] = "xyznonexistent123",
                        ["target_path"] = testDirectory
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "no match", "not found", "0 match", "no result", "\"count\":0", "\"count\": 0" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateSearchMissingPattern()
    {
        return new BenchmarkScenario
        {
            Id = "text-search-missing-pattern",
            Category = "text",
            Description = "Search without providing a pattern",
            Tags = new List<string> { "text", "search", "error-handling" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Use the search text tool but don't specify what to search for" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "search_text",
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
