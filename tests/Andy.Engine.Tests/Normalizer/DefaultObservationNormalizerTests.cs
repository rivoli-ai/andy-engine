using Andy.Engine.Contracts;
using Andy.Engine.Normalizer;
using FluentAssertions;
using System.Text.Json.Nodes;
using Xunit;

namespace Andy.Engine.Tests.Normalizer;

public class DefaultObservationNormalizerTests
{
    private readonly DefaultObservationNormalizer _sut;

    public DefaultObservationNormalizerTests()
    {
        _sut = new DefaultObservationNormalizer();
    }

    [Fact]
    public void Normalize_WithSuccessfulResult_ShouldReturnCorrectSummary()
    {
        // Arrange
        var toolName = "test_tool";
        var raw = JsonNode.Parse("""{"result": "success", "value": 42}""");
        var result = new ToolResult(
            Ok: true,
            Data: raw,
            ErrorCode: ToolErrorCode.None,
            Latency: TimeSpan.FromMilliseconds(100),
            Attempt: 1
        );

        // Act
        var observation = _sut.Normalize(toolName, raw, result);

        // Assert
        observation.Should().NotBeNull();
        observation.Summary.Should().Be($"Tool '{toolName}' executed successfully");
        observation.Raw.Should().Be(result);
    }

    [Fact]
    public void Normalize_WithFailedResult_ShouldReturnErrorSummary()
    {
        // Arrange
        var toolName = "test_tool";
        var result = new ToolResult(
            Ok: false,
            Data: null,
            ErrorCode: ToolErrorCode.InvalidInput,
            ErrorDetails: "Invalid parameter",
            Latency: TimeSpan.FromMilliseconds(50),
            Attempt: 1
        );

        // Act
        var observation = _sut.Normalize(toolName, null, result);

        // Assert
        observation.Summary.Should().Be($"Tool '{toolName}' failed: InvalidInput - Invalid parameter");
    }

    [Fact]
    public void Normalize_WithNullData_ShouldReturnNoDataSummary()
    {
        // Arrange
        var toolName = "test_tool";
        var result = new ToolResult(
            Ok: true,
            Data: null,
            ErrorCode: ToolErrorCode.None,
            Latency: TimeSpan.FromMilliseconds(100),
            Attempt: 1
        );

        // Act
        var observation = _sut.Normalize(toolName, null, result);

        // Assert
        observation.Summary.Should().Be($"Tool '{toolName}' completed with no data");
    }

    [Fact]
    public void Normalize_ShouldExtractKeyFacts()
    {
        // Arrange
        var raw = JsonNode.Parse("""
            {
                "name": "test",
                "value": 123,
                "nested": {
                    "property": "value"
                }
            }
            """);
        var result = new ToolResult(
            Ok: true,
            Data: raw,
            Latency: TimeSpan.FromMilliseconds(150),
            Attempt: 2
        );

        // Act
        var observation = _sut.Normalize("test_tool", raw, result);

        // Assert
        observation.KeyFacts.Should().ContainKey("execution_time_ms");
        observation.KeyFacts["execution_time_ms"].Should().Be("150.00");
        observation.KeyFacts.Should().ContainKey("attempt");
        observation.KeyFacts["attempt"].Should().Be("2");
        observation.KeyFacts.Should().ContainKey("name");
        observation.KeyFacts["name"].Should().Be("\"test\"");
    }

    [Fact]
    public void Normalize_WithError_ShouldDetermineRetryAffordances()
    {
        // Arrange
        var result = new ToolResult(
            Ok: false,
            Data: null,
            ErrorCode: ToolErrorCode.Timeout,
            ErrorDetails: "Request timed out",
            SchemaValidated: false,
            Attempt: 1,
            Latency: TimeSpan.FromSeconds(30)
        );

        // Act
        var observation = _sut.Normalize("test_tool", null, result);

        // Assert
        observation.Affordances.Should().Contain("retry_with_backoff");
    }

    [Fact]
    public void Normalize_WithInvalidInput_ShouldDetermineFixAffordances()
    {
        // Arrange
        var result = new ToolResult(
            Ok: false,
            Data: null,
            ErrorCode: ToolErrorCode.InvalidInput,
            ErrorDetails: "Missing required field",
            SchemaValidated: false,
            Attempt: 1,
            Latency: TimeSpan.FromMilliseconds(10)
        );

        // Act
        var observation = _sut.Normalize("test_tool", null, result);

        // Assert
        observation.Affordances.Should().Contain("fix_parameters");
        observation.Affordances.Should().Contain("ask_user_for_clarification");
    }

    [Fact]
    public void Normalize_WithPaginatedResult_ShouldDeterminePaginationAffordances()
    {
        // Arrange
        var raw = JsonNode.Parse("""
            {
                "results": [1, 2, 3],
                "next_page": "token123",
                "has_more": true
            }
            """);
        var result = new ToolResult(
            Ok: true,
            Data: raw,
            Latency: TimeSpan.FromMilliseconds(200),
            Attempt: 1
        );

        // Act
        var observation = _sut.Normalize("test_tool", raw, result);

        // Assert
        observation.Affordances.Should().Contain("fetch_next_page");
        observation.Affordances.Should().Contain("fetch_more_results");
        observation.Affordances.Should().Contain("process_results");
    }

    [Fact]
    public void Normalize_WithArrayResult_ShouldExtractCount()
    {
        // Arrange
        var raw = JsonNode.Parse("""[{"id": 1}, {"id": 2}, {"id": 3}]""");
        var result = new ToolResult(
            Ok: true,
            Data: raw,
            Latency: TimeSpan.FromMilliseconds(100),
            Attempt: 1
        );

        // Act
        var observation = _sut.Normalize("test_tool", raw, result);

        // Assert
        observation.KeyFacts.Should().ContainKey("result_count");
        observation.KeyFacts["result_count"].Should().Be("3");
        observation.KeyFacts.Should().ContainKey("first_id");
        observation.KeyFacts["first_id"].Should().Be("1");
    }

    [Fact]
    public void Normalize_AlwaysIncludesCommonAffordances()
    {
        // Arrange
        var result = new ToolResult(
            Ok: true,
            Data: JsonNode.Parse("""{"simple": "result"}"""),
            Latency: TimeSpan.FromMilliseconds(50),
            Attempt: 1
        );

        // Act
        var observation = _sut.Normalize("test_tool", result.Data, result);

        // Assert
        observation.Affordances.Should().Contain("use_different_tool");
        observation.Affordances.Should().Contain("ask_user_for_guidance");
    }
}