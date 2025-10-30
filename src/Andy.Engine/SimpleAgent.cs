using Andy.Engine.Contracts;
using Andy.Model.Llm;
using Andy.Model.Model;
using Andy.Model.Tooling;
using Andy.Tools.Core;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace Andy.Engine;

/// <summary>
/// Simplified agent that uses native LLM function calling without separate Planner/Critic layers.
/// This architecture is more reliable and follows the pattern of successful CLI agents like gemini-cli.
/// </summary>
public class SimpleAgent : IDisposable
{
    private readonly ILlmProvider _llmProvider;
    private readonly IToolRegistry _toolRegistry;
    private readonly IToolExecutor _toolExecutor;
    private readonly ILogger<SimpleAgent>? _logger;
    private readonly string _systemPrompt;
    private readonly int _maxTurns;
    private readonly string _workingDirectory;
    private readonly List<Message> _conversationHistory = new();

    public SimpleAgent(
        ILlmProvider llmProvider,
        IToolRegistry toolRegistry,
        IToolExecutor toolExecutor,
        string systemPrompt,
        int maxTurns = 10,
        string? workingDirectory = null,
        ILogger<SimpleAgent>? logger = null)
    {
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
        _systemPrompt = systemPrompt ?? throw new ArgumentNullException(nameof(systemPrompt));
        _maxTurns = maxTurns;
        _workingDirectory = workingDirectory ?? Environment.CurrentDirectory;
        _logger = logger;
    }

    /// <summary>
    /// Event raised when a tool is called.
    /// </summary>
    public event EventHandler<ToolCalledEventArgs>? ToolCalled;

