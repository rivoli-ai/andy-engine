namespace Andy.Benchmarks.Framework;

/// <summary>
/// Validation configuration for a scenario
/// </summary>
public class ValidationConfig
{
    /// <summary>
    /// Compilation validation settings
    /// </summary>
    public CompilationValidation? Compilation { get; init; }

    /// <summary>
    /// Test execution validation settings
    /// </summary>
    public TestValidation? Tests { get; init; }

    /// <summary>
    /// Behavioral validation settings
    /// </summary>
    public BehavioralValidation? Behavioral { get; init; }

    /// <summary>
    /// Code diff validation settings
    /// </summary>
    public DiffValidation? Diff { get; init; }

    /// <summary>
    /// Code quality validation settings
    /// </summary>
    public CodeQualityValidation? CodeQuality { get; init; }

    /// <summary>
    /// Conversation flow validation (for chat scenarios)
    /// </summary>
    public ConversationFlowValidation? ConversationFlow { get; init; }

    /// <summary>
    /// Strings that must appear in the response
    /// </summary>
    public List<string> ResponseMustContain { get; init; } = new();

    /// <summary>
    /// At least one of these strings must appear in the response (OR logic)
    /// </summary>
    public List<string> ResponseMustContainAny { get; init; } = new();

    /// <summary>
    /// Strings that must NOT appear in the response
    /// </summary>
    public List<string> ResponseMustNotContain { get; init; } = new();

    /// <summary>
    /// Minimum response length (in characters)
    /// </summary>
    public int? MinResponseLength { get; init; }

    /// <summary>
    /// Agent must not ask user for input (should provide answer directly)
    /// </summary>
    public bool MustNotAskUser { get; init; }
}