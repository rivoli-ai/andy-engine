using System.Text;
using Andy.Engine.SweBench.Model;

namespace Andy.Engine.SweBench.Reporting;

/// <summary>Renders a human-readable run summary to a TextWriter (stdout by default).</summary>
public sealed class ConsoleReporter
{
    private readonly TextWriter _out;

    public ConsoleReporter(TextWriter? output = null) => _out = output ?? Console.Out;

    public void Write(SweRunReport report)
    {
        _out.WriteLine(Render(report));
    }

    /// <summary>Builds the summary string (also used in tests).</summary>
    public static string Render(SweRunReport report)
    {
        var m = report.Metadata;
        var sb = new StringBuilder();
        sb.AppendLine("==================== SWE-bench Run Summary ====================");
        if (m.Model is not null) sb.AppendLine($"  Model       : {m.Model}");
        if (m.Dataset is not null) sb.AppendLine($"  Dataset     : {m.Dataset}");
        if (m.Subset is not null) sb.AppendLine($"  Subset      : {m.Subset}");
        if (m.RunId is not null) sb.AppendLine($"  Run id      : {m.RunId}");
        if (m.Stage is not null) sb.AppendLine($"  Stage       : {m.Stage}");
        sb.AppendLine("  ----------------------------------------------------------");
        sb.AppendLine($"  Total       : {report.TotalInstances}");
        sb.AppendLine($"  Submitted   : {report.SubmittedInstances}");
        sb.AppendLine($"  Completed   : {report.CompletedInstances}");
        sb.AppendLine($"  Resolved    : {report.ResolvedInstances}");
        sb.AppendLine($"  Unresolved  : {report.UnresolvedInstances}");
        sb.AppendLine($"  Empty patch : {report.EmptyPatchInstances}");
        sb.AppendLine($"  Errors      : {report.ErrorInstances}");
        sb.AppendLine("  ----------------------------------------------------------");
        sb.AppendLine($"  Resolve rate: {report.ResolveRate * 100:0.0}%  ({report.ResolvedInstances}/{report.TotalInstances})");
        if (m.DurationSeconds is { } secs) sb.AppendLine($"  Duration    : {secs:0.0}s");
        if (m.TotalTokens is { } tokens) sb.AppendLine($"  Tokens      : {tokens:N0}");
        if (m.Aborted)
            sb.AppendLine($"  ABORTED     : {m.AbortReason}");
        sb.Append("===============================================================");
        return sb.ToString();
    }
}
