using Andy.Benchmarks.Framework;
using Andy.Engine.Benchmarks.Framework;

namespace Andy.Engine.Benchmarks.Scenarios.Network;

/// <summary>
/// Provides benchmark scenarios for the http_request tool
/// </summary>
public static class HttpRequestScenarios
{
    public static List<BenchmarkScenario> CreateScenarios()
    {
        return new List<BenchmarkScenario>
        {
            CreateBasicGet(),
            CreatePostWithBody(),
            CreateCustomHeaders()
        };
    }

    public static BenchmarkScenario CreateBasicGet()
    {
        return new BenchmarkScenario
        {
            Id = "net-http-get",
            Category = "network",
            Description = "Make a basic GET request",
            Tags = new List<string> { "network", "http", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Make an HTTP GET request to https://httpbin.org/get" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "http_request",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["url"] = "https://httpbin.org/get",
                        ["method"] = "GET"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "http", "response", "status", "200", "url", "get" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(2)
        };
    }

    public static BenchmarkScenario CreatePostWithBody()
    {
        return new BenchmarkScenario
        {
            Id = "net-http-post",
            Category = "network",
            Description = "Make a POST request with JSON body",
            Tags = new List<string> { "network", "http", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Make an HTTP POST request to https://httpbin.org/post with JSON body {\"name\": \"test\", \"value\": 42}" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "http_request",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["url"] = "https://httpbin.org/post",
                        ["method"] = "POST",
                        ["body"] = "{\"name\": \"test\", \"value\": 42}",
                        ["content_type"] = "application/json"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "http", "post", "response", "status", "200", "test" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(2)
        };
    }

    public static BenchmarkScenario CreateCustomHeaders()
    {
        return new BenchmarkScenario
        {
            Id = "net-http-custom-headers",
            Category = "network",
            Description = "Make an HTTP request with custom headers",
            Tags = new List<string> { "network", "http", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Make an HTTP GET request to https://httpbin.org/headers with a custom header 'X-Custom-Header: TestValue'" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "http_request",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["url"] = "https://httpbin.org/headers",
                        ["method"] = "GET"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "header", "custom", "TestValue", "response", "http" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(2)
        };
    }
}
