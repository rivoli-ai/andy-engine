using System.Text.Json;
using Andy.Engine;
using Xunit;

namespace Andy.Engine.Tests;

/// <summary>
/// Tests for SimpleAgent's tolerant tool-argument JSON parsing. Weaker models often wrap
/// JSON in markdown fences, add prose, or leave trailing commas; the repair candidates must
/// recover a parseable object so the tool call is not silently dropped.
/// </summary>
public class SimpleAgentJsonRepairTests
{
    /// <summary>Mimics SimpleAgent: returns the first candidate that parses to a JSON object.</summary>
    private static JsonElement? FirstObject(string raw)
    {
        foreach (var candidate in SimpleAgent.JsonRepairCandidates(raw))
        {
            try
            {
                var doc = JsonDocument.Parse(candidate);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    return doc.RootElement.Clone();
            }
            catch (JsonException) { }
        }
        return null;
    }

    [Fact]
    public void Clean_Json_Parses()
    {
        var obj = FirstObject("""{"file_path": "/a/b.py"}""");
        Assert.NotNull(obj);
        Assert.Equal("/a/b.py", obj!.Value.GetProperty("file_path").GetString());
    }

    [Fact]
    public void Markdown_Fenced_Json_Is_Recovered()
    {
        var raw = "```json\n{\"path\": \"x.py\", \"start\": 1}\n```";
        var obj = FirstObject(raw);
        Assert.NotNull(obj);
        Assert.Equal("x.py", obj!.Value.GetProperty("path").GetString());
    }

    [Fact]
    public void Prose_Wrapped_Json_Is_Recovered()
    {
        var raw = "Sure! Here are the arguments: {\"query\": \"def foo\"} — let me know.";
        var obj = FirstObject(raw);
        Assert.NotNull(obj);
        Assert.Equal("def foo", obj!.Value.GetProperty("query").GetString());
    }

    [Fact]
    public void Trailing_Comma_Is_Recovered()
    {
        var raw = "{\"a\": 1, \"b\": 2,}";
        var obj = FirstObject(raw);
        Assert.NotNull(obj);
        Assert.Equal(2, obj!.Value.GetProperty("b").GetDouble());
    }

    [Fact]
    public void Garbage_Yields_No_Object()
    {
        Assert.Null(FirstObject("this is not json at all"));
    }
}
