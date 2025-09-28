using System.Text.Json;
using Andy.Engine.Contracts;
using Microsoft.Extensions.Logging;

namespace Andy.Engine.Interactive;

/// <summary>
/// Manages conversation state and history for interactive agent sessions
/// </summary>
public class ConversationManager
{
    private readonly List<ConversationTurn> _history = new();
    private readonly ILogger<ConversationManager>? _logger;
    private readonly ConversationOptions _options;

    public ConversationManager(ConversationOptions? options = null, ILogger<ConversationManager>? logger = null)
    {
        _options = options ?? ConversationOptions.Default;
        _logger = logger;
    }

    /// <summary>
    /// Unique identifier for this conversation session
    /// </summary>
    public string SessionId { get; private set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// When this conversation started
    /// </summary>
    public DateTime StartedAt { get; private set; } = DateTime.UtcNow;

    /// <summary>
    /// Current conversation history
    /// </summary>
    public IReadOnlyList<ConversationTurn> History => _history.AsReadOnly();

    /// <summary>
    /// Add a user message to the conversation
    /// </summary>
    public void AddUserMessage(string message)
    {
        var turn = new ConversationTurn
        {
            Id = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow,
            UserMessage = message,
            TurnNumber = _history.Count + 1
        };

        _history.Add(turn);
        _logger?.LogDebug("Added user message to conversation. Turn {TurnNumber}: {Message}",
            turn.TurnNumber, message);

        // Trim history if needed
        TrimHistoryIfNeeded();
    }

    /// <summary>
    /// Complete the current turn with agent result
    /// </summary>
    public void CompleteCurrentTurn(AgentResult result, string? summary = null)
    {
        if (_history.Count == 0)
        {
            _logger?.LogWarning("Attempted to complete turn but no user message exists");
            throw new InvalidOperationException("No active conversation turn to complete");
        }

        var currentTurn = _history.Last();
        if (currentTurn.AgentResult != null)
        {
            _logger?.LogWarning("Attempted to complete turn {TurnNumber} but it was already completed",
                currentTurn.TurnNumber);
            return;
        }

        currentTurn.AgentResult = result;
        currentTurn.Summary = summary ?? GenerateTurnSummary(currentTurn);
        currentTurn.CompletedAt = DateTime.UtcNow;

        _logger?.LogDebug("Completed conversation turn {TurnNumber}. Success: {Success}, Duration: {Duration}s",
            currentTurn.TurnNumber, result.Success, currentTurn.Duration?.TotalSeconds);
    }

    /// <summary>
    /// Get a summary of the conversation suitable for context
    /// </summary>
    public string GetConversationSummary()
    {
        if (_history.Count == 0)
            return "No conversation history.";

        var recentTurns = _history.TakeLast(_options.SummaryTurnCount).ToList();
        var summary = $"Conversation with {recentTurns.Count} recent turn(s):\n\n";

        foreach (var turn in recentTurns)
        {
            summary += $"Turn {turn.TurnNumber}:\n";
            summary += $"User: {turn.UserMessage}\n";

            if (turn.AgentResult != null)
            {
                var status = turn.AgentResult.Success ? "✓" : "✗";
                summary += $"Agent: {status} {turn.Summary ?? "Completed"}\n";
            }
            else
            {
                summary += "Agent: [In progress]\n";
            }
            summary += "\n";
        }

        return summary.Trim();
    }

    /// <summary>
    /// Clear conversation history
    /// </summary>
    public void Clear()
    {
        var turnCount = _history.Count;
        _history.Clear();
        SessionId = Guid.NewGuid().ToString();
        StartedAt = DateTime.UtcNow;
        _logger?.LogInformation("Cleared conversation history. Removed {TurnCount} turns", turnCount);
    }

    /// <summary>
    /// Get conversation statistics
    /// </summary>
    public ConversationStats GetStats()
    {
        var completedTurns = _history.Where(t => t.AgentResult != null).ToList();
        var successfulTurns = completedTurns.Where(t => t.AgentResult!.Success).ToList();

        return new ConversationStats
        {
            SessionId = SessionId,
            StartedAt = StartedAt,
            TotalTurns = _history.Count,
            CompletedTurns = completedTurns.Count,
            SuccessfulTurns = successfulTurns.Count,
            AverageTurnDuration = completedTurns.Any()
                ? TimeSpan.FromMilliseconds(completedTurns.Average(t => t.Duration?.TotalMilliseconds ?? 0))
                : TimeSpan.Zero,
            TotalDuration = DateTime.UtcNow - StartedAt
        };
    }

    private void TrimHistoryIfNeeded()
    {
        if (_history.Count > _options.MaxHistoryTurns)
        {
            var toRemove = _history.Count - _options.MaxHistoryTurns;
            var removed = _history.Take(toRemove).ToList();
            _history.RemoveRange(0, toRemove);

            _logger?.LogDebug("Trimmed conversation history. Removed {Count} oldest turns", toRemove);
        }
    }

    private string GenerateTurnSummary(ConversationTurn turn)
    {
        if (turn.AgentResult == null)
            return "In progress";

        if (!turn.AgentResult.Success)
            return $"Failed: {turn.AgentResult.StopReason}";

        var observation = turn.AgentResult.FinalState.LastObservation;
        if (observation != null && !string.IsNullOrEmpty(observation.Summary))
        {
            // Truncate long summaries
            var summary = observation.Summary;
            if (summary.Length > 100)
                summary = summary.Substring(0, 97) + "...";
            return summary;
        }

        return $"Completed in {turn.AgentResult.TotalTurns} turn(s)";
    }
}

/// <summary>
/// Represents a single turn in the conversation
/// </summary>
public class ConversationTurn
{
    public required string Id { get; init; }
    public required int TurnNumber { get; init; }
    public required DateTime Timestamp { get; init; }
    public required string UserMessage { get; init; }
    public AgentResult? AgentResult { get; set; }
    public string? Summary { get; set; }
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Duration of this turn (null if not completed)
    /// </summary>
    public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - Timestamp : null;
}

/// <summary>
/// Statistics about a conversation session
/// </summary>
public class ConversationStats
{
    public required string SessionId { get; init; }
    public required DateTime StartedAt { get; init; }
    public required int TotalTurns { get; init; }
    public required int CompletedTurns { get; init; }
    public required int SuccessfulTurns { get; init; }
    public required TimeSpan AverageTurnDuration { get; init; }
    public required TimeSpan TotalDuration { get; init; }

    public double SuccessRate => CompletedTurns > 0 ? (double)SuccessfulTurns / CompletedTurns : 0.0;
}

/// <summary>
/// Configuration options for conversation management
/// </summary>
public class ConversationOptions
{
    /// <summary>
    /// Maximum number of turns to keep in history
    /// </summary>
    public int MaxHistoryTurns { get; set; } = 100;

    /// <summary>
    /// Number of recent turns to include in conversation summaries
    /// </summary>
    public int SummaryTurnCount { get; set; } = 5;

    /// <summary>
    /// Default conversation options
    /// </summary>
    public static ConversationOptions Default => new();
}