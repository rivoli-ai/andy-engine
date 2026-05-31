using Andy.Engine.SweBench.Llm;
using Andy.Engine.SweBench.Orchestration;
using Andy.Llm.Extensions;
using Andy.Llm.Providers;
using Andy.Model.Llm;
using Andy.Tools;
using Andy.Tools.Core;
using Andy.Tools.Framework;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Andy.Engine.SweBench.Agent;

/// <summary>A built agent plus the resources it owns; dispose to release the DI container.</summary>
public sealed class SweAgent : IDisposable
{
    private readonly ServiceProvider _services;

    internal SweAgent(SimpleAgent agent, string providerName, IReadOnlyList<string> availableTools, ServiceProvider services)
    {
        Agent = agent;
        ProviderName = providerName;
        AvailableTools = availableTools;
        _services = services;
    }

    public SimpleAgent Agent { get; }
    public string ProviderName { get; }
    public IReadOnlyList<string> AvailableTools { get; }

    public void Dispose()
    {
        Agent.Dispose();
        _services.Dispose();
    }
}

/// <summary>
/// Builds a <see cref="SimpleAgent"/> for a single instance: tools scoped to the workspace
/// (file/search/edit only — no process execution), the LLM provider (OpenAI provider pointed
/// at OpenRouter) wrapped in the rate-limit decorator.
/// </summary>
public sealed class SweAgentFactory
{
    private readonly RunContext _ctx;

    public SweAgentFactory(RunContext ctx) => _ctx = ctx;

    /// <summary>The OpenRouter API key from the environment (null/empty if unset).</summary>
    public static string? ApiKey => Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");

    public SweAgent Create(string workspaceDir)
    {
        var services = new ServiceCollection();

        var minLevel = Enum.TryParse<LogLevel>(
            Environment.GetEnvironmentVariable("SWEBENCH_LOG_LEVEL"), ignoreCase: true, out var lvl)
            ? lvl
            : LogLevel.Warning;
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(minLevel));

        // In-memory LLM config: the native OpenRouter provider (Andy.Llm >= 2026.5.29-rc.31).
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Llm:DefaultProvider"] = "openrouter",
                ["Llm:Providers:openrouter:Provider"] = "openrouter",
                ["Llm:Providers:openrouter:ApiBase"] = _ctx.ProviderBaseUrl,
                ["Llm:Providers:openrouter:ApiKey"] = ApiKey ?? string.Empty,
                ["Llm:Providers:openrouter:Model"] = _ctx.Model,
                ["Llm:Providers:openrouter:Enabled"] = "true",
            })
            .Build();
        services.AddSingleton<IConfiguration>(config);
        services.AddLlmServices(config);

        services.AddAndyTools(options =>
        {
            options.RegisterBuiltInTools = true;
            options.EnableDetailedTracing = false;
            // Scope file access to the workspace; no process execution (agent edits files only).
            options.DefaultPermissions.AllowedPaths = new HashSet<string> { workspaceDir };
            options.DefaultPermissions.ProcessExecution = false;
            options.DefaultPermissions.NetworkAccess = false;
        });

        var provider = services.BuildServiceProvider();

        var lifecycle = provider.GetRequiredService<IToolLifecycleManager>();
        lifecycle.InitializeAsync().GetAwaiter().GetResult();

        var registry = provider.GetRequiredService<IToolRegistry>();
        var executor = provider.GetRequiredService<IToolExecutor>();

        var factory = provider.GetRequiredService<ILlmProviderFactory>();
        var rawProvider = factory.CreateProvider("openrouter");
        var policy = new RateLimitPolicy
        {
            MaxRetries = _ctx.MaxRetries,
            MaxDelay = TimeSpan.FromSeconds(_ctx.MaxDelaySeconds),
        };
        var llm = new RateLimitingLlmProvider(
            rawProvider, policy, provider.GetService<ILoggerFactory>()?.CreateLogger("swebench.llm"));

        var agent = new SimpleAgent(
            llm,
            registry,
            executor,
            systemPrompt: SweSystemPrompt.Build(workspaceDir),
            maxTurns: _ctx.MaxTurns,
            workingDirectory: workspaceDir,
            logger: provider.GetService<ILoggerFactory>()?.CreateLogger<SimpleAgent>(),
            maxOutputTokens: _ctx.MaxOutputTokens,
            maxContextTokens: _ctx.MaxContextTokens);

        return new SweAgent(
            agent,
            rawProvider.Name,
            registry.Tools.Where(t => t.IsEnabled).Select(t => t.Metadata.Id).ToList(),
            provider);
    }
}
