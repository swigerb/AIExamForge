namespace AIExamForge.Infrastructure.Data;

/// <summary>
/// T177: Database configuration settings for connection pooling and resilience.
/// Per NFR-004: Performance optimization through proper connection management.
/// </summary>
public sealed class DatabaseSettings
{
    /// <summary>
    /// Configuration section name in appsettings.
    /// </summary>
    public const string SectionName = "DatabaseSettings";

    /// <summary>
    /// Command timeout in seconds. Default: 30 seconds.
    /// </summary>
    public int CommandTimeout { get; set; } = 30;

    /// <summary>
    /// Minimum number of connections to maintain in the pool. Default: 0.
    /// For production with Azure SQL, recommend 5-10.
    /// </summary>
    public int MinPoolSize { get; set; } = 0;

    /// <summary>
    /// Maximum number of connections allowed in the pool. Default: 100.
    /// Azure SQL Basic tier supports 30, Standard S0-S1 supports 200.
    /// </summary>
    public int MaxPoolSize { get; set; } = 100;

    /// <summary>
    /// Connection lifetime in seconds before recycling. 0 = no limit.
    /// For Azure SQL with load balancing, recommend 300-600 seconds.
    /// </summary>
    public int ConnectionLifetime { get; set; } = 0;

    /// <summary>
    /// Whether to enable automatic retry on transient failures.
    /// </summary>
    public bool EnableRetryOnFailure { get; set; } = true;

    /// <summary>
    /// Maximum number of retry attempts for transient failures.
    /// </summary>
    public int MaxRetryCount { get; set; } = 3;

    /// <summary>
    /// Maximum delay in seconds between retry attempts.
    /// </summary>
    public int MaxRetryDelaySeconds { get; set; } = 30;
}
