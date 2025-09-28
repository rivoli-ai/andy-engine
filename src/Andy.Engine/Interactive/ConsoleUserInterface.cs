using Andy.Engine.Contracts;

namespace Andy.Engine.Interactive;

/// <summary>
/// Console-based implementation of IUserInterface for CLI applications
/// This is a basic implementation that can be replaced with more sophisticated
/// terminal UIs (like Andy.Cli's TUI system)
/// </summary>
public class ConsoleUserInterface : IUserInterface
{
    private readonly ConsoleUserInterfaceOptions _options;

    public ConsoleUserInterface(ConsoleUserInterfaceOptions? options = null)
    {
        _options = options ?? ConsoleUserInterfaceOptions.Default;
    }

    public async Task<string> AskAsync(string question, CancellationToken cancellationToken = default)
    {
        await ShowAsync(question, MessageType.Information, cancellationToken);
        Console.Write(_options.InputPrompt);

        var input = await ReadLineAsync(cancellationToken);
        return input ?? string.Empty;
    }

    public async Task<string> ChooseAsync(string question, IList<string> options, CancellationToken cancellationToken = default)
    {
        await ShowAsync(question, MessageType.Information, cancellationToken);

        for (int i = 0; i < options.Count; i++)
        {
            Console.WriteLine($"{i + 1}. {options[i]}");
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            Console.Write("Choose an option (1-{0}): ", options.Count);
            var input = await ReadLineAsync(cancellationToken);

            if (int.TryParse(input, out var choice) && choice >= 1 && choice <= options.Count)
            {
                return options[choice - 1];
            }

            await ShowAsync("Invalid choice. Please try again.", MessageType.Warning, cancellationToken);
        }

        return options.FirstOrDefault() ?? string.Empty;
    }

    public Task ShowAsync(string message, MessageType type = MessageType.Information, CancellationToken cancellationToken = default)
    {
        var color = GetColorForMessageType(type);
        var prefix = GetPrefixForMessageType(type);

        if (_options.UseColors)
        {
            Console.ForegroundColor = color;
        }

        Console.WriteLine($"{prefix}{message}");

        if (_options.UseColors)
        {
            Console.ResetColor();
        }

        return Task.CompletedTask;
    }

    public Task ShowProgressAsync(string status, bool isComplete = false, CancellationToken cancellationToken = default)
    {
        var prefix = isComplete ? "✓ " : "⏳ ";
        var color = isComplete ? ConsoleColor.Green : ConsoleColor.Yellow;

        if (_options.UseColors)
        {
            Console.ForegroundColor = color;
        }

        Console.WriteLine($"{prefix}{status}");

        if (_options.UseColors)
        {
            Console.ResetColor();
        }

        return Task.CompletedTask;
    }

    public Task ShowContentAsync(string content, ContentType contentType = ContentType.Markdown, CancellationToken cancellationToken = default)
    {
        switch (contentType)
        {
            case ContentType.Code:
                ShowCodeBlock(content);
                break;
            case ContentType.Json:
                ShowJsonContent(content);
                break;
            case ContentType.Markdown:
                ShowMarkdownContent(content);
                break;
            default:
                Console.WriteLine(content);
                break;
        }

        return Task.CompletedTask;
    }

    public async Task<bool> ConfirmAsync(string message, bool defaultValue = false, CancellationToken cancellationToken = default)
    {
        var prompt = defaultValue ? "[Y/n]" : "[y/N]";
        await ShowAsync($"{message} {prompt}", MessageType.Information, cancellationToken);

        Console.Write(_options.InputPrompt);
        var input = await ReadLineAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(input))
            return defaultValue;

        var response = input.Trim().ToLowerInvariant();
        return response.StartsWith("y") || response == "yes";
    }

    private async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        // Basic async console reading - in a real implementation you might want
        // to use a more sophisticated approach that properly handles cancellation
        return await Task.Run(() =>
        {
            try
            {
                return Console.ReadLine();
            }
            catch (Exception)
            {
                return null;
            }
        }, cancellationToken);
    }

    private ConsoleColor GetColorForMessageType(MessageType type)
    {
        return type switch
        {
            MessageType.Information => ConsoleColor.White,
            MessageType.Warning => ConsoleColor.Yellow,
            MessageType.Error => ConsoleColor.Red,
            MessageType.Success => ConsoleColor.Green,
            _ => ConsoleColor.White
        };
    }

    private string GetPrefixForMessageType(MessageType type)
    {
        if (!_options.ShowMessagePrefixes)
            return string.Empty;

        return type switch
        {
            MessageType.Information => "ℹ️  ",
            MessageType.Warning => "⚠️  ",
            MessageType.Error => "❌ ",
            MessageType.Success => "✅ ",
            _ => string.Empty
        };
    }

    private void ShowCodeBlock(string code)
    {
        if (_options.UseColors)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
        }

        Console.WriteLine("```");
        Console.WriteLine(code);
        Console.WriteLine("```");

        if (_options.UseColors)
        {
            Console.ResetColor();
        }
    }

    private void ShowJsonContent(string json)
    {
        try
        {
            // Try to format JSON for better readability
            var formatted = System.Text.Json.JsonSerializer.Serialize(
                System.Text.Json.JsonSerializer.Deserialize<object>(json),
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            ShowCodeBlock(formatted);
        }
        catch
        {
            // If JSON formatting fails, show as-is
            ShowCodeBlock(json);
        }
    }

    private void ShowMarkdownContent(string markdown)
    {
        // Basic markdown rendering for console
        // In a real implementation, you might want to use a proper markdown renderer
        var lines = markdown.Split('\n');

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("# "))
            {
                // Heading 1
                if (_options.UseColors) Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"\n{trimmed.Substring(2).ToUpperInvariant()}\n{new string('=', trimmed.Length - 2)}");
                if (_options.UseColors) Console.ResetColor();
            }
            else if (trimmed.StartsWith("## "))
            {
                // Heading 2
                if (_options.UseColors) Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"\n{trimmed.Substring(3)}\n{new string('-', trimmed.Length - 3)}");
                if (_options.UseColors) Console.ResetColor();
            }
            else if (trimmed.StartsWith("- "))
            {
                // Bullet point
                Console.WriteLine($"  • {trimmed.Substring(2)}");
            }
            else if (trimmed.StartsWith("**") && trimmed.EndsWith("**") && trimmed.Length > 4)
            {
                // Bold text
                if (_options.UseColors) Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(trimmed.Substring(2, trimmed.Length - 4));
                if (_options.UseColors) Console.ResetColor();
            }
            else
            {
                Console.WriteLine(line);
            }
        }
    }
}

/// <summary>
/// Configuration options for console user interface
/// </summary>
public class ConsoleUserInterfaceOptions
{
    /// <summary>
    /// Whether to use console colors
    /// </summary>
    public bool UseColors { get; set; } = true;

    /// <summary>
    /// Whether to show message type prefixes (emoji icons)
    /// </summary>
    public bool ShowMessagePrefixes { get; set; } = true;

    /// <summary>
    /// Prompt string for user input
    /// </summary>
    public string InputPrompt { get; set; } = "> ";

    /// <summary>
    /// Default console UI options
    /// </summary>
    public static ConsoleUserInterfaceOptions Default => new();
}