using System.Text.Json;
using System.Text.Json.Nodes;
using Andy.Engine.Contracts;
using Andy.Model.Llm;
using Andy.Model.Model;
using Microsoft.Extensions.Logging;

namespace Andy.Engine.Critic;

/// <summary>
/// LLM-based implementation of the critic component.
/// </summary>
public class LlmCritic : ICritic
{
    private readonly ILlmProvider _llmProvider;
    private readonly ILogger<LlmCritic>? _logger;
    private readonly CriticOptions _options;

    public LlmCritic(
        ILlmProvider llmProvider,
        CriticOptions? options = null,
        ILogger<LlmCritic>? logger = null)
    {
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _options = options ?? CriticOptions.Default;
        _logger = logger;
    }

    public async Task<Critique> AssessAsync(
        AgentGoal goal,
        Observation observation,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Assessing observation against goal: {Goal}", goal.UserGoal);

        var prompt = BuildPrompt(goal, observation);
        var response = await GetStructuredResponseAsync(prompt, cancellationToken);

        return ParseCritique(response);
    }

    private string BuildPrompt(AgentGoal goal, Observation observation)
    {
        return $$"""
            You are the Critic. Assess whether the observation satisfies the goal.

            Goal: {{goal.UserGoal}}
            Constraints: {{string.Join(", ", goal.Constraints)}}

            Observation Summary: {{observation.Summary}}
            Key Facts: {{JsonSerializer.Serialize(observation.KeyFacts)}}
            Available Actions: {{string.Join(", ", observation.Affordances)}}

            Analyze:
            1. Does this observation indicate progress toward the goal?
            2. Is the goal satisfied?
            3. What gaps remain?
            4. What should be the next action?

            Respond with a JSON object:
            {
                "goal_satisfied": boolean,
                "assessment": "brief assessment",
                "known_gaps": ["gap1", "gap2"],
                "recommendation": "continue|replan|clarify|stop|retry"
            }
            """;
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
        _logger?.LogInformation("Raw LLM critic response: '{Response}'", content);

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

        _logger?.LogInformation("LLM critic response (cleaned): '{Response}'", content);

        try
        {
            var parsed = JsonNode.Parse(content);
            _logger?.LogInformation("Parsed critic JSON successfully: {Json}", parsed?.ToJsonString());
            return parsed ?? throw new InvalidOperationException("Failed to parse LLM response as JSON");
        }
        catch (Exception ex)
        {
            _logger?.LogError("Failed to parse critic JSON: {Error}. Content was: '{Content}'", ex.Message, content);
            throw;
        }
    }

    private Critique ParseCritique(JsonNode response)
    {
        var goalSatisfied = response["goal_satisfied"]?.GetValue<bool>() ?? false;
        var assessment = response["assessment"]?.GetValue<string>() ?? "No assessment provided";
        var knownGaps = response["known_gaps"]?.AsArray()
            .Select(n => n?.GetValue<string>() ?? "")
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList() ?? new List<string>();

        var recommendationStr = response["recommendation"]?.GetValue<string>() ?? "continue";
        var recommendation = recommendationStr.ToLowerInvariant() switch
        {
            "replan" => CritiqueRecommendation.Replan,
            "clarify" => CritiqueRecommendation.Clarify,
            "stop" => CritiqueRecommendation.Stop,
            "retry" => CritiqueRecommendation.Retry,
            _ => CritiqueRecommendation.Continue
        };

        return new Critique(
            GoalSatisfied: goalSatisfied,
            Assessment: assessment,
            KnownGaps: knownGaps,
            Recommendation: recommendation
        );
    }
}

/// <summary>
/// Options for the LLM-based critic.
/// </summary>
public class CriticOptions
{
    public string SystemPrompt { get; set; } = """
        You are a critic that evaluates whether observations satisfy goals.
        Be objective and thorough in your assessment.
        Always respond with valid JSON in the specified format.
        """;

    public int MaxTokens { get; set; } = 300;
    public double Temperature { get; set; } = 0.0;

    public static CriticOptions Default => new();
}