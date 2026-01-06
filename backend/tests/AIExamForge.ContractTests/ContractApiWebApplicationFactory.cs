using AIExamForge.Api;
using AIExamForge.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace AIExamForge.ContractTests;

/// <summary>
/// Hosts the API in-memory for contract testing (OpenAPI + surface area).
/// Uses an in-memory EF provider to avoid requiring SQL Server for tests.
/// </summary>
public sealed class ContractApiWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"AIExamForge_ContractTests_{Guid.NewGuid():N}";

    public ContractApiWebApplicationFactory()
    {
        // Contract tests should never try to contact Azure Key Vault.
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

        builder.ConfigureAppConfiguration((_, config) =>
        {
            // Remove any Azure Key Vault sources for test runs.
            var keyVaultSources = config.Sources
                .Where(s => (s.GetType().FullName ?? string.Empty)
                    .Contains("AzureKeyVault", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var source in keyVaultSources)
            {
                config.Sources.Remove(source);
            }

            // Minimal configuration required for app startup.
            // The API currently throws if DefaultConnection is missing.
            var overrides = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=(localdb)\\MSSQLLocalDB;Database=AIExamForge_Test;Trusted_Connection=True;TrustServerCertificate=True",

                // Minimal AzureAd config so Microsoft.Identity.Web can bind options.
                ["AzureAd:Instance"] = "https://login.microsoftonline.com/",
                ["AzureAd:TenantId"] = "00000000-0000-0000-0000-000000000000",
                ["AzureAd:ClientId"] = "00000000-0000-0000-0000-000000000000",

                // Keep CORS predictable in tests.
                ["Cors:AllowedOrigins:0"] = "http://localhost:3000"
            };

            config.AddInMemoryCollection(overrides);
        });

        builder.ConfigureServices(services =>
        {
            // Replace SQL Server DbContext with InMemory so Program's EnsureCreated() does not require a real server.
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
        });
    }
}
