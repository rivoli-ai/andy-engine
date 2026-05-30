namespace Andy.Engine.SweBench.Orchestration;

/// <summary>Which stage(s) of the pipeline to run.</summary>
public enum RunStage
{
    /// <summary>Selection + reporting only; no agent run, no grading (dry run).</summary>
    None,

    /// <summary>Run the agent to produce predictions only.</summary>
    Agent,

    /// <summary>Grade existing predictions only.</summary>
    Grade,

    /// <summary>Agent stage followed by grade stage.</summary>
    All,
}
