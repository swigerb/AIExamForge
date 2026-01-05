using AIExamForge.Domain.Entities;
using AIExamForge.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AIExamForge.UnitTests.Infrastructure;

public sealed class EntityFrameworkModelTests
{
    [Fact]
    public void DbContextModel_ShouldBuild_AndIncludeExpectedMappings()
    {
        var options = new DbContextOptionsBuilder<AIExamForgeDbContext>()
            .UseInMemoryDatabase(databaseName: $"AIExamForge-Test-{Guid.NewGuid()}")
            .Options;

        using var db = new AIExamForgeDbContext(options);

        // Forces EF Core model creation; this is where the earlier runtime failures occurred.
        var model = db.Model;

        // Question entity should exist
        var questionEntity = model.FindEntityType(typeof(Question));
        questionEntity.Should().NotBeNull();

        // The ItemTypeCode column is backed by the CLR property `ItemType`.
        // Ensure we have an index over the CLR property (regression for HasIndex("ItemTypeCode")).
        var itemTypeIndexExists = questionEntity!
            .GetIndexes()
            .Any(i => i.Properties.Any(p => p.Name == nameof(Question.ItemType)));
        itemTypeIndexExists.Should().BeTrue();

        // Ensure we did NOT accidentally introduce a shadow property named ItemTypeCode.
        questionEntity!
            .FindProperty("ItemTypeCode")
            .Should()
            .BeNull();

        // GenerationRequest owned type DifficultyDistribution should be part of the model.
        // Accessing the model above is the main assertion (it would throw if constructor binding failed).
        model.GetEntityTypes().Any(et => et.Name.Contains("DifficultyDistribution", StringComparison.Ordinal))
            .Should().BeTrue();
    }
}
