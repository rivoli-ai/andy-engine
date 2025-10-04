using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Andy.Engine.Contracts;
using Andy.Engine.Validation;
using Andy.Tools.Core;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using EngineToolResult = Andy.Engine.Contracts.ToolResult;
using EngineRetryPolicy = Andy.Engine.Contracts.RetryPolicy;

namespace Andy.Engine.Executor;

/// <summary>
/// Tool adapter that handles execution, validation, retries, and error mapping.
/// </summary>
public sealed class ToolAdapter : IExecutor
{
    private readonly IToolRegistry _toolRegistry;
    private readonly IToolExecutor _toolExecutor;
    private readonly IJsonValidator _validator;
    private readonly ILogger<ToolAdapter>? _logger;
    private readonly Dictionary<string, ToolSpec> _toolSpecs;

    public ToolAdapter(
        IToolRegistry toolRegistry,
        IToolExecutor toolExecutor,
        IJsonValidator validator,
        ILogger<ToolAdapter>? logger = null)
    {
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _logger = logger;
        _toolSpecs = new Dictionary<string, ToolSpec>();
    }

    public async Task<EngineToolResult> ExecuteAsync(ToolCall call, CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Executing tool {ToolName}", call.ToolName);

        // Get tool spec
        if (!_toolSpecs.TryGetValue(call.ToolName, out var spec))
        {
            // Try to get from registry
            var tool = _toolRegistry.GetTool(call.ToolName);
            if (tool == null)
            {
                return new EngineToolResult(
                    Ok: false,
                    Data: null,
                    ErrorCode: ToolErrorCode.NotFound,
                    ErrorDetails: $"Unknown tool: {call.ToolName}"
                );
            }

            // Create a basic spec from tool registration metadata
            spec = CreateSpecFromRegistration(tool);
            _toolSpecs[call.ToolName] = spec;
        }

        // Validate input
        _logger?.LogDebug("Validating args for tool {ToolName}: {Args}", call.ToolName, call.Args?.ToJsonString());
        _logger?.LogDebug("Using schema: {Schema}", spec.InputSchema?.ToJsonString());

        var (inputOk, inputError) = _validator.Validate(call.Args!, spec.InputSchema!);
        if (!inputOk)
        {
            _logger?.LogError("Tool {ToolName} validation failed: {Error}", call.ToolName, inputError);
            return new EngineToolResult(
                Ok: false,
                Data: null,
                ErrorCode: ToolErrorCode.InvalidInput,
                ErrorDetails: inputError,
                SchemaValidated: false
            );
        }

        // Build retry policy
        var retryPolicy = BuildRetryPolicy(spec.RetryPolicy);

        // Execute with retry
        var sw = Stopwatch.StartNew();
        var attempt = 0;

        try
        {
            var result = await retryPolicy.ExecuteAsync(async (ct) =>
            {
                attempt++;
                _logger?.LogDebug("Tool {ToolName} execution attempt {Attempt}", call.ToolName, attempt);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(spec.Timeout);

                // Convert JsonNode args to Dictionary
                var parameters = ConvertJsonToParameters(call.Args!);
                _logger?.LogDebug("Converted parameters for {ToolName}: {Parameters}", call.ToolName, JsonSerializer.Serialize(parameters));

                // Execute tool
                var executionResult = await _toolExecutor.ExecuteAsync(
                    call.ToolName,
                    parameters,
                    new ToolExecutionContext
                    {
                        CancellationToken = cts.Token
                    }
                );

                if (!executionResult.IsSuccessful)
                {
                    throw new ToolExecutionException(executionResult.Error ?? "Tool execution failed");
                }

                return executionResult;
            }, cancellationToken);

            sw.Stop();

            // Convert result to JsonNode
            var resultData = result.Data != null
                ? JsonNode.Parse(JsonSerializer.Serialize(result.Data))
                : null;

            // Validate output
            var (outputOk, outputError, normalized) = _validator.ValidateAndNormalize(resultData!, spec.OutputSchema);

            return outputOk
                ? new EngineToolResult(
                    Ok: true,
                    Data: normalized,
                    ErrorCode: ToolErrorCode.None,
                    ErrorDetails: null,
                    SchemaValidated: true,
                    Attempt: attempt,
                    Latency: sw.Elapsed
                )
                : new EngineToolResult(
                    Ok: false,
                    Data: resultData,
                    ErrorCode: ToolErrorCode.OutputSchemaMismatch,
                    ErrorDetails: outputError,
                    SchemaValidated: false,
                    Attempt: attempt,
                    Latency: sw.Elapsed
                );
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return new EngineToolResult(
                Ok: false,
                Data: null,
                ErrorCode: ToolErrorCode.Timeout,
                ErrorDetails: $"Tool timeout after {spec.Timeout.TotalSeconds}s",
                Attempt: attempt,
                Latency: sw.Elapsed
            );
        }
        catch (ToolExecutionException ex) when (IsRetryable(ex))
        {
            sw.Stop();
            return new EngineToolResult(
                Ok: false,
                Data: null,
                ErrorCode: ex.Message.Contains("rate", StringComparison.OrdinalIgnoreCase)
                    ? ToolErrorCode.RateLimited
                    : ToolErrorCode.RetryableServer,
                ErrorDetails: ex.Message,
                Attempt: attempt,
                Latency: sw.Elapsed
            );
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger?.LogError(ex, "Tool {ToolName} crashed", call.ToolName);
            return new EngineToolResult(
                Ok: false,
                Data: null,
                ErrorCode: ToolErrorCode.ToolBug,
                ErrorDetails: ex.ToString(),
                Attempt: attempt,
                Latency: sw.Elapsed
            );
        }
    }

