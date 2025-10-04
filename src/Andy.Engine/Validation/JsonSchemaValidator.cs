using System.Text.Json;
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
            // Handle null inputs explicitly
            if (instance == null)
            {
                return (false, "Data cannot be null");
            }

            if (schemaNode == null)
            {
                return (false, "Schema is null");
            }

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
        // First try to coerce types
        var coerced = CoerceTypes(instance, schemaNode);

        // Then validate the coerced data
        var (ok, error) = Validate(coerced!, schemaNode);

        if (!ok)
        {
            return (false, error, instance);
        }

        // Apply normalization
        var normalized = NormalizeInstance(coerced!, schemaNode);
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

    private JsonNode? CoerceTypes(JsonNode? instance, JsonNode? schemaNode)
    {
        if (instance == null || schemaNode == null)
            return instance;

        // Clone the instance to avoid modifying the original
        var coerced = JsonNode.Parse(instance.ToJsonString())!;

        if (coerced is JsonObject coercedObj &&
            schemaNode is JsonObject schemaObj &&
            schemaObj["properties"] is JsonObject properties)
        {
            foreach (var prop in properties)
            {
                if (coercedObj.ContainsKey(prop.Key) &&
                    prop.Value is JsonObject propSchema)
                {
                    var value = coercedObj[prop.Key];
                    if (value != null)
                    {
                        var coercedValue = CoerceValue(value, propSchema);
                        if (coercedValue != null)
                        {
                            coercedObj[prop.Key] = coercedValue;
                        }
                    }
                }
            }
        }

        return coerced;
    }

    private JsonNode? CoerceValue(JsonNode value, JsonObject propSchema)
    {
        if (propSchema["type"]?.GetValue<string>() is not string targetType)
            return value;

        // Only coerce from string values
        if (value.GetValueKind() != JsonValueKind.String)
            return value;

        var stringValue = value.GetValue<string>();

        return targetType switch
        {
            "integer" when int.TryParse(stringValue, out var intVal) => JsonValue.Create(intVal),
            "number" when double.TryParse(stringValue, out var doubleVal) => JsonValue.Create(doubleVal),
            "boolean" when bool.TryParse(stringValue, out var boolVal) => JsonValue.Create(boolVal),
            _ => value
        };
    }

    private IEnumerable<string> GetErrorMessages(EvaluationResults result)
    {
        var messages = new List<string>();

        if (!result.IsValid)
        {
            // Check for error annotations first
            if (result.Errors != null && result.Errors.Count > 0)
            {
                foreach (var (key, value) in result.Errors)
                {
                    messages.Add($"{key}: {value}");
                }
            }

            // Check nested results for more specific errors
            if (result.Details != null)
            {
                foreach (var detail in result.Details)
                {
                    messages.AddRange(GetErrorMessages(detail));
                }
            }

            // If no specific errors found, provide a generic message
            if (messages.Count == 0)
            {
                messages.Add("Validation failed");
            }
        }

        return messages;
    }
}