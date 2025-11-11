namespace OElite.Restme.MongoDb.Management;

/// <summary>
/// Database management provider interface for advanced database operations
/// Provides sharding, indexing, and bootstrap functionality without exposing MongoDB types
/// </summary>
public interface IDbManagementProvider
{
    /// <summary>
    /// Initialize database with sharding, indexing, and bootstrap configuration
    /// </summary>
    Task<DbManagementResult> InitializeDatabaseAsync(DbBootstrapConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enable sharding for the database
    /// </summary>
    Task<DbManagementResult> EnableShardingAsync(string databaseName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a sharded collection with the specified shard key
    /// </summary>
    Task<DbManagementResult> CreateShardedCollectionAsync(string collectionName, DbShardKey shardKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create indexes for a collection
    /// </summary>
    Task<DbManagementResult> CreateIndexesAsync(string collectionName, List<DbIndexDefinition> indexes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pre-split shard chunks for better distribution
    /// </summary>
    Task<DbManagementResult> PreSplitShardsAsync(string collectionName, DbShardKey shardKey, int splitCount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get sharding status for the database
    /// </summary>
    Task<DbShardingStatus> GetShardingStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get index information for a collection
    /// </summary>
    Task<List<DbIndexInfo>> GetIndexesAsync(string collectionName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if collection exists
    /// </summary>
    Task<bool> CollectionExistsAsync(string collectionName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get database statistics
    /// </summary>
    Task<DbStatistics> GetDatabaseStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get collection statistics
    /// </summary>
    Task<DbCollectionStatistics> GetCollectionStatisticsAsync(string collectionName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate bootstrap configuration without applying it
    /// </summary>
    Task<DbManagementResult> ValidateBootstrapConfigurationAsync(DbBootstrapConfiguration configuration, CancellationToken cancellationToken = default);
}