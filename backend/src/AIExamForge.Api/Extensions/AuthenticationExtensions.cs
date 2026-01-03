using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;

namespace AIExamForge.Api.Extensions;

/// <summary>
/// Extension methods for configuring Microsoft Entra ID (Azure AD) authentication.
/// </summary>
public static class AuthenticationExtensions
{
    /// <summary>
    /// Configures Microsoft Entra ID authentication using Microsoft.Identity.Web.
    /// Reads configuration from AzureAd section in appsettings.json.
    /// </summary>
    public static IServiceCollection AddEntraIdAuthentication(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Add Microsoft Identity Platform authentication
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApi(configuration.GetSection("AzureAd"));

        // Configure authorization policies
        services.AddAuthorizationBuilder()
            .AddPolicy("RequireAuthenticatedUser", policy =>
            {
                policy.RequireAuthenticatedUser();
            })
            .AddPolicy("RequireGenerationScope", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("scp", "Questions.Generate");
            })
            .AddPolicy("RequireReadScope", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("scp", "Questions.Read");
            });

        return services;
    }

    /// <summary>
    /// Adds authentication and authorization middleware to the request pipeline.
    /// </summary>
    public static IApplicationBuilder UseEntraIdAuthentication(this IApplicationBuilder app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }

    /// <summary>
    /// Adds optional authentication for development/testing scenarios.
    /// In production, all endpoints require authentication.
    /// </summary>
    public static IServiceCollection AddDevelopmentAuthentication(
        this IServiceCollection services,
        IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            // In development, allow anonymous access for easier testing
            // Production always requires authentication
            services.AddAuthorizationBuilder()
                .AddPolicy("AllowAnonymousInDevelopment", policy =>
                {
                    // This policy allows any request to proceed
                    policy.RequireAssertion(_ => true);
                });
        }

        return services;
    }
}
