using System.Text.Json;
using Andy.Engine.SweBench.Model;

namespace Andy.Engine.SweBench.Orchestration;

/// <summary>
/// Reads/writes predictions.jsonl (one <see cref="SwePrediction"/> per line). Used as a
/// resumable checkpoint by the agent stage and as the input to the grade stage.
/// </summary>
public sealed class PredictionCheckpoint
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _path;

    public PredictionCheckpoint(string path) => _path = path;

    public string Path => _path;

    public bool Exists => File.Exists(_path);

    /// <summary>Reads all predictions, keyed by instance id (last write wins).</summary>
    public IReadOnlyDictionary<string, SwePrediction> Read()
    {
        var map = new Dictionary<string, SwePrediction>(StringComparer.Ordinal);
        if (!File.Exists(_path))
            return map;

        foreach (var raw in File.ReadAllLines(_path))
        {
            var line = raw.Trim();
            if (line.Length == 0)
                continue;
            var pred = JsonSerializer.Deserialize<SwePrediction>(line, Options);
            if (pred is not null && !string.IsNullOrEmpty(pred.InstanceId))
                map[pred.InstanceId] = pred;
        }
        return map;
    }

    /// <summary>Appends a prediction line, creating the file/dir if needed.</summary>
    public void Append(SwePrediction prediction)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_path)!);
        File.AppendAllText(_path, JsonSerializer.Serialize(prediction, Options) + "\n");
    }
}
