using Andy.Context.Context;
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
    private readonly bool _enablePromptCaching;
    private readonly string _workingDirectory;
    private readonly IReadOnlyDictionary<string, object?>? _extraBody;
    private IConversationManager _conversationManager;
    // True when the agent created its own DefaultConversationManager (no caller-supplied one).
    // Only an agent-owned manager may be replaced by ClearHistory().
    private readonly bool _ownsConversationManager;

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
        IContextCompressor? contextCompressor = null,
        bool enablePromptCaching = true,
        IReadOnlyDictionary<string, object?>? extraBody = null)
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
        _enablePromptCaching = enablePromptCaching;
        _extraBody = extraBody;
        _workingDirectory = workingDirectory ?? Environment.CurrentDirectory;
        _logger = logger;
        _ownsConversationManager = conversationManager is null;
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
    /// Event raised after each tool call completes, carrying the (truncated) result content the
    /// model will see. Not raised for a call aborted by cancellation.
    /// </summary>
    public event EventHandler<ToolCalledEventArgs>? ToolCalled;

    /// <summary>
    /// Process a user message and return a response.
    /// </summary>
    public Task<SimpleAgentResult> ProcessMessageAsync(
        string userMessage,
        CancellationToken cancellationToken = default) =>
        ProcessMessageAsync(userMessage, onResponseDelta: null, cancellationToken);

    /// <summary>
    /// Process a user message, optionally streaming the model's response text incrementally.
    ///
    /// When <paramref name="onResponseDelta"/> is provided, the LLM is called via
    /// <see cref="ILlmProvider.StreamCompleteAsync"/> and each provider text delta is forwarded in
    /// order as a <see cref="AgentResponseDeltaKind.Text"/> event tagged with its turn number.
    /// Whether a turn's text is the final answer is only knowable once that turn's stream ends: if
    /// the turn produced tool calls or was cut off by the output limit, its text was tool-round
    /// narration and a single <see cref="AgentResponseDeltaKind.Discarded"/> event is emitted for
    /// that turn so consumers can drop or relabel it. Text from the turn the returned result comes
    /// from is never discarded. Providers whose streaming path is unsupported fall back to
    /// <see cref="ILlmProvider.CompleteAsync"/> and emit the final text as one chunk. The returned
    /// <see cref="SimpleAgentResult"/> is always fully populated, identical to the non-streaming
    /// overload. The callback is invoked synchronously on the processing loop, so no events are
    /// delivered after the returned task completes (including on cancellation).
    /// </summary>
    public async Task<SimpleAgentResult> ProcessMessageAsync(
        string userMessage,
        Action<AgentResponseDelta>? onResponseDelta,
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
        // Whether the one-time "you're approaching the turn budget, wrap up" nudge has been sent.
        var wrapUpNudgeSent = false;

        var turnCount = 0;
        var startTime = DateTime.UtcNow;
        // Set once the turn has been recorded in the conversation manager, so the interrupted-turn
        // commit in the catch paths cannot double-record it.
        var turnCommitted = false;

        // Records what actually happened when the loop is interrupted (error or cancellation).
        // Executed tools may have had side effects on disk; dropping the turn would make the next
        // call's context contradict that state and is unrecoverable for snapshot/restore.
        void CommitInterruptedTurn()
        {
            if (turnCommitted)
                return;
            try
            {
                _conversationManager.AddTurn(new Turn
                {
                    UserOrSystemMessage = userMsg,
                    AssistantMessage = lastAssistantWasInterleaved ? null : finalAssistantMessage,
                    ToolMessages = new List<Message>(allInterleavedMessages),
                });
                turnCommitted = true;
            }
            catch (Exception commitEx)
            {
                _logger?.LogWarning(commitEx, "Failed to record interrupted turn in conversation history.");
            }
        }

        try
        {
            while (turnCount < _maxTurns)
            {
                turnCount++;
                _logger?.LogDebug("Turn {TurnCount}/{MaxTurns}", turnCount, _maxTurns);

                // As we approach the turn budget, nudge the model once to wrap up and stop
                // exploring. This often lets it produce a final answer before the hard limit,
                // avoiding a "max turns exceeded" stop. Mirrors the output-truncation nudge: the
                // message is added to both the in-flight request and the interleaved log so the
                // persisted turn faithfully reflects what was sent.
                if (!wrapUpNudgeSent && ShouldSendWrapUpNudge(turnCount, _maxTurns))
                {
                    _logger?.LogInformation(
                        "Approaching turn budget ({TurnCount}/{MaxTurns}); nudging the model to wrap up.",
                        turnCount, _maxTurns);

                    var wrapUp = new Message
                    {
                        Role = Role.User,
                        Content = $"You are on turn {turnCount} of a maximum {_maxTurns} tool-call turns. "
                                + "Stop exploring and produce your final answer now, making only the "
                                + "essential remaining tool calls. If you cannot fully finish, summarize "
                                + "what you have changed and what still remains.",
                    };
                    inFlightMessages.Add(wrapUp);
                    allInterleavedMessages.Add(wrapUp);
                    wrapUpNudgeSent = true;
                }

                // Build tool declarations from registry
                var toolDeclarations = BuildToolDeclarations();

                // Make LLM request with prior context + in-flight messages, re-fitted to the
                // context token budget EVERY turn: in-flight tool results accumulate up to
                // _maxToolResultChars per call, so a long tool loop would otherwise overflow the
                // provider context mid-run even though the committed history fit at loop entry.
                var allMessages = FitRequestToBudget(contextMessages, inFlightMessages);
                var request = new LlmRequest
                {
                    Messages = allMessages,
                    Tools = toolDeclarations,
                    SystemPrompt = _systemPrompt,
                    // Cache the stable system prompt prefix (no-op on auto-caching providers like
                    // OpenAI/DeepSeek; emits an Anthropic cache_control breakpoint). The system
                    // prompt is byte-stable for the agent's lifetime, unlike the compacted history.
                    CacheSystemPrompt = _enablePromptCaching,
                    // Provider-specific request fields (e.g. OpenRouter `provider` routing), passed
                    // straight through to the LLM provider that knows how to serialize them.
                    ExtraBody = _extraBody,
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

                var (response, streamedTextThisTurn) =
                    await GetLlmResponseAsync(request, turnCount, onResponseDelta, cancellationToken);

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

                    // Any text streamed from this turn was tool-round narration, not the final
                    // answer — tell the consumer to drop/relabel it.
                    if (streamedTextThisTurn)
                        onResponseDelta?.Invoke(AgentResponseDelta.DiscardedTurn(turnCount));

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

                    // The truncated text is not a final answer; the model will continue.
                    if (streamedTextThisTurn)
                        onResponseDelta?.Invoke(AgentResponseDelta.DiscardedTurn(turnCount));

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

                // Streaming consumers got the final text incrementally when the provider streams;
                // when the non-streaming fallback was used, emit it as the one immediate chunk.
                if (onResponseDelta is not null && !streamedTextThisTurn && response.Content.Length > 0)
                    onResponseDelta(AgentResponseDelta.TextChunk(turnCount, response.Content));

                // Record the complete turn in conversation
                // ToolMessages contains ALL intermediate messages in order:
                // assistant(tool_calls) → tool results → assistant(tool_calls) → tool results → ...
                // AssistantMessage holds the final response (no tool calls). A COPY of the
                // interleaved list is committed so the stored Turn never aliases loop-local state.
                _conversationManager.AddTurn(new Turn
                {
                    UserOrSystemMessage = userMsg,
                    AssistantMessage = finalAssistantMessage,
                    ToolMessages = new List<Message>(allInterleavedMessages)
                });
                turnCommitted = true;

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
                ToolMessages = new List<Message>(allInterleavedMessages)
            });
            turnCommitted = true;

            _logger?.LogWarning("Reached maximum turn count ({MaxTurns})", _maxTurns);

            // Log the full conversation for debugging, but DO NOT return it as the response.
            // Previously the entire history — every raw tool-result payload, embedded CRLFs and all —
            // was packed into Response, and callers (e.g. the CLI feed) rendered it verbatim, flooding
            // the UI with tool JSON. The detail stays in the logs; callers get a concise message.
            if (_logger?.IsEnabled(LogLevel.Warning) == true)
            {
                var allConversationMessages = _conversationManager.Conversation.ToChronoMessages().ToList();
                for (int i = 0; i < allConversationMessages.Count; i++)
                {
                    var msg = allConversationMessages[i];
                    _logger.LogWarning("  [{Index}] {Role}: {Content} (ToolCalls: {ToolCallCount})",
                        i, msg.Role, msg.Content ?? "(empty)", msg.ToolCalls?.Count ?? 0);
                }
            }

            return new SimpleAgentResult(
                Success: false,
                Response: $"Reached the maximum of {_maxTurns} tool-call turns before completing the request.",
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
            // The partial turn is still committed first: tools that already ran had side
            // effects, and a resumed session must see them.
            CommitInterruptedTurn();
            throw;
        }
        catch (Exception ex)
        {
            CommitInterruptedTurn();

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
    /// True when the named tool declares its own timeout parameter (a parameter whose name contains
    /// "timeout", e.g. execute_command's <c>timeout_seconds</c>). Such tools enforce their own
    /// execution limit, so the engine must not impose its default execution-time cap on top - that
    /// would override the tool's timeout and kill legitimate long-running work.
    /// </summary>
    private bool ToolDeclaresOwnTimeout(string toolName)
    {
        try
        {
            var parameters = _toolRegistry.GetTool(toolName)?.Metadata?.Parameters;
            if (parameters == null)
            {
                return false;
            }

            foreach (var p in parameters)
            {
                if (!string.IsNullOrEmpty(p.Name) &&
                    p.Name.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
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

        // One event per call, raised AFTER execution so subscribers see the actual result.
        var traceId = Guid.NewGuid();

        try
        {
            // Parse tool arguments
            var args = ParseToolArguments(toolCall.ArgumentsJson);

            // The default 100MB memory cap is measured process-wide and is exceeded by the .NET
            // runtime alone, producing spurious "resource limit exceeded" warnings on ordinary
            // file ops - so raise it.
            var resourceLimits = new ToolResourceLimits { MaxMemoryBytes = 2L * 1024 * 1024 * 1024 };

            // The framework also imposes a default 30s execution-time cap. Only keep it for tools
            // that do NOT manage their own timeout. A tool that declares a timeout parameter (e.g.
            // execute_command's timeout_seconds) enforces its own limit; layering the engine's 30s
            // cap on top would override it and kill legitimate long-running work (builds, test runs,
            // indexing). For those tools, leave the cap unbounded so the tool's own timeout governs.
            if (ToolDeclaresOwnTimeout(toolCall.Name))
            {
                resourceLimits.MaxExecutionTimeMs = 0; // unbounded - the tool enforces its own timeout
            }

            // Execute tool
            var toolResult = await _toolExecutor.ExecuteAsync(
                toolCall.Name,
                args,
                new ToolExecutionContext
                {
                    WorkingDirectory = _workingDirectory,
                    Environment = new Dictionary<string, string>(),
                    CancellationToken = cancellationToken,
                    ResourceLimits = resourceLimits
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

            ToolCalled?.Invoke(this, new ToolCalledEventArgs(traceId, toolCall.Name, resultContent));

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
            ToolCalled?.Invoke(this, new ToolCalledEventArgs(traceId, toolCall.Name, errorContent));
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
    /// Clear conversation history. With the agent's own default manager the conversation is fully
    /// emptied. A caller-supplied <see cref="IConversationManager"/> is NEVER replaced — it gets
    /// <see cref="IConversationManager.Reset"/> and keeps receiving subsequent turns, so whether
    /// past turns survive is that manager's own Reset contract (DefaultConversationManager, for
    /// example, preserves them). Previously the injected manager was silently swapped for a fresh
    /// in-memory one, orphaning persisting managers mid-lifetime.
    /// </summary>
    public void ClearHistory()
    {
        _conversationManager.Reset();
        if (_ownsConversationManager)
        {
            // Turns are append-only on Conversation and Reset() preserves them; replacing the
            // agent-owned manager is the only way to actually empty the history.
            _conversationManager = new DefaultConversationManager();
        }
        _logger?.LogInformation("Conversation history cleared");
    }

    /// <summary>
    /// Get conversation history.
    /// </summary>
    public IReadOnlyList<Message> GetHistory() =>
        BuildContextFromConversation().AsReadOnly();

    /// <summary>
    /// Exports a read-only, versioned snapshot of the conversation transcript (issue #32). The
    /// snapshot deep-copies message content, tool calls and tool results; it holds no provider,
    /// tool-executor, logger or cancellation state. Interrupted turns (which #37 now commits with
    /// a null final answer) export faithfully and restore the same way.
    /// </summary>
    public TranscriptSnapshot ExportTranscript()
    {
        var turns = new List<TranscriptTurn>();
        foreach (var turn in _conversationManager.Conversation.Turns)
        {
            // Newer Turn versions carry an authoritative ordered Messages sequence that
            // supersedes the legacy AssistantMessage/ToolMessages members when non-empty.
            IReadOnlyList<Message> interleaved;
            Message? final;
            if (turn.Messages is { Count: > 0 } ordered)
            {
                final = ordered[^1].Role == Role.Assistant && (ordered[^1].ToolCalls?.Count ?? 0) == 0
                    ? ordered[^1]
                    : null;
                interleaved = final is null ? ordered : ordered.Take(ordered.Count - 1).ToList();
            }
            else
            {
                interleaved = turn.ToolMessages ?? (IReadOnlyList<Message>)Array.Empty<Message>();
                final = turn.AssistantMessage;
            }

            turns.Add(new TranscriptTurn
            {
                User = TranscriptMessage.FromMessage(turn.UserOrSystemMessage
                    ?? new Message { Role = Role.User, Content = string.Empty }),
                Interleaved = interleaved.Select(TranscriptMessage.FromMessage).ToList(),
                FinalAssistant = final is null ? null : TranscriptMessage.FromMessage(final),
            });
        }

        return new TranscriptSnapshot { Turns = turns };
    }

    /// <summary>
    /// Restores a previously exported transcript into this agent (issue #32). The agent must have
    /// an EMPTY conversation (a freshly constructed agent, or one after <see cref="ClearHistory"/>
    /// with the default manager) — restoring is not merging. The entire snapshot is validated
    /// before any state is touched: an invalid snapshot throws and leaves the agent unmodified.
    /// </summary>
    /// <exception cref="ArgumentNullException">The snapshot is null.</exception>
    /// <exception cref="NotSupportedException">The snapshot version is unsupported.</exception>
    /// <exception cref="InvalidOperationException">This agent already has conversation history.</exception>
    /// <exception cref="ArgumentException">Roles are invalid or tool-call correlation is broken.</exception>
    public void RestoreTranscript(TranscriptSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Version != TranscriptSnapshot.CurrentVersion)
            throw new NotSupportedException(
                $"Transcript version {snapshot.Version} is not supported (expected {TranscriptSnapshot.CurrentVersion}).");
        if (_conversationManager.Conversation.Turns.Count > 0)
            throw new InvalidOperationException(
                "RestoreTranscript requires an empty conversation; restore into a fresh agent.");

        // Validate and materialize EVERYTHING before mutating any agent state.
        var restored = new List<Turn>();
        for (var t = 0; t < snapshot.Turns.Count; t++)
        {
            var turn = snapshot.Turns[t];
            if (turn.User is null || !turn.User.TryGetRole(out var userRole))
                throw new ArgumentException($"Turn {t}: opening message has an invalid role '{turn.User?.Role}'.");
            if (userRole is not (Role.User or Role.System))
                throw new ArgumentException($"Turn {t}: opening message must be 'user' or 'system', got '{turn.User.Role}'.");

            var knownCallIds = new HashSet<string>(StringComparer.Ordinal);
            var interleaved = new List<Message>();
            for (var i = 0; i < turn.Interleaved.Count; i++)
            {
                var msg = turn.Interleaved[i];
                if (!msg.TryGetRole(out var role))
                    throw new ArgumentException($"Turn {t}, message {i}: invalid role '{msg.Role}'.");

                switch (role)
                {
                    case Role.Assistant:
                        foreach (var tc in msg.ToolCalls)
                            knownCallIds.Add(tc.Id);
                        break;
                    case Role.Tool:
                        if (msg.ToolResults.Count == 0)
                            throw new ArgumentException($"Turn {t}, message {i}: tool message has no tool results.");
                        foreach (var tr in msg.ToolResults)
                        {
                            if (!knownCallIds.Contains(tr.CallId))
                                throw new ArgumentException(
                                    $"Turn {t}, message {i}: tool result '{tr.CallId}' has no preceding tool call in this turn.");
                        }
                        break;
                    default:
                        throw new ArgumentException(
                            $"Turn {t}, message {i}: interleaved messages must be 'assistant' or 'tool', got '{msg.Role}'.");
                }

                interleaved.Add(msg.ToMessage(role));
            }

            Message? final = null;
            if (turn.FinalAssistant is { } fa)
            {
                if (!fa.TryGetRole(out var finalRole) || finalRole != Role.Assistant)
                    throw new ArgumentException($"Turn {t}: final message must have role 'assistant', got '{fa.Role}'.");
                if (fa.ToolCalls.Count > 0)
                    throw new ArgumentException(
                        $"Turn {t}: final assistant message must not carry tool calls (they belong in the interleaved sequence).");
                final = fa.ToMessage(Role.Assistant);
            }

            restored.Add(new Turn
            {
                UserOrSystemMessage = turn.User.ToMessage(userRole),
                AssistantMessage = final,
                ToolMessages = interleaved,
            });
        }

        foreach (var turn in restored)
            _conversationManager.AddTurn(turn);

        _logger?.LogInformation("Restored transcript with {TurnCount} turn(s).", restored.Count);
    }

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

    /// <summary>
    /// Gets one LLM response for the current turn. Without a delta callback this is a plain
    /// <see cref="ILlmProvider.CompleteAsync"/> call. With one, the provider's streaming API is
    /// used: text deltas are forwarded to the callback in order as they arrive (tagged with the
    /// turn), tool-call and finish-reason chunks are accumulated, and the assembled
    /// <see cref="LlmResponse"/> is returned. An error chunk becomes an exception, matching
    /// CompleteAsync failure semantics. Providers that do not support streaming
    /// (NotSupported/NotImplemented before the first chunk) fall back to CompleteAsync;
    /// the returned flag then reports that no text was streamed so the caller can emit the
    /// final content as a single chunk.
    /// </summary>
    private async Task<(LlmResponse Response, bool StreamedText)> GetLlmResponseAsync(
        LlmRequest request,
        int turn,
        Action<AgentResponseDelta>? onResponseDelta,
        CancellationToken cancellationToken)
    {
        if (onResponseDelta is null)
            return (await _llmProvider.CompleteAsync(request, cancellationToken), false);

        var content = new StringBuilder();
        var toolCalls = new List<Andy.Model.Model.ToolCall>();
        string? finishReason = null;
        LlmUsage? usage = null;
        var streamedText = false;

        var enumerator = _llmProvider.StreamCompleteAsync(request, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        try
        {
            while (true)
            {
                bool moved;
                try
                {
                    moved = await enumerator.MoveNextAsync();
                }
                catch (Exception ex) when (!streamedText && toolCalls.Count == 0 &&
                                           ex is NotSupportedException or NotImplementedException)
                {
                    // Provider has no streaming path; use the non-streaming call instead.
                    _logger?.LogDebug("Provider {Provider} does not support streaming; falling back to CompleteAsync.",
                        _llmProvider.Name);
                    return (await _llmProvider.CompleteAsync(request, cancellationToken), false);
                }

                if (!moved)
                    break;

                var chunk = enumerator.Current;
                if (chunk.Error is { Length: > 0 } error)
                    throw new InvalidOperationException(error);

                if (chunk.Delta?.Content is { Length: > 0 } text)
                {
                    content.Append(text);
                    streamedText = true;
                    onResponseDelta(AgentResponseDelta.TextChunk(turn, text));
                }

                if (chunk.Delta?.ToolCalls is { Count: > 0 } chunkToolCalls)
                    toolCalls.AddRange(chunkToolCalls);

                if (chunk.FinishReason is { Length: > 0 } fr)
                    finishReason = fr;
                if (chunk.Usage is not null)
                    usage = chunk.Usage;
            }
        }
        finally
        {
            await enumerator.DisposeAsync();
        }

        var response = new LlmResponse
        {
            AssistantMessage = new Message
            {
                Role = Role.Assistant,
                Content = content.ToString(),
                ToolCalls = toolCalls,
            },
            FinishReason = finishReason,
            Usage = usage,
        };
        return (response, streamedText);
    }

    // Rough chars-per-token factor shared by the budget estimates below (~4 chars/token, the
    // common heuristic for English/code).
    private const int CharsPerTokenEstimate = 4;
    // The most recent messages of the combined request are never shrunk: the model needs the
    // latest exchanges verbatim to act correctly.
    private const int BudgetProtectedTailMessages = 8;
    // Head slice kept when an old tool result is elided to fit the budget.
    private const int ElidedToolResultHeadChars = 400;

    /// <summary>
    /// Fits the combined request view (committed context + current in-flight messages) to the
    /// context token budget. Applied EVERY loop turn, because in-flight tool results grow the
    /// request far beyond what the pre-loop compression saw. Two stages: (1) elide old tool-result
    /// payloads (the dominant weight in tool-heavy sessions) oldest-first, sparing the protected
    /// tail; (2) if still over, drop whole committed turns oldest-first (in-flight messages are
    /// never dropped). Only the returned request view is modified — the conversation log and the
    /// live in-flight lists keep their full payloads.
    /// </summary>
    internal List<Message> FitRequestToBudget(
        IReadOnlyList<Message> contextMessages, IReadOnlyList<Message> inFlightMessages)
    {
        var messages = new List<Message>(contextMessages.Count + inFlightMessages.Count);
        messages.AddRange(contextMessages);
        messages.AddRange(inFlightMessages);

        var budgetChars = (long)_maxContextTokens * CharsPerTokenEstimate;
        var totalChars = messages.Sum(EstimateMessageChars);
        if (totalChars <= budgetChars)
            return messages;

        // Stage 1: elide old tool results (request view only).
        for (var i = 0; i < messages.Count - BudgetProtectedTailMessages && totalChars > budgetChars; i++)
        {
            if (messages[i].Role != Role.Tool)
                continue;
            var shrunk = ShrinkToolResultMessage(messages[i]);
            if (shrunk is null)
                continue;
            totalChars -= EstimateMessageChars(messages[i]) - EstimateMessageChars(shrunk);
            messages[i] = shrunk;
        }

        if (totalChars <= budgetChars)
            return messages;

        // Stage 2: drop whole committed turns (a user/system message up to the next one) from the
        // front, keeping tool-call pairing intact. The current call's messages are never dropped.
        var committedCount = contextMessages.Count;
        var dropCount = 0;
        while (totalChars > budgetChars && dropCount < committedCount)
        {
            var end = dropCount + 1;
            while (end < committedCount &&
                   messages[end].Role != Role.User && messages[end].Role != Role.System)
                end++;
            for (var i = dropCount; i < end; i++)
                totalChars -= EstimateMessageChars(messages[i]);
            dropCount = end;
        }
        if (dropCount > 0)
        {
            _logger?.LogWarning(
                "Context budget: dropped the oldest {Dropped} committed message(s) from the request view.",
                dropCount);
            messages.RemoveRange(0, dropCount);
        }

        if (totalChars > budgetChars)
            _logger?.LogWarning(
                "Request still exceeds the context budget (~{Tokens} tokens > {Budget}) after elision; sending as-is.",
                totalChars / CharsPerTokenEstimate, _maxContextTokens);

        return messages;
    }

    /// <summary>Rough per-message size: content plus tool-call arguments plus tool-result payloads.</summary>
    internal static long EstimateMessageChars(Message m)
    {
        long n = m.Content?.Length ?? 0;
        if (m.ToolCalls is { } toolCalls)
            foreach (var tc in toolCalls)
                n += (tc.ArgumentsJson?.Length ?? 0) + (tc.Name?.Length ?? 0);
        if (m.ToolResults is { } toolResults)
            foreach (var tr in toolResults)
                n += tr.ResultJson?.Length ?? 0;
        return n + 16; // rough per-message wire overhead
    }

    /// <summary>
    /// Returns a copy of a tool message with its result payloads (Content and every
    /// ToolResult.ResultJson — providers serialize tool messages from ResultJson, so eliding
    /// Content alone would change nothing on the wire) elided to a head slice. Returns null when
    /// the message is already small enough that eliding would not help.
    /// </summary>
    private static Message? ShrinkToolResultMessage(Message m)
    {
        const int shrinkThreshold = ElidedToolResultHeadChars * 2;
        var contentOversized = (m.Content?.Length ?? 0) > shrinkThreshold;
        var anyResultOversized = m.ToolResults?.Any(tr => (tr.ResultJson?.Length ?? 0) > shrinkThreshold) == true;
        if (!contentOversized && !anyResultOversized)
            return null;

        return new Message
        {
            Role = m.Role,
            Id = m.Id,
            Timestamp = m.Timestamp,
            Content = ElideForBudget(m.Content ?? string.Empty),
            ToolCalls = m.ToolCalls ?? new List<Andy.Model.Model.ToolCall>(),
            ToolResults = m.ToolResults?.Select(tr => new Andy.Model.Model.ToolResult
            {
                CallId = tr.CallId,
                Name = tr.Name,
                IsError = tr.IsError,
                ResultJson = ElideForBudget(tr.ResultJson ?? string.Empty),
            }).ToList() ?? new List<Andy.Model.Model.ToolResult>(),
        };
    }

    /// <summary>Replaces an oversized payload with a small, valid JSON stub keeping a head slice.</summary>
    internal static string ElideForBudget(string payload)
    {
        if (payload.Length <= ElidedToolResultHeadChars * 2)
            return payload;
        return JsonSerializer.Serialize(new
        {
            elided = true,
            original_chars = payload.Length,
            head = payload[..ElidedToolResultHeadChars],
            note = "Older tool result compacted to fit the context budget. Re-run the tool if the full output is needed.",
        });
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
        // Always emit non-null collections. The Message model defaults these to new() and
        // consumers (Message.Parts, provider request builders such as OpenRouterProvider.
        // ConvertMessage) dereference .Count without null-guarding. Emitting null here breaks
        // that invariant and caused a NullReferenceException on any follow-up turn whose
        // compressed context contained a message without tool calls/results.
        ToolCalls = m.ToolCalls is { Count: > 0 }
            ? m.ToolCalls.Select(tc => new Andy.Model.Model.ToolCall
            {
                Id = tc.Id,
                Name = tc.Name,
                ArgumentsJson = tc.ArgumentsJson,
            }).ToList()
            : new List<Andy.Model.Model.ToolCall>(),
        ToolResults = m.ToolResults is { Count: > 0 }
            ? m.ToolResults.Select(tr => new Andy.Model.Model.ToolResult
            {
                CallId = tr.CallId,
                Name = tr.Name,
                IsError = tr.IsError,
                ResultJson = tr.ResultJson,
            }).ToList()
            : new List<Andy.Model.Model.ToolResult>(),
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

    /// <summary>
    /// True when a finish reason indicates the response was cut off by the output-token limit
    /// (rather than the model choosing to stop). Providers report this as "length" (OpenAI/
    /// OpenRouter) or "max_tokens" (Anthropic).
    /// </summary>
    internal static bool IsTruncatedByOutputLimit(string? finishReason) =>
        finishReason is not null &&
        (finishReason.Equals("length", StringComparison.OrdinalIgnoreCase) ||
         finishReason.Equals("max_tokens", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Whether to emit the one-time "wrap up" nudge on the current turn: once the agent has used
    /// roughly 80% of its turn budget. Skipped for very small budgets (&lt; 3) where there is no
    /// room for a nudge to help before the hard limit.
    /// </summary>
    internal static bool ShouldSendWrapUpNudge(int turnCount, int maxTurns)
    {
        if (maxTurns < 3) return false;
        var threshold = (int)Math.Ceiling(maxTurns * 0.8);
        return turnCount >= threshold;
    }

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
                args[property.Name] = ConvertElement(property.Value);
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

    // Recursively converts a JSON element to a plain CLR value: objects become
    // Dictionary<string, object?>, arrays become string[]/object?[], scalars become their CLR type.
    // Preserving nested structure is what lets tools with object/array parameters — e.g. the
    // dataframe_* tools' 'predicate', 'aggregations', 'expression' — receive real dictionaries and
    // lists instead of raw JSON text (which the tools reject as "must be an object").
    private static object? ConvertElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => ConvertNumber(element),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        JsonValueKind.Object => DeserializeObject(element),
        JsonValueKind.Array => DeserializeArray(element),
        _ => element.GetRawText()
    };

    // Largest integer a double represents exactly (2^53).
    private const long MaxDoubleExactInteger = 1L << 53;

    /// <summary>
    /// JSON numbers surface as double by ecosystem convention: Andy.Tools reads numeric
    /// parameters via GetParameter&lt;double?&gt;, whose Convert.ChangeType fallback throws for a
    /// Nullable&lt;T&gt; target, so boxing small integers as long would null out every numeric tool
    /// argument. But a double only holds integers exactly up to 2^53 — larger int64 values (ids,
    /// offsets, hashes) were silently corrupted by the double round-trip. Those stay long: they
    /// were already unusable as double, so fidelity wins over the convention there.
    /// </summary>
    private static object ConvertNumber(JsonElement element) =>
        element.TryGetInt64(out var integer) &&
        (integer > MaxDoubleExactInteger || integer < -MaxDoubleExactInteger)
            // Explicit box: an unboxed conditional would unify long/double to double and undo this.
            ? (object)integer
            : element.GetDouble();

    private static Dictionary<string, object?> DeserializeObject(JsonElement objectElement)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in objectElement.EnumerateObject())
        {
            result[property.Name] = ConvertElement(property.Value);
        }
        return result;
    }

    private static object DeserializeArray(JsonElement arrayElement)
    {
        var items = new List<object?>();
        foreach (var item in arrayElement.EnumerateArray())
        {
            items.Add(ConvertElement(item));
        }

        // Keep a string[] when every element is a string (back-compat for string-array params like
        // group_by / columns); otherwise return object?[], preserving nested dictionaries so arrays
        // of objects (aggregations, sort keys, expectations, window functions) survive intact.
        return items.All(x => x is string)
            ? items.Cast<string>().ToArray()
            : items.ToArray();
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

// Note: ToolCalledEventArgs is defined in ToolCalledEventArgs.cs and shared across scenario runners and SimpleAgent
