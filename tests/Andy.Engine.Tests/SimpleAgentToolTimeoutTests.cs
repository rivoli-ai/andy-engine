using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Andy.Engine;
using Andy.Model.Llm;
using Andy.Model.Model;
using Andy.Tools.Core;
using Moq;
using Xunit;

namespace Andy.Engine.Tests;

/// <summary>
/// The framework imposes a default 30s per-tool execution cap via
/// <see cref="ToolResourceLimits.MaxExecutionTimeMs"/>. The agent must only keep that cap for tools
/// that do NOT manage their own timeout. A tool that declares a timeout parameter (e.g.
/// execute_command's <c>timeout_seconds</c>) enforces its own limit, so the engine leaves the cap
/// unbounded - otherwise the 30s cap would override the tool's timeout and kill long-running work.
/// </summary>
public class SimpleAgentToolTimeoutTests
{
    private static async Task<ToolExecutionContext?> RunAndCaptureContextAsync(ToolMetadata toolMeta)
    {
        var registration = new ToolRegistration { IsEnabled = true, Metadata = toolMeta };

        var registry = new Mock<IToolRegistry>();
        registry.Setup(r => r.Tools).Returns(new List<ToolRegistration> { registration });
        registry.Setup(r => r.GetTool(toolMeta.Id)).Returns(registration);

        ToolExecutionContext? captured = null;
        var executor = new Mock<IToolExecutor>();
        executor.Setup(e => e.ExecuteAsync(
                It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<ToolExecutionContext>()))
            .Callback<string, Dictionary<string, object?>, ToolExecutionContext>((_, _, ctx) => captured = ctx)
            .ReturnsAsync(new ToolExecutionResult { IsSuccessful = true, Data = "ok" });

        var provider = new Mock<ILlmProvider>();
        provider.SetupSequence(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                AssistantMessage = new Message
                {
                    Role = Role.Assistant,
                    Content = "",
                    ToolCalls = new List<ToolCall>
                    {
                        new() { Id = "c1", Name = toolMeta.Id, ArgumentsJson = "{}" },
                    },
                },
            })
            .ReturnsAsync(new LlmResponse
            {
                AssistantMessage = new Message { Role = Role.Assistant, Content = "Done." },
                FinishReason = "stop",
            });

        var agent = new SimpleAgent(provider.Object, registry.Object, executor.Object, "system", maxTurns: 5);
        await agent.ProcessMessageAsync("go");
        return captured;
    }

    [Fact]
    public async Task ToolWithTimeoutParameter_GetsUnboundedExecutionCap()
    {
        var meta = new ToolMetadata
        {
            Id = "execute_command",
            Name = "Execute Command",
            Description = "Runs a shell command.",
            Parameters = new List<ToolParameter>
            {
                new() { Name = "command", Type = "string", Required = true },
                new() { Name = "timeout_seconds", Type = "integer", Required = false },
            },
        };

        var ctx = await RunAndCaptureContextAsync(meta);

        Assert.NotNull(ctx);
        Assert.NotNull(ctx!.ResourceLimits);
        Assert.Equal(0, ctx.ResourceLimits!.MaxExecutionTimeMs); // unbounded - tool enforces its own timeout
    }

    [Fact]
    public async Task ToolWithoutTimeoutParameter_KeepsDefaultExecutionCap()
    {
        var meta = new ToolMetadata
        {
            Id = "read_file",
            Name = "Read File",
            Description = "Reads a file.",
            Parameters = new List<ToolParameter>
            {
                new() { Name = "file_path", Type = "string", Required = true },
            },
        };

        var ctx = await RunAndCaptureContextAsync(meta);

        Assert.NotNull(ctx);
        Assert.NotNull(ctx!.ResourceLimits);
        Assert.True(ctx.ResourceLimits!.MaxExecutionTimeMs > 0,
            "a tool without its own timeout should keep the default execution-time backstop");
    }
}
