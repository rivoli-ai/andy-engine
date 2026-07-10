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

/// <summary>
/// The andy in-process agent (a <see cref="SimpleAgent"/>) plus the resources it owns; dispose
/// to release the DI container. Implements <see cref="ISweAgent"/> so it is interchangeable with
/// external CLI agents at the <c>SweInstanceRunner</c> seam.
/// </summary>
public sealed class SweAgent : ISweAgent
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

    public async Task<SweAgentRunResult> RunAsync(string problemStatement, CancellationToken cancellationToken = default)
    {
        var r = await Agent.ProcessMessageAsync(problemStatement, cancellationToken);
        return new SweAgentRunResult(r.Success, r.StopReason, r.TurnCount, r.Duration);
    }

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
public sealed class SweAgentFactory : ISweAgentFactory
{
    private readonly RunContext _ctx;
    private readonly SwePromptConfig _prompt;

    // Loads/validates the prompt sources once (throws a clear ArgumentException on a bad
    // --system-prompt-file / --rules-dir, before any instance work begins).
    public SweAgentFactory(RunContext ctx)
    {
        _ctx = ctx;
        _prompt = SwePromptConfig.Load(ctx.SystemPromptFile, ctx.RulesDir);
    }

    /// <summary>The OpenRouter API key from the environment (null/empty if unset).</summary>
    public static string? ApiKey => Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");

    ISweAgent ISweAgentFactory.Create(string workspaceDir, Model.SweBenchInstance instance) =>
        Create(workspaceDir, instance);

    public SweAgent Create(string workspaceDir, Model.SweBenchInstance instance)
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

        // Read-only PDF document tools (pdf_*) over the managed Andy.Doc engine. They require only
        // filesystem-read, so they operate within the workspace-scoped permissions above.
        Andy.Tools.Pdf.ServiceCollectionExtensions.AddAndyPdfTools(services);

        var provider = services.BuildServiceProvider();

        var lifecycle = provider.GetRequiredService<IToolLifecycleManager>();
        lifecycle.InitializeAsync().GetAwaiter().GetResult();

        var registry = provider.GetRequiredService<IToolRegistry>();
        var executor = provider.GetRequiredService<IToolExecutor>();

        // Opt-in in-loop test tool: lets the agent run tests against its current edits in the
        // official Docker image and iterate. Registered per-instance so it carries this instance's
        // image/repo and a private invocation counter.
        if (_ctx.EnableTestTool)
        {
            var testRunner = new Grading.SweTestRunner(
                new Grading.DockerClient(), new Grading.TestSpecBuilder(),
                maxOutputChars: _ctx.MaxToolResultChars);
            var runTests = new RunTestsTool(instance, workspaceDir, testRunner, _ctx.MaxTestRuns);
            registry.RegisterTool(runTests.Metadata, _ => runTests, new Dictionary<string, object>());
            registry.SetToolEnabled(runTests.Metadata.Id, true);
        }

        // Optional Agent Skills: a `skill` tool the agent calls to load a skill's body on demand.
        // Registered as an instance (like RunTestsTool) because SkillTool has a constructor
        // dependency (ISkillCatalog) the registry's type-based instantiation can't satisfy. The tool
        // reads SKILL.md directly (not via the workspace-scoped file tools), so the skills dir may
        // live outside the workspace.
        Andy.Skills.Tools.ISkillCatalog? skillCatalog = null;
        if (!string.IsNullOrWhiteSpace(_ctx.SkillsDir))
        {
            var options = new Andy.Skills.Tools.SkillCatalogOptions();
            options.Roots.Add(_ctx.SkillsDir!);
            skillCatalog = new Andy.Skills.Tools.SkillCatalog(options);

            var skillTool = new Andy.Skills.Tools.SkillTool(skillCatalog);
            registry.RegisterTool(skillTool.Metadata, _ => skillTool, new Dictionary<string, object>());
            registry.SetToolEnabled(skillTool.Metadata.Id, true);
        }

        var factory = provider.GetRequiredService<ILlmProviderFactory>();
        var rawProvider = factory.CreateProvider("openrouter");
        var policy = new RateLimitPolicy
        {
            MaxRetries = _ctx.MaxRetries,
            MaxDelay = TimeSpan.FromSeconds(_ctx.MaxDelaySeconds),
        };
        var llm = new RateLimitingLlmProvider(
            rawProvider, policy, provider.GetService<ILoggerFactory>()?.CreateLogger("swebench.llm"));

        var systemPrompt = _prompt.Build(workspaceDir, instance.Repo);
        if (skillCatalog is not null)
        {
            var skills = skillCatalog.GetSkillsAsync().GetAwaiter().GetResult();
            systemPrompt = SwePromptConfig.AppendSkillsBlock(systemPrompt, skills);
        }

        var agent = new SimpleAgent(
            llm,
            registry,
            executor,
            systemPrompt: systemPrompt,
            maxTurns: _ctx.MaxTurns,
            workingDirectory: workspaceDir,
            logger: provider.GetService<ILoggerFactory>()?.CreateLogger<SimpleAgent>(),
            maxOutputTokens: _ctx.MaxOutputTokens,
            maxContextTokens: _ctx.MaxContextTokens,
            maxToolResultChars: _ctx.MaxToolResultChars);

        return new SweAgent(
            agent,
            rawProvider.Name,
            registry.Tools.Where(t => t.IsEnabled).Select(t => t.Metadata.Id).ToList(),
            provider);
    }
}
