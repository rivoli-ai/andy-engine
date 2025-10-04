namespace Andy.Benchmarks.Framework;

/// <summary>
/// Defines a benchmark scenario for testing Andy.Engine's capabilities
/// </summary>
public class BenchmarkScenario
{
    /// <summary>
    /// Unique identifier for the scenario
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Category this scenario belongs to (e.g., "bug-fixes", "feature-additions")
    /// </summary>
    public required string Category { get; init; }

    /// <summary>
    /// Human-readable description of what this scenario tests
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Tags for filtering scenarios (e.g., "single-tool", "multi-turn")
    /// </summary>
    public List<string> Tags { get; init; } = new();

    /// <summary>
    /// Workspace configuration (where the test code lives)
    /// </summary>
    public required WorkspaceConfig Workspace { get; init; }

    /// <summary>
    /// Context to inject into the engine (prompts, MCP data, etc.)
    /// </summary>
    public required ContextInjection Context { get; init; }

    /// <summary>
    /// Expected tool invocations (optional - for validation)
    /// </summary>
    public List<ExpectedToolInvocation> ExpectedTools { get; init; } = new();

    /// <summary>
    /// Validation criteria for this scenario
    /// </summary>
    public required ValidationConfig Validation { get; init; }

    /// <summary>
    /// Maximum time allowed for this scenario to complete
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(2);
}

/// <summary>
/// Configuration for the workspace where the benchmark runs
/// </summary>
public class WorkspaceConfig
{
    /// <summary>
    /// Type of workspace: "git-clone", "directory-copy", "in-memory"
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Source path or URL for the workspace
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Git branch to checkout (if Type is "git-clone")
    /// </summary>
    public string? Branch { get; init; }
}

/// <summary>
/// Context data to inject into the engine
/// </summary>
public class ContextInjection
{
    /// <summary>
    /// User prompts (single or multi-turn conversation)
    /// </summary>
    public List<string> Prompts { get; init; } = new();

    /// <summary>
    /// Simulated MCP server responses
    /// </summary>
    public Dictionary<string, object> McpServerData { get; init; } = new();

    /// <summary>
    /// Simulated API tool responses
    /// </summary>
    public Dictionary<string, object> ApiToolData { get; init; } = new();

    /// <summary>
    /// Additional files to inject into context (path -> content)
    /// </summary>
    public Dictionary<string, string> InjectedFiles { get; init; } = new();
}

/// <summary>
/// Expected tool invocation for validation
/// </summary>
public class ExpectedToolInvocation
{
    /// <summary>
    /// Type of tool (e.g., "ReadFile", "WriteFile", "EditFile")
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Expected parameters (key-value pairs)
    /// </summary>
    public Dictionary<string, object> Parameters { get; init; } = new();

    /// <summary>
    /// Pattern to match against file paths (supports wildcards)
    /// </summary>
    public string? PathPattern { get; init; }

    /// <summary>
    /// Minimum number of times this tool should be invoked
    /// </summary>
    public int MinInvocations { get; init; } = 1;

    /// <summary>
    /// Maximum number of times this tool should be invoked
    /// </summary>
    public int? MaxInvocations { get; init; }
}

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
}

public class CompilationValidation
{
    /// <summary>
    /// Whether compilation must succeed
    /// </summary>
    public bool MustSucceed { get; init; } = true;

    /// <summary>
    /// Maximum number of warnings allowed
    /// </summary>
    public int? MaxWarnings { get; init; }
}

public class TestValidation
{
    /// <summary>
    /// Whether all tests must pass
    /// </summary>
    public bool MustPass { get; init; } = true;

    /// <summary>
    /// Minimum code coverage percentage required
    /// </summary>
    public double? MinCoverage { get; init; }

    /// <summary>
    /// Specific test that must pass (optional)
    /// </summary>
    public string? SpecificTest { get; init; }
}

public class BehavioralValidation
{
    /// <summary>
    /// Type of behavioral validation: "unit-tests", "console-app", "web-app"
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Command to run the application (for console-app/web-app)
    /// </summary>
    public string? RunCommand { get; init; }

    /// <summary>
    /// Test cases with inputs and expected outputs
    /// </summary>
    public List<BehavioralTestCase> TestCases { get; init; } = new();
}

public class BehavioralTestCase
{
    /// <summary>
    /// Standard input to provide to the application
    /// </summary>
    public string? Stdin { get; init; }

    /// <summary>
    /// Expected patterns in stdout
    /// </summary>
    public List<string> ExpectedStdoutContains { get; init; } = new();

    /// <summary>
    /// Expected exit code
    /// </summary>
    public int ExpectedExitCode { get; init; } = 0;
}

public class DiffValidation
{
    /// <summary>
    /// Maximum number of files that should be changed
    /// </summary>
    public int? MaxFilesChanged { get; init; }

    /// <summary>
    /// Expected files to be changed
    /// </summary>
    public List<string> ExpectedFiles { get; init; } = new();

    /// <summary>
    /// Files that should NOT be changed
    /// </summary>
    public List<string> UnexpectedFiles { get; init; } = new();
}

public class CodeQualityValidation
{
    /// <summary>
    /// Whether to run code formatting validation
    /// </summary>
    public bool RunLinting { get; init; } = true;

    /// <summary>
    /// Maximum cyclomatic complexity allowed
    /// </summary>
    public int? MaxComplexity { get; init; }
}
