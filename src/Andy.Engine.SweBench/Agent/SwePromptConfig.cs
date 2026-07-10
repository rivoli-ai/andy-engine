using System.Text;

namespace Andy.Engine.SweBench.Agent;

/// <summary>
/// Resolves the system prompt for the andy agent from optional external sources, validated and
/// loaded ONCE at construction (so a misconfigured path/file fails the run fast, before any
/// instance work):
///
///  - <c>--system-prompt-file</c>: replaces the built-in base prompt. If the file contains the
///    token <c>{workspace}</c> it is substituted with the instance workspace path; otherwise a
///    "Work only within &lt;dir&gt;" line is appended so the agent still knows its sandbox.
///  - <c>--rules-dir</c>: per-repo rules. For repo <c>django/django</c> the harness appends the
///    contents of <c>&lt;dir&gt;/django__django.md</c> (or <c>django.md</c>) to the prompt, if present.
///    Per-repo and out-of-band, so nothing lands in the captured git diff.
///
/// With neither flag set, <see cref="Build"/> returns the built-in <see cref="SweSystemPrompt"/>
/// unchanged (zero behavior change by default).
/// </summary>
public sealed class SwePromptConfig
{
    // Guard rails: a system prompt is text and modest in size. These catch mistakes (wrong path,
    // a binary, a runaway file) rather than impose a hard product limit.
    internal const long MaxPromptFileBytes = 256 * 1024;   // 256 KB
    internal const long MaxRulesFileBytes = 64 * 1024;     // 64 KB per repo

    private readonly string? _baseTemplate;                // null => use the built-in prompt
    private readonly IReadOnlyDictionary<string, string> _repoRules; // bare-repo key -> rules text

    private SwePromptConfig(string? baseTemplate, IReadOnlyDictionary<string, string> repoRules)
    {
        _baseTemplate = baseTemplate;
        _repoRules = repoRules;
    }

    /// <summary>Validates and loads the configured sources. Throws <see cref="ArgumentException"/>
    /// with a user-facing message on any invalid path/file.</summary>
    public static SwePromptConfig Load(string? systemPromptFile, string? rulesDir)
    {
        var baseTemplate = systemPromptFile is { Length: > 0 }
            ? ReadTextFile(systemPromptFile, "--system-prompt-file", MaxPromptFileBytes, requireNonEmpty: true)
            : null;

        var rules = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (rulesDir is { Length: > 0 })
        {
            var full = Path.GetFullPath(rulesDir);
            if (!Directory.Exists(full))
                throw new ArgumentException($"--rules-dir '{rulesDir}' is not an existing directory.");

            foreach (var file in Directory.EnumerateFiles(full, "*.md", SearchOption.TopDirectoryOnly))
            {
                var key = Path.GetFileNameWithoutExtension(file); // "django__django" or "django"
                var text = ReadTextFile(file, $"--rules-dir entry '{Path.GetFileName(file)}'",
                    MaxRulesFileBytes, requireNonEmpty: false);
                if (text.Length > 0)
                    rules[key] = text;
            }
        }

        return new SwePromptConfig(baseTemplate, rules);
    }

    /// <summary>The system prompt for one instance: base (built-in or override) + per-repo rules.</summary>
    public string Build(string workspaceDir, string repo)
    {
        var basePrompt = _baseTemplate is null
            ? SweSystemPrompt.Build(workspaceDir)
            : ApplyWorkspace(_baseTemplate, workspaceDir);

        var rules = LookupRules(repo);
        if (rules is null)
            return basePrompt;

        return basePrompt
            + "\n\n--- Repository-specific rules (" + repo + ") ---\n"
            + rules.Trim()
            + "\n--- End repository-specific rules ---";
    }

    /// <summary>
    /// The system-prompt header for the skills block. Points the agent at the <c>skill</c> tool
    /// (registered by <c>AddAndySkills</c>) rather than at the raw SKILL.md path, since skill files
    /// live outside the workspace-scoped file permissions.
    /// </summary>
    internal const string SkillsHeader =
        "## Skills\n" +
        "The following skills are available. When one is relevant to the task, call the `skill` " +
        "tool with its name to load its full instructions, then follow them.";

    /// <summary>
    /// Appends the lazy-disclosure skills block (one line per skill) to <paramref name="prompt"/>.
    /// Returns the prompt unchanged when there are no skills.
    /// </summary>
    public static string AppendSkillsBlock(string prompt, IReadOnlyList<Andy.Skills.Skill> skills)
    {
        var block = Andy.Skills.SkillPromptComposer.Compose(skills, SkillsHeader);
        return block.Length == 0 ? prompt : prompt.TrimEnd() + "\n\n" + block;
    }

    private string? LookupRules(string repo)
    {
        // Accept either the bare-repo key "django__django" or the short key "django".
        var underscored = repo.Replace("/", "__");                                  // django/django -> django__django
        var shortName = repo.Contains('/') ? repo[(repo.IndexOf('/') + 1)..] : repo; // django/django -> django
        foreach (var key in new[] { underscored, shortName, repo })
            if (_repoRules.TryGetValue(key, out var text))
                return text;
        return null;
    }

    private static string ApplyWorkspace(string template, string workspaceDir) =>
        template.Contains("{workspace}", StringComparison.Ordinal)
            ? template.Replace("{workspace}", workspaceDir, StringComparison.Ordinal)
            : template.TrimEnd() + $"\n\nWork only within {workspaceDir}.";

    private static string ReadTextFile(string path, string label, long maxBytes, bool requireNonEmpty)
    {
        string full;
        try
        {
            full = Path.GetFullPath(path);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"{label}: invalid path '{path}': {ex.Message}");
        }

        if (Directory.Exists(full))
            throw new ArgumentException($"{label} '{path}' is a directory, not a file.");
        if (!File.Exists(full))
            throw new ArgumentException($"{label} '{path}' does not exist.");

        long size;
        try
        {
            size = new FileInfo(full).Length;
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"{label} '{path}' could not be read: {ex.Message}");
        }

        if (size > maxBytes)
            throw new ArgumentException(
                $"{label} '{path}' is {size} bytes; the limit is {maxBytes} bytes. " +
                "A system prompt should be modest text — check you pointed at the right file.");

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(full);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"{label} '{path}' could not be read: {ex.Message}");
        }

        // Reject binary: a NUL byte means this isn't a text prompt (e.g. wrong file).
        if (Array.IndexOf(bytes, (byte)0) >= 0)
            throw new ArgumentException($"{label} '{path}' looks binary (contains NUL bytes); expected UTF-8 text.");

        var text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false)
            .GetString(bytes)
            .Replace("﻿", string.Empty); // strip BOM

        if (requireNonEmpty && string.IsNullOrWhiteSpace(text))
            throw new ArgumentException($"{label} '{path}' is empty.");

        return text;
    }
}
