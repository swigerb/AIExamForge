using AIExamForge.Api;
using AIExamForge.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AIExamForge.IntegrationTests;

public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // Replace SQL Server DbContext with InMemory for integration tests.
            services.RemoveAll<DbContextOptions<AIExamForgeDbContext>>();
            services.RemoveAll<AIExamForgeDbContext>();

            // Keep EF provider registrations isolated for the test DbContext to avoid
            // the "only a single database provider can be registered" error.
            var inMemoryProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            services.AddDbContext<AIExamForgeDbContext>(options =>
            {
                options.UseInMemoryDatabase("AIExamForge_IntegrationTests");
                options.UseInternalServiceProvider(inMemoryProvider);
            });
        });

        builder.ConfigureAppConfiguration((_, config) =>
        {
            // Provide minimal configuration required for app startup.
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
    }
}
