using Andy.Engine.Contracts;

namespace Andy.Engine.Planner;

/// <summary>
/// Base class for planner decisions.
/// </summary>
public abstract record PlannerDecision
{
    public abstract PlannerDecisionType Type { get; }
}

public enum PlannerDecisionType
{
    CallTool,
    AskUser,
    Stop,
    Replan
}

/// <summary>
/// Decision to call a tool.
/// </summary>
public sealed record CallToolDecision(ToolCall Call) : PlannerDecision
{
    public override PlannerDecisionType Type => PlannerDecisionType.CallTool;
}

/// <summary>
/// Decision to ask the user for clarification.
/// </summary>
public sealed record AskUserDecision(string Question, IReadOnlyList<string> MissingFields) : PlannerDecision
{
    public override PlannerDecisionType Type => PlannerDecisionType.AskUser;
}

/// <summary>
/// Decision to stop execution.
/// </summary>
public sealed record StopDecision(string Reason) : PlannerDecision
{
    public override PlannerDecisionType Type => PlannerDecisionType.Stop;
}

/// <summary>
/// Decision to replan with new subgoals.
/// </summary>
public sealed record ReplanDecision(IReadOnlyList<string> NewSubgoals) : PlannerDecision
{
    public override PlannerDecisionType Type => PlannerDecisionType.Replan;
}