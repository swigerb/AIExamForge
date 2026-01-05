using System.Text;
using AIExamForge.Domain.Entities;
using AIExamForge.Domain.ValueObjects;
using AIExamForge.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace AIExamForge.IntegrationTests;

internal static class TestDataSeeder
{
    public const string DefaultUserId = "local-dev";

    public static async Task<(Guid GenerationId, Guid QuestionSetId, Guid QuestionId)> SeedCompletedGenerationWithOneQuestionAsync(
        ApiWebApplicationFactory factory,
        Guid outlineId,
        Guid selectedNodeId,
        CancellationToken cancellationToken = default)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AIExamForgeDbContext>();

        var generation = GenerationRequest.Create(
            userId: DefaultUserId,
            outlineId: outlineId,
            selectedNodeIds: new[] { selectedNodeId },
            questionCount: 1,
            itemTypes: new[] { ItemTypeCode.Create("M1") },
            difficultyDistribution: DifficultyDistribution.Default,
            examName: "AZ-204");

        var questionSet = QuestionSet.Create(
            userId: DefaultUserId,
            generationRequestId: generation.Id,
            examName: "AZ-204",
            renderMode: RenderMode.Study);

        var tags = QuestionTags.Create(
            exam: "AZ-204",
            objective: "Implement Azure App Service",
            section: "Compute",
            topics: new[] { "App Service" });

        var question = Question.Create(
            itemType: ItemTypeCode.Create("M1"),
            title: "App Service deployment slots",
            stem: "You need to swap slots in Azure App Service. What should you do?",
            difficulty: QuestionDifficulty.Easy,
            tags: tags,
            scoring: QuestionScoring.Dichotomous(1),
            contentJson: "{\"choices\":[{\"id\":\"A\",\"text\":\"Swap\"}],\"answer\":\"A\"}",
            questionSetId: questionSet.Id,
            orderIndex: 0,
            instruction: null,
            rationale: "Swap slots to exchange content and configuration.");

        questionSet.AddQuestion(question);

        generation.StartContentRetrieval();
        generation.StartGeneration();
        generation.StartValidation(1);
        generation.Complete(questionSet.Id, questionsValidated: 1);

        db.GenerationRequests.Add(generation);
        db.QuestionSets.Add(questionSet);
        db.Questions.Add(question);
        await db.SaveChangesAsync(cancellationToken);

        return (generation.Id, questionSet.Id, question.Id);
    }

    public static async Task<Guid> SeedGenerationAsync(
        ApiWebApplicationFactory factory,
        GenerationStatus status,
        Guid outlineId,
        Guid selectedNodeId,
        Guid? questionSetId = null,
        CancellationToken cancellationToken = default)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AIExamForgeDbContext>();

        var generation = GenerationRequest.Create(
            userId: DefaultUserId,
            outlineId: outlineId,
            selectedNodeIds: new[] { selectedNodeId },
            questionCount: 1,
            itemTypes: new[] { ItemTypeCode.Create("M1") },
            difficultyDistribution: DifficultyDistribution.Default,
            examName: "AZ-204");

        // Move through legal state transitions when needed.
        if (status == GenerationStatus.Pending)
        {
            // leave as-is
        }
        else if (status == GenerationStatus.Failed)
        {
            generation.Fail("boom");
        }
        else
        {
            generation.StartContentRetrieval();
            generation.StartGeneration();
            generation.StartValidation(1);

            if (status == GenerationStatus.Completed || status == GenerationStatus.CompletedPartial)
            {
                generation.Complete(questionSetId ?? Guid.NewGuid(), questionsValidated: 1);
            }
            else
            {
                // map other statuses to something valid; leave at Validating
            }
        }

        db.GenerationRequests.Add(generation);
        await db.SaveChangesAsync(cancellationToken);
        return generation.Id;
    }
}
