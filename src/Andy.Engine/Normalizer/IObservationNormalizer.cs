using System.Text.Json.Nodes;
using Andy.Engine.Contracts;

namespace Andy.Engine.Normalizer;

/// <summary>
/// Interface for normalizing tool outputs into observations.
/// </summary>
public interface IObservationNormalizer
{
    /// <summary>
    /// Normalizes a raw tool result into a structured observation.
    /// </summary>
    /// <param name="toolName">Name of the tool that produced the result.</param>
    /// <param name="raw">Raw output data from the tool.</param>
    /// <param name="result">The tool execution result.</param>
    /// <returns>A normalized observation.</returns>
    Observation Normalize(string toolName, JsonNode? raw, ToolResult result);
}