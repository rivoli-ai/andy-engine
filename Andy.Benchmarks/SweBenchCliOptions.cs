using Andy.Engine.SweBench.Dataset;
using Andy.Engine.SweBench.Orchestration;

namespace Andy.Benchmarks;

/// <summary>
/// Parses command-line arguments into a <see cref="RunContext"/>. Minimal, dependency-free
/// "--flag value" / "--flag=value" parsing.
/// </summary>
public static class SweBenchCliOptions
{
    public static string Usage =>
        """
        Andy SWE-bench runner

        Usage:
          dotnet run --project Andy.Benchmarks -- --dataset <path> [options]

        Selection:
          --dataset <path>            Path to SWE-bench dataset (.jsonl or .json)   [required]
          --max-instances <n>         Cap the subset to the first n instances
          --instance-ids <a,b,c>      Comma-separated instance ids to run
          --subset-file <path>        File with one instance id per line (# comments ok)

        Stage:
          --stage none|agent|grade|all   Default: all
          --predictions-path <path|gold> Grade an existing predictions.jsonl, or "gold"
          --gold-survey               Gold mode: grade ALL instances (no fail-fast on first non-resolve)

        Agent (agent stage):
          --agent andy|external       Which agent drives each instance. Default: andy (in-process SimpleAgent)
          --agent-cmd "<template>"    For --agent external: command to run per instance. Whole-token
                                      placeholders {model} {workspace} {prompt} {prompt_file} are
                                      substituted (no shell). Cwd = workspace. If no {prompt}/{prompt_file}
                                      token, the problem statement is piped to stdin.
                                      Example: opencode run --model {model} {prompt}
          --system-prompt-file <path> (andy) Replace the built-in base prompt with this file's text
                                      ({workspace} token substituted). Validated: exists, <=256KB, text.
          --rules-dir <path>          (andy) Dir of per-repo rules <repo>.md (e.g. django__django.md)
                                      appended to the system prompt for matching instances (<=64KB each).

        Model / provider (agent stage):
          --model <id>                Default: openai/gpt-oss-20b:free (free; Kimi: moonshotai/kimi-k2.6:free)
          --provider-base <url>       Default: https://openrouter.ai/api/v1
          --max-turns <n>             Default: 50
          --max-output-tokens <n>     Per-response output-token cap (default: 16384)
          --max-context-tokens <n>    Compressed-context budget (default: 1000000; lower for small-context models)
          --max-tool-result-chars <n> Per-tool-result char cap before truncate-with-guidance (default: 100000)
          --agent-timeout-seconds <n> Per-instance wall-clock cap; 0 disables (default: 1800)
          --max-parallel <n>          Instances to run concurrently in the agent stage (default: 1).
                                      >1 disables the windowed fail-fast (hard-error abort only).
                                      Keep modest (e.g. 5) to respect rate limits / local Docker.

        Rate-limit:
          --max-retries <n>           Default: 6
          --max-delay-seconds <n>     Default: 60

        Fail-fast:
          --fail-fast-window <n>      Default: 5
          --fail-fast-threshold <f>   Default: 0.6
          --max-consecutive-errors <n>  Default: 3

        Grading:
          --docker-timeout-seconds <n>  Default: 1800

        Output:
          --work-dir <path>           Default: ./swebench-runs
          --run-id <name>             Default: timestamp
          --reporter <c,j,h>          console,json,html  (default: console,json)
                    --render-report <report.json|run-dir>  Render to HTML and exit; a run-dir consolidates all
                                      batches (no re-grade). Add --dataset <path> to enrich with task
                                      summaries + which target tests failed; --out <path> for output.
          --resume                    Skip instances already in predictions.jsonl
          --keep-workspaces           Keep per-instance workspaces for debugging
          -h | --help                 Show this help
        """;

    public static bool WantsHelp(string[] args) =>
        args.Any(a => a is "-h" or "--help");

