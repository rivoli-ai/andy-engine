using Andy.Model.Llm;
using Andy.Model.Model;

namespace Andy.Engine;

/// <summary>
/// Capability contract for providers whose ACTIVE model accepts image input (issue #35).
///
/// <see cref="ILlmProvider"/> has no per-active-model capability query
/// (<see cref="ILlmProvider.ListModelsAsync"/> describes the catalog, not the model a provider
/// instance is configured to call), and none of the stock Andy.Llm providers serialize image
/// content today — sending them an image would silently drop it. SimpleAgent therefore refuses
/// image-bearing messages unless the provider explicitly opts in through this interface, which
/// also commits it to serializing the parts exposed by
/// <see cref="MultimodalMessage.GetAttachedParts"/> into its wire format.
/// </summary>
public interface IVisionCapableLlmProvider : ILlmProvider
{
    /// <summary>True when the provider's currently configured model accepts image input.</summary>
    ValueTask<bool> SupportsImageInputAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Builds and reads structured multimodal user messages (issue #35).
///
/// The pinned Andy.Model cannot store an <see cref="ImagePart"/> on a <see cref="Message"/>:
/// <see cref="Message.Parts"/> is a projection computed from Content/ToolCalls/ToolResults, so
/// anything added to it is discarded. Until the model library carries parts natively, the engine
/// stores the ordered part list in <see cref="Message.Metadata"/> under
/// <see cref="PartsMetadataKey"/>; <see cref="Message.Content"/> holds the concatenated text
/// parts so part-unaware consumers still see the prompt text. Providers and clients read the
/// structured content back with <see cref="GetAttachedParts"/>.
/// </summary>
public static class MultimodalMessage
{
    /// <summary>Metadata key under which the ordered <see cref="MessagePart"/> list is stored.</summary>
    public const string PartsMetadataKey = "andy.engine.message_parts";

    /// <summary>Default per-image byte cap (10 MiB) applied to raw data and data: URIs.</summary>
    public const int DefaultMaxImageBytes = 10 * 1024 * 1024;

    /// <summary>
    /// Returns the ordered structured parts attached to a message, or null when the message
    /// carries none (a plain text message).
    /// </summary>
    /// <exception cref="InvalidOperationException">The parts metadata key is present but its
    /// value is not a part list — the parts were corrupted (e.g. by JSON round-tripping the
    /// conversation outside the transcript snapshot API). Failing loudly here is what keeps a
    /// corrupted image from being silently dropped from the wire.</exception>
    public static IReadOnlyList<MessagePart>? GetAttachedParts(Message message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!message.Metadata.TryGetValue(PartsMetadataKey, out var value))
            return null;
        if (value is IReadOnlyList<MessagePart> parts)
            return parts;
        throw new InvalidOperationException(
            $"Message metadata key '{PartsMetadataKey}' holds a '{value?.GetType().Name ?? "null"}' instead of " +
            "the structured part list. The parts were lost — most likely the conversation was persisted and " +
            "reloaded outside the TranscriptSnapshot API, which is the only supported round-trip for multimodal content.");
    }

    /// <summary>Copies the attached parts (if any) from one message onto another.</summary>
    internal static void CopyAttachedParts(Message from, Message to)
    {
        if (GetAttachedParts(from) is { } parts)
            to.Metadata[PartsMetadataKey] = parts;
    }

    /// <summary>
    /// Validates a caller-supplied part list and builds the user <see cref="Message"/> carrying
    /// it. Bounded media metadata is enforced here; loading files into parts is the client's
    /// responsibility (issue #35 keeps workspace IO at the client boundary).
    /// </summary>
    /// <exception cref="ArgumentException">The list is empty, contains a null or unsupported
    /// part, or an <see cref="ImagePart"/> is malformed or oversized.</exception>
    internal static Message BuildUserMessage(IReadOnlyList<MessagePart> parts, int maxImageBytes)
    {
        ArgumentNullException.ThrowIfNull(parts);
        if (parts.Count == 0)
            throw new ArgumentException("Message parts cannot be empty.", nameof(parts));

        var textPieces = new List<string>();
        var hasContent = false;
        // Deep-copied at the boundary: image byte arrays are cloned so a caller that reuses its
        // buffer after dispatch (e.g. a pooled buffer) cannot retroactively corrupt committed
        // history or exported snapshots. TextPart/ImagePart themselves are init-only.
        var stored = new MessagePart[parts.Count];
        for (var i = 0; i < parts.Count; i++)
        {
            switch (parts[i])
            {
                case TextPart text:
                    if (!string.IsNullOrWhiteSpace(text.Text))
                    {
                        textPieces.Add(text.Text);
                        hasContent = true;
                    }
                    stored[i] = text;
                    break;
                case ImagePart image:
                    ValidateImagePart(image, i, maxImageBytes);
                    hasContent = true;
                    stored[i] = image.ImageData is { Length: > 0 } data
                        ? new ImagePart { MimeType = image.MimeType, ImageUrl = image.ImageUrl, ImageData = data.ToArray() }
                        : image;
                    break;
                case null:
                    throw new ArgumentException($"Message part {i} is null.", nameof(parts));
                default:
                    throw new ArgumentException(
                        $"Message part {i} has unsupported type '{parts[i].Type}'. User input accepts text and image parts only.",
                        nameof(parts));
            }
        }

        if (!hasContent)
            throw new ArgumentException("Message parts contain no text or image content.", nameof(parts));

        var message = new Message
        {
            Role = Role.User,
            Content = string.Join("\n", textPieces),
        };
        message.Metadata[PartsMetadataKey] = Array.AsReadOnly(stored);
        return message;
    }

    internal static bool HasImageParts(Message message) =>
        GetAttachedParts(message)?.Any(p => p is ImagePart) == true;

    /// <summary>
    /// Rough size contribution of attached image parts for the context-budget estimate: bytes
    /// weigh in at their base64 wire size, URI sources at the URI length.
    /// </summary>
    internal static long EstimateAttachedPartChars(Message message)
    {
        if (GetAttachedParts(message) is not { } parts)
            return 0;
        long n = 0;
        foreach (var part in parts)
        {
            if (part is ImagePart image)
            {
                n += image.ImageData is { } data
                    ? (data.Length + 2) / 3 * 4
                    : image.ImageUrl.Length;
            }
        }
        return n;
    }

    private static void ValidateImagePart(ImagePart image, int index, int maxImageBytes)
    {
        if (string.IsNullOrWhiteSpace(image.MimeType))
            throw new ArgumentException($"Image part {index} has no media type; a MimeType such as 'image/png' is required.");
        if (!image.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Image part {index} has media type '{image.MimeType}'; only image/* media is supported.");

        var hasData = image.ImageData is { Length: > 0 };
        var hasUrl = !string.IsNullOrWhiteSpace(image.ImageUrl);
        if (hasData == hasUrl)
            throw new ArgumentException(
                $"Image part {index} must carry exactly one source: raw ImageData or an ImageUrl.");

        if (hasData && image.ImageData!.Length > maxImageBytes)
            throw new ArgumentException(
                $"Image part {index} is {image.ImageData.Length} bytes, exceeding the {maxImageBytes}-byte limit.");

        if (hasUrl)
        {
            if (image.ImageUrl.Length > maxImageBytes)
                throw new ArgumentException(
                    $"Image part {index} URI is {image.ImageUrl.Length} chars, exceeding the {maxImageBytes}-char limit.");
            if (!Uri.TryCreate(image.ImageUrl, UriKind.Absolute, out var uri) ||
                uri.Scheme is not ("http" or "https" or "data"))
                throw new ArgumentException(
                    $"Image part {index} URI must be an absolute http(s) or data: URI.");
        }
    }
}
