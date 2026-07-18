using Andy.Skills.Tools;
using Andy.Tools.Core;
using Andy.Tools.Library;
using Andy.Tools.Library.Common;
using Microsoft.Extensions.Logging;

namespace Andy.Engine.SweBench.Agent;

/// <summary>
/// The <c>skill_file</c> tool: least-privilege, read-only access to the resources bundled with an
/// available skill (its <c>scripts/</c>, <c>references/</c>, templates, assets …). Agent Skills can
/// live outside the SWE-bench workspace, which is the only path the general file tools may touch;
/// this tool is the sanctioned bridge for a skill to load its OWN package files without widening
/// those workspace-scoped permissions.
///
/// Guarantees:
/// <list type="bullet">
///   <item>Access is confined to the named skill's own directory — resolved symlinks and <c>..</c>
///     segments that escape it are denied, as are absolute/rooted paths.</item>
///   <item>Read-only: there is no write path. The workspace stays the only writable area.</item>
///   <item>No path outside a skill directory is ever reachable, so no arbitrary host path leaks.</item>
/// </list>
/// </summary>
public sealed class SkillResourceTool : ToolBase
{
    // A skill resource is bundled text/data, not a runaway file. This catches mistakes.
    internal const long MaxResourceBytes = 1024 * 1024; // 1 MB

    private readonly ISkillCatalog _catalog;
    private readonly ILogger<SkillResourceTool>? _logger;

    public SkillResourceTool(ISkillCatalog catalog, ILogger<SkillResourceTool>? logger = null)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _logger = logger;
    }

    /// <inheritdoc />
    public override ToolMetadata Metadata => new()
    {
        Id = "skill_file",
        Name = "skill_file",
        Description =
            "Read a resource file bundled with an available skill — for example a script or reference "
            + "under the skill's own directory that its instructions point you to. Access is read-only "
            + "and confined to the named skill's directory.",
        Category = ToolCategory.General,
        Parameters =
        [
            new ToolParameter
            {
                Name = "skill",
                Type = "string",
                Description = "The skill's name, exactly as listed in the Skills section of the system prompt.",
                Required = true,
            },
            new ToolParameter
            {
                Name = "path",
                Type = "string",
                Description =
                    "The resource's path RELATIVE to the skill directory, e.g. 'references/api.md' or "
                    + "'scripts/run.py'. Must stay within the skill directory.",
                Required = true,
            },
        ],
    };

    /// <inheritdoc />
    protected override async Task<ToolResult> ExecuteInternalAsync(
        Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var name = GetParameter<string>(parameters, "skill", string.Empty);
        var relative = GetParameter<string>(parameters, "path", string.Empty);

        if (string.IsNullOrWhiteSpace(name))
            return ToolResults.Failure("The 'skill' parameter is required.");
        if (string.IsNullOrWhiteSpace(relative))
            return ToolResults.Failure("The 'path' parameter is required.");

        context.CancellationToken.ThrowIfCancellationRequested();

        var skill = await _catalog.FindAsync(name, context.CancellationToken).ConfigureAwait(false);
        if (skill is null)
        {
            var available = await _catalog.GetSkillsAsync(context.CancellationToken).ConfigureAwait(false);
            var names = available.Count == 0 ? "(none)" : string.Join(", ", available.Select(s => s.Name));
            return ToolResults.Failure($"No skill named '{name}'. Available skills: {names}.");
        }

        // Reject absolute/rooted paths outright — a resource is always relative to its skill.
        if (Path.IsPathRooted(relative))
            return ToolResults.Failure("'path' must be relative to the skill directory, not an absolute path.");

        var root = ResolveReal(skill.DirectoryPath);
        var candidate = ResolveReal(Path.Combine(root, relative));

        // Containment check AFTER resolving symlinks and collapsing '..' — this is the boundary that
        // keeps a skill from reading another skill, the parent root, or an arbitrary host path.
        if (!IsWithin(root, candidate))
        {
            _logger?.LogWarning(
                "skill_file: '{Path}' escapes skill '{Skill}' directory; denied.", relative, skill.Name);
            return ToolResults.Failure(
                $"'{relative}' resolves outside skill '{skill.Name}' directory; access denied.");
        }

        if (Directory.Exists(candidate))
            return ToolResults.Failure($"'{relative}' is a directory, not a file.");
        if (!File.Exists(candidate))
            return ToolResults.Failure($"No resource '{relative}' in skill '{skill.Name}'.");

        long size;
        try
        {
            size = new FileInfo(candidate).Length;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ToolResults.Failure($"Could not read '{relative}' in skill '{skill.Name}': {ex.Message}");
        }

        if (size > MaxResourceBytes)
            return ToolResults.Failure(
                $"'{relative}' is {size} bytes; the limit is {MaxResourceBytes} bytes.");

        try
        {
            var text = await File.ReadAllTextAsync(candidate, context.CancellationToken).ConfigureAwait(false);
            return ToolResults.TextSuccess(text, message: $"Loaded '{relative}' from skill '{skill.Name}'.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ToolResults.Failure($"Could not read '{relative}' in skill '{skill.Name}': {ex.Message}");
        }
    }

    /// <summary>
    /// The absolute path with symlinks resolved to their final target and <c>..</c> collapsed.
    /// Resolving both the root and the candidate the same way makes containment robust to a resource
    /// that is a symlink pointing outside the skill, and to platform root symlinks (e.g. macOS
    /// <c>/var</c> → <c>/private/var</c>).
    /// </summary>
    private static string ResolveReal(string path)
    {
        var full = Path.GetFullPath(path);
        try
        {
            FileSystemInfo info = Directory.Exists(full) ? new DirectoryInfo(full) : new FileInfo(full);
            var target = info.ResolveLinkTarget(returnFinalTarget: true);
            if (target is not null)
                return Path.GetFullPath(target.FullName);
        }
        catch (IOException) { /* missing / not a link: fall through */ }
        catch (UnauthorizedAccessException) { }
        return full;
    }

    /// <summary>True when <paramref name="path"/> is <paramref name="root"/> itself or lives under it.</summary>
    private static bool IsWithin(string root, string path)
    {
        var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        var full = Path.GetFullPath(path);
        return string.Equals(full, rootFull, StringComparison.Ordinal)
            || full.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }
}
