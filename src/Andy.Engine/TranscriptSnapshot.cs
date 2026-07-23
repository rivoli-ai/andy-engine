using System.Text.Json;
using System.Text.Json.Serialization;
using Andy.Model.Model;

namespace Andy.Engine;

/// <summary>
/// Versioned, serializable snapshot of a SimpleAgent conversation transcript (issue #32).
///
/// Contains ONLY conversation content — message roles, text, tool-call ids/names/arguments, tool
/// results, timestamps. Provider clients, API keys, cancellation state, loggers, and tool
/// executors are structurally excluded: none of them are reachable from this type. Callers that
/// persist snapshots are responsible for redacting sensitive text the conversation itself may
/// contain (tool results can embed file contents, for example).
///
/// Produce with <see cref="SimpleAgent.ExportTranscript"/>, rehydrate a NEW agent with
/// <see cref="SimpleAgent.RestoreTranscript"/>. JSON round-trips via <see cref="ToJson"/> /
/// <see cref="FromJson"/>.
/// </summary>
public sealed record TranscriptSnapshot
{
    /// <summary>The snapshot format version this library writes.</summary>
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;

    public IReadOnlyList<TranscriptTurn> Turns { get; init; } = Array.Empty<TranscriptTurn>();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>
    /// Deserializes a snapshot. Throws <see cref="JsonException"/> on malformed JSON and
    /// <see cref="NotSupportedException"/> on an unsupported version. Structural validity
    /// (roles, tool-call correlation) is checked by <see cref="SimpleAgent.RestoreTranscript"/>.
    /// </summary>
    public static TranscriptSnapshot FromJson(string json)
    {
        var snapshot = JsonSerializer.Deserialize<TranscriptSnapshot>(json, JsonOptions)
            ?? throw new JsonException("Transcript JSON deserialized to null.");
        if (snapshot.Version != CurrentVersion)
            throw new NotSupportedException(
                $"Transcript version {snapshot.Version} is not supported (expected {CurrentVersion}).");
        return snapshot;
    }
}

/// <summary>
/// One conversational turn: the opening user/system message, the ordered interleaved
/// assistant(tool_calls)/tool-result messages, and the final assistant answer (null when the turn
/// ended without one — max-turns, error, or cancellation).
/// </summary>
public sealed record TranscriptTurn
{
    public required TranscriptMessage User { get; init; }
    public IReadOnlyList<TranscriptMessage> Interleaved { get; init; } = Array.Empty<TranscriptMessage>();
    public TranscriptMessage? FinalAssistant { get; init; }
}

public sealed record TranscriptMessage
{
    /// <summary>"system", "user", "assistant" or "tool" (stable strings, not enum ordinals).</summary>
    public required string Role { get; init; }

    public string Content { get; init; } = string.Empty;

    public DateTimeOffset Timestamp { get; init; }

    public string? Id { get; init; }

    public IReadOnlyList<TranscriptToolCall> ToolCalls { get; init; } = Array.Empty<TranscriptToolCall>();

    public IReadOnlyList<TranscriptToolResult> ToolResults { get; init; } = Array.Empty<TranscriptToolResult>();

    /// <summary>
    /// Structured multimodal parts attached to the message (issue #35); null for plain text
    /// messages, so pre-multimodal snapshots serialize unchanged.
    /// </summary>
    public IReadOnlyList<TranscriptPart>? Parts { get; init; }

    internal static TranscriptMessage FromMessage(Message m) => new()
    {
        Role = m.Role switch
        {
            Andy.Model.Model.Role.System => "system",
            Andy.Model.Model.Role.User => "user",
            Andy.Model.Model.Role.Assistant => "assistant",
            Andy.Model.Model.Role.Tool => "tool",
            _ => throw new InvalidOperationException($"Unknown message role: {m.Role}"),
        },
        Content = m.Content ?? string.Empty,
        Timestamp = m.Timestamp,
        Id = m.Id,
        ToolCalls = m.ToolCalls?.Select(tc => new TranscriptToolCall
        {
            Id = tc.Id,
            Name = tc.Name,
            ArgumentsJson = tc.ArgumentsJson,
        }).ToList() ?? (IReadOnlyList<TranscriptToolCall>)Array.Empty<TranscriptToolCall>(),
        ToolResults = m.ToolResults?.Select(tr => new TranscriptToolResult
        {
            CallId = tr.CallId,
            Name = tr.Name,
            IsError = tr.IsError,
            ResultJson = tr.ResultJson,
        }).ToList() ?? (IReadOnlyList<TranscriptToolResult>)Array.Empty<TranscriptToolResult>(),
        Parts = MultimodalMessage.GetAttachedParts(m) is { } parts
            ? parts.Select(TranscriptPart.FromPart).ToList()
            : null,
    };

