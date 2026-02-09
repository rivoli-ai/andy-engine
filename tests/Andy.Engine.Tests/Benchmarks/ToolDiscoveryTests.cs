using Andy.Tools;
using Andy.Tools.Core;
using Andy.Tools.Framework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Andy.Engine.Tests.Benchmarks;

public class ToolDiscoveryTests
{
    private readonly ITestOutputHelper _output;

    public ToolDiscoveryTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void PrintAllToolMetadata()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddAndyTools(options =>
        {
            options.RegisterBuiltInTools = true;
        });

        var provider = services.BuildServiceProvider();
        var lifecycleManager = provider.GetRequiredService<IToolLifecycleManager>();
        lifecycleManager.InitializeAsync().GetAwaiter().GetResult();

        var toolRegistry = provider.GetRequiredService<IToolRegistry>();

        foreach (var tool in toolRegistry.Tools.Where(t => t.IsEnabled).OrderBy(t => t.Metadata.Id))
        {
            _output.WriteLine($"Tool: {tool.Metadata.Id} ({tool.Metadata.Name})");
            _output.WriteLine($"  Description: {tool.Metadata.Description}");
            foreach (var param in tool.Metadata.Parameters)
            {
                var extras = new List<string>();
                if (param.Required) extras.Add("required");
                if (param.AllowedValues?.Count > 0) extras.Add($"allowed=[{string.Join(",", param.AllowedValues)}]");
                if (param.DefaultValue != null) extras.Add($"default={param.DefaultValue}");
                _output.WriteLine($"  Param: {param.Name} ({param.Type}) [{string.Join(", ", extras)}] - {param.Description}");
            }
            _output.WriteLine("");
        }
    }
}
