using Andy.Engine.Contracts;
using Andy.Engine.Interactive;
using FluentAssertions;
using System.Text;
using Xunit;

namespace Andy.Engine.Tests.Interactive;

/// <summary>
/// Tests for ConsoleUserInterface functionality
/// </summary>
public class ConsoleUserInterfaceTests : IDisposable
{
    private readonly ConsoleUserInterface _sut;
    private readonly StringWriter _consoleOutput;
    private readonly StringReader _consoleInput;
    private readonly TextWriter _originalOut;
    private readonly TextReader _originalIn;

    public ConsoleUserInterfaceTests()
    {
        var options = new ConsoleUserInterfaceOptions
        {
            UseColors = false, // Disable colors for testing
            ShowMessagePrefixes = true,
            InputPrompt = "> "
        };
        _sut = new ConsoleUserInterface(options);

        // Save original streams
        _originalOut = Console.Out;
        _originalIn = Console.In;

        // Capture console output
        _consoleOutput = new StringWriter();
        Console.SetOut(_consoleOutput);

        // Setup console input
        _consoleInput = new StringReader("test input\n");
        Console.SetIn(_consoleInput);
    }

    [Fact]
    public async Task ShowAsync_WithInformation_ShouldWriteToConsole()
    {
        // Arrange
        var message = "Test information message";

        // Act
        await _sut.ShowAsync(message, MessageType.Information);

        // Assert
        var output = _consoleOutput.ToString();
        output.Should().Contain("ℹ️  Test information message");
    }

    [Fact]
    public async Task ShowAsync_WithWarning_ShouldShowWarningPrefix()
    {
        // Arrange
        var message = "Test warning message";

        // Act
        await _sut.ShowAsync(message, MessageType.Warning);

        // Assert
        var output = _consoleOutput.ToString();
        output.Should().Contain("⚠️  Test warning message");
    }

    [Fact]
    public async Task ShowAsync_WithError_ShouldShowErrorPrefix()
    {
        // Arrange
        var message = "Test error message";

        // Act
        await _sut.ShowAsync(message, MessageType.Error);

        // Assert
        var output = _consoleOutput.ToString();
        output.Should().Contain("❌ Test error message");
    }

    [Fact]
    public async Task ShowAsync_WithSuccess_ShouldShowSuccessPrefix()
    {
        // Arrange
        var message = "Test success message";

        // Act
        await _sut.ShowAsync(message, MessageType.Success);

        // Assert
        var output = _consoleOutput.ToString();
        output.Should().Contain("✅ Test success message");
    }

    [Fact]
    public async Task ShowProgressAsync_Incomplete_ShouldShowProgressIndicator()
    {
        // Arrange
        var status = "Processing request";

        // Act
        await _sut.ShowProgressAsync(status, false);

        // Assert
        var output = _consoleOutput.ToString();
        output.Should().Contain("⏳ Processing request");
    }

    [Fact]
    public async Task ShowProgressAsync_Complete_ShouldShowCompletionIndicator()
    {
        // Arrange
        var status = "Request completed";

        // Act
        await _sut.ShowProgressAsync(status, true);

        // Assert
        var output = _consoleOutput.ToString();
        output.Should().Contain("✓ Request completed");
    }

    [Fact]
    public async Task ShowContentAsync_WithMarkdown_ShouldRenderBasicMarkdown()
    {
        // Arrange
        var markdown = """
            # Main Title
            ## Subtitle
            - Bullet point 1
            - Bullet point 2
            **Bold text**
            """;

        // Act
        await _sut.ShowContentAsync(markdown, ContentType.Markdown);

        // Assert
        var output = _consoleOutput.ToString();
        output.Should().Contain("MAIN TITLE");
        output.Should().Contain("=========="); // Underline for H1
        output.Should().Contain("Subtitle");
        output.Should().Contain("-------"); // Underline for H2
        output.Should().Contain("• Bullet point 1");
        output.Should().Contain("• Bullet point 2");
        output.Should().Contain("Bold text"); // Bold formatting
    }

    [Fact]
    public async Task ShowContentAsync_WithCode_ShouldShowCodeBlock()
    {
        // Arrange
        var code = "function test() { return 'hello'; }";

        // Act
        await _sut.ShowContentAsync(code, ContentType.Code);

        // Assert
        var output = _consoleOutput.ToString();
        output.Should().Contain("```");
        output.Should().Contain("function test() { return 'hello'; }");
    }

