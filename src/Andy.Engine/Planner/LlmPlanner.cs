using System.Text.Json;
using System.Text.Json.Nodes;
using Andy.Engine.Contracts;
using Andy.Model.Llm;
using Andy.Model.Model;
using Andy.Tools.Core;
using Microsoft.Extensions.Logging;
using EngineToolCall = Andy.Engine.Contracts.ToolCall;

namespace Andy.Engine.Planner;

/// <summary>
/// LLM-based planner implementation using structured output.
/// </summary>
public class LlmPlanner : IPlanner
{
    private readonly ILlmProvider _llmProvider;
    private readonly IToolRegistry _toolRegistry;
    private readonly ILogger<LlmPlanner>? _logger;
    private readonly PlannerOptions _options;

    public LlmPlanner(
        ILlmProvider llmProvider,
        IToolRegistry toolRegistry,
        PlannerOptions? options = null,
        ILogger<LlmPlanner>? logger = null)
    {
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _options = options ?? PlannerOptions.Default;
        _logger = logger;
    }

    public async Task<PlannerDecision> DecideAsync(AgentState state, CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Planning next action for turn {TurnIndex}", state.TurnIndex);

        var prompt = BuildPrompt(state);
        var response = await GetStructuredResponseAsync(prompt, cancellationToken);

        return ParseDecision(response);
    }

    private string BuildPrompt(AgentState state)
    {
        var toolCatalog = GetToolCatalog();

        return $$"""
            You are the Planner. Choose exactly one next action:
            - call_tool: { "name": <tool_name>, "args": { ... } }
            - ask_user: { "question": "...", "missing_fields": ["..."] }
            - replan: { "subgoals": ["..."] }
            - stop: { "reason": "..." }

            Current Goal: {{state.Goal.UserGoal}}
            Constraints: {{string.Join(", ", state.Goal.Constraints)}}
            Subgoals: {{string.Join(", ", state.Subgoals)}}

            Turn: {{state.TurnIndex}}/{{state.Budget.MaxTurns}}

            Available Tools:
            {{toolCatalog}}

            {{(state.LastObservation != null ? $"Last Observation:\n{state.LastObservation.Summary}\nFacts: {JsonSerializer.Serialize(state.LastObservation.KeyFacts)}\nNext Actions: {string.Join(", ", state.LastObservation.Affordances)}" : "")}}

            If a tool fails:
            - Retry ≤2 with backoff for retryables
            - Else attempt fallback
            - Else ask_user for missing information
            - Else stop with a short summary

            Respond with a JSON object containing your decision.
            """;
    }

    private string GetToolCatalog()
    {
        var tools = _toolRegistry.Tools;
        var catalog = new List<string>();

        foreach (var registration in tools)
        {
            if (registration.IsEnabled)
            {
                var metadata = registration.Metadata;
                catalog.Add($"- {metadata.Name}: {metadata.Description}");
            }
        }

        return string.Join("\n", catalog);
    }

    private async Task<JsonNode> GetStructuredResponseAsync(string prompt, CancellationToken cancellationToken)
    {
        var request = new LlmRequest
        {
            Messages = new[]
            {
                new Message
                {
                    Role = MessageRole.System,
                    Content = _options.SystemPrompt
                },
                new Message
                {
                    Role = MessageRole.User,
                    Content = prompt
                }
            }.ToList(),
            Config = new LlmClientConfig
            {
                MaxTokens = _options.MaxTokens,
                Temperature = (decimal)_options.Temperature
            }
        };

        var response = await _llmProvider.CompleteAsync(request, cancellationToken);

        if (response.Content == null)
            throw new InvalidOperationException("LLM returned null content");

        return JsonNode.Parse(response.Content) ??
            throw new InvalidOperationException("Failed to parse LLM response as JSON");
    }

    private PlannerDecision ParseDecision(JsonNode response)
    {
        var action = response["action"]?.GetValue<string>() ??
            throw new InvalidOperationException("Missing 'action' field in response");

        return action switch
        {
            "call_tool" => new CallToolDecision(
                new EngineToolCall(
                    response["name"]?.GetValue<string>() ?? throw new InvalidOperationException("Missing tool name"),
                    response["args"] ?? throw new InvalidOperationException("Missing tool args")
                )
            ),
            "ask_user" => new AskUserDecision(
                response["question"]?.GetValue<string>() ?? throw new InvalidOperationException("Missing question"),
                response["missing_fields"]?.AsArray().Select(n => n?.GetValue<string>() ?? "").ToList() ?? new List<string>()
            ),
            "replan" => new ReplanDecision(
                response["subgoals"]?.AsArray().Select(n => n?.GetValue<string>() ?? "").ToList() ??
                    throw new InvalidOperationException("Missing subgoals")
            ),
            "stop" => new StopDecision(
                response["reason"]?.GetValue<string>() ?? "Task completed"
            ),
            _ => throw new InvalidOperationException($"Unknown action type: {action}")
        };
    }
}

public class PlannerOptions
{
    public string SystemPrompt { get; set; } = """
        You are a planning agent that decides the next action to take.
        Always respond with valid JSON in the specified format.
        Be deterministic and focused on achieving the goal efficiently.
        """;

    public int MaxTokens { get; set; } = 500;
    public double Temperature { get; set; } = 0.0;

    public static PlannerOptions Default => new();
}