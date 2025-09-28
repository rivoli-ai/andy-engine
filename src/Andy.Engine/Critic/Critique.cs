namespace Andy.Engine.Critic;

/// <summary>
/// Represents the result of a critic's assessment.
/// </summary>
public sealed record Critique(
    bool GoalSatisfied,
    string Assessment,
    IReadOnlyList<string> KnownGaps,
    CritiqueRecommendation Recommendation
);

/// <summary>
/// Recommendation from the critic about next steps.
/// </summary>
public enum CritiqueRecommendation
{
    /// <summary>
    /// Continue with the current plan.
    /// </summary>
    Continue,

    /// <summary>
    /// Replan with new subgoals.
    /// </summary>
    Replan,

    /// <summary>
    /// Ask for user clarification.
    /// </summary>
    Clarify,

    /// <summary>
    /// Stop execution as goal is achieved.
    /// </summary>
    Stop,

    /// <summary>
    /// Retry the last action with modifications.
    /// </summary>
    Retry
}