using System.Text.Json;
using Andy.Engine;
using Andy.Model.Llm;
using Andy.Model.Model;
using Andy.Tools.Core;
using Moq;
using Xunit;

namespace Andy.Engine.Tests;

/// <summary>
/// Large successful tool results (e.g. a full file read or directory listing) must not be
/// serialized whole into the conversation history. They are capped with a head slice plus
/// actionable guidance (progressive disclosure). Error results and small results are untouched.
/// </summary>
public class SimpleAgentToolOutputLimitTests
{
    // --- TruncateToolResult helper (unit-tested directly) ---

    [Fact]
    public void TruncateToolResult_Leaves_Small_Result_Unchanged()
    {
        var small = JsonSerializer.Serialize(new { success = true, result = "tiny", message = "ok" });

        var output = SimpleAgent.TruncateToolResult(small, maxChars: 16000);

        Assert.Equal(small, output);
    }

    [Fact]
    public void TruncateToolResult_NonPositive_Cap_Disables_Truncation()
    {
        var big = new string('x', 50_000);

        var output = SimpleAgent.TruncateToolResult(big, maxChars: 0);

        Assert.Equal(big, output);
    }

    [Fact]
    public void TruncateToolResult_Caps_Large_Result_With_Guidance()
    {
        // A payload well over the cap.
        var original = JsonSerializer.Serialize(new
        {
            success = true,
            result = new string('A', 40_000),
            message = "ok"
        });
        const int cap = 16000;

        var output = SimpleAgent.TruncateToolResult(original, cap);

        // Output must be valid JSON the model can parse.
        using var doc = JsonDocument.Parse(output);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("truncated").GetBoolean());
        Assert.Equal(original.Length, root.GetProperty("total_chars").GetInt32());
        Assert.Equal(cap, root.GetProperty("shown_chars").GetInt32());

        // The head slice is kept under "result".
        var result = root.GetProperty("result").GetString();
        Assert.NotNull(result);
        Assert.Equal(cap, result!.Length);
        Assert.Equal(original[..cap], result);

        // Actionable guidance is present.
        var guidance = root.GetProperty("guidance").GetString();
        Assert.NotNull(guidance);
        Assert.Contains("truncated", guidance!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("line range", guidance!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"{cap}", guidance!);
        Assert.Contains($"{original.Length}", guidance!);
    }

    // --- Full-loop integration: the tool message added to the conversation is truncated ---

    private static SimpleAgent NewAgent(ILlmProvider provider, IToolExecutor executor, int maxToolResultChars)
    {
        var registry = new Mock<IToolRegistry>();
        registry.Setup(r => r.Tools).Returns(new List<ToolRegistration>());
        return new SimpleAgent(
            provider,
            registry.Object,
            executor,
            systemPrompt: "system",
            maxTurns: 10,
            maxToolResultChars: maxToolResultChars);
    }

    [Fact]
    public async Task Large_Successful_Tool_Result_Is_Truncated_In_Conversation()
    {
        const int cap = 2000;

        // Turn 1: request a tool call. Turn 2: final answer.
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
                        new ToolCall { Id = "call_1", Name = "read_file", ArgumentsJson = "{}" }
                    }
                }
            })
            .ReturnsAsync(new LlmResponse
            {
                AssistantMessage = new Message { Role = Role.Assistant, Content = "Done." },
                FinishReason = "stop"
            });

        // The tool returns a large successful result.
        var executor = new Mock<IToolExecutor>();
        executor.Setup(e => e.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<ToolExecutionContext>()))
            .ReturnsAsync(new ToolExecutionResult
            {
                IsSuccessful = true,
                Data = new string('Z', 50_000),
                Message = "read"
            });

        var agent = NewAgent(provider.Object, executor.Object, cap);

        var result = await agent.ProcessMessageAsync("Read the file.");

        Assert.True(result.Success);

        // Find the tool result message recorded in the conversation.
        var toolMessage = agent.Conversation.ToChronoMessages()
            .First(m => m.Role == Role.Tool);

        Assert.True(toolMessage.Content!.Length <= cap + 1000,
            $"Tool message should be capped near {cap} chars but was {toolMessage.Content.Length}.");

        using var doc = JsonDocument.Parse(toolMessage.Content!);
        Assert.True(doc.RootElement.GetProperty("truncated").GetBoolean());
        Assert.Contains("truncated",
            doc.RootElement.GetProperty("guidance").GetString()!,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Error results are already small and must be preserved verbatim, never truncated.</summary>
    [Fact]
    public async Task Error_Tool_Result_Is_Not_Truncated()
    {
        const int cap = 10;

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
                        new ToolCall { Id = "call_1", Name = "read_file", ArgumentsJson = "{}" }
                    }
                }
            })
            .ReturnsAsync(new LlmResponse
            {
                AssistantMessage = new Message { Role = Role.Assistant, Content = "Done." },
                FinishReason = "stop"
            });

        var executor = new Mock<IToolExecutor>();
        executor.Setup(e => e.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<ToolExecutionContext>()))
            .ReturnsAsync(new ToolExecutionResult
            {
                IsSuccessful = false,
                ErrorMessage = "a fairly long error message that exceeds the tiny cap"
            });

        var agent = NewAgent(provider.Object, executor.Object, cap);

        await agent.ProcessMessageAsync("Read the file.");

        var toolMessage = agent.Conversation.ToChronoMessages()
            .First(m => m.Role == Role.Tool);

        using var doc = JsonDocument.Parse(toolMessage.Content!);
        // Error payload is preserved verbatim, not wrapped as truncated.
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.False(doc.RootElement.TryGetProperty("truncated", out _));
    }
}
