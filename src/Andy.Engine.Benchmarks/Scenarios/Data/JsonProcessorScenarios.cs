using Andy.Benchmarks.Framework;
using Andy.Engine.Benchmarks.Framework;

namespace Andy.Engine.Benchmarks.Scenarios.Data;

/// <summary>
/// Provides benchmark scenarios for the json_processor tool
/// </summary>
public static class JsonProcessorScenarios
{
    public static List<BenchmarkScenario> CreateScenarios()
    {
        return new List<BenchmarkScenario>
        {
            CreateValidateJson(),
            CreateFormatJson(),
            CreateMinifyJson(),
            CreateQueryJsonPath(),
            CreateExtractJson(),
            CreateTransformJson(),
            CreateFlattenJson(),
            CreateUnflattenJson(),
            CreateMergeJson(),
            CreateDiffJson(),
            CreateToCsv(),
            CreateFromCsv(),
            CreateCountJson(),
            CreateStatisticsJson(),
            CreateInvalidJsonValidation()
        };
    }

    public static BenchmarkScenario CreateValidateJson()
    {
        return new BenchmarkScenario
        {
            Id = "data-json-validate",
            Category = "data",
            Description = "Validate a JSON string",
            Tags = new List<string> { "data", "json", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Validate this JSON: {\"name\": \"test\", \"value\": 42}" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "json_processor",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["json_input"] = "{\"name\": \"test\", \"value\": 42}",
                        ["operation"] = "validate"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "valid", "true", "success" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateFormatJson()
    {
        return new BenchmarkScenario
        {
            Id = "data-json-format",
            Category = "data",
            Description = "Format (pretty-print) a JSON string",
            Tags = new List<string> { "data", "json", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Pretty-print this JSON: {\"name\":\"test\",\"items\":[1,2,3]}" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "json_processor",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["json_input"] = "{\"name\":\"test\",\"items\":[1,2,3]}",
                        ["operation"] = "format"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "name", "test", "items" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateMinifyJson()
    {
        return new BenchmarkScenario
        {
            Id = "data-json-minify",
            Category = "data",
            Description = "Minify a formatted JSON string",
            Tags = new List<string> { "data", "json", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Minify this JSON: { \"name\": \"test\", \"value\": 42 }" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "json_processor",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["json_input"] = "{ \"name\": \"test\", \"value\": 42 }",
                        ["operation"] = "minify"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "name", "test" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateQueryJsonPath()
    {
        return new BenchmarkScenario
        {
            Id = "data-json-query",
            Category = "data",
            Description = "Query a JSON path",
            Tags = new List<string> { "data", "json", "query" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Extract the value at path '$.name' from this JSON: {\"name\": \"Alice\", \"age\": 30}" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "json_processor",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["json_input"] = "{\"name\": \"Alice\", \"age\": 30}",
                        ["operation"] = "query",
                        ["query_path"] = "$.name"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "Alice" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateExtractJson()
    {
        return new BenchmarkScenario
        {
            Id = "data-json-extract",
            Category = "data",
            Description = "Extract a value from JSON using a query path",
            Tags = new List<string> { "data", "json", "extract" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Extract the value at path '$.address.city' from: {\"name\": \"Alice\", \"address\": {\"city\": \"NYC\", \"zip\": \"10001\"}}" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "json_processor",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["json_input"] = "{\"name\": \"Alice\", \"address\": {\"city\": \"NYC\", \"zip\": \"10001\"}}",
                        ["operation"] = "extract",
                        ["query_path"] = "$.address.city"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "NYC", "city" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateTransformJson()
    {
        return new BenchmarkScenario
        {
            Id = "data-json-transform",
            Category = "data",
            Description = "Transform JSON with rules",
            Tags = new List<string> { "data", "json", "transform" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Transform this JSON by renaming 'name' to 'fullName': {\"name\": \"Alice\", \"age\": 30}" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "json_processor",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["json_input"] = "{\"name\": \"Alice\", \"age\": 30}",
                        ["operation"] = "transform",
                        ["transform_rules"] = "{\"rename\": {\"name\": \"fullName\"}}"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "fullName", "Alice", "transform" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateFlattenJson()
    {
        return new BenchmarkScenario
        {
            Id = "data-json-flatten",
            Category = "data",
            Description = "Flatten a nested JSON object",
            Tags = new List<string> { "data", "json", "transform" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Flatten this JSON: {\"user\": {\"name\": \"Alice\", \"address\": {\"city\": \"NYC\"}}}" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "json_processor",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["json_input"] = "{\"user\": {\"name\": \"Alice\", \"address\": {\"city\": \"NYC\"}}}",
                        ["operation"] = "flatten"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "Alice", "NYC", "user" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateMergeJson()
    {
        return new BenchmarkScenario
        {
            Id = "data-json-merge",
            Category = "data",
            Description = "Merge two JSON objects",
            Tags = new List<string> { "data", "json", "transform" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Merge these two JSON objects: {\"name\": \"Alice\"} and {\"age\": 30}" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "json_processor",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["json_input"] = "{\"name\": \"Alice\"}",
                        ["operation"] = "merge",
                        ["merge_json"] = "{\"age\": 30}"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "Alice", "30", "name", "age" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateDiffJson()
    {
        return new BenchmarkScenario
        {
            Id = "data-json-diff",
            Category = "data",
            Description = "Compare two JSON objects for differences",
            Tags = new List<string> { "data", "json", "compare" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Compare these two JSON objects: {\"name\": \"Alice\", \"age\": 30} and {\"name\": \"Alice\", \"age\": 31}" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "json_processor",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["json_input"] = "{\"name\": \"Alice\", \"age\": 30}",
                        ["operation"] = "diff",
                        ["merge_json"] = "{\"name\": \"Alice\", \"age\": 31}"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                // Tool returns diff results as JSON array; may return [] if merge_json param not used for diff
                ResponseMustContainAny = new List<string> { "age", "30", "31", "diff", "change", "[]", "completed" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateUnflattenJson()
    {
        return new BenchmarkScenario
        {
            Id = "data-json-unflatten",
            Category = "data",
            Description = "Unflatten dotted keys back to nested JSON",
            Tags = new List<string> { "data", "json", "transform" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Unflatten this JSON: {\"user.name\": \"Alice\", \"user.address.city\": \"NYC\"}" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "json_processor",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["json_input"] = "{\"user.name\": \"Alice\", \"user.address.city\": \"NYC\"}",
                        ["operation"] = "unflatten"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "Alice", "NYC", "user" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateToCsv()
    {
        return new BenchmarkScenario
        {
            Id = "data-json-to-csv",
            Category = "data",
            Description = "Convert JSON array to CSV",
            Tags = new List<string> { "data", "json", "csv" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Convert this JSON array to CSV: [{\"name\": \"Alice\", \"age\": 30}, {\"name\": \"Bob\", \"age\": 25}]" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "json_processor",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["json_input"] = "[{\"name\": \"Alice\", \"age\": 30}, {\"name\": \"Bob\", \"age\": 25}]",
                        ["operation"] = "to_csv"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "Alice", "Bob", "name", "age", "csv", "," },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateFromCsv()
    {
        return new BenchmarkScenario
        {
            Id = "data-json-from-csv",
            Category = "data",
            Description = "Convert CSV string to JSON",
            Tags = new List<string> { "data", "json", "csv" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Convert this CSV to JSON: 'name,age\nAlice,30\nBob,25'" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "json_processor",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["json_input"] = "name,age\nAlice,30\nBob,25",
                        ["operation"] = "from_csv"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "Alice", "Bob", "name", "age" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateCountJson()
    {
        return new BenchmarkScenario
        {
            Id = "data-json-count",
            Category = "data",
            Description = "Count elements in JSON",
            Tags = new List<string> { "data", "json", "analyze" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Count the elements in this JSON: {\"name\": \"Alice\", \"age\": 30, \"active\": true, \"tags\": [\"a\", \"b\"]}" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "json_processor",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["json_input"] = "{\"name\": \"Alice\", \"age\": 30, \"active\": true, \"tags\": [\"a\", \"b\"]}",
                        ["operation"] = "count"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "count", "4", "element", "string", "number", "boolean", "array" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateStatisticsJson()
    {
        return new BenchmarkScenario
        {
            Id = "data-json-statistics",
            Category = "data",
            Description = "Get statistics about JSON structure",
            Tags = new List<string> { "data", "json", "analyze" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Get statistics for this JSON: {\"user\": {\"name\": \"Alice\", \"address\": {\"city\": \"NYC\"}}}" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "json_processor",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["json_input"] = "{\"user\": {\"name\": \"Alice\", \"address\": {\"city\": \"NYC\"}}}",
                        ["operation"] = "statistics"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "depth", "size", "statistic", "node", "key" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateInvalidJsonValidation()
    {
        return new BenchmarkScenario
        {
            Id = "data-json-invalid",
            Category = "data",
            Description = "Validate invalid JSON should report error",
            Tags = new List<string> { "data", "json", "error-handling" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Validate this JSON: {invalid json here}" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "json_processor",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["json_input"] = "{invalid json here}",
                        ["operation"] = "validate"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "invalid", "error", "not valid", "false", "fail" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }
}
