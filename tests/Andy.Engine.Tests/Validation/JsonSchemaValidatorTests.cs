using Andy.Engine.Validation;
using FluentAssertions;
using System.Text.Json.Nodes;
using Xunit;

namespace Andy.Engine.Tests.Validation;

public class JsonSchemaValidatorTests
{
    private readonly JsonSchemaValidator _sut;

    public JsonSchemaValidatorTests()
    {
        _sut = new JsonSchemaValidator();
    }

    [Fact]
    public void Validate_ValidData_ShouldReturnSuccess()
    {
        // Arrange
        var schema = JsonNode.Parse("""
            {
                "type": "object",
                "properties": {
                    "name": { "type": "string" },
                    "age": { "type": "number" }
                },
                "required": ["name"]
            }
            """);

        var data = JsonNode.Parse("""
            {
                "name": "John",
                "age": 30
            }
            """);

        // Act
        var (isValid, error) = _sut.Validate(data!, schema!);

        // Assert
        isValid.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public void Validate_MissingRequiredProperty_ShouldReturnError()
    {
        // Arrange
        var schema = JsonNode.Parse("""
            {
                "type": "object",
                "properties": {
                    "name": { "type": "string" },
                    "age": { "type": "number" }
                },
                "required": ["name", "age"]
            }
            """);

        var data = JsonNode.Parse("""
            {
                "name": "John"
            }
            """);

        // Act
        var (isValid, error) = _sut.Validate(data!, schema!);

        // Assert
        isValid.Should().BeFalse();
        error.Should().NotBeNull();
        error.Should().Contain("age");
    }

    [Fact]
    public void Validate_WrongType_ShouldReturnError()
    {
        // Arrange
        var schema = JsonNode.Parse("""
            {
                "type": "object",
                "properties": {
                    "count": { "type": "number" }
                }
            }
            """);

        var data = JsonNode.Parse("""
            {
                "count": "not a number"
            }
            """);

        // Act
        var (isValid, error) = _sut.Validate(data!, schema!);

        // Assert
        isValid.Should().BeFalse();
        error.Should().NotBeNull();
    }

    [Fact]
    public void ValidateAndNormalize_ValidData_ShouldNormalizeTypes()
    {
        // Arrange
        var schema = JsonNode.Parse("""
            {
                "type": "object",
                "properties": {
                    "id": { "type": "integer" },
                    "active": { "type": "boolean" },
                    "score": { "type": "number" }
                }
            }
            """);

        var data = JsonNode.Parse("""
            {
                "id": "123",
                "active": "true",
                "score": "45.5"
            }
            """);

        // Act
        var (isValid, error, normalized) = _sut.ValidateAndNormalize(data!, schema!);

        // Assert
        isValid.Should().BeTrue();
        error.Should().BeNull();
        normalized.Should().NotBeNull();
        normalized!["id"]!.GetValue<int>().Should().Be(123);
        normalized!["active"]!.GetValue<bool>().Should().BeTrue();
        normalized!["score"]!.GetValue<double>().Should().Be(45.5);
    }

    [Fact]
    public void ValidateAndNormalize_ArrayData_ShouldValidateItems()
    {
        // Arrange
        var schema = JsonNode.Parse("""
            {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": {
                        "id": { "type": "number" }
                    },
                    "required": ["id"]
                }
            }
            """);

        var data = JsonNode.Parse("""
            [
                { "id": 1 },
                { "id": 2 },
                { "id": 3 }
            ]
            """);

        // Act
        var (isValid, error, normalized) = _sut.ValidateAndNormalize(data!, schema!);

        // Assert
        isValid.Should().BeTrue();
        error.Should().BeNull();
        normalized.Should().NotBeNull();
        var array = normalized as JsonArray;
        array.Should().HaveCount(3);
    }

    [Fact]
    public void ValidateAndNormalize_NestedObject_ShouldValidateRecursively()
    {
        // Arrange
        var schema = JsonNode.Parse("""
            {
                "type": "object",
                "properties": {
                    "user": {
                        "type": "object",
                        "properties": {
                            "name": { "type": "string" },
                            "email": { "type": "string" }
                        },
                        "required": ["name"]
                    }
                }
            }
            """);

        var data = JsonNode.Parse("""
            {
                "user": {
                    "name": "Alice",
                    "email": "alice@example.com"
                }
            }
            """);

        // Act
        var (isValid, error, normalized) = _sut.ValidateAndNormalize(data!, schema!);

        // Assert
        isValid.Should().BeTrue();
        error.Should().BeNull();
        normalized!["user"]!["name"]!.GetValue<string>().Should().Be("Alice");
    }

    [Fact]
    public void ValidateAndNormalize_WithAdditionalProperties_ShouldKeepThem()
    {
        // Arrange
        var schema = JsonNode.Parse("""
            {
                "type": "object",
                "properties": {
                    "id": { "type": "number" }
                },
                "additionalProperties": true
            }
            """);

        var data = JsonNode.Parse("""
            {
                "id": 1,
                "extra": "value",
                "another": true
            }
            """);

        // Act
        var (isValid, error, normalized) = _sut.ValidateAndNormalize(data!, schema!);

        // Assert
        isValid.Should().BeTrue();
        normalized!["extra"]!.GetValue<string>().Should().Be("value");
        normalized!["another"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact]
    public void ValidateAndNormalize_StringToNumberCoercion_ShouldWork()
    {
        // Arrange
        var schema = JsonNode.Parse("""
            {
                "type": "object",
                "properties": {
                    "temperature": { "type": "number" },
                    "count": { "type": "integer" }
                }
            }
            """);

        var data = JsonNode.Parse("""
            {
                "temperature": "98.6",
                "count": "42"
            }
            """);

        // Act
        var (isValid, error, normalized) = _sut.ValidateAndNormalize(data!, schema!);

        // Assert
        isValid.Should().BeTrue();
        normalized!["temperature"]!.GetValue<double>().Should().BeApproximately(98.6, 0.01);
        normalized!["count"]!.GetValue<int>().Should().Be(42);
    }

    [Fact]
    public void Validate_NullData_ShouldReturnError()
    {
        // Arrange
        var schema = JsonNode.Parse("""{"type": "object"}""");

        // Act
        var (isValid, error) = _sut.Validate(null!, schema!);

        // Assert
        isValid.Should().BeFalse();
        error.Should().Contain("null");
    }

    [Fact]
    public void Validate_NullSchema_ShouldReturnError()
    {
        // Arrange
        var data = JsonNode.Parse("""{"test": "value"}""");

        // Act
        var (isValid, error) = _sut.Validate(data!, null!);

        // Assert
        isValid.Should().BeFalse();
        error.Should().Contain("Schema is null");
    }
}