    /// <summary>
    /// Process a user message and return a response.
    /// </summary>
    public async Task<SimpleAgentResult> ProcessMessageAsync(
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
            throw new ArgumentException("User message cannot be empty", nameof(userMessage));

        _logger?.LogInformation("Processing user message: {Message}", userMessage);

        // Add user message to history
        _conversationHistory.Add(new Message
        {
            Role = Role.User,
            Content = userMessage
        });

        var turnCount = 0;
        var startTime = DateTime.UtcNow;

        try
        {
            while (turnCount < _maxTurns)
            {
                turnCount++;
                _logger?.LogDebug("Turn {TurnCount}/{MaxTurns}", turnCount, _maxTurns);

                // Build tool declarations from registry
                var toolDeclarations = BuildToolDeclarations();

                // Make LLM request with conversation history and tools
                var request = new LlmRequest
                {
                    Messages = _conversationHistory,
                    Tools = toolDeclarations,
                    SystemPrompt = _systemPrompt,
                    Config = new LlmClientConfig
                    {
                        // Temperature defaults to null, allowing models to use their own defaults
                        MaxTokens = 4096,
                        TopP = 1.0m
                    }
                };

                _logger?.LogDebug("Sending request - Messages: {MessageCount}, Tools: {ToolCount}, SystemPrompt length: {PromptLength}",
                    request.Messages.Count, request.Tools.Count, _systemPrompt.Length);

                var response = await _llmProvider.CompleteAsync(request, cancellationToken);

                _logger?.LogInformation("LLM response - Content: '{Content}', HasToolCalls: {HasToolCalls}, FinishReason: {FinishReason}",
                    response.Content, response.HasToolCalls, response.FinishReason);

                // Add assistant message to history
                _conversationHistory.Add(response.AssistantMessage);

                // Check if we have tool calls
                if (response.HasToolCalls)
                {
                    _logger?.LogInformation("LLM requested {ToolCallCount} tool calls", response.ToolCalls.Count);

                    // Execute all tool calls and collect results
                    var toolResults = new List<Message>();
                    foreach (var toolCall in response.ToolCalls)
                    {
                        _logger?.LogDebug("Executing tool: {ToolName}", toolCall.Name);

                        // Raise event
                        var traceId = Guid.NewGuid();
                        ToolCalled?.Invoke(this, new ToolCalledEventArgs(
                            traceId,
                            toolCall.Name,
                            "" // Result will be populated after execution
                        ));

                        try
                        {
                            // Parse tool arguments
                            var args = ParseToolArguments(toolCall.ArgumentsJson);

                            // Execute tool
                            var toolResult = await _toolExecutor.ExecuteAsync(
                                toolCall.Name,
                                args,
                                new ToolExecutionContext
                                {
                                    WorkingDirectory = _workingDirectory,
                                    Environment = new Dictionary<string, string>(),
                                    CancellationToken = cancellationToken
                                }
                            );

                            // Create tool result message with explicit success indicator
                            // IMPORTANT: Always wrap results in a consistent format so LLM can interpret success/failure
                            var resultContent = toolResult.IsSuccessful
                                ? JsonSerializer.Serialize(new
                                  {
                                      success = true,
                                      result = toolResult.Data,
                                      message = toolResult.Message
                                  })
                                : JsonSerializer.Serialize(new
                                  {
                                      success = false,
                                      error = toolResult.ErrorMessage
                                  });

                            toolResults.Add(new Message
                            {
                                Role = Role.Tool,
                                Content = resultContent,
                                ToolCallId = toolCall.Id
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Error executing tool {ToolName}", toolCall.Name);
                            toolResults.Add(new Message
                            {
                                Role = Role.Tool,
                                Content = JsonSerializer.Serialize(new { success = false, error = ex.Message }),
                                ToolCallId = toolCall.Id
                            });
                        }
                    }

                    // Add tool results to history
                    _conversationHistory.AddRange(toolResults);

                    // Continue loop to get LLM's response to tool results
                    continue;
                }

                // No tool calls - we have a final response
                _logger?.LogInformation("LLM provided final response");
                var duration = DateTime.UtcNow - startTime;

                return new SimpleAgentResult(
                    Success: true,
                    Response: response.Content,
                    TurnCount: turnCount,
                    Duration: duration,
                    StopReason: response.FinishReason ?? "completed"
                );
            }

            // Exhausted max turns
            _logger?.LogWarning("Reached maximum turn count ({MaxTurns})", _maxTurns);

            // Log conversation history to help debug why max turns was exceeded
            _logger?.LogWarning("Conversation history at max_turns:");
            for (int i = 0; i < _conversationHistory.Count; i++)
            {
                var msg = _conversationHistory[i];
                var preview = msg.Content?.Length > 100 ? msg.Content.Substring(0, 100) + "..." : msg.Content;
                _logger?.LogWarning("  [{Index}] {Role}: {Preview} (ToolCalls: {ToolCallCount})",
                    i, msg.Role, preview, msg.ToolCalls?.Count ?? 0);
            }

            return new SimpleAgentResult(
                Success: false,
                Response: "I've reached the maximum number of steps. Could you please rephrase your request?",
                TurnCount: turnCount,
                Duration: DateTime.UtcNow - startTime,
                StopReason: "max_turns_exceeded"
            );
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing message: {Message}", ex.Message);

            // Log full stack trace for debugging
            _logger?.LogError("Stack trace: {StackTrace}", ex.StackTrace);
            if (ex.InnerException != null)
            {
                _logger?.LogError("Inner exception: {InnerMessage}", ex.InnerException.Message);
            }

            // Include exception details in stop reason so it's visible in benchmark results
            var errorMessage = ex.Message;
            if (ex.InnerException != null)
            {
                errorMessage += $"\nInner exception: {ex.InnerException.Message}";
            }

            return new SimpleAgentResult(
                Success: false,
                Response: $"Error: {errorMessage}",
                TurnCount: turnCount,
                Duration: DateTime.UtcNow - startTime,
                StopReason: $"error: {errorMessage}"
            );
        }
    }

    /// <summary>
    /// Clear conversation history.
    /// </summary>
    public void ClearHistory()
    {
        _conversationHistory.Clear();
        _logger?.LogInformation("Conversation history cleared");
    }

    /// <summary>
    /// Get conversation history.
    /// </summary>
    public IReadOnlyList<Message> GetHistory() => _conversationHistory.AsReadOnly();

    private IReadOnlyList<ToolDeclaration> BuildToolDeclarations()
    {
        var declarations = new List<ToolDeclaration>();

        foreach (var registration in _toolRegistry.Tools.Where(t => t.IsEnabled))
        {
            var metadata = registration.Metadata;

            // Build JSON Schema-compatible parameter schema
            var properties = new Dictionary<string, object>();
            var required = new List<string>();

            foreach (var param in metadata.Parameters)
            {
                var propertySchema = new Dictionary<string, object>
                {
                    ["type"] = param.Type.ToLowerInvariant(),
                    ["description"] = param.Description
                };

                // Handle array types - add items schema
                if (param.Type.Equals("array", StringComparison.OrdinalIgnoreCase))
                {
                    if (param.ItemType != null)
                    {
                        propertySchema["items"] = new Dictionary<string, object>
                        {
                            ["type"] = param.ItemType.Type.ToLowerInvariant(),
                            ["description"] = param.ItemType.Description ?? ""
                        };
                    }
                    else
                    {
                        // Default to string items if ItemType not specified
                        propertySchema["items"] = new Dictionary<string, object>
                        {
                            ["type"] = "string"
                        };
                    }
                }

                properties[param.Name] = propertySchema;

                if (param.Required)
                    required.Add(param.Name);
            }

            var parameters = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = properties
            };

            if (required.Any())
            {
                parameters["required"] = required;
            }

            declarations.Add(new ToolDeclaration
            {
                Name = metadata.Id,
                Description = metadata.Description,
                Parameters = parameters
            });
        }

        return declarations;
    }

    private Dictionary<string, object?> ParseToolArguments(string argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
            return new Dictionary<string, object?>();

        try
        {
            var jsonDoc = JsonDocument.Parse(argumentsJson);
            var args = new Dictionary<string, object?>();

            foreach (var property in jsonDoc.RootElement.EnumerateObject())
            {
                args[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Number => property.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    JsonValueKind.Object => property.Value.GetRawText(),
                    JsonValueKind.Array => property.Value.GetRawText(),
                    _ => property.Value.GetRawText()
                };
            }

            return args;
        }
        catch (JsonException ex)
        {
            _logger?.LogError(ex, "Failed to parse tool arguments: {Json}", argumentsJson);
            return new Dictionary<string, object?>();
        }
    }

    public void Dispose()
    {
        // Nothing to dispose currently, but implementing for future resource management
    }
}

/// <summary>
/// Result from a SimpleAgent message processing.
/// </summary>
public record SimpleAgentResult(
    bool Success,
    string Response,
    int TurnCount,
    TimeSpan Duration,
    string StopReason
);

// Note: ToolCalledEventArgs is defined in Agent.cs and shared across both Agent and SimpleAgent
