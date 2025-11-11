using OElite.Common;

namespace OElite.Restme.MongoDb.Management;

/// <summary>
/// Extension methods to add database management capabilities to MongoDbCentre
/// Follows the existing Restme patterns for seamless integration
/// </summary>
public static class MongoDbCentreExtensions
{
    /// <summary>
    /// Get database management provider for advanced database operations
    /// </summary>
    /// <param name="dbCentre">The MongoDbCentre instance</param>
    /// <returns>Database management provider interface</returns>
    public static IDbManagementProvider GetDbManagementProvider(this MongoDbCentre dbCentre)
    {
        var database = dbCentre.GetDatabase();
        return new MongoDbManagementProvider(database);
    }

    /// <summary>
    /// Initialize database with bootstrap configuration
    /// Convenience method for common bootstrap scenarios
    /// </summary>
    /// <param name="dbCentre">The MongoDbCentre instance</param>
    /// <param name="configuration">Bootstrap configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Bootstrap result</returns>
    public static async Task<DbManagementResult> InitializeDatabaseAsync(
        this MongoDbCentre dbCentre,
        DbBootstrapConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var managementProvider = dbCentre.GetDbManagementProvider();
        return await managementProvider.InitializeDatabaseAsync(configuration, cancellationToken);
    }

    /// <summary>
    /// Bootstrap database for specific entity types with automatic configuration
    /// Follows OElite coding standards for entity-based configuration
    /// </summary>
    /// <param name="dbCentre">The MongoDbCentre instance</param>
    /// <param name="entityTypes">Entity types to configure</param>
    /// <param name="options">Bootstrap options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Bootstrap result</returns>
    public static async Task<DbManagementResult> BootstrapEntitiesAsync(
        this MongoDbCentre dbCentre,
        Type[] entityTypes,
        DbBootstrapOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new DbBootstrapOptions();
        var configuration = BuildBootstrapConfigurationFromEntities(entityTypes, options);

        return await dbCentre.InitializeDatabaseAsync(configuration, cancellationToken);
    }

    /// <summary>
    /// Bootstrap database for specific entity types with fluent configuration
    /// </summary>
    /// <typeparam name="T1">First entity type</typeparam>
    /// <param name="dbCentre">The MongoDbCentre instance</param>
    /// <param name="options">Bootstrap options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Bootstrap result</returns>
    public static async Task<DbManagementResult> BootstrapEntitiesAsync<T1>(
        this MongoDbCentre dbCentre,
        DbBootstrapOptions? options = null,
        CancellationToken cancellationToken = default)
        where T1 : BaseEntity
    {
        return await BootstrapEntitiesAsync(dbCentre, new[] { typeof(T1) }, options, cancellationToken);
    }

    /// <summary>
    /// Bootstrap database for two entity types
    /// </summary>
    public static async Task<DbManagementResult> BootstrapEntitiesAsync<T1, T2>(
        this MongoDbCentre dbCentre,
        DbBootstrapOptions? options = null,
        CancellationToken cancellationToken = default)
        where T1 : BaseEntity
        where T2 : BaseEntity
    {
        return await BootstrapEntitiesAsync(dbCentre, new[] { typeof(T1), typeof(T2) }, options, cancellationToken);
    }

    /// <summary>
    /// Bootstrap database for three entity types
    /// </summary>
    public static async Task<DbManagementResult> BootstrapEntitiesAsync<T1, T2, T3>(
        this MongoDbCentre dbCentre,
        DbBootstrapOptions? options = null,
        CancellationToken cancellationToken = default)
        where T1 : BaseEntity
        where T2 : BaseEntity
        where T3 : BaseEntity
    {
        return await BootstrapEntitiesAsync(dbCentre, new[] { typeof(T1), typeof(T2), typeof(T3) }, options, cancellationToken);
    }


    /// <summary>
    /// Get database health status including sharding and index information
    /// </summary>
    /// <param name="dbCentre">The MongoDbCentre instance</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Database health status</returns>
    public static async Task<DbHealthStatus> GetHealthStatusAsync(
        this MongoDbCentre dbCentre,
        CancellationToken cancellationToken = default)
    {
        var managementProvider = dbCentre.GetDbManagementProvider();
        var healthStatus = new DbHealthStatus();

        try
        {
            // Get database statistics
            var dbStats = await managementProvider.GetDatabaseStatisticsAsync(cancellationToken);
            healthStatus.DatabaseStatistics = dbStats;

            // Get sharding status
            var shardingStatus = await managementProvider.GetShardingStatusAsync(cancellationToken);
            healthStatus.ShardingStatus = shardingStatus;

            // Check if database is accessible
            healthStatus.IsAccessible = true;
            healthStatus.LastChecked = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            healthStatus.IsAccessible = false;
            healthStatus.ErrorMessage = ex.Message;
            healthStatus.LastChecked = DateTime.UtcNow;
        }

        return healthStatus;
    }

