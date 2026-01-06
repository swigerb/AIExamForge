using AIExamForge.Api;
using AIExamForge.Application.Export;
using AIExamForge.Application.Export.Interfaces;
using AIExamForge.Application.Generation.Interfaces;
using AIExamForge.Application.Validation.Interfaces;
using AIExamForge.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace AIExamForge.IntegrationTests;

/// <summary>
/// Test factory that keeps the app in Development (avoids HTTPS redirects)
/// but disables the Development-only local user injection so we can assert 401s.
/// </summary>
public sealed class UnauthenticatedApiWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"AIExamForge_IntegrationTests_Unauth_{Guid.NewGuid():N}";

    public UnauthenticatedApiWebApplicationFactory()
    {
        // Auth-negative tests should never try to contact Azure Key Vault.
        Environment.SetEnvironmentVariable("AZURE_KEY_VAULT_ENDPOINT", null);
        Environment.SetEnvironmentVariable("AZURE_KEY_VAULT_URL", null);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Warning);
        });

        builder.ConfigureServices(services =>
        {
            // Keep auth tests isolated from external dependencies.
            // Replace SQL Server DbContext with InMemory to avoid LocalDB startup/EnsureCreated hangs.
            services.RemoveAll<DbContextOptions<AIExamForgeDbContext>>();
            services.RemoveAll<AIExamForgeDbContext>();

            var inMemoryProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            services.AddDbContext<AIExamForgeDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName);
                options.UseInternalServiceProvider(inMemoryProvider);
            });

            // Ensure nothing tries to hit LLM/schema/export pipelines during startup.
            services.RemoveAll<IQuestionGenerationService>();
            services.AddSingleton<IQuestionGenerationService, FakeQuestionGenerationService>();

            services.RemoveAll<ISchemaValidator>();
            services.AddSingleton<ISchemaValidator, FakeSchemaValidator>();

            services.RemoveAll<ILintValidator>();
            services.AddSingleton<ILintValidator, FakeLintValidator>();

            services.RemoveAll<IExportService>();
            services.AddSingleton<IExportService, FakeExportService>();
        });

        builder.ConfigureAppConfiguration((_, config) =>
        {
            var keyVaultSources = config.Sources
                .Where(s => (s.GetType().FullName ?? string.Empty).Contains("AzureKeyVault", StringComparison.OrdinalIgnoreCase))
                .ToList();
            foreach (var source in keyVaultSources)
            {
                config.Sources.Remove(source);
            }

            var overrides = new Dictionary<string, string?>
            {
                ["Auth:InjectLocalDevUser"] = "false",

                // Minimal AzureAd config so Microsoft.Identity.Web can bind options.
                ["AzureAd:Instance"] = "https://login.microsoftonline.com/",
                ["AzureAd:TenantId"] = "00000000-0000-0000-0000-000000000000",
                ["AzureAd:ClientId"] = "00000000-0000-0000-0000-000000000000",

                // Connection string is required on startup even though tests won't hit SQL.
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=(localdb)\\MSSQLLocalDB;Database=AIExamForge_Test;Trusted_Connection=True;TrustServerCertificate=True"
            };

            config.AddInMemoryCollection(overrides);
        });
    }
}
