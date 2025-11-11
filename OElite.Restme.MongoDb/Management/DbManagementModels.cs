using OElite.Common;

namespace OElite.Restme.MongoDb.Management;

/// <summary>
/// Configuration for database bootstrap operations
/// </summary>
public class DbBootstrapConfiguration
{
    /// <summary>
    /// Whether to enable sharding during bootstrap
    /// </summary>
    public bool EnableSharding { get; set; } = true;

    /// <summary>
    /// Whether to enable pre-splitting of shard chunks
    /// </summary>
    public bool EnablePreSplitting { get; set; } = true;

    /// <summary>
    /// Number of initial chunks to create for pre-splitting
    /// </summary>
    public int PreSplitCount { get; set; } = 256;

    /// <summary>
    /// Whether to create indexes in background mode
    /// </summary>
    public bool CreateIndexesInBackground { get; set; } = true;

    /// <summary>
    /// Maximum retry attempts for bootstrap operations
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Timeout for bootstrap operations in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Collection configurations for bootstrap
    /// </summary>
    public List<DbCollectionConfiguration> Collections { get; set; } = new();
}

/// <summary>
/// Configuration for individual collection setup
/// </summary>
public class DbCollectionConfiguration
{
    /// <summary>
    /// Collection name
    /// </summary>
    public string CollectionName { get; set; } = string.Empty;

    /// <summary>
    /// Shard key configuration (null if not sharded)
    /// </summary>
    public DbShardKey? ShardKey { get; set; }

    /// <summary>
    /// Index definitions for the collection
    /// </summary>
    public List<DbIndexDefinition> Indexes { get; set; } = new();

    /// <summary>
    /// TTL (Time To Live) configuration for automatic document expiration
    /// </summary>
    public TimeSpan? TtlExpiration { get; set; }

    /// <summary>
    /// Whether this collection should be validated during bootstrap
    /// </summary>
    public bool ValidateSchema { get; set; } = false;

    /// <summary>
    /// Bootstrap priority for ordering collection creation (lower values first)
    /// </summary>
    public int BootstrapPriority { get; set; } = 100;
}

/// <summary>
/// Shard key definition for MongoDB collections
/// </summary>
public class DbShardKey
{
    /// <summary>
    /// Field definitions for the shard key
    /// </summary>
    public List<DbShardKeyField> Fields { get; set; } = new();

    /// <summary>
    /// Whether this shard key should be unique
    /// </summary>
    public bool IsUnique { get; set; } = false;

    /// <summary>
    /// Creates a shard key with a single hashed field (common for ID-based sharding)
    /// </summary>
    /// <param name="field">Field name to use for hashed sharding</param>
    /// <returns>Configured shard key</returns>
    public static DbShardKey Hashed(string field)
    {
        return new DbShardKey
        {
            Fields = new List<DbShardKeyField>
            {
                new() { FieldName = field, IsHashed = true, Direction = DbSortDirection.Ascending }
            }
        };
    }

    /// <summary>
    /// Creates a compound shard key for bucket-style sharding (common in S3 scenarios)
    /// </summary>
    /// <param name="bucketField">Primary bucket/partition field</param>
    /// <param name="keyField">Secondary key field</param>
    /// <param name="hashSecondary">Whether to hash the secondary field</param>
    /// <returns>Configured compound shard key</returns>
    public static DbShardKey Compound(string bucketField, string keyField, bool hashSecondary = false)
    {
        return new DbShardKey
        {
            Fields = new List<DbShardKeyField>
            {
                new() { FieldName = bucketField, IsHashed = false, Direction = DbSortDirection.Ascending },
                new() { FieldName = keyField, IsHashed = hashSecondary, Direction = DbSortDirection.Ascending }
            }
        };
    }
}

/// <summary>
/// Individual field in a shard key
/// </summary>
public class DbShardKeyField
{
    /// <summary>
    /// Field name
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// Sort direction for the field
    /// </summary>
    public DbSortDirection Direction { get; set; } = DbSortDirection.Ascending;

    /// <summary>
    /// Whether this field uses hashed sharding
    /// </summary>
    public bool IsHashed { get; set; } = false;
}

/// <summary>
/// Sort direction enumeration
/// </summary>
public enum DbSortDirection
{
    Ascending = 1,
    Descending = -1
}

/// <summary>
/// Database index definition
/// </summary>
public class DbIndexDefinition
{
    /// <summary>
    /// Index name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Fields included in the index
    /// </summary>
    public List<DbIndexField> Fields { get; set; } = new();

    /// <summary>
    /// Whether this is a unique index
    /// </summary>
    public bool IsUnique { get; set; } = false;