    public static RunContext Parse(string[] args, string defaultRunId)
    {
        var map = ToMap(args, out var flags);

        var dataset = Require(map, "dataset");

        IReadOnlyList<string>? instanceIds = null;
        if (map.TryGetValue("instance-ids", out var idsRaw) && !string.IsNullOrWhiteSpace(idsRaw))
            instanceIds = idsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var subset = new SubsetSelector
        {
            InstanceIds = instanceIds,
            SubsetFilePath = Get(map, "subset-file"),
            MaxInstances = GetInt(map, "max-instances"),
        };

        var reporters = map.TryGetValue("reporter", out var rep) && !string.IsNullOrWhiteSpace(rep)
            ? rep.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : new[] { "console", "json" };

        return new RunContext
        {
            DatasetPath = dataset,
            Subset = subset,
            Stage = ParseStage(Get(map, "stage")),
            PredictionsPath = Get(map, "predictions-path"),
            Agent = Get(map, "agent") ?? "andy",
            AgentCommand = Get(map, "agent-cmd"),
            SystemPromptFile = Get(map, "system-prompt-file"),
            RulesDir = Get(map, "rules-dir"),
            Model = Get(map, "model") ?? "openai/gpt-oss-20b:free",
            ProviderBaseUrl = Get(map, "provider-base") ?? "https://openrouter.ai/api/v1",
            MaxTurns = GetInt(map, "max-turns") ?? 50,
            MaxOutputTokens = GetInt(map, "max-output-tokens") ?? 16_384,
            MaxContextTokens = GetInt(map, "max-context-tokens") ?? 1_000_000,
            MaxToolResultChars = GetInt(map, "max-tool-result-chars") ?? 100_000,
            MaxRetries = GetInt(map, "max-retries") ?? 6,
            MaxDelaySeconds = GetInt(map, "max-delay-seconds") ?? 60,
            FailFastWindow = GetInt(map, "fail-fast-window") ?? 5,
            FailFastThreshold = GetDouble(map, "fail-fast-threshold") ?? 0.6,
            MaxConsecutiveErrors = GetInt(map, "max-consecutive-errors") ?? 3,
            DockerTimeoutSeconds = GetInt(map, "docker-timeout-seconds") ?? 1800,
            AgentTimeoutSeconds = GetInt(map, "agent-timeout-seconds") ?? 1800,
            MaxParallel = GetInt(map, "max-parallel") ?? 1,
            WorkDir = Get(map, "work-dir") ?? Path.Combine(Directory.GetCurrentDirectory(), "swebench-runs"),
            RunId = Get(map, "run-id") ?? defaultRunId,
            Reporters = reporters,
            Resume = flags.Contains("resume"),
            GoldSurvey = flags.Contains("gold-survey"),
            KeepWorkspaces = flags.Contains("keep-workspaces"),
        };
    }

    private static RunStage ParseStage(string? value) => (value ?? "all").ToLowerInvariant() switch
    {
        "none" => RunStage.None,
        "agent" => RunStage.Agent,
        "grade" => RunStage.Grade,
        "all" => RunStage.All,
        _ => throw new ArgumentException($"Unknown --stage '{value}'. Expected none|agent|grade|all."),
    };

    private static Dictionary<string, string> ToMap(string[] args, out HashSet<string> flags)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        flags = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
                continue;

            var key = arg[2..];
            var eq = key.IndexOf('=');
            if (eq >= 0)
            {
                map[key[..eq]] = key[(eq + 1)..];
                continue;
            }

            // Boolean flag if next token is missing or another option.
            if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                flags.Add(key);
            }
            else
            {
                map[key] = args[++i];
            }
        }

        return map;
    }

    private static string? Get(Dictionary<string, string> map, string key) =>
        map.TryGetValue(key, out var v) ? v : null;

    private static string Require(Dictionary<string, string> map, string key) =>
        Get(map, key) ?? throw new ArgumentException($"Missing required option --{key}.");

    private static int? GetInt(Dictionary<string, string> map, string key) =>
        Get(map, key) is { } s ? int.Parse(s) : null;

    private static double? GetDouble(Dictionary<string, string> map, string key) =>
        Get(map, key) is { } s ? double.Parse(s, System.Globalization.CultureInfo.InvariantCulture) : null;
}
