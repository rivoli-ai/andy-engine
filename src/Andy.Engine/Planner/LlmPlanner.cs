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
            You are the Planner. Choose exactly one next action and respond with valid JSON.

            Response format examples:
            - To call a tool: { "action": "call_tool", "name": "tool_name", "args": { "param": "value" } }
            - To ask user: { "action": "ask_user", "question": "What do you need?", "missing_fields": [] }
            - To replan: { "action": "replan", "subgoals": ["subgoal 1", "subgoal 2"] }
            - To stop: { "action": "stop", "reason": "Complete response here" }

            Current Goal: {{state.Goal.UserGoal}}
            Constraints: {{string.Join(", ", state.Goal.Constraints)}}
            Subgoals: {{string.Join(", ", state.Subgoals)}}

            Turn: {{state.TurnIndex}}/{{state.Budget.MaxTurns}}

            Available Tools:
            {{toolCatalog}}

            {{(state.LastObservation != null ? $"Last Observation:\n{state.LastObservation.Summary}\nFacts: {JsonSerializer.Serialize(state.LastObservation.KeyFacts)}\nNext Actions: {string.Join(", ", state.LastObservation.Affordances)}" : "")}}

            Guidelines:
            - CRITICAL: For simple greetings (hello, hi, hey) or conversational responses, use 'stop' immediately - DO NOT call any tools
            - For questions about yourself (what model are you, who are you), use 'stop' with a direct answer - DO NOT call any tools
            - For questions you can answer directly (explanations, code examples, general knowledge), use 'stop' with your COMPLETE answer
            - For requests like "write a program" or "show me code", use 'stop' with the FULL CODE in the reason field
            - When using 'stop', put the ENTIRE response content in the 'reason' field - don't just describe what you'll provide
            - After successfully executing a tool, ALWAYS use 'stop' with a detailed message explaining what was done
              Example: "I've written the C# program to /tmp/HelloWorld.cs. The file contains a simple Hello World application."
            - When you see a tool result in Last Observation, extract key details (file paths, results) and include them in your 'stop' message
            - Only use 'ask_user' if absolutely critical information is missing (e.g., API keys when no default available)
            - DO NOT ask for clarification on routine requests - make reasonable assumptions and provide a helpful response
            - Use 'call_tool' ONLY when file system operations or external tools are actually needed (reading files, executing commands, etc.)
            - DO NOT call tools to gather information you can provide directly (like greetings, self-identification, general knowledge)
            - If a tool fails: retry ≤2 with backoff for retryables, then attempt fallback, or stop with explanation
            - Maintain conversation context: if the user refers to "that file" or "the program", use information from previous turns
            - When the goal is achieved, include "goal achieved" at the END of your detailed response, not as the only text

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
                var toolDescription = $"- {metadata.Id}: {metadata.Description}";

                if (metadata.Parameters.Count > 0)
                {
                    toolDescription += "\n  Parameters:";
                    foreach (var param in metadata.Parameters)
                    {
                        var paramDesc = $"\n    - {param.Name} ({param.Type})";
                        if (param.Required)
                            paramDesc += " [required]";
                        paramDesc += $": {param.Description}";

                        if (param.AllowedValues != null && param.AllowedValues.Count > 0)
                        {
                            paramDesc += $" (allowed: {string.Join(", ", param.AllowedValues)})";
                        }

                        if (param.DefaultValue != null)
                        {
                            paramDesc += $" (default: {param.DefaultValue})";
                        }

                        toolDescription += paramDesc;
                    }
                }

                catalog.Add(toolDescription);
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

        if (response?.AssistantMessage?.Content == null)
            throw new InvalidOperationException("LLM returned null content");

        var content = response.AssistantMessage.Content;
        _logger?.LogWarning("Raw LLM response: '{Response}'", content);

        // Clean up markdown-wrapped JSON if present
        content = content.Trim();
        if (content.StartsWith("```"))
        {
            // Remove markdown code block wrapper
            var lines = content.Split('\n');
            var startIdx = 0;
            var endIdx = lines.Length - 1;

            // Find start of JSON (skip ```json or ```)
            if (lines[0].StartsWith("```"))
                startIdx = 1;

            // Find end (skip closing ```)
            if (endIdx > 0 && lines[endIdx].Trim() == "```")
                endIdx--;

            content = string.Join('\n', lines[startIdx..(endIdx + 1)]);
        }

        _logger?.LogInformation("LLM response (cleaned): '{Response}'", content);

        try
        {
            var parsed = JsonNode.Parse(content);
            _logger?.LogInformation("Parsed JSON successfully: {Json}", parsed?.ToJsonString());
            return parsed ?? throw new InvalidOperationException("Failed to parse LLM response as JSON");
        }
        catch (Exception ex)
        {
            _logger?.LogError("Failed to parse JSON: {Error}. Content was: '{Content}'", ex.Message, content);
            throw;
        }
    }

    internal PlannerDecision ParseDecision(JsonNode response)
    {
        // Try to get action field, with fallback logic
        var action = response["action"]?.GetValue<string>();

        if (string.IsNullOrEmpty(action))
        {
            _logger?.LogWarning("No 'action' field found. Response: {Response}", response.ToJsonString());

            // Fallback: Check if response has "call_tool" field (alternative format)
            var callTool = response["call_tool"];
            if (callTool != null)
            {
                var toolName = callTool["name"]?.GetValue<string>();
                var args = callTool["args"];

                _logger?.LogInformation("Found call_tool format, converting to standard format");
                _logger?.LogInformation("Tool name from LLM: {ToolName}, Args: {Args}", toolName, args?.ToJsonString());

                // Map tool names
                var mappedToolName = toolName switch
                {
                    "Date Time Tool" => "datetime_tool",
                    "Encoding Tool" => "encoding_tool",
                    _ => toolName?.ToLowerInvariant().Replace(" ", "_")
                };

                if (mappedToolName == "datetime_tool" && (args == null || !args!.AsObject().Any()))
                {
                    args = JsonNode.Parse("""{"operation": "now"}""");
                    _logger?.LogInformation("Applied default args for datetime_tool: {Args}", args!.ToJsonString());
                }

                var finalCall = new EngineToolCall(
                    mappedToolName ?? "unknown_tool",
                    args ?? JsonNode.Parse("{}")!
                );
                _logger?.LogWarning("Creating CallToolDecision: Tool={ToolName}, Args={Args}", finalCall.ToolName, finalCall.Args.ToJsonString());
                return new CallToolDecision(finalCall);
            }

            // Fallback: Check if response has "ask_user" field (alternative format for conversational responses)
            var askUser = response["ask_user"];
            if (askUser != null)
            {
                var question = askUser["question"]?.GetValue<string>();
                var missingFields = askUser["missing_fields"]?.AsArray()
                    .Select(n => n?.GetValue<string>() ?? "")
                    .ToList() ?? new List<string>();

                _logger?.LogInformation("Found ask_user format (conversational response), converting to standard format");

                // For simple chat responses, this is actually a completion, not asking for missing info
                // Convert to a stop decision with the response content
                if (missingFields.Count == 0 && !string.IsNullOrEmpty(question))
                {
                    _logger?.LogInformation("Treating as conversational response (stop with content)");
                    return new StopDecision(question);
                }

                return new AskUserDecision(question ?? "Could you provide more information?", missingFields);
            }

            // Fallback: Check if response has "stop" field (alternative format)
            var stop = response["stop"];
            if (stop != null)
            {
                var reason = stop["reason"]?.GetValue<string>() ?? "Task completed";
                _logger?.LogInformation("Found stop format, converting to standard format");
                return new StopDecision(reason);
            }

            throw new InvalidOperationException($"Missing 'action' field in response: {response.ToJsonString()}");

        }

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