    private ToolSpec CreateSpecFromRegistration(ToolRegistration registration)
    {
        // Create basic schemas from tool metadata
        var inputSchema = CreateSchemaFromParameters(registration.Metadata.Parameters);
        var outputSchema = JsonNode.Parse("""{"type": "object"}""")!;

        return new ToolSpec(
            Name: registration.Metadata.Name,
            Version: new Version(1, 0),
            InputSchema: inputSchema,
            OutputSchema: outputSchema,
            RetryPolicy: EngineRetryPolicy.Default,
            Timeout: TimeSpan.FromSeconds(30)
        );
    }

    private JsonNode CreateSchemaFromParameters(IList<ToolParameter> parameters)
    {
        var properties = new JsonObject();
        var required = new JsonArray();

        foreach (var param in parameters)
        {
            var prop = new JsonObject
            {
                ["type"] = GetJsonType(param),
                ["description"] = param.Description
            };

            // Add enum constraint if AllowedValues is specified
            if (param.AllowedValues != null && param.AllowedValues.Count > 0)
            {
                var enumArray = new JsonArray();
                foreach (var value in param.AllowedValues)
                {
                    enumArray.Add(value);
                }
                prop["enum"] = enumArray;
            }

            // Add default value if specified
            if (param.DefaultValue != null)
            {
                prop["default"] = JsonValue.Create(param.DefaultValue);
            }

            properties[param.Name] = prop;

            if (param.Required)
            {
                required.Add(param.Name);
            }
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required
        };
    }

    private string GetJsonType(ToolParameter param)
    {
        // ToolParameter.Type is already a string with the JSON schema type
        return param.Type.ToLowerInvariant() switch
        {
            "boolean" or "bool" => "boolean",
            "integer" or "int" or "long" => "integer",
            "number" or "float" or "double" or "decimal" => "number",
            "string" or "text" => "string",
            "array" or "list" => "array",
            "object" => "object",
            _ => "string"
        };
    }

    private Dictionary<string, object?> ConvertJsonToParameters(JsonNode json)
    {
        var dict = new Dictionary<string, object?>();

        if (json is JsonObject obj)
        {
            foreach (var prop in obj)
            {
                dict[prop.Key] = ConvertJsonValue(prop.Value);
            }
        }

        return dict;
    }

    private object? ConvertJsonValue(JsonNode? node)
    {
        if (node == null) return null;

        return node switch
        {
            JsonValue value => GetValueWithLogging(value),
            JsonArray array => array.Select(ConvertJsonValue).ToList(),
            JsonObject obj => ConvertJsonToParameters(obj),
            _ => node.ToString()
        };
    }

    private object? GetValueWithLogging(JsonValue value)
    {
        // Extract the actual CLR value instead of JsonElement
        object? converted = value.GetValueKind() switch
        {
            JsonValueKind.String => value.GetValue<string>(),
            JsonValueKind.Number => value.TryGetValue<int>(out var intVal) ? intVal :
                                  value.TryGetValue<long>(out var longVal) ? longVal :
                                  value.GetValue<double>(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => value.GetValue<object>()
        };

        _logger?.LogDebug("Converted JsonValue to {Type}: {Value}", converted?.GetType().Name ?? "null", converted);
        return converted;
    }

    private IAsyncPolicy BuildRetryPolicy(EngineRetryPolicy policy)
    {
        if (policy.MaxRetries == 0)
        {
            return Polly.Policy.NoOpAsync();
        }

        return Polly.Policy
            .Handle<ToolExecutionException>(ex => IsRetryable(ex))
            .Or<OperationCanceledException>()
            .WaitAndRetryAsync(
                policy.MaxRetries,
                retryAttempt => CalculateBackoff(retryAttempt, policy),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger?.LogWarning(
                        "Retrying tool execution. Attempt {RetryCount} after {Delay}ms",
                        retryCount,
                        timespan.TotalMilliseconds
                    );
                }
            );
    }

    private TimeSpan CalculateBackoff(int retryAttempt, EngineRetryPolicy policy)
    {
        var baseDelay = policy.BaseBackoff.TotalMilliseconds;

        var delay = policy.Strategy switch
        {
            BackoffStrategy.None => 0,
            BackoffStrategy.Linear => baseDelay * retryAttempt,
            BackoffStrategy.Exponential => baseDelay * Math.Pow(2, retryAttempt - 1),
            BackoffStrategy.ExponentialWithJitter => CalculateJitteredBackoff(baseDelay, retryAttempt, policy.JitterFactor),
            _ => baseDelay
        };

        return TimeSpan.FromMilliseconds(delay);
    }

    private double CalculateJitteredBackoff(double baseDelay, int retryAttempt, double jitterFactor)
    {
        var exponentialDelay = baseDelay * Math.Pow(2, retryAttempt - 1);
        var jitter = exponentialDelay * jitterFactor * (Random.Shared.NextDouble() * 2 - 1);
        return exponentialDelay + jitter;
    }

    private bool IsRetryable(ToolExecutionException ex)
    {
        // Check if the error message indicates a retryable condition
        var message = ex.Message.ToLowerInvariant();
        return message.Contains("retry") ||
               message.Contains("rate") ||
               message.Contains("throttle") ||
               message.Contains("temporary") ||
               message.Contains("unavailable");
    }
}

public class ToolExecutionException : Exception
{
    public ToolExecutionException(string message) : base(message) { }
    public ToolExecutionException(string message, Exception innerException) : base(message, innerException) { }
}