using System.Net;
using System.Text;
using Andy.Engine.SweBench.Model;

namespace Andy.Engine.SweBench.Reporting;

/// <summary>
/// Renders a <see cref="SweRunReport"/> as a self-contained HTML page (inline CSS, no assets).
/// Used by the "html" reporter and by the render-only CLI mode, so an existing report.json can
/// be turned into a presentable report without re-running the benchmark.
/// </summary>
public static class HtmlReporter
{
    /// <summary>Writes report.html into <paramref name="outputPath"/> (a file) and returns it.</summary>
    public static string Write(SweRunReport report, string outputPath)
    {
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(outputPath, Render(report));
        return outputPath;
    }

    public static string Render(SweRunReport report)
    {
        var m = report.Metadata;
        var rate = report.ResolveRate * 100;
        var sb = new StringBuilder();

        sb.Append("""
            <!doctype html><html lang="en"><head><meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>SWE-bench Run Report</title>
            <style>
            :root{--bg:#0d1117;--card:#161b22;--line:#30363d;--fg:#e6edf3;--mut:#8b949e;
              --ok:#3fb950;--bad:#f85149;--warn:#d29922;--accent:#58a6ff}
            *{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--fg);
              font:15px/1.5 -apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,Helvetica,Arial,sans-serif}
            .wrap{max-width:1000px;margin:0 auto;padding:32px 20px 64px}
            h1{font-size:22px;margin:0 0 4px}.sub{color:var(--mut);font-size:13px;margin-bottom:24px}
            .hero{display:flex;align-items:center;gap:24px;background:var(--card);border:1px solid var(--line);
              border-radius:12px;padding:24px;margin-bottom:20px}
            .rate{font-size:54px;font-weight:700;line-height:1}.rate small{font-size:18px;color:var(--mut);font-weight:400}
            .bar{flex:1;height:14px;background:#21262d;border-radius:7px;overflow:hidden}
            .bar>span{display:block;height:100%;background:var(--ok)}
            .cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(130px,1fr));gap:12px;margin-bottom:24px}
            .c{background:var(--card);border:1px solid var(--line);border-radius:10px;padding:14px 16px}
            .c .n{font-size:26px;font-weight:600}.c .l{color:var(--mut);font-size:12px;text-transform:uppercase;letter-spacing:.04em}
            .ok{color:var(--ok)}.bad{color:var(--bad)}.warn{color:var(--warn)}.acc{color:var(--accent)}
            table{width:100%;border-collapse:collapse;background:var(--card);border:1px solid var(--line);
              border-radius:10px;overflow:hidden;margin:8px 0 24px}
            th,td{text-align:left;padding:9px 14px;border-bottom:1px solid var(--line);font-size:13px}
            th{color:var(--mut);font-weight:600;text-transform:uppercase;font-size:11px;letter-spacing:.04em}
            tr:last-child td{border-bottom:none}
            h2{font-size:15px;margin:28px 0 6px}
            .pill{display:inline-block;padding:1px 8px;border-radius:10px;font-size:11px;font-weight:600}
            .pill.ok{background:rgba(63,185,80,.15)}.pill.bad{background:rgba(248,81,73,.15)}
            .pill.warn{background:rgba(210,153,34,.15)}
            .banner{background:rgba(210,153,34,.12);border:1px solid var(--warn);border-radius:10px;
              padding:12px 16px;margin-bottom:20px;color:var(--warn)}
            details{margin:6px 0}summary{cursor:pointer;color:var(--mut)}
            code{font-family:ui-monospace,SFMono-Regular,Menlo,monospace;font-size:12px}
            </style></head><body><div class="wrap">
            """);

        sb.Append($"<h1>SWE-bench Run Report</h1>");
        sb.Append("<div class=\"sub\">");
        AppendKv(sb, "model", m.Model);
        AppendKv(sb, "dataset", m.Dataset);
        AppendKv(sb, "subset", m.Subset);
        AppendKv(sb, "run", m.RunId);
        AppendKv(sb, "stage", m.Stage);
        if (m.CompletedAt is { } ca) AppendKv(sb, "completed", ca.ToString("u"));
        if (m.DurationSeconds is { } d) AppendKv(sb, "duration", $"{d:0}s");
        sb.Append("</div>");

        if (m.Aborted)
            sb.Append($"<div class=\"banner\">⚠ Run aborted: {Esc(m.AbortReason ?? "fail-fast")}</div>");

        // Hero: resolve rate + bar
        sb.Append("<div class=\"hero\">");
        sb.Append($"<div class=\"rate ok\">{rate:0.0}<small>%</small></div>");
        sb.Append($"<div style=\"flex:1\"><div style=\"margin-bottom:8px\">"
                + $"<b>{report.ResolvedInstances}</b> of <b>{report.TotalInstances}</b> resolved</div>"
                + $"<div class=\"bar\"><span style=\"width:{rate:0.##}%\"></span></div></div>");
        sb.Append("</div>");

        // Stat cards
        sb.Append("<div class=\"cards\">");
        Card(sb, "Total", report.TotalInstances, "");
        Card(sb, "Resolved", report.ResolvedInstances, "ok");
        Card(sb, "Unresolved", report.UnresolvedInstances, "bad");
        Card(sb, "Empty patch", report.EmptyPatchInstances, "warn");
        Card(sb, "Errors", report.ErrorInstances, report.ErrorInstances > 0 ? "bad" : "");
        Card(sb, "Completed", report.CompletedInstances, "");
        sb.Append("</div>");

        // Per-repo breakdown
        var repos = RepoBreakdown(report);
        if (repos.Count > 1)
        {
            sb.Append("<h2>By repository</h2><table><tr><th>Repository</th><th>Resolved</th><th>Rate</th></tr>");
            foreach (var r in repos.OrderByDescending(x => x.Total))
            {
                var pr = r.Total == 0 ? 0 : 100.0 * r.Resolved / r.Total;
                sb.Append($"<tr><td><code>{Esc(r.Repo)}</code></td><td>{r.Resolved}/{r.Total}</td>"
                        + $"<td class=\"{(pr >= 50 ? "ok" : "bad")}\">{pr:0.0}%</td></tr>");
            }
            sb.Append("</table>");
        }

        // Instance lists
        InstanceList(sb, "Resolved", report.ResolvedIds, "ok");
        InstanceList(sb, "Unresolved", report.UnresolvedIds, "bad");
        InstanceList(sb, "Empty patch", report.EmptyPatchIds, "warn");
        if (report.ErrorIds.Count > 0)
            InstanceList(sb, "Errors", report.ErrorIds, "bad");

        sb.Append($"<div class=\"sub\" style=\"margin-top:32px\">schema v{report.SchemaVersion} · generated from report.json</div>");
        sb.Append("</div></body></html>");
        return sb.ToString();
    }

