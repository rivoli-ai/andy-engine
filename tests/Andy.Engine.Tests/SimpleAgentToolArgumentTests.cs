using Andy.Engine;
using Andy.Model.Llm;
using Andy.Model.Model;
using Andy.Tools.Core;
using Moq;
using Xunit;

namespace Andy.Engine.Tests;

/// <summary>
/// LLM tool calls arrive as a JSON arguments string. This verifies the agent deserializes nested
/// JSON into real CLR dictionaries and lists (not raw JSON text), so tools with object/array
/// parameters — e.g. the dataframe_* tools' 'predicate', 'aggregations', 'having' — receive usable
/// structures. Regression for "Parameter 'predicate' must be an object" and an 'aggregations'
/// argument arriving as System.String[].
/// </summary>
public class SimpleAgentToolArgumentTests
{
    [Fact]
    public async Task Nested_json_tool_arguments_become_dictionaries_and_lists()
    {
        var registry = new Mock<IToolRegistry>();
        registry.Setup(r => r.Tools).Returns(new List<ToolRegistration>
        {
            new()
            {
                IsEnabled = true,
                Metadata = new ToolMetadata { Id = "dataframe_group_by", Name = "Group By", Description = "Groups rows." },
            },
        });

        // Capture the parameters the executor actually receives.
        Dictionary<string, object?>? captured = null;
        var executor = new Mock<IToolExecutor>();
        executor.Setup(e => e.ExecuteAsync(
                It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<ToolExecutionContext>()))
            .Callback<string, Dictionary<string, object?>, ToolExecutionContext>((_, p, _) => captured = p)
            .ReturnsAsync(new ToolExecutionResult { IsSuccessful = true, Data = "ok" });

        // A realistic group_by call: a string array (group_by), an array of objects (aggregations),
        // and a nested object (having).
        const string argsJson =
            "{\"dataset_id\":\"sales\",\"group_by\":[\"region\"]," +
            "\"aggregations\":[{\"column\":\"amount\",\"function\":\"sum\",\"alias\":\"total\"}]," +
            "\"having\":{\"column\":\"total\",\"op\":\"gt\",\"value\":100}}";

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
                        new() { Id = "c1", Name = "dataframe_group_by", ArgumentsJson = argsJson },
                    },
                },
            })
            .ReturnsAsync(new LlmResponse
            {
                AssistantMessage = new Message { Role = Role.Assistant, Content = "Done." },
                FinishReason = "stop",
            });

        var agent = new SimpleAgent(provider.Object, registry.Object, executor.Object, "system", maxTurns: 5);
        await agent.ProcessMessageAsync("group it");

        Assert.NotNull(captured);

        // String arrays still come through as a string array.
        var groupBy = Assert.IsAssignableFrom<IEnumerable<string>>(captured!["group_by"]);
        Assert.Equal(new[] { "region" }, groupBy);

        // Arrays of objects survive as a list of dictionaries (NOT a string[] of raw JSON).
        var aggregations = Assert.IsAssignableFrom<System.Collections.IEnumerable>(captured["aggregations"])
            .Cast<object?>().ToList();
        var agg = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(Assert.Single(aggregations));
        Assert.Equal("amount", agg["column"]);
        Assert.Equal("sum", agg["function"]);
        Assert.Equal("total", agg["alias"]);

        // Nested objects survive as a dictionary (NOT a raw JSON string).
        var having = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(captured["having"]);
        Assert.Equal("total", having["column"]);
        Assert.Equal("gt", having["op"]);
    }
}
