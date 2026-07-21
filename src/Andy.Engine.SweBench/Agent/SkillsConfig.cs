using System.Text;
using Andy.Skills;
using Andy.Skills.Tools;

namespace Andy.Engine.SweBench.Agent;

/// <summary>
/// Validates and loads the Agent Skills catalog for a run ONCE — at factory construction, before
/// any instance is cloned or run. A misconfigured <c>--skills-dir</c> (missing directory, or a
/// directory that yields zero usable skills) fails fast here rather than silently collapsing the
/// with-skills arm of a no-skills-vs-skills study into the baseline.
///
/// The single <see cref="ISkillCatalog"/> is shared by every instance (discovery is not repeated
/// per instance). Discovery diagnostics — skipped/unreadable/duplicate/malformed manifests and
/// lint warnings — are surfaced: fatal ones through the thrown message, non-fatal ones through the
/// warning sink (stderr by default), so a skill that is silently absent from the with-skills arm
/// is always visible.
/// </summary>
public sealed class SkillsConfig
{
    /// <summary>The disabled configuration: no <c>--skills-dir</c> was given.</summary>
    public static SkillsConfig Disabled { get; } =
        new(null, Array.Empty<Skill>(), Array.Empty<SkillDiagnostic>());

    private SkillsConfig(
        ISkillCatalog? catalog,
        IReadOnlyList<Skill> skills,
        IReadOnlyList<SkillDiagnostic> diagnostics)
    {
        Catalog = catalog;
        Skills = skills;
        Diagnostics = diagnostics;
    }

    /// <summary>The shared, pre-scanned catalog, or <c>null</c> when skills are disabled.</summary>
    public ISkillCatalog? Catalog { get; }

    /// <summary>Usable skills discovered under the root, in precedence order (empty when disabled).</summary>
    public IReadOnlyList<Skill> Skills { get; }

    /// <summary>Every diagnostic from the single discovery scan (empty when disabled).</summary>
    public IReadOnlyList<SkillDiagnostic> Diagnostics { get; }

    /// <summary>True when a valid, non-empty skills directory was configured.</summary>
    public bool Enabled => Catalog is not null;

    /// <summary>
    /// Validates <paramref name="skillsDir"/> and scans it once. Returns <see cref="Disabled"/> for a
    /// null/blank path. Throws <see cref="ArgumentException"/> with a user-facing message when the
    /// directory is missing or contains no usable skills.
    /// </summary>
    /// <param name="skillsDir">The configured <c>--skills-dir</c> value (null/blank = disabled).</param>
    /// <param name="warn">Sink for non-fatal diagnostics; defaults to <c>Console.Error</c>.</param>
    public static SkillsConfig Load(string? skillsDir, Action<string>? warn = null)
    {
        if (string.IsNullOrWhiteSpace(skillsDir))
            return Disabled;

        string full;
        try
        {
            full = Path.GetFullPath(skillsDir);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"--skills-dir: invalid path '{skillsDir}': {ex.Message}");
        }

        if (!Directory.Exists(full))
            throw new ArgumentException(
                $"--skills-dir '{skillsDir}' is not an existing directory. Point it at a directory of "
                + "Agent Skills (each <skill-name>/SKILL.md), or omit the flag for the no-skills arm.");

        var options = new SkillCatalogOptions();
        options.Roots.Add(full);
        var catalog = new SkillCatalog(options);

        // A single scan for the whole run; every instance reuses this catalog.
        var skills = catalog.GetSkillsAsync().GetAwaiter().GetResult();
        var diagnostics = catalog.GetDiagnosticsAsync().GetAwaiter().GetResult();

        if (skills.Count == 0)
            throw new ArgumentException(
                $"--skills-dir '{skillsDir}' contains no usable skills. Each skill needs a "
                + "<skill-name>/SKILL.md with valid frontmatter (name + description)."
                + FormatDiagnostics(diagnostics));

        // Non-fatal diagnostics still change a benchmark: a shadowed or malformed skill silently
        // missing from the with-skills arm alters results. Make them visible.
        var sink = warn ?? (m => Console.Error.WriteLine(m));
        foreach (var d in diagnostics)
            sink($"[skills] {d.Severity}: {d.Path}: {d.Message}");

        return new SkillsConfig(catalog, skills, diagnostics);
    }

    private static string FormatDiagnostics(IReadOnlyList<SkillDiagnostic> diagnostics)
    {
        if (diagnostics.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        sb.Append(" Diagnostics:");
        foreach (var d in diagnostics)
            sb.Append($"\n  - {d.Severity} {d.Path}: {d.Message}");
        return sb.ToString();
    }
}