    /// <summary>
    /// Whether this is a sparse index (only index documents that have the indexed field)
    /// </summary>
    public bool IsSparse { get; set; } = false;

    /// <summary>
    /// Whether to create the index in background mode
    /// </summary>
    public bool CreateInBackground { get; set; } = true;

    /// <summary>
    /// TTL expiration for TTL indexes
    /// </summary>
    public TimeSpan? TtlExpiration { get; set; }

    /// <summary>
    /// Partial filter expression for partial indexes
    /// </summary>
    public MongoDbDocument? PartialFilterExpression { get; set; }
}

/// <summary>
/// Individual field in a database index
/// </summary>
public class DbIndexField
{
    /// <summary>
    /// Field name
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// Sort direction for the field
    /// </summary>
    public DbSortDirection Direction { get; set; } = DbSortDirection.Ascending;

    /// <summary>
    /// Whether this field uses text indexing
    /// </summary>
    public bool IsText { get; set; } = false;

    /// <summary>
    /// Whether this field uses hashed indexing
    /// </summary>
    public bool IsHashed { get; set; } = false;
}

/// <summary>
/// Result of database management operations
/// </summary>
public class DbManagementResult
{
    /// <summary>
    /// Whether the operation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if operation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Detailed operation results
    /// </summary>
    public List<string> Messages { get; set; } = new();

    /// <summary>
    /// Operation execution time
    /// </summary>
    public TimeSpan ExecutionTime { get; set; }

    /// <summary>
    /// Additional metadata about the operation
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Operation duration (alias for ExecutionTime)
    /// </summary>
    public TimeSpan Duration
    {
        get => ExecutionTime;
        set => ExecutionTime = value;
    }

    /// <summary>
    /// Health status information (if applicable)
    /// </summary>
    public DbHealthStatus? HealthStatus { get; set; }
}

/// <summary>
/// Specific result for bootstrap operations with additional bootstrap-specific information
/// </summary>
public class DbBootstrapResult : DbManagementResult
{
    /// <summary>
    /// Bootstrap start time
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// Bootstrap completion time
    /// </summary>
    public DateTime CompletedAt { get; set; }

    /// <summary>
    /// Configuration used for bootstrap
    /// </summary>
    public DbBootstrapConfiguration? Configuration { get; set; }

    /// <summary>
    /// Configuration validation result
    /// </summary>
    public DbManagementResult? ValidationResult { get; set; }

    /// <summary>
    /// Bootstrap execution result
    /// </summary>
    public DbManagementResult? BootstrapResult { get; set; }

    /// <summary>
    /// List of collections that were configured during bootstrap
    /// </summary>
    public List<string>? ConfiguredCollections { get; set; } = new();

    /// <summary>
    /// Number of indexes created during bootstrap
    /// </summary>
    public int IndexesCreated { get; set; }

    /// <summary>
    /// Number of shard keys configured during bootstrap
    /// </summary>
    public int ShardKeysConfigured { get; set; }

    /// <summary>
    /// Bootstrap configuration used
    /// </summary>
    public DbBootstrapOptions? BootstrapOptions { get; set; }
}

/// <summary>
/// Options for database bootstrap operations
/// </summary>
public class DbBootstrapOptions
{
    /// <summary>
    /// Whether to enable sharding during bootstrap
    /// </summary>
    public bool EnableSharding { get; set; } = true;

    /// <summary>
    /// Whether to enable pre-splitting of shard chunks
    /// </summary>
    public bool EnablePreSplitting { get; set; } = true;

    /// <summary>
    /// Number of initial chunks to create for pre-splitting
    /// </summary>
    public int PreSplitCount { get; set; } = 256;

    /// <summary>
    /// Whether to create indexes in background mode
    /// </summary>
    public bool CreateIndexesInBackground { get; set; } = true;

    /// <summary>
    /// Maximum retry attempts for bootstrap operations
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Timeout for bootstrap operations in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Geographic configuration for region-aware operations
    /// </summary>
    public GeographicConfiguration? GeographicConfiguration { get; set; }
}

/// <summary>
/// Database health status information
/// </summary>
public class DbHealthStatus
{
    /// <summary>
    /// Whether the database is accessible
    /// </summary>
    public bool IsAccessible { get; set; }

    /// <summary>
    /// Connection latency in milliseconds
    /// </summary>
    public double ConnectionLatencyMs { get; set; }

    /// <summary>
    /// Database information
    /// </summary>
    public DbStatistics? DatabaseInfo { get; set; }

    /// <summary>
    /// Database statistics (alias for DatabaseInfo for compatibility)
    /// </summary>
    public DbStatistics? DatabaseStatistics
    {
        get => DatabaseInfo;
        set => DatabaseInfo = value;
    }

