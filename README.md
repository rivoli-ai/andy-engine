# Andy.Engine

A C# framework for building LLM-driven agents that call tools via native function-calling.

> **ALPHA RELEASE WARNING**
>
> This software is in ALPHA stage. **NO GUARANTEES** are made about its functionality, stability, or safety.
>
> **CRITICAL WARNINGS:**
> - This tool performs **DESTRUCTIVE OPERATIONS** on files and directories
> - Permission management is **NOT FULLY TESTED** and may have security vulnerabilities
> - **DO NOT USE** in production environments
> - **DO NOT USE** on systems with critical or irreplaceable data
> - **DO NOT USE** on systems without complete, verified backups
> - The authors assume **NO RESPONSIBILITY** for data loss, system damage, or security breaches
>
> **USE AT YOUR OWN RISK**

## Overview

`SimpleAgent` is the agent entry point. It runs a turn-based loop that uses the LLM provider's
native function-calling to decide which registered tools to invoke, executes them through the
Andy.Tools framework, feeds the results back to the model, and repeats until the model produces a
final answer or a limit (turns / output tokens / context tokens) is reached. There is no separate
planner/critic layer — the loop mirrors the pattern used by successful CLI agents.

## Features

- **Native function-calling loop** — the model drives tool use directly; no bespoke planner DSL.
- **Andy.Tools integration** — a registry + executor of built-in tools (file, search, text, …) with
  per-run permission scoping (allowed paths, process/network toggles).
- **Turn & token budgets** — bound each run with `maxTurns`, `maxOutputTokens`, `maxContextTokens`.
- **Context compression** — the per-request view is compressed to fit the token budget while the
  full conversation log is retained.
- **Cancellation** — `ProcessMessageAsync` honors a `CancellationToken`.
- **Tool events** — subscribe to `ToolCalled` for monitoring and debugging.

## Installation

```bash
dotnet add package Andy.Engine --version 1.0.0-alpha.1
```

Target framework: **.NET 8.0**.

## Quick Start

The example below is maintained as a compiling project at
[`examples/Andy.Engine.QuickStart`](examples/Andy.Engine.QuickStart) (built in CI, so it cannot
drift from the public API). Set `OPENAI_API_KEY` before running.

```csharp
using Andy.Engine;
using Andy.Llm.Extensions;
using Andy.Llm.Providers;
using Andy.Tools;
using Andy.Tools.Core;
using Andy.Tools.Framework;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// 1. Configure an LLM provider (OpenAI-compatible; the key is read from OPENAI_API_KEY).
var config = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Llm:DefaultProvider"] = "openai",
        ["Llm:Providers:openai:Provider"] = "openai",
        ["Llm:Providers:openai:ApiKey"] = Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
        ["Llm:Providers:openai:Model"] = "gpt-4o-mini",
        ["Llm:Providers:openai:Enabled"] = "true",
    })
    .Build();

// 2. Register the LLM services and the built-in tools, scoped to the current directory.
var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(config);
services.AddLlmServices(config);
services.AddAndyTools(options =>
{
    options.RegisterBuiltInTools = true;
    options.DefaultPermissions.AllowedPaths = new HashSet<string> { Environment.CurrentDirectory };
});

using var provider = services.BuildServiceProvider();

// 3. Initialize the tool framework, then resolve the registry, executor, and LLM provider.
await provider.GetRequiredService<IToolLifecycleManager>().InitializeAsync();
var registry = provider.GetRequiredService<IToolRegistry>();
var executor = provider.GetRequiredService<IToolExecutor>();
var llm = provider.GetRequiredService<ILlmProviderFactory>().CreateProvider("openai");

// 4. Build the agent. It drives the registered tools with native LLM function-calling.
using var agent = new SimpleAgent(
    llm,
    registry,
    executor,
    systemPrompt: "You are a helpful assistant. Use the available tools to answer the user.",
    maxTurns: 10,
    workingDirectory: Environment.CurrentDirectory);

// 5. Run a turn and print the outcome.
var result = await agent.ProcessMessageAsync("List the files in the current directory.");
Console.WriteLine(result.Success ? result.Response : $"Stopped: {result.StopReason}");
```

## Prerequisites

`SimpleAgent` takes three collaborators, all from the Andy ecosystem:

- **`ILlmProvider`** (Andy.Llm) — resolved from `ILlmProviderFactory.CreateProvider(name)` after
  `services.AddLlmServices(config)`.
- **`IToolRegistry`** (Andy.Tools) — the set of available tools, populated by `services.AddAndyTools(...)`.
  Call `IToolLifecycleManager.InitializeAsync()` once before the first run.
- **`IToolExecutor`** (Andy.Tools) — validates and runs tool calls with the configured permissions.

Useful `SimpleAgent` constructor options: `maxTurns`, `maxOutputTokens`, `maxToolResultChars`,
`maxContextTokens`, `enablePromptCaching`, and `extraBody` (provider-specific request fields).

`ProcessMessageAsync` returns a `SimpleAgentResult(bool Success, string Response, int TurnCount,
TimeSpan Duration, string StopReason)`.

## Integration with the Andy ecosystem

- **Andy.Tools** — tool registry and execution framework
- **Andy.Llm** — LLM provider abstractions
- **Andy.Model** — shared data models
- **Andy.Context** — context management and compression

## License

Apache-2.0 License — see LICENSE file for details

## Contributing

Contributions are welcome! Please see CONTRIBUTING.md for guidelines.

## Support

For issues and questions, please use the GitHub issue tracker.
