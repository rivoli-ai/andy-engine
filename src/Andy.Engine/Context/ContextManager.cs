using Andy.Engine.Util;

namespace Andy.Engine.Context;

/// <summary>
/// Manages conversation context with compression and history
/// </summary>
public class ContextManager
{
    private readonly List<ContextEntry> _history = new();
    private readonly int _maxTokens;
    private readonly int _compressionThreshold;
    private string _systemPrompt;

    public ContextManager(
        string systemPrompt,
        int maxTokens = 12000,
        int compressionThreshold = 10000)
    {
        _systemPrompt = systemPrompt;
        _maxTokens = maxTokens;
        _compressionThreshold = compressionThreshold;
    }
    
    /// <summary>
    /// Add a user message to the context
    /// </summary>
    public void AddUserMessage(string message)
    {
        Guard.NullOrEmpty(message, nameof(message));
        
        _history.Add(new ContextEntry
        {
            Role = MessageRole.User,
            Content = message,
            Timestamp = DateTime.UtcNow
        });
    }
    
    /// <summary>
    /// Add an assistant message to the context
    /// </summary>
    public void AddAssistantMessage(string message, List<ContextToolCall>? toolCalls = null)
    {
        Guard.NullOrEmpty(message, nameof(message));

        _history.Add(new ContextEntry
        {
            Role = MessageRole.Assistant,
            Content = message,
            Timestamp = DateTime.UtcNow,
            ToolCalls = toolCalls
        });
    }
}