    /// <summary>
    /// Sharding status information
    /// </summary>
    public DbShardingStatus? ShardingStatus { get; set; }

    /// <summary>
    /// Whether sharding is enabled
    /// </summary>
    public bool IsShardingEnabled => ShardingStatus?.IsShardingEnabled ?? false;

    /// <summary>
    /// Last health check timestamp
    /// </summary>
    public DateTime LastChecked { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Error message if health check failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Database sharding status information
/// </summary>
public class DbShardingStatus
{
    /// <summary>
    /// Whether sharding is enabled for the database
    /// </summary>
    public bool IsShardingEnabled { get; set; }

    /// <summary>
    /// Number of shards in the cluster
    /// </summary>
    public int ShardCount { get; set; }

    /// <summary>
    /// List of shard information
    /// </summary>
    public List<DbShardInfo> Shards { get; set; } = new();

    /// <summary>
    /// List of sharded collections
    /// </summary>
    public List<string> ShardedCollections { get; set; } = new();
}

/// <summary>
/// Information about individual shard
/// </summary>
public class DbShardInfo
{
    /// <summary>
    /// Shard identifier
    /// </summary>
    public string ShardId { get; set; } = string.Empty;

    /// <summary>
    /// Shard host information
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Whether this shard is active
    /// </summary>
    public bool IsActive { get; set; }
}

/// <summary>
/// Index information for a collection
/// </summary>
public class DbIndexInfo
{
    /// <summary>
    /// Index name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Index key specification
    /// </summary>
    public Dictionary<string, object> KeySpec { get; set; } = new();

    /// <summary>
    /// Whether this is a unique index
    /// </summary>
    public bool IsUnique { get; set; }

    /// <summary>
    /// Whether this is a sparse index
    /// </summary>
    public bool IsSparse { get; set; }

    /// <summary>
    /// TTL expiration seconds (if applicable)
    /// </summary>
    public int? TtlSeconds { get; set; }

    /// <summary>
    /// Index size in bytes
    /// </summary>
    public long SizeInBytes { get; set; }

    /// <summary>
    /// Index usage statistics
    /// </summary>
    public DbIndexUsageStats? UsageStats { get; set; }
}

/// <summary>
/// Index usage statistics
/// </summary>
public class DbIndexUsageStats
{
    /// <summary>
    /// Number of operations that used this index
    /// </summary>
    public long Operations { get; set; }

    /// <summary>
    /// Last time this index was used
    /// </summary>
    public DateTime? LastUsed { get; set; }
}

/// <summary>
/// Database statistics
/// </summary>
public class DbStatistics
{
    /// <summary>
    /// Database name
    /// </summary>
    public string DatabaseName { get; set; } = string.Empty;

    /// <summary>
    /// Number of collections in the database
    /// </summary>
    public long CollectionCount { get; set; }

    /// <summary>
    /// Total database size in bytes
    /// </summary>
    public long SizeInBytes { get; set; }

    /// <summary>
    /// Data size in bytes
    /// </summary>
    public long DataSizeInBytes { get; set; }

    /// <summary>
    /// Index size in bytes
    /// </summary>
    public long IndexSizeInBytes { get; set; }

    /// <summary>
    /// Storage size in bytes
    /// </summary>
    public long StorageSizeInBytes { get; set; }

    /// <summary>
    /// Average object size in bytes
    /// </summary>
    public double AverageObjectSizeInBytes { get; set; }
}

/// <summary>
/// Collection-specific statistics
/// </summary>
public class DbCollectionStatistics
{
    /// <summary>
    /// Collection name
    /// </summary>
    public string CollectionName { get; set; } = string.Empty;

    /// <summary>
    /// Number of documents in the collection
    /// </summary>
    public long DocumentCount { get; set; }

    /// <summary>
    /// Total size of the collection in bytes
    /// </summary>
    public long SizeInBytes { get; set; }

    /// <summary>
    /// Average document size in bytes
    /// </summary>
    public double AverageDocumentSizeInBytes { get; set; }

    /// <summary>
    /// Number of indexes on the collection
    /// </summary>
    public int IndexCount { get; set; }

    /// <summary>
    /// Total size of all indexes in bytes
    /// </summary>
    public long TotalIndexSizeInBytes { get; set; }

    /// <summary>
    /// Whether the collection is sharded
    /// </summary>
    public bool IsSharded { get; set; }

    /// <summary>
    /// Shard key if collection is sharded
    /// </summary>
    public Dictionary<string, object>? ShardKey { get; set; }
}