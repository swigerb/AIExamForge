using AIExamForge.Application.Validation;
using AIExamForge.Domain.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace AIExamForge.UnitTests.Application.Validation;

public sealed class SchemaValidatorTests
{
    private static SchemaValidator CreateSut(Mock<IItemTypeRegistry> registry, Mock<ILogger<SchemaValidator>> logger)
        => new(registry.Object, logger.Object);

    [Fact]
    public async Task ValidateQuestionAsync_WhenBothSchemasValid_ReturnsSuccess()
    {
        var registry = new Mock<IItemTypeRegistry>(MockBehavior.Strict);
        var logger = new Mock<ILogger<SchemaValidator>>();

        registry
            .Setup(r => r.GetBaseQuestionSchema())
            .Returns("""
                {
                  "type": "object",
                  "properties": { "x": { "type": "string" } },
                  "required": ["x"]
                }
                """);

        registry
            .Setup(r => r.GetSchema("M1"))
            .Returns("""
                {
                  "type": "object",
                  "properties": { "y": { "type": "string" } },
                  "required": ["y"]
                }
                """);

        var sut = CreateSut(registry, logger);

        var result = await sut.ValidateQuestionAsync("{\"x\":\"a\",\"y\":\"b\"}", "M1");

        result.IsValid.Should().BeTrue();
        result.SchemaErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateQuestionAsync_WhenBaseSchemaFails_ReturnsSchemaErrors()
    {
        var registry = new Mock<IItemTypeRegistry>(MockBehavior.Strict);
        var logger = new Mock<ILogger<SchemaValidator>>();

        registry
            .Setup(r => r.GetBaseQuestionSchema())
            .Returns("""
                {
                  "type": "object",
                  "properties": { "x": { "type": "string" } },
                  "required": ["x"]
                }
                """);

        registry
            .Setup(r => r.GetSchema("M1"))
            .Returns("""
                {
                  "type": "object",
                  "properties": { "y": { "type": "string" } },
                  "required": ["y"]
                }
                """);

        var sut = CreateSut(registry, logger);

        var result = await sut.ValidateQuestionAsync("{\"y\":\"b\"}", "M1");

        result.IsValid.Should().BeFalse();
        result.SchemaErrors.Should().Contain(e => e.SchemaKeyword == "PropertyRequired");
    }

    [Fact]
    public async Task ValidateQuestionAsync_WhenItemTypeSchemaUnavailable_ReturnsFailureWithMessage()
    {
        var registry = new Mock<IItemTypeRegistry>(MockBehavior.Strict);
        var logger = new Mock<ILogger<SchemaValidator>>();

        registry
            .Setup(r => r.GetBaseQuestionSchema())
            .Returns("{ \"type\": \"object\" }");

        registry
            .Setup(r => r.GetSchema("M1"))
            .Throws(new InvalidOperationException("no schema"));

        var sut = CreateSut(registry, logger);

        var result = await sut.ValidateQuestionAsync("{}", "M1");

        result.IsValid.Should().BeFalse();
        result.SchemaErrors.Should().ContainSingle(e => e.Message.Contains("Schema not available for item type: M1"));
    }

    [Fact]
    public async Task ValidateBaseSchemaAsync_WhenBaseSchemaUnavailable_ReturnsFailureWithMessage()
    {
        var registry = new Mock<IItemTypeRegistry>(MockBehavior.Strict);
        var logger = new Mock<ILogger<SchemaValidator>>();

        registry
            .Setup(r => r.GetBaseQuestionSchema())
            .Throws(new InvalidOperationException("no base schema"));

        var sut = CreateSut(registry, logger);

        var result = await sut.ValidateBaseSchemaAsync("{}");

        result.IsValid.Should().BeFalse();
        result.SchemaErrors.Should().ContainSingle(e => e.Message.Contains("Base question schema not available"));
    }

    [Fact]
    public async Task ValidateItemTypeSchemaAsync_WhenSchemaFails_PrefixesMessageWithItemType()
    {
        var registry = new Mock<IItemTypeRegistry>(MockBehavior.Strict);
        var logger = new Mock<ILogger<SchemaValidator>>();

        registry
            .Setup(r => r.GetSchema("M1"))
            .Returns("""
                {
                  "type": "object",
                  "properties": { "y": { "type": "string" } },
                  "required": ["y"]
                }
                """);

        var sut = CreateSut(registry, logger);

        var result = await sut.ValidateItemTypeSchemaAsync("{}", "M1");

        result.IsValid.Should().BeFalse();
        result.SchemaErrors.Should().NotBeEmpty();
        result.SchemaErrors.Should().AllSatisfy(e => e.Message.Should().StartWith("[M1] "));
    }
}
