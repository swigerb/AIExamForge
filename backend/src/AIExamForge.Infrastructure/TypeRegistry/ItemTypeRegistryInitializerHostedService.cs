using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AIExamForge.Infrastructure.TypeRegistry;

public sealed class ItemTypeRegistryInitializerHostedService : IHostedService
{
    private readonly ItemTypeRegistry _registry;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<ItemTypeRegistryInitializerHostedService> _logger;

    public ItemTypeRegistryInitializerHostedService(
        ItemTypeRegistry registry,
        IHostEnvironment environment,
        ILogger<ItemTypeRegistryInitializerHostedService> logger)
    {
        _registry = registry;
        _environment = environment;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var (schemasBasePath, promptsBasePath) = FindPaths(_environment.ContentRootPath);

        _logger.LogInformation(
            "Initializing ItemTypeRegistry. SchemasBasePath={SchemasBasePath}, PromptsBasePath={PromptsBasePath}",
            schemasBasePath,
            promptsBasePath);

        await _registry.InitializeAsync(schemasBasePath, promptsBasePath);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static (string schemasBasePath, string promptsBasePath) FindPaths(string contentRootPath)
    {
        // Expected in repo root:
        // - docs/schemas/common
        // - prompts
        const int maxParents = 10;

        var current = new DirectoryInfo(contentRootPath);
        for (var i = 0; i < maxParents && current != null; i++)
        {
            var schemas = Path.Combine(current.FullName, "docs", "schemas", "common");
            var prompts = Path.Combine(current.FullName, "prompts");

            if (Directory.Exists(schemas) && Directory.Exists(prompts))
            {
                return (schemas, prompts);
            }

            current = current.Parent;
        }

        // Fall back to best-effort relative paths.
        return (
            Path.Combine(contentRootPath, "docs", "schemas", "common"),
            Path.Combine(contentRootPath, "prompts"));
    }
}