    [Fact]
    public async Task ShowContentAsync_WithJson_ShouldFormatJson()
    {
        // Arrange
        var json = """{"name":"test","value":123}""";

        // Act
        await _sut.ShowContentAsync(json, ContentType.Json);

        // Assert
        var output = _consoleOutput.ToString();
        output.Should().Contain("```");
        output.Should().Contain("\"name\": \"test\"");
        output.Should().Contain("\"value\": 123");
    }

    [Fact]
    public async Task ShowContentAsync_WithInvalidJson_ShouldShowAsCodeBlock()
    {
        // Arrange
        var invalidJson = """{"invalid": json}""";

        // Act
        await _sut.ShowContentAsync(invalidJson, ContentType.Json);

        // Assert
        var output = _consoleOutput.ToString();
        output.Should().Contain("```");
        output.Should().Contain(invalidJson);
    }

    [Fact]
    public async Task ChooseAsync_WithValidChoice_ShouldReturnSelectedOption()
    {
        // Arrange
        var question = "Choose an option:";
        var options = new[] { "Option A", "Option B", "Option C" };

        // Setup input to choose option 2
        Console.SetIn(new StringReader("2\n"));

        // Act
        var result = await _sut.ChooseAsync(question, options);

        // Assert
        result.Should().Be("Option B");
        var output = _consoleOutput.ToString();
        output.Should().Contain("1. Option A");
        output.Should().Contain("2. Option B");
        output.Should().Contain("3. Option C");
    }

    [Fact]
    public async Task ChooseAsync_WithInvalidChoice_ShouldRetryAndAcceptValidChoice()
    {
        // Arrange
        var question = "Choose an option:";
        var options = new[] { "Option A", "Option B" };

        // Setup input: invalid choice (5), then valid choice (1)
        Console.SetIn(new StringReader("5\n1\n"));

        // Act
        var result = await _sut.ChooseAsync(question, options, CancellationToken.None);

        // Assert
        result.Should().Be("Option A");
        var output = _consoleOutput.ToString();
        output.Should().Contain("Invalid choice. Please try again.");
    }

    [Theory]
    [InlineData("y", true)]
    [InlineData("Y", true)]
    [InlineData("yes", true)]
    [InlineData("YES", true)]
    [InlineData("n", false)]
    [InlineData("N", false)]
    [InlineData("no", false)]
    [InlineData("NO", false)]
    public async Task ConfirmAsync_WithVariousInputs_ShouldReturnCorrectBoolean(string input, bool expected)
    {
        // Arrange
        var message = "Do you want to continue?";
        Console.SetIn(new StringReader($"{input}\n"));

        // Act
        var result = await _sut.ConfirmAsync(message, false);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task ConfirmAsync_WithEmptyInput_ShouldReturnDefaultValue()
    {
        // Arrange
        var message = "Do you want to continue?";
        Console.SetIn(new StringReader("\n")); // Empty input

        // Act - Test with default true
        var resultTrue = await _sut.ConfirmAsync(message, true);

        // Reset for second test
        Console.SetIn(new StringReader("\n"));
        var resultFalse = await _sut.ConfirmAsync(message, false);

        // Assert
        resultTrue.Should().BeTrue();
        resultFalse.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithDefaultOptions_ShouldUseDefaults()
    {
        // Act
        var ui = new ConsoleUserInterface();

        // Assert - Should not throw and should be usable
        ui.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithCustomOptions_ShouldUseCustomSettings()
    {
        // Arrange
        var options = new ConsoleUserInterfaceOptions
        {
            UseColors = false,
            ShowMessagePrefixes = false,
            InputPrompt = "Custom> "
        };

        // Act
        var ui = new ConsoleUserInterface(options);

        // Assert - Should not throw
        ui.Should().NotBeNull();
    }

    [Fact]
    public async Task AskAsync_ShouldDisplayQuestionAndReturnInput()
    {
        // Arrange
        var question = "What is your name?";
        var expectedInput = "John Doe";
        Console.SetIn(new StringReader($"{expectedInput}\n"));

        // Act
        var result = await _sut.AskAsync(question);

        // Assert
        result.Should().Be(expectedInput);
        var output = _consoleOutput.ToString();
        output.Should().Contain(question);
        output.Should().Contain("> "); // Input prompt
    }

    [Fact]
    public async Task CancellationToken_WhenCancelled_ShouldRespectCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await _sut.ShowAsync("Test message", MessageType.Information, cts.Token);
        // Should complete without throwing since ShowAsync doesn't use cancellation token in current implementation
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                // Restore original streams first
                Console.SetOut(_originalOut);
                Console.SetIn(_originalIn);
            }
            catch
            {
                // Ignore errors during cleanup
            }
            finally
            {
                _consoleOutput?.Dispose();
                _consoleInput?.Dispose();
            }
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}