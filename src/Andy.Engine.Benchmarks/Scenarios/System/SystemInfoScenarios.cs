using Andy.Benchmarks.Framework;
using Andy.Engine.Benchmarks.Framework;

namespace Andy.Engine.Benchmarks.Scenarios.SystemTools;

/// <summary>
/// Provides benchmark scenarios for the system_info tool
/// </summary>
public static class SystemInfoScenarios
{
    public static List<BenchmarkScenario> CreateScenarios()
    {
        return new List<BenchmarkScenario>
        {
            CreateGetAllInfo(),
            CreateGetOsInfo(),
            CreateGetMemoryInfo(),
            CreateGetCpuInfo(),
            CreateGetRuntimeInfo(),
            CreateGetStorageInfo(),
            CreateGetDetailedInfo(),
            CreateGetMultiCategoryInfo()
        };
    }

    public static BenchmarkScenario CreateGetAllInfo()
    {
        return new BenchmarkScenario
        {
            Id = "sys-info-all",
            Category = "system",
            Description = "Get all system information",
            Tags = new List<string> { "system", "info", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Get system information" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "system_info",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>()
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "OS", "os", "system", "platform", "Windows", "Linux", "macOS", "Darwin" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateGetOsInfo()
    {
        return new BenchmarkScenario
        {
            Id = "sys-info-os",
            Category = "system",
            Description = "Get operating system information",
            Tags = new List<string> { "system", "info", "os" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "What operating system am I running?" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "system_info",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["categories"] = new[] { "os" }
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "Windows", "Linux", "macOS", "Darwin", "Unix", "OS" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateGetMemoryInfo()
    {
        return new BenchmarkScenario
        {
            Id = "sys-info-memory",
            Category = "system",
            Description = "Get system memory information",
            Tags = new List<string> { "system", "info", "memory" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "How much memory does my system have?" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "system_info",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["categories"] = new[] { "memory" }
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "memory", "MB", "GB", "RAM", "bytes" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateGetCpuInfo()
    {
        return new BenchmarkScenario
        {
            Id = "sys-info-cpu",
            Category = "system",
            Description = "Get CPU information",
            Tags = new List<string> { "system", "info", "cpu" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "What CPU does my system have?" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "system_info",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["categories"] = new[] { "cpu" }
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "CPU", "cpu", "processor", "core", "thread" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateGetRuntimeInfo()
    {
        return new BenchmarkScenario
        {
            Id = "sys-info-runtime",
            Category = "system",
            Description = "Get .NET runtime information",
            Tags = new List<string> { "system", "info", "runtime" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "What .NET runtime version am I using?" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "system_info",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["categories"] = new[] { "runtime" }
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "runtime", ".NET", "version", "framework", "dotnet" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateGetStorageInfo()
    {
        return new BenchmarkScenario
        {
            Id = "sys-info-storage",
            Category = "system",
            Description = "Get disk storage information",
            Tags = new List<string> { "system", "info", "storage" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "How much disk space does my system have?" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "system_info",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["categories"] = new[] { "storage" }
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "storage", "disk", "drive", "GB", "TB", "space", "free" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateGetDetailedInfo()
    {
        return new BenchmarkScenario
        {
            Id = "sys-info-detailed",
            Category = "system",
            Description = "Get detailed system information",
            Tags = new List<string> { "system", "info", "detailed" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Get detailed system information including hardware details" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "system_info",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["detailed"] = true
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "OS", "os", "system", "processor", "cpu", "memory" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateGetMultiCategoryInfo()
    {
        return new BenchmarkScenario
        {
            Id = "sys-info-multi-category",
            Category = "system",
            Description = "Get OS and memory information together",
            Tags = new List<string> { "system", "info", "multi-category" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Get both OS and memory information for this system" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "system_info",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["categories"] = new[] { "os", "memory" }
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "OS", "os", "memory", "Windows", "Linux", "macOS", "Darwin", "MB", "GB" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }
}
