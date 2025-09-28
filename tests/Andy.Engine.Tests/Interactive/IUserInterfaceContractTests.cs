using Andy.Engine.Contracts;
using Andy.Engine.Interactive;
using FluentAssertions;
using Xunit;

namespace Andy.Engine.Tests.Interactive;

/// <summary>
/// Contract tests for IUserInterface implementations to ensure they behave consistently
/// </summary>
public class IUserInterfaceContractTests
{
    [Theory]
    [MemberData(nameof(GetUserInterfaceImplementations))]
    public async Task ShowAsync_WithValidMessage_ShouldNotThrow(IUserInterface userInterface)
    {
        // Act & Assert
        var action = async () => await userInterface.ShowAsync("Test message", MessageType.Information);
        await action.Should().NotThrowAsync();
    }

    [Theory]
    [MemberData(nameof(GetUserInterfaceImplementations))]
    public async Task ShowAsync_WithAllMessageTypes_ShouldNotThrow(IUserInterface userInterface)
    {
        // Arrange
        var messageTypes = Enum.GetValues<MessageType>();

        // Act & Assert
        foreach (var messageType in messageTypes)
        {
            var action = async () => await userInterface.ShowAsync($"Test {messageType} message", messageType);
            await action.Should().NotThrowAsync();
        }
    }

    [Theory]
    [MemberData(nameof(GetUserInterfaceImplementations))]
    public async Task ShowProgressAsync_WithValidStatus_ShouldNotThrow(IUserInterface userInterface)
    {
        // Act & Assert
        var action = async () => await userInterface.ShowProgressAsync("Processing...", false);
        await action.Should().NotThrowAsync();

        var actionComplete = async () => await userInterface.ShowProgressAsync("Completed!", true);
        await actionComplete.Should().NotThrowAsync();
    }

    [Theory]
    [MemberData(nameof(GetUserInterfaceImplementations))]
    public async Task ShowContentAsync_WithAllContentTypes_ShouldNotThrow(IUserInterface userInterface)
    {
        // Arrange
        var contentTypes = Enum.GetValues<ContentType>();
        var testContent = "Test content";

        // Act & Assert
        foreach (var contentType in contentTypes)
        {
            var action = async () => await userInterface.ShowContentAsync(testContent, contentType);
            await action.Should().NotThrowAsync();
        }
    }

    [Theory]
    [MemberData(nameof(GetUserInterfaceImplementations))]
    public async Task ShowContentAsync_WithMarkdown_ShouldHandleComplexMarkdown(IUserInterface userInterface)
    {
        // Arrange
        var complexMarkdown = """
            # Main Title
            ## Subtitle

            - Bullet point 1
            - Bullet point 2

            **Bold text** and *italic text*

            ```code
            function test() {
                return "hello";
            }
            ```

            > Blockquote

            [Link](https://example.com)
            """;

        // Act & Assert
        var action = async () => await userInterface.ShowContentAsync(complexMarkdown, ContentType.Markdown);
        await action.Should().NotThrowAsync();
    }

    [Theory]
    [MemberData(nameof(GetUserInterfaceImplementations))]
    public async Task ShowContentAsync_WithJson_ShouldHandleValidAndInvalidJson(IUserInterface userInterface)
    {
        // Arrange
        var validJson = """{"name": "test", "value": 123, "nested": {"property": "value"}}""";
        var invalidJson = """{invalid: json}""";

        // Act & Assert
        var validAction = async () => await userInterface.ShowContentAsync(validJson, ContentType.Json);
        await validAction.Should().NotThrowAsync();

        var invalidAction = async () => await userInterface.ShowContentAsync(invalidJson, ContentType.Json);
        await invalidAction.Should().NotThrowAsync();
    }

    [Theory]
    [MemberData(nameof(GetUserInterfaceImplementations))]
    public async Task ShowContentAsync_WithCode_ShouldHandleMultilineCode(IUserInterface userInterface)
    {
        // Arrange
        var multilineCode = """
            using System;

            public class TestClass
            {
                public void Method()
                {
                    Console.WriteLine("Hello, World!");
                }
            }
            """;

        // Act & Assert
        var action = async () => await userInterface.ShowContentAsync(multilineCode, ContentType.Code);
        await action.Should().NotThrowAsync();
    }

    [Theory]
    [MemberData(nameof(GetUserInterfaceImplementations))]
    public async Task ShowAsync_WithEmptyMessage_ShouldNotThrow(IUserInterface userInterface)
    {
        // Act & Assert
        var action = async () => await userInterface.ShowAsync("", MessageType.Information);
        await action.Should().NotThrowAsync();
    }

    [Theory]
    [MemberData(nameof(GetUserInterfaceImplementations))]
    public async Task ShowAsync_WithNullMessage_ShouldNotThrow(IUserInterface userInterface)
    {
        // Act & Assert
        var action = async () => await userInterface.ShowAsync(null!, MessageType.Information);
        await action.Should().NotThrowAsync();
    }

    [Theory]
    [MemberData(nameof(GetUserInterfaceImplementations))]
    public async Task ShowProgressAsync_WithEmptyStatus_ShouldNotThrow(IUserInterface userInterface)
    {
        // Act & Assert
        var action = async () => await userInterface.ShowProgressAsync("", false);
        await action.Should().NotThrowAsync();
    }

    [Theory]
    [MemberData(nameof(GetUserInterfaceImplementations))]
    public async Task ShowContentAsync_WithEmptyContent_ShouldNotThrow(IUserInterface userInterface)
    {
        // Act & Assert
        var action = async () => await userInterface.ShowContentAsync("", ContentType.Markdown);
        await action.Should().NotThrowAsync();
    }

    [Theory]
    [MemberData(nameof(GetUserInterfaceImplementations))]
    public async Task CancellationToken_WhenNotCancelled_ShouldComplete(IUserInterface userInterface)
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        // Act & Assert
        var action = async () => await userInterface.ShowAsync("Test message", MessageType.Information, cts.Token);
        await action.Should().NotThrowAsync();
    }

    public static IEnumerable<object[]> GetUserInterfaceImplementations()
    {
        // Test with different console UI configurations
        yield return new object[] { new ConsoleUserInterface() };

        yield return new object[] { new ConsoleUserInterface(new ConsoleUserInterfaceOptions
        {
            UseColors = false,
            ShowMessagePrefixes = false,
            InputPrompt = ">> "
        })};

        yield return new object[] { new ConsoleUserInterface(new ConsoleUserInterfaceOptions
        {
            UseColors = true,
            ShowMessagePrefixes = true,
            InputPrompt = "Custom> "
        })};
    }
}