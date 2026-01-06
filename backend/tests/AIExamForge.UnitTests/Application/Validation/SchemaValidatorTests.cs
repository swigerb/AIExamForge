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
                                    "properties": { "x": { "type": "string" }, "content": { "type": "object" } },
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

        var result = await sut.ValidateQuestionAsync("{\"x\":\"a\",\"content\":{\"y\":\"b\"}}", "M1");

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
                                    "properties": { "x": { "type": "string" }, "content": { "type": "object" } },
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

        var result = await sut.ValidateQuestionAsync("{\"content\":{\"y\":\"b\"}}", "M1");

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
            .Returns("{ \"type\": \"object\", \"properties\": { \"content\": { \"type\": \"object\" } } }");

        registry
            .Setup(r => r.GetSchema("M1"))
            .Throws(new InvalidOperationException("no schema"));

        var sut = CreateSut(registry, logger);

        var result = await sut.ValidateQuestionAsync("{\"content\":{}}", "M1");

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

    [Fact]
    public async Task ValidateQuestionAsync_WhenContentOnlyJson_SkipsBaseSchemaAndValidatesItemType()
    {
        var registry = new Mock<IItemTypeRegistry>(MockBehavior.Strict);
        var logger = new Mock<ILogger<SchemaValidator>>();

        // Base schema should NOT be called for content-only JSON.
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

        result.IsValid.Should().BeTrue();
        result.SchemaErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateItemTypeSchemaAsync_WhenSchemaHasAiexamforgeRefs_ResolvesLocally()
    {
        var registry = new Mock<IItemTypeRegistry>(MockBehavior.Strict);
        var logger = new Mock<ILogger<SchemaValidator>>();

        static string FindM1SchemaPath()
        {
            const int maxParents = 15;
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            for (var i = 0; i < maxParents && current != null; i++)
            {
                var candidate = Path.Combine(current.FullName, "docs", "schemas", "common", "item-types", "M1.schema.json");
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                current = current.Parent;
            }

            throw new FileNotFoundException("Could not locate docs/schemas/common/item-types/M1.schema.json from the test base directory.");
        }

        var m1SchemaPath = FindM1SchemaPath();
        registry
            .Setup(r => r.GetSchema("M1"))
            .Returns(File.ReadAllText(m1SchemaPath));

        var sut = CreateSut(registry, logger);

        var json = """
        {
          "choices": [
            { "key": "A", "text": "A" },
            { "key": "B", "text": "B" },
            { "key": "C", "text": "C" }
          ],
          "correctChoiceKey": "A",
          "randomizeChoices": true
        }
        """;

        var result = await sut.ValidateItemTypeSchemaAsync(json, "M1");

        result.IsValid.Should().BeTrue();
        result.SchemaErrors.Should().BeEmpty();
    }
}
