using System.Text.Json;
using Andy.MCP.Client;
using Andy.MCP.Configuration;
using Andy.MCP.Protocol;
using Andy.MCP.Server;
using Andy.Model.Llm;
using Andy.Model.Model;
using Andy.Tools;
using Andy.Tools.Core;
using Andy.Tools.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using Role = Andy.Model.Model.Role;

namespace Andy.Engine.Tests.Mcp;

public class McpAgentScenarioTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SimpleAgentDiscoversAndExecutesRemoteToolThroughSharedAdapter(bool destructive)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var (transport, serverTransport) = InMemoryTransport.CreatePair();
        await using var server = new McpServer(serverTransport);
        using var schema = JsonDocument.Parse("""{"type":"object","properties":{"message":{"type":"string"}},"required":["message"],"additionalProperties":false}""");
        var calls = 0;
        server.AddTool("echo", "Echo a remote message", schema.RootElement.Clone(), (JsonElement? args, CancellationToken ct) =>
        {
            calls++;
            return Task.FromResult(CallToolResult.Text("remote: " + args!.Value.GetProperty("message").GetString()));
        }, annotations: new ToolAnnotations { DestructiveHint = destructive });
        var running = server.RunAsync(timeout.Token);
        await using var client = await McpClient.ConnectAsync(transport, cancellationToken: timeout.Token);
        var manager = new Mock<IMcpConnectionManager>();
        manager.SetupGet(m => m.ConnectedServers).Returns(["demo"]);
        manager.Setup(m => m.GetClient("demo")).Returns(client);
        var services = new ServiceCollection().AddLogging();
        services.AddAndyTools(options => { options.RegisterBuiltInTools = false; options.EnableObservability = false; });
        services.AddSingleton<IMcpToolInvoker>(new McpToolInvoker(manager.Object));
        await using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IToolRegistry>();
        var executor = provider.GetRequiredService<IToolExecutor>();
        using var registrar = new McpToolRegistrar(manager.Object, registry, NullLogger<McpToolRegistrar>.Instance);
        await registrar.StartAsync(timeout.Token);
        var requests = new List<LlmRequest>();
        var llm = new Mock<ILlmProvider>();
        llm.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmRequest request, CancellationToken ct) =>
            {
                requests.Add(request);
                return requests.Count == 1
                    ? new LlmResponse
                    {
                        AssistantMessage = new Message
                        {
                            Role = Role.Assistant,
                            Content = "",
                            ToolCalls = [new() { Id = "echo-call", Name = "mcp__demo__echo", ArgumentsJson = """{"message":"hello"}""" }],
                        },
                    }
                    : new LlmResponse { AssistantMessage = new Message { Role = Role.Assistant, Content = "Done." }, FinishReason = "stop" };
            });
        try
        {
            var agent = new SimpleAgent(llm.Object, registry, executor, systemPrompt: "system", maxTurns: 3);
            await agent.ProcessMessageAsync("Echo hello using the remote tool.", timeout.Token);
            Assert.Equal(2, requests.Count);
            var declaration = Assert.Single(requests[0].Tools);
            Assert.Equal("mcp__demo__echo", declaration.Name);
            Assert.Equal("Echo a remote message", declaration.Description);
            var toolMessage = Assert.Single(requests[1].Messages, m => m.Role == Role.Tool);
            using var result = JsonDocument.Parse(toolMessage.Content!);
            Assert.Equal(!destructive, result.RootElement.GetProperty("success").GetBoolean());
            if (destructive)
                Assert.Contains("explicit permission is not granted", result.RootElement.GetProperty("error").GetString());
            else
                Assert.Equal("remote: hello", result.RootElement.GetProperty("result").GetString());
            Assert.Equal(destructive ? 0 : 1, calls);
            Assert.Equal(destructive ? 0 : 1, executor.GetStatistics().SuccessfulExecutions);
        }
        finally
        {
            await registrar.StopAsync(CancellationToken.None);
            await timeout.CancelAsync();
            try { await running; } catch (OperationCanceledException) { }
        }
        Assert.Empty(registry.Tools);
    }
}
