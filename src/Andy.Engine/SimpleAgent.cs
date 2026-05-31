using Andy.Context.Context;
using Andy.Engine.Contracts;
using Andy.Model.Conversation;
using Andy.Model.Llm;
using Andy.Model.Model;
using Andy.Model.Tooling;
using Andy.Tools.Core;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using CtxMessage = Andy.Context.Model.Message;
using CtxRole = Andy.Context.Model.Role;
using CtxToolCall = Andy.Context.Model.ToolCall;
using CtxToolResult = Andy.Context.Model.ToolResult;

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
    private readonly int _maxOutputTokens;
    private readonly int _maxToolResultChars;
    private readonly int _maxContextTokens;
    private readonly IContextCompressor _contextCompressor;
    private readonly string _workingDirectory;
    private IConversationManager _conversationManager;

    public SimpleAgent(
        ILlmProvider llmProvider,
        IToolRegistry toolRegistry,
        IToolExecutor toolExecutor,
        string systemPrompt,
        int maxTurns = 10,
        string? workingDirectory = null,
        ILogger<SimpleAgent>? logger = null,
        IConversationManager? conversationManager = null,
        int maxOutputTokens = 4096,
        int maxToolResultChars = 16000,
        int maxContextTokens = 120_000,
        IContextCompressor? contextCompressor = null)
    {
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
        _systemPrompt = systemPrompt ?? throw new ArgumentNullException(nameof(systemPrompt));
        _maxTurns = maxTurns;
        _maxOutputTokens = maxOutputTokens;
        _maxToolResultChars = maxToolResultChars;
        _maxContextTokens = maxContextTokens > 0 ? maxContextTokens : 120_000;
        _contextCompressor = contextCompressor ?? new SmartCompressor();
        _workingDirectory = workingDirectory ?? Environment.CurrentDirectory;
        _logger = logger;
        _conversationManager = conversationManager ?? new DefaultConversationManager();
    }

    /// <summary>
    /// The conversation manager managing conversation state.
    /// </summary>
    public IConversationManager ConversationManager => _conversationManager;

    /// <summary>
    /// The underlying conversation with turn-based history.
    /// </summary>
    public Conversation Conversation => _conversationManager.Conversation;

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

        // Prior context from conversation turns (properly interleaved), compressed to fit the
        // per-request token budget. The full conversation log is retained unmodified; only this
        // per-request VIEW is compressed.
        var contextMessages = BuildRequestContext();

        var userMsg = new Message
        {
            Role = Role.User,
            Content = userMessage
        };

        // In-flight messages for the current processing loop (not yet committed as a Turn)
        var inFlightMessages = new List<Message> { userMsg };
        // Tracks ALL intermediate messages in order: assistant(tool_calls), tool results, assistant(tool_calls), tool results, ...
        // The final assistant message is NOT included here — it goes in Turn.AssistantMessage
        var allInterleavedMessages = new List<Message>();
        Message? finalAssistantMessage = null;
        // True when the most recent assistant message was appended to allInterleavedMessages
        // (a tool-call turn or an output-limit truncation turn) rather than being a genuine
        // final answer. Used at max-turns to avoid recording that already-interleaved message
        // a second time as Turn.AssistantMessage.
        var lastAssistantWasInterleaved = false;

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

                // Make LLM request with prior context + in-flight messages
                var allMessages = contextMessages.Concat(inFlightMessages).ToList();
                var request = new LlmRequest
                {
                    Messages = allMessages,
                    Tools = toolDeclarations,
                    SystemPrompt = _systemPrompt,
                    Config = new LlmClientConfig
                    {
                        // Temperature defaults to null, allowing models to use their own defaults
                        MaxTokens = _maxOutputTokens,
                        TopP = 1.0m
                    }
                };

                _logger?.LogDebug("Sending request - Messages: {MessageCount}, Tools: {ToolCount}, SystemPrompt length: {PromptLength}",
                    request.Messages.Count, request.Tools.Count, _systemPrompt.Length);

                if (_logger?.IsEnabled(LogLevel.Debug) == true && toolDeclarations.Count > 0)
                {
                    _logger.LogDebug("Available tools in request: {ToolNames}",
                        string.Join(", ", toolDeclarations.Select(t => t.Name)));
                }

                var response = await _llmProvider.CompleteAsync(request, cancellationToken);

                _logger?.LogInformation("LLM response - Content: '{Content}', HasToolCalls: {HasToolCalls}, FinishReason: {FinishReason}",
                    response.Content, response.HasToolCalls, response.FinishReason);

                // Add assistant message to in-flight messages
                inFlightMessages.Add(response.AssistantMessage);
                finalAssistantMessage = response.AssistantMessage;
                lastAssistantWasInterleaved = false;

                // Check if we have tool calls
                if (response.HasToolCalls)
                {
                    _logger?.LogInformation("LLM requested {ToolCallCount} tool calls", response.ToolCalls.Count);

                    // Store the intermediate assistant message (with ToolCalls) for proper context reconstruction
                    allInterleavedMessages.Add(response.AssistantMessage);
                    lastAssistantWasInterleaved = true;

                    // Execute all tool calls CONCURRENTLY to cut latency, then assemble the
                    // results in the ORIGINAL tool-call order. The model maps tool results to
                    // tool calls by order/CallId, so the order of `toolResults` must match
                    // `response.ToolCalls` regardless of which call finishes first.
                    //
                    // Concurrency approach: one independent Task per tool call via Task.WhenAll,
                    // each with its OWN ToolExecutionContext. There is no cross-call shared
                    // mutable state introduced here (each lambda only touches its own captured
                    // toolCall and builds its own Message), so file-mutating tools are no riskier
                    // than the executor itself already allows for a single multi-tool turn — the
                    // model chose to issue these calls together. Per-call exceptions are captured
                    // as that call's error result (isolation); OperationCanceledException is NOT
                    // captured — it is rethrown so it cancels the whole run.
                    var toolTasks = response.ToolCalls
                        .Select(toolCall => ExecuteToolCallAsync(toolCall, cancellationToken))
                        .ToList();

                    // Task.WhenAll surfaces the first faulting task's exception; since each task
                    // only rethrows OperationCanceledException (all other failures become error
                    // results inside the task), the only exception that can propagate here is a
                    // cancellation — which we want to propagate.
                    var toolResults = (await Task.WhenAll(toolTasks)).ToList();

                    // Add tool results to in-flight messages and track them for the Turn.
                    // toolResults is already in original tool-call order (WhenAll preserves the
                    // input task order in its result array).
                    inFlightMessages.AddRange(toolResults);
                    allInterleavedMessages.AddRange(toolResults);

                    // Continue loop to get LLM's response to tool results
                    continue;
                }

                // No tool calls, but the turn was cut off by the output-token limit
                // (FinishReason "length"/"max_tokens"). The model was interrupted mid-task,
                // NOT finished — treating this as a final answer ends the run with a partial
                // or empty result (e.g. an empty patch). Nudge it to continue instead. This is
                // bounded by _maxTurns, so it cannot loop forever.
                if (IsTruncatedByOutputLimit(response.FinishReason))
                {
                    _logger?.LogWarning(
                        "LLM response truncated by output-token limit (FinishReason: {FinishReason}) on turn {TurnCount}; continuing.",
                        response.FinishReason, turnCount);

                    // Record the partial assistant message and the nudge in the interleaved log
                    // so the persisted Turn (and any later replay/compaction of it) faithfully
                    // reflects what was actually sent to the model. Without this, both messages
                    // would live only in inFlightMessages and be dropped from conversation
                    // history. The interleaved log is therefore the single source of truth for
                    // everything between the user message and the final answer.
                    allInterleavedMessages.Add(response.AssistantMessage);
                    lastAssistantWasInterleaved = true;

                    var nudge = new Message
                    {
                        Role = Role.User,
                        Content = "Your previous response was cut off by the output limit before you "
                                + "produced a tool call or a complete answer. Continue from where you "
                                + "left off and make the necessary tool calls to apply your change.",
                    };
                    inFlightMessages.Add(nudge);
                    allInterleavedMessages.Add(nudge);
                    continue;
                }

                // No tool calls - we have a final response
                _logger?.LogInformation("LLM provided final response");

                // Record the complete turn in conversation
                // ToolMessages contains ALL intermediate messages in order:
                // assistant(tool_calls) → tool results → assistant(tool_calls) → tool results → ...
                // AssistantMessage holds the final response (no tool calls)
                _conversationManager.AddTurn(new Turn
                {
                    UserOrSystemMessage = userMsg,
                    AssistantMessage = finalAssistantMessage,
                    ToolMessages = allInterleavedMessages
                });

                var duration = DateTime.UtcNow - startTime;

                return new SimpleAgentResult(
                    Success: true,
                    Response: response.Content,
                    TurnCount: turnCount,
                    Duration: duration,
                    StopReason: response.FinishReason ?? "completed"
                );
            }

            // Exhausted max turns — still record the turn so conversation state is preserved.
            //
            // Every iteration of the loop ends in either `continue` (tool calls or output-limit
            // truncation, both of which append the assistant message to allInterleavedMessages)
            // or `return` (a genuine final answer, which exits this method). So when the loop
            // falls through here, the last assistant message is ALREADY inside
            // allInterleavedMessages. Storing it again as Turn.AssistantMessage would duplicate
            // it in the reconstructed history (BuildContextFromConversation emits ToolMessages
            // then AssistantMessage), producing a dangling/duplicated assistant message. There
            // is no genuine final answer at max-turns, so leave it null in that case.
            _conversationManager.AddTurn(new Turn
            {
                UserOrSystemMessage = userMsg,
                AssistantMessage = lastAssistantWasInterleaved ? null : finalAssistantMessage,
                ToolMessages = allInterleavedMessages
            });

            _logger?.LogWarning("Reached maximum turn count ({MaxTurns})", _maxTurns);

            // Build conversation history for debugging
            var allConversationMessages = _conversationManager.Conversation.ToChronoMessages().ToList();
            var historyBuilder = new System.Text.StringBuilder();
            historyBuilder.AppendLine($"Reached maximum turn count ({_maxTurns})");
            historyBuilder.AppendLine("Conversation history (FULL - no truncation):");
            historyBuilder.AppendLine("=".PadRight(80, '='));
            for (int i = 0; i < allConversationMessages.Count; i++)
            {
                var msg = allConversationMessages[i];
                var content = msg.Content ?? "(empty)";
                historyBuilder.AppendLine($"\n[{i}] Role: {msg.Role}");
                historyBuilder.AppendLine($"Content: {content}");
                historyBuilder.AppendLine($"ToolCalls Count: {msg.ToolCalls?.Count ?? 0}");

                if (msg.ToolCalls != null && msg.ToolCalls.Count > 0)
                {
                    foreach (var toolCall in msg.ToolCalls)
                    {
                        historyBuilder.AppendLine($"  - Tool: {toolCall.Name}");
                        historyBuilder.AppendLine($"    ID: {toolCall.Id}");
                        historyBuilder.AppendLine($"    Arguments: {toolCall.ArgumentsJson}");
                    }
                }

                if (msg.ToolResults != null && msg.ToolResults.Count > 0)
                {
                    foreach (var toolResult in msg.ToolResults)
                    {
                        historyBuilder.AppendLine($"  - ToolResult: {toolResult.Name} (CallId: {toolResult.CallId}, IsError: {toolResult.IsError})");
                    }
                }
                historyBuilder.AppendLine("-".PadRight(80, '-'));

                // Also log to logger with full content
                _logger?.LogWarning("  [{Index}] {Role}: {Content} (ToolCalls: {ToolCallCount})",
                    i, msg.Role, content, msg.ToolCalls?.Count ?? 0);
            }
            historyBuilder.AppendLine("=".PadRight(80, '='));

            return new SimpleAgentResult(
                Success: false,
                Response: historyBuilder.ToString(),
                TurnCount: turnCount,
                Duration: DateTime.UtcNow - startTime,
                StopReason: "max_turns_exceeded"
            );
        }
        catch (OperationCanceledException)
        {
            // Cancellation must surface to callers as a cancellation, not be masked as a
            // failed SimpleAgentResult. Consumers (e.g. the headless cancel protocol) rely
            // on OperationCanceledException propagating out of ProcessMessageAsync.
            throw;
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
    /// Executes a single tool call (parse args → execute → shape the result Message) with the
    /// same try/catch semantics used for serial execution, so it is safe to run concurrently
    /// with sibling calls from the same turn. Each invocation builds its OWN
    /// <see cref="ToolExecutionContext"/> and never shares mutable state with other calls.
    ///
    /// A failing call is captured as THAT call's error result (so one failure does not fail the
    /// others), but <see cref="OperationCanceledException"/> is rethrown so a cancellation
    /// cancels the whole run rather than being masked as a tool error.
    /// </summary>
    private async Task<Message> ExecuteToolCallAsync(Andy.Model.Model.ToolCall toolCall, CancellationToken cancellationToken)
    {
        _logger?.LogDebug("Executing tool: {ToolName}", toolCall.Name);

        // Raise event (one per call, as before).
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
                    CancellationToken = cancellationToken,
                    // The default 100MB cap is measured process-wide and is
                    // exceeded by the .NET runtime alone, producing spurious
                    // "resource limit exceeded" warnings on ordinary file ops.
                    ResourceLimits = new ToolResourceLimits { MaxMemoryBytes = 2L * 1024 * 1024 * 1024 }
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

            // Progressive disclosure: a large successful result (e.g. a full file read or
            // directory listing) serialized whole would blow up context and cost. Cap it,
            // keeping a head slice plus guidance for narrowing the request. Error results are
            // already small and are never truncated.
            if (toolResult.IsSuccessful)
                resultContent = TruncateToolResult(resultContent, _maxToolResultChars);

            return new Message
            {
                Role = Role.Tool,
                Content = resultContent,
                ToolResults = new List<Andy.Model.Model.ToolResult>
                {
                    new Andy.Model.Model.ToolResult
                    {
                        CallId = toolCall.Id,
                        Name = toolCall.Name,
                        ResultJson = resultContent,
                        IsError = !toolResult.IsSuccessful
                    }
                }
            };
        }
        catch (OperationCanceledException)
        {
            // Cancellation is not a tool failure; propagate so callers can
            // distinguish "cancelled" from "the tool errored". Propagating from this task
            // makes Task.WhenAll surface the cancellation, which cancels the whole run.
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error executing tool {ToolName}", toolCall.Name);
            var errorContent = JsonSerializer.Serialize(new { success = false, error = ex.Message });
            return new Message
            {
                Role = Role.Tool,
                Content = errorContent,
                ToolResults = new List<Andy.Model.Model.ToolResult>
                {
                    new Andy.Model.Model.ToolResult
                    {
                        CallId = toolCall.Id,
                        Name = toolCall.Name,
                        ResultJson = errorContent,
                        IsError = true
                    }
                }
            };
        }
    }

    /// <summary>
    /// Clear conversation history.
    /// </summary>
    public void ClearHistory()
    {
        _conversationManager.Reset();
        _conversationManager = new DefaultConversationManager();
        _logger?.LogInformation("Conversation history cleared");
    }

    /// <summary>
    /// Get conversation history.
    /// </summary>
    public IReadOnlyList<Message> GetHistory() =>
        BuildContextFromConversation().AsReadOnly();

    /// <summary>
    /// Reconstructs a properly ordered flat message list from conversation turns.
    /// For each turn, emits: UserOrSystemMessage → ToolMessages (interleaved assistant+tool) → AssistantMessage (final).
    /// This ensures tool result messages are always preceded by their triggering assistant message with ToolCalls.
    /// </summary>
    private List<Message> BuildContextFromConversation()
    {
        var messages = new List<Message>();
        foreach (var turn in _conversationManager.Conversation.Turns)
        {
            if (turn.UserOrSystemMessage != null)
                messages.Add(turn.UserOrSystemMessage);

            // ToolMessages contains interleaved: assistant(tool_calls) → tool results → ...
            if (turn.ToolMessages != null)
                messages.AddRange(turn.ToolMessages);

            // Final assistant message (the one without tool calls)
            if (turn.AssistantMessage != null)
                messages.Add(turn.AssistantMessage);
        }
        return messages;
    }

    /// <summary>
    /// Builds the token-budgeted, compressed per-request view of the conversation. The full
    /// conversation log in <see cref="_conversationManager"/> is never mutated; this returns a
    /// fresh list fitted to <see cref="_maxContextTokens"/> via the configured
    /// <see cref="IContextCompressor"/>. Small histories pass through unchanged; oversized ones are
    /// shrunk (tool-call/result pairs and system/tool messages preserved). Falls back to the raw
    /// list if compression throws so a request is never lost to a context-library error.
    /// </summary>
    internal List<Message> BuildRequestContext()
    {
        var raw = BuildContextFromConversation();
        if (raw.Count == 0)
            return raw;

        try
        {
            var options = new ContextBuildOptions
            {
                TokenBudget = _maxContextTokens,
                // Bound how many recent messages survive verbatim; large enough not to drop a
                // normal interaction, while still letting the compressor shorten older content.
                MaxRecentMessages = Math.Max(20, raw.Count),
                IncludeToolMessages = true,
                IncludeSystemMessages = true,
                PreserveToolCallPairs = true,
                CompressionStrategy = Andy.Context.Context.CompressionStrategy.Smart,
            };

            var compressed = _contextCompressor.Compress(raw.Select(ToCtxMessage).ToList(), options);
            return compressed.Select(FromCtxMessage).ToList();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Context compression failed; falling back to uncompressed request view.");
            return raw;
        }
    }

    private static CtxMessage ToCtxMessage(Message m) => new CtxMessage
    {
        Role = (CtxRole)(int)m.Role,
        Content = m.Content ?? string.Empty,
        ToolCalls = m.ToolCalls?.Select(tc => new CtxToolCall
        {
            Id = tc.Id,
            Name = tc.Name,
            ArgumentsJson = tc.ArgumentsJson,
        }).ToList() ?? new List<CtxToolCall>(),
        ToolResults = m.ToolResults?.Select(tr => new CtxToolResult
        {
            CallId = tr.CallId,
            Name = tr.Name,
            IsError = tr.IsError,
            ResultJson = tr.ResultJson,
        }).ToList() ?? new List<CtxToolResult>(),
        Id = m.Id,
        Timestamp = m.Timestamp,
    };

    private static Message FromCtxMessage(CtxMessage m) => new Message
    {
        Role = (Role)(int)m.Role,
        Content = m.Content ?? string.Empty,
        Timestamp = m.Timestamp,
        Id = string.IsNullOrEmpty(m.Id) ? Guid.NewGuid().ToString() : m.Id,
        ToolCalls = m.ToolCalls is { Count: > 0 }
            ? m.ToolCalls.Select(tc => new Andy.Model.Model.ToolCall
            {
                Id = tc.Id,
                Name = tc.Name,
                ArgumentsJson = tc.ArgumentsJson,
            }).ToList()
            : null,
        ToolResults = m.ToolResults is { Count: > 0 }
            ? m.ToolResults.Select(tr => new Andy.Model.Model.ToolResult
            {
                CallId = tr.CallId,
                Name = tr.Name,
                IsError = tr.IsError,
                ResultJson = tr.ResultJson,
            }).ToList()
            : null,
    };

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

    /// <summary>
    /// True when a finish reason indicates the response was cut off by the output-token limit
    /// (rather than the model choosing to stop). Providers report this as "length" (OpenAI/
    /// OpenRouter) or "max_tokens" (Anthropic).
    /// </summary>
    /// <summary>
    /// Caps a successful tool-result JSON payload at <paramref name="maxChars"/> characters.
    /// When under the cap (or the cap is disabled with a non-positive value), the original
    /// payload is returned unchanged. When over the cap, returns a new, valid JSON object that
    /// keeps a head slice of the original under <c>"result"</c>, flags it as truncated, reports
    /// the original/shown sizes, and tells the model how to retrieve the rest.
    /// </summary>
    internal static string TruncateToolResult(string resultContent, int maxChars)
    {
        if (maxChars <= 0 || resultContent.Length <= maxChars)
            return resultContent;

        var head = resultContent[..maxChars];

        return JsonSerializer.Serialize(new
        {
            success = true,
            truncated = true,
            total_chars = resultContent.Length,
            shown_chars = head.Length,
            result = head,
            guidance = $"Result truncated (showed {head.Length} of {resultContent.Length} chars). "
                     + "Narrow the request: read a specific line range, grep/search for the relevant "
                     + "part, or filter the listing to get only what you need."
        });
    }

    internal static bool IsTruncatedByOutputLimit(string? finishReason) =>
        finishReason is not null &&
        (finishReason.Equals("length", StringComparison.OrdinalIgnoreCase) ||
         finishReason.Equals("max_tokens", StringComparison.OrdinalIgnoreCase));

    private Dictionary<string, object?> ParseToolArguments(string argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
            return new Dictionary<string, object?>();

        // Smaller / weaker models frequently wrap the JSON in markdown fences, add prose,
        // leave trailing commas, or double-encode it as a JSON string. Try a series of
        // best-effort repairs so a recoverable tool call is not silently dropped.
        foreach (var candidate in JsonRepairCandidates(argumentsJson))
        {
            if (TryExtractArguments(candidate, out var args))
                return args;
        }

        _logger?.LogWarning("Could not parse tool arguments as a JSON object; using empty args. Raw: {Json}",
            argumentsJson.Length > 500 ? argumentsJson[..500] + "…" : argumentsJson);
        return new Dictionary<string, object?>();
    }

    /// <summary>Attempts to parse one candidate string into an argument dictionary (unwrapping a double-encoded JSON string).</summary>
    private bool TryExtractArguments(string candidate, out Dictionary<string, object?> args)
    {
        args = new Dictionary<string, object?>();
        try
        {
            using var doc = JsonDocument.Parse(candidate);
            var root = doc.RootElement;

            // Double-encoded: the arguments are a JSON string that itself holds an object.
            if (root.ValueKind == JsonValueKind.String)
            {
                var inner = root.GetString();
                return !string.IsNullOrWhiteSpace(inner) && TryExtractArguments(inner!, out args);
            }

            if (root.ValueKind != JsonValueKind.Object)
                return false;

            foreach (var property in root.EnumerateObject())
            {
                args[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Number => property.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    JsonValueKind.Object => property.Value.GetRawText(),
                    JsonValueKind.Array => DeserializeArray(property.Value),
                    _ => property.Value.GetRawText()
                };
            }
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>Yields progressively-repaired candidate strings to attempt JSON parsing on.</summary>
    internal static IEnumerable<string> JsonRepairCandidates(string raw)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        string Emit(string s) => s.Trim();

        // 1. As-is.
        var asIs = Emit(raw);
        if (seen.Add(asIs)) yield return asIs;

        // 2. Strip a markdown code fence (```json ... ``` or ``` ... ```).
        var fenced = StripCodeFence(asIs);
        if (fenced != asIs && seen.Add(fenced)) yield return fenced;

        // 3. The substring spanning the first '{' to the last '}'.
        var braced = ExtractOutermostBraces(fenced);
        if (braced is not null && seen.Add(braced)) yield return braced;

        // 4. Remove trailing commas before } or ].
        var candidate = braced ?? fenced;
        var noTrailingCommas = TrailingCommaRegex.Replace(candidate, "$1");
        if (noTrailingCommas != candidate && seen.Add(noTrailingCommas)) yield return noTrailingCommas;
    }

    private static string StripCodeFence(string s)
    {
        if (!s.Contains("```", StringComparison.Ordinal))
            return s;
        var first = s.IndexOf("```", StringComparison.Ordinal);
        var afterOpen = s.IndexOf('\n', first);
        if (afterOpen < 0) return s;
        var close = s.IndexOf("```", afterOpen, StringComparison.Ordinal);
        var body = close < 0 ? s[(afterOpen + 1)..] : s[(afterOpen + 1)..close];
        return body.Trim();
    }

    private static string? ExtractOutermostBraces(string s)
    {
        var open = s.IndexOf('{');
        var close = s.LastIndexOf('}');
        return open >= 0 && close > open ? s[open..(close + 1)] : null;
    }

    private static readonly System.Text.RegularExpressions.Regex TrailingCommaRegex =
        new(@",(\s*[}\]])", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static object DeserializeArray(JsonElement arrayElement)
    {
        // Try to deserialize as string array first (most common case)
        var items = new List<object>();
        foreach (var item in arrayElement.EnumerateArray())
        {
            items.Add(item.ValueKind switch
            {
                JsonValueKind.String => item.GetString() ?? string.Empty,
                JsonValueKind.Number => item.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null!,
                JsonValueKind.Object => item.GetRawText(),
                JsonValueKind.Array => DeserializeArray(item),
                _ => item.GetRawText()
            });
        }

        // If all items are strings, return as string array
        if (items.All(x => x is string))
        {
            return items.Cast<string>().ToArray();
        }

        // Otherwise return as object array
        return items.ToArray();
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
