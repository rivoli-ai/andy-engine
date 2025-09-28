using Andy.Engine.Planner;
using Andy.Engine.Contracts;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json.Nodes;
using Xunit;

namespace Andy.Engine.Tests.Planner;

public class LlmPlannerResponseParsingTests
{
    [Fact]
    public void ParseDecision_WithStandardActionFormat_ShouldParseCorrectly()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<LlmPlanner>>();
        var planner = new LlmPlanner(
            Mock.Of<Andy.Model.Llm.ILlmProvider>(),
            Mock.Of<Andy.Tools.Core.IToolRegistry>(),
            new PlannerOptions(),
            mockLogger.Object
        );

        var response = JsonNode.Parse("""
            {"action": "call_tool", "name": "datetime_tool", "args": {"operation": "now"}}
            """);

        // Act
        var decision = planner.ParseDecision(response!);

        // Assert
        decision.Should().BeOfType<CallToolDecision>();
        var callDecision = (CallToolDecision)decision;
        callDecision.Call.ToolName.Should().Be("datetime_tool");
        callDecision.Call.Args["operation"]?.GetValue<string>().Should().Be("now");
    }

    [Fact]
    public void ParseDecision_WithAlternativeCallToolFormat_ShouldParseCorrectly()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<LlmPlanner>>();
        var planner = new LlmPlanner(
            Mock.Of<Andy.Model.Llm.ILlmProvider>(),
            Mock.Of<Andy.Tools.Core.IToolRegistry>(),
            new PlannerOptions(),
            mockLogger.Object
        );

        // This is the format the LLM was actually returning
        var response = JsonNode.Parse("""
            {"call_tool":{"name":"Date Time Tool","args":{}}}
            """);

        // Act
        var decision = planner.ParseDecision(response!);

        // Assert
        decision.Should().BeOfType<CallToolDecision>();
        var callDecision = (CallToolDecision)decision;
        callDecision.Call.ToolName.Should().Be("datetime_tool");
        callDecision.Call.Args["operation"]?.GetValue<string>().Should().Be("now");
    }

    [Fact]
    public void ParseDecision_WithEncodingToolAlternativeFormat_ShouldMapCorrectly()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<LlmPlanner>>();
        var planner = new LlmPlanner(
            Mock.Of<Andy.Model.Llm.ILlmProvider>(),
            Mock.Of<Andy.Tools.Core.IToolRegistry>(),
            new PlannerOptions(),
            mockLogger.Object
        );

        var response = JsonNode.Parse("""
            {"call_tool":{"name":"Encoding Tool","args":{"operation":"base64_encode","input":"test"}}}
            """);

        // Act
        var decision = planner.ParseDecision(response!);

        // Assert
        decision.Should().BeOfType<CallToolDecision>();
        var callDecision = (CallToolDecision)decision;
        callDecision.Call.ToolName.Should().Be("encoding_tool");
        callDecision.Call.Args["operation"]?.GetValue<string>().Should().Be("base64_encode");
        callDecision.Call.Args["input"]?.GetValue<string>().Should().Be("test");
    }

    [Fact]
    public void ParseDecision_WithStopAction_ShouldParseCorrectly()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<LlmPlanner>>();
        var planner = new LlmPlanner(
            Mock.Of<Andy.Model.Llm.ILlmProvider>(),
            Mock.Of<Andy.Tools.Core.IToolRegistry>(),
            new PlannerOptions(),
            mockLogger.Object
        );

        var response = JsonNode.Parse("""
            {"action": "stop", "reason": "Task completed"}
            """);

        // Act
        var decision = planner.ParseDecision(response!);

        // Assert
        decision.Should().BeOfType<StopDecision>();
        var stopDecision = (StopDecision)decision;
        stopDecision.Reason.Should().Be("Task completed");
    }

    [Fact]
    public void ParseDecision_WithMissingActionAndNoFallback_ShouldThrow()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<LlmPlanner>>();
        var planner = new LlmPlanner(
            Mock.Of<Andy.Model.Llm.ILlmProvider>(),
            Mock.Of<Andy.Tools.Core.IToolRegistry>(),
            new PlannerOptions(),
            mockLogger.Object
        );

        var response = JsonNode.Parse("""
            {"invalid": "format"}
            """);

        // Act & Assert
        var act = () => planner.ParseDecision(response!);
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("Missing 'action' field in response: *");
    }

    [Fact]
    public void ParseDecision_WithGenericToolNameMapping_ShouldNormalizeCorrectly()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<LlmPlanner>>();
        var planner = new LlmPlanner(
            Mock.Of<Andy.Model.Llm.ILlmProvider>(),
            Mock.Of<Andy.Tools.Core.IToolRegistry>(),
            new PlannerOptions(),
            mockLogger.Object
        );

        var response = JsonNode.Parse("""
            {"call_tool":{"name":"Some Custom Tool","args":{"param":"value"}}}
            """);

        // Act
        var decision = planner.ParseDecision(response!);

        // Assert
        decision.Should().BeOfType<CallToolDecision>();
        var callDecision = (CallToolDecision)decision;
        callDecision.Call.ToolName.Should().Be("some_custom_tool");
        callDecision.Call.Args["param"]?.GetValue<string>().Should().Be("value");
    }
}