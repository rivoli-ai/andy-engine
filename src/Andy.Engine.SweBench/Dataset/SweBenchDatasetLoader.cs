using System.Text.Json;
using Andy.Engine.SweBench.Model;

namespace Andy.Engine.SweBench.Dataset;

/// <summary>
/// Loads SWE-bench (Verified) instances from a local JSONL (one object per line)
/// or JSON (array) file.
///
/// The dataset's FAIL_TO_PASS / PASS_TO_PASS fields are JSON-ENCODED STRINGS in the
/// official distribution (e.g. "[\"test_a\", \"test_b\"]"). Some exports store them as
/// real arrays. The loader accepts both shapes.
/// </summary>
public sealed class SweBenchDatasetLoader
{
    /// <summary>Load all instances from a .jsonl or .json file.</summary>
    public IReadOnlyList<SweBenchInstance> LoadFromFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"SWE-bench dataset file not found: {path}", path);

        var text = File.ReadAllText(path);
        return LoadFromText(text, path);
    }

    /// <summary>Load instances from raw text. <paramref name="sourceName"/> is for error messages.</summary>
    public IReadOnlyList<SweBenchInstance> LoadFromText(string text, string sourceName = "<text>")
    {
        var trimmed = text.TrimStart();
        var results = new List<SweBenchInstance>();

        if (trimmed.StartsWith('['))
        {
            // JSON array form.
            using var doc = JsonDocument.Parse(text);
            int index = 0;
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                results.Add(ParseInstance(element, sourceName, index));
                index++;
            }
        }
        else
        {
            // JSONL form: one JSON object per non-blank line.
            var lines = text.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0)
                    continue;

                using var doc = JsonDocument.Parse(line);
                results.Add(ParseInstance(doc.RootElement, sourceName, i + 1));
            }
        }

        if (results.Count == 0)
            throw new InvalidDataException($"No instances parsed from dataset: {sourceName}");

        return results;
    }

    private static SweBenchInstance ParseInstance(JsonElement el, string sourceName, int position)
    {
        try
        {
            return new SweBenchInstance
            {
                InstanceId = GetRequiredString(el, "instance_id"),
                Repo = GetRequiredString(el, "repo"),
                BaseCommit = GetRequiredString(el, "base_commit"),
                ProblemStatement = GetString(el, "problem_statement") ?? string.Empty,
                Version = GetString(el, "version") ?? string.Empty,
                EnvironmentSetupCommit = GetString(el, "environment_setup_commit"),
                FailToPass = ReadStringList(el, "FAIL_TO_PASS"),
                PassToPass = ReadStringList(el, "PASS_TO_PASS"),
                GoldPatch = GetString(el, "patch") ?? string.Empty,
                TestPatch = GetString(el, "test_patch") ?? string.Empty,
            };
        }
        catch (Exception ex) when (ex is not InvalidDataException)
        {
            throw new InvalidDataException(
                $"Failed to parse instance at position {position} in {sourceName}: {ex.Message}", ex);
        }
    }

    private static string GetRequiredString(JsonElement el, string name)
    {
        var value = GetString(el, name);
        if (string.IsNullOrEmpty(value))
            throw new InvalidDataException($"Missing required field '{name}'.");
        return value;
    }

    private static string? GetString(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var prop))
            return null;
        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString(),
            JsonValueKind.Null => null,
            _ => prop.GetRawText(),
        };
    }

    /// <summary>
    /// Reads a list of strings that may be encoded either as a real JSON array or as a
    /// JSON-encoded string (the official SWE-bench shape for FAIL_TO_PASS/PASS_TO_PASS).
    /// </summary>
    internal static IReadOnlyList<string> ReadStringList(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var prop) || prop.ValueKind == JsonValueKind.Null)
            return Array.Empty<string>();

        if (prop.ValueKind == JsonValueKind.Array)
            return ToStringArray(prop);

        if (prop.ValueKind == JsonValueKind.String)
        {
            var raw = prop.GetString();
            if (string.IsNullOrWhiteSpace(raw))
                return Array.Empty<string>();

            using var inner = JsonDocument.Parse(raw);
            if (inner.RootElement.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException($"Field '{name}' is a string but does not decode to a JSON array.");
            return ToStringArray(inner.RootElement);
        }

        throw new InvalidDataException($"Field '{name}' has unexpected JSON kind {prop.ValueKind}.");
    }

    private static IReadOnlyList<string> ToStringArray(JsonElement array)
    {
        var list = new List<string>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var s = item.GetString();
                if (!string.IsNullOrEmpty(s))
                    list.Add(s);
            }
        }
        return list;
    }
}
