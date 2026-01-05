using AIExamForge.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace AIExamForge.IntegrationTests;

/// <summary>
/// Test factory that keeps the app in Development (avoids HTTPS redirects)
/// but disables the Development-only local user injection so we can assert 401s.
/// </summary>
public sealed class UnauthenticatedApiWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
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
