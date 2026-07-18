// Andy.Engine quick start — the same code shown in the root README.
//
// This file is kept in the solution so the README example is a compile check: if the public
// SimpleAgent wiring changes, the build breaks here. Run it with an OpenAI-compatible key set in
// the OPENAI_API_KEY environment variable.
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

Console.WriteLine(result.Success
    ? $"Completed in {result.TurnCount} turn(s):\n{result.Response}"
    : $"Stopped ({result.StopReason}) after {result.TurnCount} turn(s).");
