using Andy.Engine;
using Andy.Model.Llm;
using Andy.Model.Model;
using Andy.Tools.Core;
using Moq;
using Xunit;

namespace Andy.Engine.Tests;

/// <summary>
/// The agent surfaces tools to the model via the LLM request's native tool declarations (not the
/// system-prompt text). This verifies that every enabled registry tool — which is how the
/// dataframe_* tools, like all others, reach the model — is sent with its id, description, and a
/// JSON-schema parameter shape (required list + array item types), and that disabled tools are
/// omitted.
/// </summary>
public class SimpleAgentToolDeclarationTests
{
    private static IToolExecutor NoopExecutor()
    {
        var executor = new Mock<IToolExecutor>();
        executor.Setup(e => e.ExecuteAsync(
                It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<ToolExecutionContext>()))
            .ReturnsAsync(new ToolExecutionResult { IsSuccessful = true, Data = "ok" });
        return executor.Object;
    }

    // A provider that finishes on the first turn, capturing the request it received.
    private static Mock<ILlmProvider> FinishingProvider(List<LlmRequest> capture)
    {
        var provider = new Mock<ILlmProvider>();
        provider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmRequest, CancellationToken>((req, _) => capture.Add(req))
            .ReturnsAsync(new LlmResponse
            {
                AssistantMessage = new Message { Role = Role.Assistant, Content = "Done." },
                FinishReason = "stop",
            });
        return provider;
    }

    [Fact]
    public async Task Enabled_registry_tools_are_declared_to_the_model_with_their_schema()
    {
        var enabled = new ToolRegistration
        {
            IsEnabled = true,
            Metadata = new ToolMetadata
            {
                Id = "dataframe_filter",
                Name = "DataFrame Filter",
                Description = "Selects rows from a dataset that match a structured predicate tree.",
                Parameters = new List<ToolParameter>
                {
                    new() { Name = "dataset_id", Type = "string", Required = true, Description = "Input dataset id." },
                    new()
                    {
                        Name = "columns", Type = "array", Required = false, Description = "Columns.",
                        ItemType = new ToolParameter { Type = "string", Description = "A column name." },
                    },
                },
            },
        };
        var disabled = new ToolRegistration
        {
            IsEnabled = false,
            Metadata = new ToolMetadata { Id = "dataframe_drop", Name = "DataFrame Drop", Description = "Releases a dataset." },
        };

        var registry = new Mock<IToolRegistry>();
        registry.Setup(r => r.Tools).Returns(new List<ToolRegistration> { enabled, disabled });

        var requests = new List<LlmRequest>();
        var agent = new SimpleAgent(
            FinishingProvider(requests).Object, registry.Object, NoopExecutor(),
            systemPrompt: "system", maxTurns: 5);

        await agent.ProcessMessageAsync("hi");

        var tools = Assert.Single(requests).Tools;

        // Enabled tool is declared; disabled tool is omitted.
        Assert.Contains(tools, t => t.Name == "dataframe_filter");
        Assert.DoesNotContain(tools, t => t.Name == "dataframe_drop");

        var decl = tools.Single(t => t.Name == "dataframe_filter");
        Assert.Equal("Selects rows from a dataset that match a structured predicate tree.", decl.Description);

        var schema = Assert.IsAssignableFrom<IDictionary<string, object>>(decl.Parameters);
        Assert.Equal("object", schema["type"]);

        var properties = Assert.IsAssignableFrom<IDictionary<string, object>>(schema["properties"]);
        Assert.True(properties.ContainsKey("dataset_id"));
        Assert.True(properties.ContainsKey("columns"));

        // Required list reflects the per-parameter Required flags.
        var required = Assert.IsAssignableFrom<IEnumerable<string>>(schema["required"]).ToList();
        Assert.Contains("dataset_id", required);
        Assert.DoesNotContain("columns", required);

        // Scalar param carries its type; array param carries an items schema.
        var dsid = Assert.IsAssignableFrom<IDictionary<string, object>>(properties["dataset_id"]);
        Assert.Equal("string", dsid["type"]);

        var columns = Assert.IsAssignableFrom<IDictionary<string, object>>(properties["columns"]);
        Assert.Equal("array", columns["type"]);
        var items = Assert.IsAssignableFrom<IDictionary<string, object>>(columns["items"]);
        Assert.Equal("string", items["type"]);
    }

    [Fact]
    public async Task ExtraBody_flows_through_to_the_llm_request()
    {
        var registry = new Mock<IToolRegistry>();
        registry.Setup(r => r.Tools).Returns(new List<ToolRegistration>());

        // OpenRouter-style provider routing supplied at the engine boundary.
        var routing = new Dictionary<string, object?>
        {
            ["provider"] = new Dictionary<string, object?>
            {
                ["order"] = new[] { "deepinfra/turbo" },
                ["allow_fallbacks"] = false
            }
        };

        var requests = new List<LlmRequest>();
        var agent = new SimpleAgent(
            FinishingProvider(requests).Object, registry.Object, NoopExecutor(),
            systemPrompt: "system", maxTurns: 5, extraBody: routing);

        await agent.ProcessMessageAsync("hi");

        // The dictionary the caller passed reaches LlmRequest.ExtraBody unchanged.
        Assert.Same(routing, Assert.Single(requests).ExtraBody);
    }
}
