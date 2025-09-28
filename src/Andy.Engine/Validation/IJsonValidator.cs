using System.Text.Json.Nodes;

namespace Andy.Engine.Validation;

/// <summary>
/// Interface for JSON schema validation.
/// </summary>
public interface IJsonValidator
{
    /// <summary>
    /// Validates a JSON instance against a schema.
    /// </summary>
    (bool ok, string? error) Validate(JsonNode instance, JsonNode schema);

    /// <summary>
    /// Validates and normalizes a JSON instance against a schema.
    /// </summary>
    (bool ok, string? error, JsonNode normalized) ValidateAndNormalize(JsonNode instance, JsonNode schema);
}