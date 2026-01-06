using AIExamForge.Api.Streaming;
using AIExamForge.Application.Generation;
using AIExamForge.Application.Generation.Interfaces;
using AIExamForge.Domain.Repositories;
using Microsoft.Extensions.Hosting;

namespace AIExamForge.Api.Services;

public sealed class GenerationBackgroundWorker(
    IGenerationBackgroundQueue queue,
    IServiceScopeFactory scopeFactory,
    GenerationProgressHandler progressHandler,
    ILogger<GenerationBackgroundWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Generation background worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            GenerationWorkItem item;
            try
            {
                item = await queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await RunOneAsync(item, stoppingToken);
        }

        logger.LogInformation("Generation background worker stopping");
    }

    private async Task RunOneAsync(GenerationWorkItem item, CancellationToken stoppingToken)
    {
        logger.LogInformation("Background generation dequeued for {GenerationId}", item.GenerationId);

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var generationService = scope.ServiceProvider.GetRequiredService<IQuestionGenerationService>();

            logger.LogInformation("Background generation started for {GenerationId}", item.GenerationId);

            await generationService.GenerateStreamingAsync(
                item.Request,
                item.UserId,
                item.GenerationId,
                async p =>
                {
                    if (p.Status == GenerationProgressStatus.Failed)
                    {
                        await progressHandler.SendErrorAsync(item.GenerationId, p.Message);
                        return;
                    }

                    if (p.Status == GenerationProgressStatus.Completed)
                    {
                        await progressHandler.SendProgressAsync(item.GenerationId, 100, p.Message);
                        await progressHandler.SendCompleteAsync(item.GenerationId);
                        progressHandler.CleanupGeneration(item.GenerationId);
                        return;
                    }

                    var pct = p.PercentComplete ?? 0;
                    await progressHandler.SendProgressAsync(item.GenerationId, pct, p.Message);
                },
                stoppingToken);

            logger.LogInformation("Background generation finished for {GenerationId}", item.GenerationId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Background generation crashed for {GenerationId}", item.GenerationId);

            try
            {
                // Persist failure so polling can see it even if generation never truly started.
                await using var failScope = scopeFactory.CreateAsyncScope();
                var uow = failScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var existing = await uow.GenerationRequests.GetByIdAsync(item.GenerationId, stoppingToken);
                if (existing != null)
                {
                    existing.Fail($"Error: {ex.Message}");
                    await uow.GenerationRequests.UpdateAsync(existing, stoppingToken);
                    await uow.SaveChangesAsync(stoppingToken);
                }
            }
            catch
            {
                // Best-effort only; don't hide the original exception.
            }

            await progressHandler.SendErrorAsync(item.GenerationId, $"Error: {ex.Message}");
            progressHandler.CleanupGeneration(item.GenerationId);
        }
    }
}
