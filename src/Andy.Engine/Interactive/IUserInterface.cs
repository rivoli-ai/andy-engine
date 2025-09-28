using Andy.Engine.Contracts;

namespace Andy.Engine.Interactive;

/// <summary>
/// Abstraction for user interface interactions, allowing Andy.Engine to work
/// with different UI implementations (CLI, web, API, etc.)
/// </summary>
public interface IUserInterface
{
    /// <summary>
    /// Ask the user a question and wait for their response
    /// </summary>
    Task<string> AskAsync(string question, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ask the user to choose from a list of options
    /// </summary>
    Task<string> ChooseAsync(string question, IList<string> options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Display a message to the user
    /// </summary>
    Task ShowAsync(string message, MessageType type = MessageType.Information, CancellationToken cancellationToken = default);

    /// <summary>
    /// Show progress or status updates during agent execution
    /// </summary>
    Task ShowProgressAsync(string status, bool isComplete = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Display formatted content (markdown, code, etc.)
    /// </summary>
    Task ShowContentAsync(string content, ContentType contentType = ContentType.Markdown, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirm a potentially dangerous or irreversible action
    /// </summary>
    Task<bool> ConfirmAsync(string message, bool defaultValue = false, CancellationToken cancellationToken = default);
}

/// <summary>
/// Types of messages that can be displayed to the user
/// </summary>
public enum MessageType
{
    Information,
    Warning,
    Error,
    Success
}

/// <summary>
/// Types of content that can be displayed
/// </summary>
public enum ContentType
{
    PlainText,
    Markdown,
    Code,
    Json,
    Html
}