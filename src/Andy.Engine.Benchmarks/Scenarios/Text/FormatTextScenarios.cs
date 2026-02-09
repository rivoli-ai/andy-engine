using Andy.Benchmarks.Framework;
using Andy.Engine.Benchmarks.Framework;

namespace Andy.Engine.Benchmarks.Scenarios.Text;

/// <summary>
/// Provides benchmark scenarios for the format_text tool
/// </summary>
public static class FormatTextScenarios
{
    public static List<BenchmarkScenario> CreateScenarios()
    {
        return new List<BenchmarkScenario>
        {
            CreateTrimWhitespace(),
            CreateUpperCase(),
            CreateLowerCase(),
            CreateSnakeCase(),
            CreateTitleCase(),
            CreateCamelCase(),
            CreateKebabCase(),
            CreatePascalCase(),
            CreateReverse(),
            CreateCountWords(),
            CreateCountChars(),
            CreateSortLines(),
            CreateRemoveDuplicates(),
            CreateRemoveEmptyLines(),
            CreateNormalizeWhitespace(),
            CreateWordWrap(),
            CreateExtractNumbers(),
            CreateExtractEmails(),
            CreateExtractUrls(),
            CreateMissingInput()
        };
    }

    public static BenchmarkScenario CreateTrimWhitespace()
    {
        return new BenchmarkScenario
        {
            Id = "text-format-trim",
            Category = "text",
            Description = "Trim whitespace from text",
            Tags = new List<string> { "text", "format", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Trim the whitespace from '  Hello World  '" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "format_text",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["input_text"] = "  Hello World  ",
                        ["operation"] = "trim"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "Hello World" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateUpperCase()
    {
        return new BenchmarkScenario
        {
            Id = "text-format-upper",
            Category = "text",
            Description = "Convert text to upper case",
            Tags = new List<string> { "text", "format", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Convert 'hello world' to upper case" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "format_text",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["input_text"] = "hello world",
                        ["operation"] = "upper"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "HELLO WORLD" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateLowerCase()
    {
        return new BenchmarkScenario
        {
            Id = "text-format-lower",
            Category = "text",
            Description = "Convert text to lower case",
            Tags = new List<string> { "text", "format", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Convert 'HELLO WORLD' to lower case" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "format_text",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["input_text"] = "HELLO WORLD",
                        ["operation"] = "lower"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "hello world" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateSnakeCase()
    {
        return new BenchmarkScenario
        {
            Id = "text-format-snake",
            Category = "text",
            Description = "Convert text to snake_case",
            Tags = new List<string> { "text", "format", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Convert 'HelloWorld' to snake_case" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "format_text",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["input_text"] = "HelloWorld",
                        ["operation"] = "snake"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "hello_world", "snake" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateTitleCase()
    {
        return new BenchmarkScenario
        {
            Id = "text-format-title",
            Category = "text",
            Description = "Convert text to Title Case",
            Tags = new List<string> { "text", "format", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Convert 'hello world foo bar' to title case" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "format_text",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["input_text"] = "hello world foo bar",
                        ["operation"] = "title"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "Hello World", "Hello world", "title" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateCamelCase()
    {
        return new BenchmarkScenario
        {
            Id = "text-format-camel",
            Category = "text",
            Description = "Convert text to camelCase",
            Tags = new List<string> { "text", "format", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Convert 'hello world' to camelCase" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "format_text",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["input_text"] = "hello world",
                        ["operation"] = "camel"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "helloWorld", "camel" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateKebabCase()
    {
        return new BenchmarkScenario
        {
            Id = "text-format-kebab",
            Category = "text",
            Description = "Convert text to kebab-case",
            Tags = new List<string> { "text", "format", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Convert 'HelloWorld' to kebab-case" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "format_text",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["input_text"] = "HelloWorld",
                        ["operation"] = "kebab"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "hello-world", "kebab" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateReverse()
    {
        return new BenchmarkScenario
        {
            Id = "text-format-reverse",
            Category = "text",
            Description = "Reverse a text string",
            Tags = new List<string> { "text", "format", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Reverse the text 'Hello'" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "format_text",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["input_text"] = "Hello",
                        ["operation"] = "reverse"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "olleH", "reverse" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateCountWords()
    {
        return new BenchmarkScenario
        {
            Id = "text-format-count-words",
            Category = "text",
            Description = "Count words in text",
            Tags = new List<string> { "text", "format", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Count the words in 'The quick brown fox jumps over the lazy dog'" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "format_text",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["input_text"] = "The quick brown fox jumps over the lazy dog",
                        ["operation"] = "count_words"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "9", "nine" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreatePascalCase()
    {
        return new BenchmarkScenario
        {
            Id = "text-format-pascal",
            Category = "text",
            Description = "Convert text to PascalCase",
            Tags = new List<string> { "text", "format", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Convert 'hello world' to PascalCase" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "format_text",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["input_text"] = "hello world",
                        ["operation"] = "pascal"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "HelloWorld", "pascal" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateCountChars()
    {
        return new BenchmarkScenario
        {
            Id = "text-format-count-chars",
            Category = "text",
            Description = "Count characters in text",
            Tags = new List<string> { "text", "format", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Count the characters in 'Hello World'" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "format_text",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["input_text"] = "Hello World",
                        ["operation"] = "count_chars"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "11", "character", "count" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateSortLines()
    {
        return new BenchmarkScenario
        {
            Id = "text-format-sort-lines",
            Category = "text",
            Description = "Sort lines alphabetically",
            Tags = new List<string> { "text", "format", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Sort these lines alphabetically: 'cherry\napple\nbanana'" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "format_text",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["input_text"] = "cherry\napple\nbanana",
                        ["operation"] = "sort_lines"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "apple", "banana", "cherry", "sorted" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateRemoveDuplicates()
    {
        return new BenchmarkScenario
        {
            Id = "text-format-remove-duplicates",
            Category = "text",
            Description = "Remove duplicate lines from text",
            Tags = new List<string> { "text", "format", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Remove duplicate lines from: 'apple\nbanana\napple\ncherry\nbanana'" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "format_text",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["input_text"] = "apple\nbanana\napple\ncherry\nbanana",
                        ["operation"] = "remove_duplicates"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "apple", "banana", "cherry", "duplicate" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateRemoveEmptyLines()
    {
        return new BenchmarkScenario
        {
            Id = "text-format-remove-empty-lines",
            Category = "text",
            Description = "Remove empty lines from text",
            Tags = new List<string> { "text", "format", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Remove empty lines from: 'Line 1\n\nLine 2\n\n\nLine 3'" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "format_text",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["input_text"] = "Line 1\n\nLine 2\n\n\nLine 3",
                        ["operation"] = "remove_empty_lines"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "Line 1", "Line 2", "Line 3", "removed" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateNormalizeWhitespace()
    {
        return new BenchmarkScenario
        {
            Id = "text-format-normalize-whitespace",
            Category = "text",
            Description = "Normalize whitespace in text",
            Tags = new List<string> { "text", "format", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Normalize the whitespace in: 'Hello    World   from    here'" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "format_text",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["input_text"] = "Hello    World   from    here",
                        ["operation"] = "normalize_whitespace"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "Hello World", "Hello world", "normalize" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateWordWrap()
    {
        return new BenchmarkScenario
        {
            Id = "text-format-word-wrap",
            Category = "text",
            Description = "Wrap text at a specified width",
            Tags = new List<string> { "text", "format", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Word wrap this text at 20 characters: 'The quick brown fox jumps over the lazy dog'" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "format_text",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["input_text"] = "The quick brown fox jumps over the lazy dog",
                        ["operation"] = "word_wrap"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "quick", "brown", "fox", "wrap" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateExtractNumbers()
    {
        return new BenchmarkScenario
        {
            Id = "text-format-extract-numbers",
            Category = "text",
            Description = "Extract numbers from text",
            Tags = new List<string> { "text", "format", "extract" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Extract all numbers from: 'I have 3 cats, 5 dogs, and 12 fish'" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "format_text",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["input_text"] = "I have 3 cats, 5 dogs, and 12 fish",
                        ["operation"] = "extract_numbers"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "3", "5", "12" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateExtractEmails()
    {
        return new BenchmarkScenario
        {
            Id = "text-format-extract-emails",
            Category = "text",
            Description = "Extract email addresses from text",
            Tags = new List<string> { "text", "format", "extract" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Extract email addresses from: 'Contact us at info@example.com or support@test.org for help'" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "format_text",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["input_text"] = "Contact us at info@example.com or support@test.org for help",
                        ["operation"] = "extract_emails"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "info@example.com", "support@test.org", "email" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateExtractUrls()
    {
        return new BenchmarkScenario
        {
            Id = "text-format-extract-urls",
            Category = "text",
            Description = "Extract URLs from text",
            Tags = new List<string> { "text", "format", "extract" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Extract URLs from: 'Visit https://example.com and http://test.org for more info'" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "format_text",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["input_text"] = "Visit https://example.com and http://test.org for more info",
                        ["operation"] = "extract_urls"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "example.com", "test.org", "url", "http" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateMissingInput()
    {
        return new BenchmarkScenario
        {
            Id = "text-format-missing-input",
            Category = "text",
            Description = "Call format_text without input text",
            Tags = new List<string> { "text", "format", "error-handling" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Use the format text tool to convert to upper case but don't provide any text" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "format_text",
                    MinInvocations = 0,
                    MaxInvocations = 1
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "input", "text", "required", "provide" }
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }
}
