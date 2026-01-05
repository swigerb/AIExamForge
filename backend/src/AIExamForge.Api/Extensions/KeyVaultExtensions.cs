using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AIExamForge.Api.Extensions;

/// <summary>
/// T166: Azure Key Vault integration for secrets per NFR-001.
/// Provides secure secret management for connection strings, API keys, etc.
/// </summary>
public static class KeyVaultExtensions
{
    private static string? GetKeyVaultUrlFromEnvironment()
    {
        // Prefer explicit AZD/Bicep output, but support common alternatives.
        return Environment.GetEnvironmentVariable("AZURE_KEY_VAULT_ENDPOINT")
            ?? Environment.GetEnvironmentVariable("AZURE_KEY_VAULT_URL");
    }

    /// <summary>
    /// Adds Azure Key Vault as a configuration source.
    /// </summary>
    public static IConfigurationBuilder AddAzureKeyVault(
        this IConfigurationBuilder builder,
        string? keyVaultUrl = null)
    {
        // Get Key Vault URL from environment or parameter
        keyVaultUrl ??= GetKeyVaultUrlFromEnvironment();

        if (string.IsNullOrWhiteSpace(keyVaultUrl))
        {
            // Key Vault not configured - skip (development mode)
            return builder;
        }

        // Use DefaultAzureCredential for flexible authentication
        // Supports: Managed Identity (production), Azure CLI (development), VS Code, etc.
        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            // Exclude interactive browsers in production
            ExcludeInteractiveBrowserCredential = true,
            // Optimize credential chain ordering
            ExcludeEnvironmentCredential = false,
            ExcludeManagedIdentityCredential = false,
            ExcludeAzureCliCredential = false,
            ExcludeVisualStudioCodeCredential = false,
            ExcludeVisualStudioCredential = false
        });

        builder.AddAzureKeyVault(new Uri(keyVaultUrl), credential);

        return builder;
    }

    /// <summary>
    /// Adds Key Vault secret client for runtime secret access.
    /// </summary>
    public static IServiceCollection AddKeyVaultClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var keyVaultUrl = configuration["KeyVault:Url"]
            ?? configuration["KeyVault:VaultUri"]
            ?? GetKeyVaultUrlFromEnvironment();

        if (string.IsNullOrWhiteSpace(keyVaultUrl))
        {
            // Register a null service for development
            services.AddSingleton<ISecretProvider, NullSecretProvider>();
            return services;
        }

        var credential = new DefaultAzureCredential();
        var client = new SecretClient(new Uri(keyVaultUrl), credential);

        services.AddSingleton(client);
        services.AddSingleton<ISecretProvider, KeyVaultSecretProvider>();

        return services;
    }
}

/// <summary>
/// Interface for secret retrieval abstraction.
/// </summary>
public interface ISecretProvider
{
    /// <summary>
    /// Gets a secret value by name.
    /// </summary>
    Task<string?> GetSecretAsync(string secretName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the secret provider is available.
    /// </summary>
    bool IsAvailable { get; }
}

/// <summary>
/// Azure Key Vault implementation of secret provider.
/// </summary>
public sealed class KeyVaultSecretProvider : ISecretProvider
{
    private readonly SecretClient _client;
    private readonly ILogger<KeyVaultSecretProvider> _logger;

    public bool IsAvailable => true;

    public KeyVaultSecretProvider(SecretClient client, ILogger<KeyVaultSecretProvider> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<string?> GetSecretAsync(string secretName, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.GetSecretAsync(secretName, cancellationToken: cancellationToken);
            return response.Value.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Secret {SecretName} not found in Key Vault", secretName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving secret {SecretName} from Key Vault", secretName);
            throw;
        }
    }
}

/// <summary>
/// Null implementation for development without Key Vault.
/// </summary>
public sealed class NullSecretProvider : ISecretProvider
{
    public bool IsAvailable => false;

    public Task<string?> GetSecretAsync(string secretName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>(null);
    }
}