    internal bool TryGetRole(out Role role)
    {
        switch (Role)
        {
            case "system": role = Andy.Model.Model.Role.System; return true;
            case "user": role = Andy.Model.Model.Role.User; return true;
            case "assistant": role = Andy.Model.Model.Role.Assistant; return true;
            case "tool": role = Andy.Model.Model.Role.Tool; return true;
            default: role = default; return false;
        }
    }

    internal Message ToMessage(Role role)
    {
        var message = new Message
        {
            Role = role,
            Content = Content,
            Timestamp = Timestamp,
            Id = string.IsNullOrEmpty(Id) ? Guid.NewGuid().ToString("N") : Id,
            ToolCalls = ToolCalls.Select(tc => new ToolCall
            {
                Id = tc.Id,
                Name = tc.Name,
                ArgumentsJson = tc.ArgumentsJson,
            }).ToList(),
            ToolResults = ToolResults.Select(tr => new ToolResult
            {
                CallId = tr.CallId,
                Name = tr.Name,
                IsError = tr.IsError,
                ResultJson = tr.ResultJson,
            }).ToList(),
        };

        if (Parts is { Count: > 0 })
        {
            message.Metadata[MultimodalMessage.PartsMetadataKey] =
                Parts.Select((p, i) => p.ToMessagePart(i)).ToArray();
        }

        return message;
    }
}

/// <summary>
/// Serialized form of one structured message part (issue #35): "text" carries Text, "image"
/// carries MimeType plus exactly one of ImageUrl / ImageDataBase64.
/// </summary>
public sealed record TranscriptPart
{
    public required string Type { get; init; }
    public string? Text { get; init; }
    public string? MimeType { get; init; }
    public string? ImageUrl { get; init; }
    public string? ImageDataBase64 { get; init; }

    internal static TranscriptPart FromPart(MessagePart part) => part switch
    {
        TextPart text => new TranscriptPart { Type = "text", Text = text.Text },
        ImagePart image => new TranscriptPart
        {
            Type = "image",
            MimeType = image.MimeType,
            ImageUrl = string.IsNullOrEmpty(image.ImageUrl) ? null : image.ImageUrl,
            ImageDataBase64 = image.ImageData is { } data ? Convert.ToBase64String(data) : null,
        },
        _ => throw new InvalidOperationException(
            $"Message part type '{part?.Type}' cannot be exported to a transcript."),
    };

    internal MessagePart ToMessagePart(int index)
    {
        switch (Type)
        {
            case "text":
                return new TextPart(Text ?? string.Empty);
            case "image":
                if (string.IsNullOrWhiteSpace(MimeType))
                    throw new ArgumentException($"Image part {index} has no media type.");
                var hasUrl = !string.IsNullOrWhiteSpace(ImageUrl);
                var hasData = !string.IsNullOrWhiteSpace(ImageDataBase64);
                if (hasUrl == hasData)
                    throw new ArgumentException(
                        $"Image part {index} must carry exactly one source: ImageUrl or ImageDataBase64.");
                byte[]? bytes = null;
                if (hasData)
                {
                    try
                    {
                        bytes = Convert.FromBase64String(ImageDataBase64!);
                    }
                    catch (FormatException ex)
                    {
                        throw new ArgumentException($"Image part {index} has invalid base64 data.", ex);
                    }
                }
                return new ImagePart
                {
                    MimeType = MimeType,
                    ImageUrl = ImageUrl ?? string.Empty,
                    ImageData = bytes,
                };
            default:
                throw new ArgumentException($"Message part {index} has unsupported type '{Type}'.");
        }
    }
}

public sealed record TranscriptToolCall
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string ArgumentsJson { get; init; } = "{}";
}

public sealed record TranscriptToolResult
{
    public required string CallId { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsError { get; init; }
    public string ResultJson { get; init; } = string.Empty;
}