    #region Private Helper Methods

    private static DbBootstrapConfiguration BuildBootstrapConfigurationFromEntities(Type[] entityTypes, DbBootstrapOptions options)
    {
        // Use the new attribute scanner for automatic configuration discovery
        return EntityAttributeScanner.ScanEntitiesForBootstrapConfiguration(entityTypes, options);
    }

    private static DbCollectionConfiguration BuildCollectionConfigurationFromEntity(Type entityType, DbBootstrapOptions options)
    {
        var collectionName = MongoDbAttributeMapper.GetCollectionName(entityType) ?? entityType.Name.ToLowerInvariant();

        var config = new DbCollectionConfiguration
        {
            CollectionName = collectionName,
            Indexes = new List<DbIndexDefinition>()
        };

        // Add basic indexes based on BaseEntity properties
        if (entityType.IsSubclassOf(typeof(BaseEntity)))
        {
            // Always create index on Id field
            config.Indexes.Add(new DbIndexDefinition
            {
                Name = "idx_id",
                Fields = new List<DbIndexField>
                {
                    new() { FieldName = "_id", Direction = DbSortDirection.Ascending }
                },
                IsUnique = true,
                CreateInBackground = options.CreateIndexesInBackground
            });

            // Add common BaseEntity indexes if properties exist
            AddBaseEntityIndexes(config, entityType, options);
        }

        // Configure sharding based on entity analysis
        if (options.EnableSharding && ShouldShardEntity(entityType))
        {
            config.ShardKey = BuildShardKeyForEntity(entityType);
        }

        return config;
    }

    private static void AddBaseEntityIndexes(DbCollectionConfiguration config, Type entityType, DbBootstrapOptions options)
    {
        var properties = entityType.GetProperties();

        // Check for common indexable properties
        var indexableProperties = new[]
        {
            ("CreatedOnUtc", "idx_created"),
            ("UpdatedOnUtc", "idx_updated"),
            ("IsActive", "idx_active"),
            ("Status", "idx_status"),
            ("OwnerId", "idx_owner"),
            ("OwnerMerchantId", "idx_owner_merchant"),
            ("OwnerContactId", "idx_owner_contact")
        };

        foreach (var (propertyName, indexName) in indexableProperties)
        {
            var property = properties.FirstOrDefault(p => p.Name == propertyName);
            if (property != null)
            {
                config.Indexes.Add(new DbIndexDefinition
                {
                    Name = indexName,
                    Fields = new List<DbIndexField>
                    {
                        new() { FieldName = propertyName.ToLowerInvariant(), Direction = DbSortDirection.Ascending }
                    },
                    CreateInBackground = options.CreateIndexesInBackground,
                    IsSparse = propertyName.Contains("Owner") // Sparse for owner fields
                });
            }
        }
    }

    private static bool ShouldShardEntity(Type entityType)
    {
        // Check if entity has properties that suggest it should be sharded
        var properties = entityType.GetProperties();

        // Entities with owner isolation or bucket-like patterns should be sharded
        return properties.Any(p => p.Name.Contains("Owner") || p.Name.Contains("Bucket") || p.Name.Contains("Tenant"));
    }

    private static DbShardKey BuildShardKeyForEntity(Type entityType)
    {
        var properties = entityType.GetProperties();
        var shardKey = new DbShardKey();

        // Priority order for shard key fields
        var shardKeyCandidates = new[]
        {
            "BucketName",
            "Bucket",
            "OwnerMerchantId",
            "OwnerContactId",
            "TenantId",
            "_id"
        };

        foreach (var candidate in shardKeyCandidates)
        {
            var property = properties.FirstOrDefault(p => p.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase));
            if (property != null)
            {
                shardKey.Fields.Add(new DbShardKeyField
                {
                    FieldName = property.Name.ToLowerInvariant(),
                    Direction = DbSortDirection.Ascending,
                    IsHashed = candidate == "_id" // Hash ID fields for better distribution
                });

                // For compound shard keys, add a secondary field
                if (candidate != "_id" && properties.Any(p => p.Name.Equals("_id", StringComparison.OrdinalIgnoreCase)))
                {
                    shardKey.Fields.Add(new DbShardKeyField
                    {
                        FieldName = "_id",
                        Direction = DbSortDirection.Ascending,
                        IsHashed = true
                    });
                }

                break;
            }
        }

        return shardKey.Fields.Any() ? shardKey : null;
    }


    #endregion
}

