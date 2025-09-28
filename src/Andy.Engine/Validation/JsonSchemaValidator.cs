using System.Text.Json.Nodes;
using Json.Schema;

namespace Andy.Engine.Validation;

/// <summary>
/// JSON schema validator implementation using Json.Schema.
/// </summary>
public class JsonSchemaValidator : IJsonValidator
{
    public (bool ok, string? error) Validate(JsonNode instance, JsonNode schemaNode)
    {
        try
        {
            var schema = JsonSchema.FromText(schemaNode.ToJsonString());
            var result = schema.Evaluate(instance);

            if (result.IsValid)
            {
                return (true, null);
            }

            var errors = GetErrorMessages(result);
            return (false, string.Join("; ", errors));
        }
        catch (Exception ex)
        {
            return (false, $"Schema validation error: {ex.Message}");
        }
    }

    public (bool ok, string? error, JsonNode normalized) ValidateAndNormalize(JsonNode instance, JsonNode schemaNode)
    {
        var (ok, error) = Validate(instance, schemaNode);

        if (!ok)
        {
            return (false, error, instance);
        }

        // Apply normalization
        var normalized = NormalizeInstance(instance, schemaNode);
        return (true, null, normalized);
    }

    private JsonNode NormalizeInstance(JsonNode instance, JsonNode schemaNode)
    {
        // Basic normalization - can be extended
        var normalized = JsonNode.Parse(instance.ToJsonString())!;

        // Add default values if specified in schema
        if (schemaNode is JsonObject schemaObj &&
            schemaObj["properties"] is JsonObject properties &&
            normalized is JsonObject normalizedObj)
        {
            foreach (var prop in properties)
            {
                if (!normalizedObj.ContainsKey(prop.Key) &&
                    prop.Value is JsonObject propSchema &&
                    propSchema.ContainsKey("default"))
                {
                    normalizedObj[prop.Key] = propSchema["default"]!.DeepClone();
                }
            }
        }

        return normalized;
    }

    private IEnumerable<string> GetErrorMessages(EvaluationResults result)
    {
        var messages = new List<string>();

        if (!result.IsValid)
        {
            if (result.Errors != null && result.Errors.Any())
            {
                foreach (var (key, value) in result.Errors)
                {
                    messages.Add($"{key}: {value}");
                }
            }
            else
            {
                messages.Add("Validation failed");
            }
        }

        // Recursively get errors from nested results
        if (result.Details != null)
        {
            foreach (var detail in result.Details)
            {
                messages.AddRange(GetErrorMessages(detail));
            }
        }

        return messages;
    }
}