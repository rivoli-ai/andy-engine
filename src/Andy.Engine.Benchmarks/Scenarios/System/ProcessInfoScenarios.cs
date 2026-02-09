using Andy.Benchmarks.Framework;
using Andy.Engine.Benchmarks.Framework;

namespace Andy.Engine.Benchmarks.Scenarios.SystemTools;

/// <summary>
/// Provides benchmark scenarios for the process_info tool
/// </summary>
public static class ProcessInfoScenarios
{
    public static List<BenchmarkScenario> CreateScenarios()
    {
        return new List<BenchmarkScenario>
        {
            CreateGetCurrentProcess(),
            CreateListProcesses(),
            CreateFindProcessByName(),
            CreateSortByMemory(),
            CreateSortByCpu()
        };
    }

    public static BenchmarkScenario CreateGetCurrentProcess()
    {
        return new BenchmarkScenario
        {
            Id = "sys-process-current",
            Category = "system",
            Description = "Get current process information",
            Tags = new List<string> { "system", "process", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Get information about the current process" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "process_info",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["include_current"] = true
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "process", "pid", "dotnet", "memory", "id" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateListProcesses()
    {
        return new BenchmarkScenario
        {
            Id = "sys-process-list",
            Category = "system",
            Description = "List running processes",
            Tags = new List<string> { "system", "process", "list" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "List the top 5 running processes" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "process_info",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["max_results"] = 5
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "process", "name", "pid", "id" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateFindProcessByName()
    {
        return new BenchmarkScenario
        {
            Id = "sys-process-find",
            Category = "system",
            Description = "Find a specific process by name",
            Tags = new List<string> { "system", "process", "search" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Find process information for 'dotnet'" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "process_info",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["process_name"] = "dotnet"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "dotnet", "process", "found", "running", "not found" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateSortByMemory()
    {
        return new BenchmarkScenario
        {
            Id = "sys-process-sort-memory",
            Category = "system",
            Description = "List processes sorted by memory usage",
            Tags = new List<string> { "system", "process", "sort" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "List the top 3 processes sorted by memory usage" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "process_info",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["sort_by"] = "memory",
                        ["max_results"] = 3
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "process", "memory", "MB", "KB" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateSortByCpu()
    {
        return new BenchmarkScenario
        {
            Id = "sys-process-sort-cpu",
            Category = "system",
            Description = "List processes sorted by CPU usage",
            Tags = new List<string> { "system", "process", "sort" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "List the top 3 processes sorted by CPU usage" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "process_info",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["sort_by"] = "cpu_time",
                        ["max_results"] = 3
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "process", "cpu", "CPU", "name", "id" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }
}