    private static void Card(StringBuilder sb, string label, int n, string cls) =>
        sb.Append($"<div class=\"c\"><div class=\"n {cls}\">{n}</div><div class=\"l\">{Esc(label)}</div></div>");

    private static void InstanceList(StringBuilder sb, string title, IReadOnlyList<string> ids, string cls)
    {
        if (ids.Count == 0) return;
        sb.Append($"<details><summary><span class=\"pill {cls}\">{ids.Count}</span> {Esc(title)}</summary>");
        sb.Append("<table>");
        foreach (var id in ids)
            sb.Append($"<tr><td><code>{Esc(id)}</code></td></tr>");
        sb.Append("</table></details>");
    }

    private static void AppendKv(StringBuilder sb, string k, string? v)
    {
        if (string.IsNullOrEmpty(v)) return;
        sb.Append($"<span class=\"acc\">{k}</span> {Esc(v)} &nbsp;·&nbsp; ");
    }

    private static List<(string Repo, int Resolved, int Total)> RepoBreakdown(SweRunReport report)
    {
        var resolved = new HashSet<string>(report.ResolvedIds, StringComparer.Ordinal);
        // "submitted" is the set attempted; fall back to the union of all id buckets if empty.
        var all = report.SubmittedIds.Count > 0
            ? report.SubmittedIds
            : report.ResolvedIds.Concat(report.UnresolvedIds).Concat(report.EmptyPatchIds).Concat(report.ErrorIds).ToList();
        var map = new Dictionary<string, (int Resolved, int Total)>(StringComparer.Ordinal);
        foreach (var id in all)
        {
            var repo = RepoOf(id);
            map.TryGetValue(repo, out var cur);
            map[repo] = (cur.Resolved + (resolved.Contains(id) ? 1 : 0), cur.Total + 1);
        }
        return map.Select(kv => (kv.Key, kv.Value.Resolved, kv.Value.Total)).ToList();
    }

    /// <summary>"django__django-12209" -> "django/django"; "astropy__astropy-7166" -> "astropy/astropy".</summary>
    private static string RepoOf(string instanceId)
    {
        var us = instanceId.IndexOf("__", StringComparison.Ordinal);
        if (us < 0) return "(unknown)";
        var owner = instanceId[..us];
        var rest = instanceId[(us + 2)..];
        var dash = rest.LastIndexOf('-');
        var name = dash > 0 ? rest[..dash] : rest;
        return $"{owner}/{name}";
    }

    private static string Esc(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);
}
