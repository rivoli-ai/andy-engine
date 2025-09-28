using System.Text.Json;
using System.Text.Json.Nodes;
using Andy.Engine.Contracts;
using Andy.Tools.Core.OutputLimiting;
using Microsoft.Extensions.Logging;

namespace Andy.Engine.Normalizer;

/// <summary>
/// Default implementation of observation normalizer.
/// </summary>
public class DefaultObservationNormalizer : IObservationNormalizer
{
    private readonly IToolOutputLimiter? _outputLimiter;
    private readonly ILogger<DefaultObservationNormalizer>? _logger;
    private readonly NormalizerOptions _options;

    public DefaultObservationNormalizer(
        IToolOutputLimiter? outputLimiter = null,
        NormalizerOptions? options = null,
        ILogger<DefaultObservationNormalizer>? logger = null)
    {
        _outputLimiter = outputLimiter;
        _options = options ?? NormalizerOptions.Default;
        _logger = logger;
    }

    public Observation Normalize(string toolName, JsonNode? raw, ToolResult result)
    {
        _logger?.LogDebug("Normalizing observation for tool {ToolName}", toolName);

        // Build summary
        var summary = BuildSummary(toolName, result);

        // Extract key facts
        var keyFacts = ExtractKeyFacts(raw, result);

        // Determine affordances (next possible actions)
        var affordances = DetermineAffordances(raw, result);

        // Apply output limiting if available
        if (_outputLimiter != null && keyFacts.Count > _options.MaxKeyFacts)
        {
            var limitedFacts = keyFacts
                .Take(_options.MaxKeyFacts)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            keyFacts = limitedFacts;
        }

        return new Observation(
            Summary: summary,
            KeyFacts: keyFacts,
            Affordances: affordances,
            Raw: result
        );
    }

    private string BuildSummary(string toolName, ToolResult result)
    {
        if (!result.Ok)
        {
            return $"Tool '{toolName}' failed: {result.ErrorCode} - {result.ErrorDetails}";
        }

        if (result.Data == null)
        {
            return $"Tool '{toolName}' completed with no data";
        }

        return $"Tool '{toolName}' executed successfully";
    }

    private Dictionary<string, string> ExtractKeyFacts(JsonNode? raw, ToolResult result)
    {
        var facts = new Dictionary<string, string>();

        // Add execution metadata
        facts["execution_time_ms"] = result.Latency.TotalMilliseconds.ToString("F2");
        facts["attempt"] = result.Attempt.ToString();

        if (!result.Ok)
        {
            facts["error_code"] = result.ErrorCode.ToString();
            if (!string.IsNullOrEmpty(result.ErrorDetails))
            {
                facts["error_details"] = TruncateString(result.ErrorDetails, 200);
            }
            return facts;
        }

        // Extract facts from successful result
        if (raw is JsonObject obj)
        {
            ExtractFactsFromObject(obj, facts, "", 0);
        }
        else if (raw is JsonArray arr)
        {
            facts["result_count"] = arr.Count.ToString();
            if (arr.Count > 0 && arr[0] is JsonObject firstItem)
            {
                ExtractFactsFromObject(firstItem, facts, "first_", 1);
            }
        }
        else if (raw != null)
        {
            facts["result"] = TruncateString(raw.ToString(), 200);
        }

        return facts;
    }

    private void ExtractFactsFromObject(JsonObject obj, Dictionary<string, string> facts, string prefix, int depth)
    {
        if (depth >= _options.MaxDepth)
            return;

        foreach (var prop in obj)
        {
            if (facts.Count >= _options.MaxKeyFacts)
                break;

            var key = prefix + prop.Key;
            var value = prop.Value;

            if (value == null)
                continue;

            switch (value)
            {
                case JsonValue val:
                    facts[key] = TruncateString(val.ToString(), 100);
                    break;
                case JsonArray arr:
                    facts[key + "_count"] = arr.Count.ToString();
                    break;
                case JsonObject subObj when depth < _options.MaxDepth - 1:
                    ExtractFactsFromObject(subObj, facts, key + ".", depth + 1);
                    break;
            }
        }
    }

    private List<string> DetermineAffordances(JsonNode? raw, ToolResult result)
    {
        var affordances = new List<string>();

        if (!result.Ok)
        {
            switch (result.ErrorCode)
            {
                case ToolErrorCode.Timeout:
                case ToolErrorCode.RetryableServer:
                case ToolErrorCode.RateLimited:
                    affordances.Add("retry_with_backoff");
                    break;
                case ToolErrorCode.InvalidInput:
                    affordances.Add("fix_parameters");
                    affordances.Add("ask_user_for_clarification");
                    break;
                case ToolErrorCode.Unauthorized:
                case ToolErrorCode.Forbidden:
                    affordances.Add("check_permissions");
                    affordances.Add("use_fallback_tool");
                    break;
            }
        }
        else if (raw is JsonObject obj)
        {
            // Check for pagination
            if (obj.ContainsKey("next_page") || obj.ContainsKey("nextToken"))
            {
                affordances.Add("fetch_next_page");
            }

            // Check for incomplete results
            if (obj.ContainsKey("has_more") && obj["has_more"]?.GetValue<bool>() == true)
            {
                affordances.Add("fetch_more_results");
            }

            // Check for results that suggest follow-up actions
            if (obj.ContainsKey("results") && obj["results"] is JsonArray { Count: > 0 })
            {
                affordances.Add("process_results");
                affordances.Add("filter_results");
            }
        }

        // Always available affordances
        affordances.Add("use_different_tool");
        affordances.Add("ask_user_for_guidance");

        return affordances;
    }

    private string TruncateString(string str, int maxLength)
    {
        if (str.Length <= maxLength)
            return str;

        return str.Substring(0, maxLength - 3) + "...";
    }
}

/// <summary>
/// Options for the observation normalizer.
/// </summary>
public class NormalizerOptions
{
    public int MaxKeyFacts { get; set; } = 10;
    public int MaxDepth { get; set; } = 2;
    public int MaxStringLength { get; set; } = 200;

    public static NormalizerOptions Default => new();
}