using System.Text.Json;
using Andy.Engine.SweBench.Model;

namespace Andy.Engine.SweBench.Reporting;

/// <summary>Writes the aggregate <see cref="SweRunReport"/> to report.json.</summary>
public sealed class JsonReporter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
    };

    /// <summary>Writes report.json into <paramref name="runDir"/> and returns the path.</summary>
    public string Write(SweRunReport report, string runDir)
    {
        Directory.CreateDirectory(runDir);
        var path = Path.Combine(runDir, "report.json");
        File.WriteAllText(path, JsonSerializer.Serialize(report, Options));
        return path;
    }
}
