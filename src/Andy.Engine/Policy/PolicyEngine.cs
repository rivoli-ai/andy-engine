using Andy.Engine.Contracts;
using Andy.Engine.Critic;
using Andy.Engine.Planner;
using Microsoft.Extensions.Logging;

namespace Andy.Engine.Policy;

/// <summary>
/// Policy engine that determines the next action based on decisions and observations.
/// </summary>
public class PolicyEngine
{
    private readonly ILogger<PolicyEngine>? _logger;
    private int _retryCount = 0;
    private string? _lastToolName;

    public PolicyEngine(ILogger<PolicyEngine>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Resolves a planner decision into an agent action based on policy.
    /// </summary>
    public AgentAction Resolve(
        PlannerDecision decision,
        Observation? lastObservation,
        ErrorHandlingPolicy policy,
        AgentState state)
    {
        _logger?.LogDebug("Resolving decision of type {DecisionType}", decision.GetType().Name);

        // Check budget exhaustion first
        if (state.Budget.Exhausted(state.TurnIndex, state.Goal.CreatedAt ?? DateTime.UtcNow))
        {
            _logger?.LogWarning("Budget exhausted at turn {Turn}", state.TurnIndex);
            return new StopAction("Budget exhausted");
        }

        // Handle decision based on type
        return decision switch
        {
            CallToolDecision toolDecision => ResolveToolCall(toolDecision, lastObservation, policy),
            AskUserDecision askDecision => new AskUserAction(askDecision.Question, askDecision.MissingFields),
            StopDecision stopDecision => new StopAction(stopDecision.Reason),
            ReplanDecision replanDecision => new ReplanAction(replanDecision.NewSubgoals),
            _ => new StopAction("Unknown decision type")
        };
    }

    private AgentAction ResolveToolCall(
        CallToolDecision decision,
        Observation? lastObservation,
        ErrorHandlingPolicy policy)
    {
        // Check if this is a retry scenario
        if (lastObservation?.Raw != null && !lastObservation.Raw.Ok)
        {
            var errorCode = lastObservation.Raw.ErrorCode;
            var toolName = decision.Call.ToolName;

            // Track retries for the same tool
            if (toolName == _lastToolName)
            {
                _retryCount++;
            }
            else
            {
                _retryCount = 0;
                _lastToolName = toolName;
            }

            // Check if we should retry - consider both internal count and attempt from result
            var currentAttempt = Math.Max(_retryCount, lastObservation.Raw.Attempt - 1);
            if (IsRetryable(errorCode) && currentAttempt < policy.MaxRetries)
            {
                _logger?.LogInformation(
                    "Retrying tool {Tool} (attempt {Attempt}/{Max}) after {Error}",
                    toolName, currentAttempt + 1, policy.MaxRetries, errorCode);

                return new CallToolAction(decision.Call, currentAttempt + 1);
            }

            // Check for fallback
            if (policy.UseFallbacks && HasFallback(toolName))
            {
                var fallbackTool = GetFallbackTool(toolName);
                _logger?.LogInformation("Using fallback tool {Fallback} for {Original}", fallbackTool, toolName);

                return new CallToolAction(
                    new ToolCall(fallbackTool, decision.Call.Args),
                    0 // Reset retry count for fallback
                );
            }

            // Ask user for missing fields
            if (policy.AskUserWhenMissingFields && errorCode == ToolErrorCode.InvalidInput)
            {
                return new AskUserAction(
                    $"Tool '{toolName}' failed with invalid input. Please provide correct parameters.",
                    ExtractMissingFields(lastObservation.Raw.ErrorDetails)
                );
            }

            // Otherwise, stop with error summary
            var finalAttemptCount = Math.Max(_retryCount, lastObservation.Raw.Attempt - 1);
            return new StopAction($"Max retries exceeded after {finalAttemptCount + 1} attempts: {errorCode}");
        }

        // Normal tool call (not a retry)
        _retryCount = 0;
        _lastToolName = decision.Call.ToolName;
        return new CallToolAction(decision.Call, 0);
    }

    private bool IsRetryable(ToolErrorCode errorCode)
    {
        return errorCode switch
        {
            ToolErrorCode.Timeout => true,
            ToolErrorCode.RetryableServer => true,
            ToolErrorCode.RateLimited => true,
            _ => false
        };
    }

    private bool HasFallback(string toolName)
    {
        // Define fallback mappings
        var fallbacks = new Dictionary<string, string>
        {
            ["search_web"] = "search_local",
            ["gpt4"] = "gpt3.5",
            ["expensive_api"] = "cheaper_api"
        };

        return fallbacks.ContainsKey(toolName.ToLowerInvariant());
    }

    private string GetFallbackTool(string toolName)
    {
        var fallbacks = new Dictionary<string, string>
        {
            ["search_web"] = "search_local",
            ["gpt4"] = "gpt3.5",
            ["expensive_api"] = "cheaper_api"
        };

        return fallbacks.GetValueOrDefault(toolName.ToLowerInvariant(), toolName);
    }

    private IReadOnlyList<string> ExtractMissingFields(string? errorDetails)
    {
        if (string.IsNullOrEmpty(errorDetails))
            return Array.Empty<string>();

        // Simple extraction - in production, parse the error message more intelligently
        var fields = new List<string>();

        if (errorDetails.Contains("required", StringComparison.OrdinalIgnoreCase))
        {
            // Try to extract field names from error message
            var words = errorDetails.Split(' ', ',', '.', ':', ';')
                .Where(w => w.Length > 2 && !w.Any(char.IsDigit))
                .Distinct()
                .Take(5);

            fields.AddRange(words);
        }

        return fields;
    }
}

/// <summary>
/// Base class for agent actions.
/// </summary>
public abstract record AgentAction
{
    public abstract AgentActionType Type { get; }
}

/// <summary>
/// Types of agent actions.
/// </summary>
public enum AgentActionType
{
    CallTool,
    AskUser,
    Stop,
    Replan
}

/// <summary>
/// Action to call a tool.
/// </summary>
public sealed record CallToolAction(ToolCall Call, int RetryAttempt) : AgentAction
{
    public override AgentActionType Type => AgentActionType.CallTool;
}

/// <summary>
/// Action to ask the user for information.
/// </summary>
public sealed record AskUserAction(string Question, IReadOnlyList<string> MissingFields) : AgentAction
{
    public override AgentActionType Type => AgentActionType.AskUser;
}

/// <summary>
/// Action to stop execution.
/// </summary>
public sealed record StopAction(string Reason) : AgentAction
{
    public override AgentActionType Type => AgentActionType.Stop;
}

/// <summary>
/// Action to replan with new subgoals.
/// </summary>
public sealed record ReplanAction(IReadOnlyList<string> NewSubgoals) : AgentAction
{
    public override AgentActionType Type => AgentActionType.Replan;
}