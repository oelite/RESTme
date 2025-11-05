using OElite.Common;

namespace OElite.Restme.MongoDb.Management;

/// <summary>
/// Service for bootstrapping database with advanced configurations
/// Follows OElite service patterns and integrates with existing DbCentre infrastructure
/// </summary>
public class DbBootstrapService
{
    private readonly MongoDbCentre _dbCentre;
    private readonly IDbManagementProvider _managementProvider;

    public DbBootstrapService(MongoDbCentre dbCentre)
    {
        _dbCentre = dbCentre ?? throw new ArgumentNullException(nameof(dbCentre));
        _managementProvider = _dbCentre.GetDbManagementProvider();
    }

    /// <summary>
    /// Bootstrap database with full configuration and retry logic
    /// </summary>
    /// <param name="configuration">Bootstrap configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Bootstrap result with detailed information</returns>
    public async Task<DbBootstrapResult> BootstrapAsync(
        DbBootstrapConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var result = new DbBootstrapResult
        {
            StartedAt = DateTime.UtcNow,
            Configuration = configuration
        };

        try
        {
            // Validate configuration first
            var validationResult = await _managementProvider.ValidateBootstrapConfigurationAsync(configuration, cancellationToken);
            result.ValidationResult = validationResult;

            if (!validationResult.Success)
            {
                result.Success = false;
                result.ErrorMessage = "Configuration validation failed";
                result.CompletedAt = DateTime.UtcNow;
                return result;
            }

            // Execute bootstrap with retry logic
            var bootstrapResult = await ExecuteBootstrapWithRetryAsync(configuration, cancellationToken);
            result.BootstrapResult = bootstrapResult;
            result.Success = bootstrapResult.Success;
            result.ErrorMessage = bootstrapResult.ErrorMessage;

            // Copy essential bootstrap results to main result
            result.ConfiguredCollections = bootstrapResult.ConfiguredCollections;
            result.ShardKeysConfigured = bootstrapResult.ShardKeysConfigured;
            if (bootstrapResult.Messages != null && result.Messages == null)
            {
                result.Messages = new List<string>();
            }
            if (bootstrapResult.Messages != null)
            {
                foreach (var message in bootstrapResult.Messages)
                {
                    result.Messages.Add(message);
                }
            }

            // Gather post-bootstrap health information
            if (result.Success)
            {
                result.HealthStatus = await _dbCentre.GetHealthStatusAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            result.CompletedAt = DateTime.UtcNow;
            result.Duration = result.CompletedAt - result.StartedAt;
        }

        return result;
    }


    /// <summary>
    /// Bootstrap database for specific entity types with intelligent configuration
    /// </summary>
    /// <param name="entityTypes">Entity types to configure</param>
    /// <param name="options">Bootstrap options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Bootstrap result</returns>
    public async Task<DbBootstrapResult> BootstrapForEntitiesAsync(
        Type[] entityTypes,
        DbBootstrapOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new DbBootstrapOptions();

        var configuration = new DbBootstrapConfiguration
        {
            EnableSharding = options.EnableSharding,
            EnablePreSplitting = options.EnablePreSplitting,
            PreSplitCount = options.PreSplitCount,
            CreateIndexesInBackground = options.CreateIndexesInBackground,
            MaxRetryAttempts = options.MaxRetryAttempts,
            TimeoutSeconds = options.TimeoutSeconds,
            Collections = CreateEntityCollections(entityTypes, options)
        };

        return await BootstrapAsync(configuration, cancellationToken);
    }


    /// <summary>
    /// Validate database configuration without applying changes
    /// </summary>
    /// <param name="configuration">Configuration to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result</returns>
    public async Task<DbManagementResult> ValidateConfigurationAsync(
        DbBootstrapConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        return await _managementProvider.ValidateBootstrapConfigurationAsync(configuration, cancellationToken);
    }

    /// <summary>
    /// Get current database status and recommendations
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Database status with recommendations</returns>
    public async Task<DbStatusReport> GetDatabaseStatusAsync(CancellationToken cancellationToken = default)
    {
        var report = new DbStatusReport
        {
            GeneratedAt = DateTime.UtcNow
        };

        try
        {
            // Get basic health status
            var healthStatus = await _dbCentre.GetHealthStatusAsync(cancellationToken);
            report.HealthStatus = healthStatus;

            // Get detailed statistics
            var dbStats = await _managementProvider.GetDatabaseStatisticsAsync(cancellationToken);
            report.DatabaseStatistics = dbStats;

            // Get sharding information
            var shardingStatus = await _managementProvider.GetShardingStatusAsync(cancellationToken);
            report.ShardingStatus = shardingStatus;

            // Generate recommendations
            report.Recommendations = GenerateRecommendations(healthStatus, dbStats, shardingStatus);

            report.Success = true;
        }
        catch (Exception ex)
        {
            report.Success = false;
            report.ErrorMessage = ex.Message;
        }

        return report;
    }

    #region Private Methods

    private async Task<DbBootstrapResult> ExecuteBootstrapWithRetryAsync(
        DbBootstrapConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var maxAttempts = configuration.MaxRetryAttempts;
        var delay = TimeSpan.FromSeconds(2);

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                // Debug: Log configuration details
                Console.WriteLine($"DEBUG: Attempting bootstrap with {configuration.Collections?.Count ?? 0} collections");
                foreach (var col in configuration.Collections ?? Enumerable.Empty<DbCollectionConfiguration>())
                {
                    Console.WriteLine($"DEBUG: Collection: {col.CollectionName}");
                }

                var managementResult = await _managementProvider.InitializeDatabaseAsync(configuration, cancellationToken);

                Console.WriteLine($"DEBUG: InitializeDatabaseAsync returned Success = {managementResult.Success}");
                Console.WriteLine($"DEBUG: Error message: {managementResult.ErrorMessage}");

                // Convert DbManagementResult to DbBootstrapResult
                var result = new DbBootstrapResult
                {
                    Success = managementResult.Success,
                    ErrorMessage = managementResult.ErrorMessage,
                    Messages = managementResult.Messages,
                    ExecutionTime = managementResult.ExecutionTime,
                    Metadata = managementResult.Metadata
                };

                if (result.Success)
                {
                    Console.WriteLine($"DEBUG: Bootstrap SUCCESS branch entered");
                    result.Messages.Add($"Bootstrap completed successfully on attempt {attempt}");

                    // Populate ConfiguredCollections and ShardKeysConfigured
                    if (configuration.Collections != null)
                    {
                        Console.WriteLine($"DEBUG: Populating ConfiguredCollections with {configuration.Collections.Count} collections");
                        var configuredCollections = new List<string>();
                        foreach (var collection in configuration.Collections)
                        {
                            if (!string.IsNullOrEmpty(collection.CollectionName))
                            {
                                configuredCollections.Add(collection.CollectionName);
                                Console.WriteLine($"DEBUG: Added collection: {collection.CollectionName}");
                            }
                        }
                        result.ConfiguredCollections = configuredCollections;
                        Console.WriteLine($"DEBUG: Final ConfiguredCollections count: {result.ConfiguredCollections?.Count ?? 0}");

                        // Count shard keys configured
                        var shardKeysCount = configuration.Collections
                            .Count(c => c.ShardKey != null && c.ShardKey.Fields?.Any() == true);
                        result.ShardKeysConfigured = shardKeysCount;
                        Console.WriteLine($"DEBUG: ShardKeysConfigured: {result.ShardKeysConfigured}");
                    }
                    else
                    {
                        Console.WriteLine($"DEBUG: configuration.Collections is null!");
                    }

                    return result;
                }

                if (attempt == maxAttempts)
                {
                    result.Messages.Add($"Bootstrap failed after {maxAttempts} attempts");
                    return result;
                }

                // Exponential backoff
                await Task.Delay(delay, cancellationToken);
                delay = TimeSpan.FromSeconds(delay.TotalSeconds * 2);
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                // Continue to next attempt
                await Task.Delay(delay, cancellationToken);
                delay = TimeSpan.FromSeconds(delay.TotalSeconds * 2);

                if (attempt == maxAttempts)
                {
                    return new DbBootstrapResult
                    {
                        Success = false,
                        ErrorMessage = ex.Message,
                        Messages = new List<string> { $"Bootstrap failed after {maxAttempts} attempts: {ex.Message}" }
                    };
                }
            }
        }

        return new DbBootstrapResult
        {
            Success = false,
            ErrorMessage = "Bootstrap failed after all retry attempts"
        };
    }


    private static List<DbCollectionConfiguration> CreateEntityCollections(Type[] entityTypes, DbBootstrapOptions options)
    {
        var collections = new List<DbCollectionConfiguration>();

        foreach (var entityType in entityTypes)
        {
            var collectionName = MongoDbAttributeMapper.GetCollectionName(entityType) ?? entityType.Name.ToLowerInvariant();
            var config = new DbCollectionConfiguration
            {
                CollectionName = collectionName,
                Indexes = CreateEntityIndexes(entityType, options)
            };

            // Configure sharding if appropriate
            if (options.EnableSharding && ShouldShardEntity(entityType))
            {
                config.ShardKey = CreateShardKeyForEntity(entityType);

                // Debug logging - remove after fix
                if (config.ShardKey == null)
                {
                    throw new InvalidOperationException($"Failed to create shard key for entity {entityType.Name} - this should not happen when sharding is enabled");
                }
            }

            collections.Add(config);
        }

        return collections;
    }

    private static List<DbIndexDefinition> CreateEntityIndexes(Type entityType, DbBootstrapOptions options)
    {
        // Use the proper EntityAttributeScanner to get indexes from DbIndexAttribute and automatic indexes
        return EntityAttributeScanner.ScanEntityForIndexes(entityType, options);
    }

    private static bool ShouldShardEntity(Type entityType)
    {
        // First, check for explicit sharding attributes - these take priority
        var dbCollectionAttr = entityType.GetCustomAttributes(typeof(DbCollectionAttribute), false)
            .Cast<DbCollectionAttribute>()
            .FirstOrDefault();

        if (dbCollectionAttr?.EnableSharding == true)
        {
            return true;
        }

        // Also check for explicit DbShardKey attribute
        var shardKeyAttributes = entityType.GetCustomAttributes(typeof(DbShardKeyAttribute), false);
        if (shardKeyAttributes.Length > 0)
        {
            return true;
        }

        // Fallback to property-based heuristics for automatic detection
        var properties = entityType.GetProperties();
        return properties.Any(p =>
            p.Name.Contains("Owner", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Bucket", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Tenant", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Partition", StringComparison.OrdinalIgnoreCase));
    }

    private static DbShardKey CreateShardKeyForEntity(Type entityType)
    {
        try
        {
            // First check if entity has a DbShardKeyAttribute - this takes priority
            var shardKeyAttributes = entityType.GetCustomAttributes(typeof(DbShardKeyAttribute), false);

            if (shardKeyAttributes.Length > 0)
            {
                var shardKeyAttr = (DbShardKeyAttribute)shardKeyAttributes[0];
                var shardKey = new DbShardKey();

                // Process the fields from the attribute
                if (shardKeyAttr.Fields != null)
                {
                    foreach (var fieldName in shardKeyAttr.Fields)
                    {
                        if (!string.IsNullOrEmpty(fieldName))
                        {
                            shardKey.Fields.Add(new DbShardKeyField
                            {
                                FieldName = fieldName,
                                Direction = DbSortDirection.Ascending,
                                IsHashed = false // Default to false, will be enhanced later if needed
                            });
                        }
                    }
                }

                // Handle region inclusion if specified
                if (shardKeyAttr.IncludeRegion)
                {
                    var regionFieldName = "region";
                    var insertPosition = 0;

                    // Determine position based on strategy
                    switch (shardKeyAttr.RegionStrategy)
                    {
                        case RegionShardingStrategy.RegionFirst:
                            insertPosition = 0;
                            break;
                        case RegionShardingStrategy.RegionMiddle:
                            insertPosition = shardKey.Fields.Count / 2;
                            break;
                        case RegionShardingStrategy.RegionLast:
                            insertPosition = shardKey.Fields.Count;
                            break;
                    }

                    shardKey.Fields.Insert(insertPosition, new DbShardKeyField
                    {
                        FieldName = regionFieldName,
                        Direction = DbSortDirection.Ascending,
                        IsHashed = false
                    });
                }

                return shardKey.Fields.Any() ? shardKey : null;
            }
        }
        catch (Exception ex)
        {
            // For debugging - in production this should be logged properly
            throw new InvalidOperationException($"Error creating shard key for entity {entityType.Name}: {ex.Message}", ex);
        }

        // Fallback to automatic shard key detection for entities without explicit attributes
        var properties = entityType.GetProperties();
        var fallbackShardKey = new DbShardKey();

        // Priority order for shard key selection
        var candidates = new[]
        {
            ("Bucket", false),
            ("BucketName", false),
            ("OwnerMerchantId", false),
            ("OwnerContactId", false),
            ("TenantId", false),
            ("PartitionKey", false),
            ("_id", true) // Fallback with hashing
        };

        foreach (var (propertyName, useHashing) in candidates)
        {
            var property = properties.FirstOrDefault(p => p.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase));
            if (property != null)
            {
                fallbackShardKey.Fields.Add(new DbShardKeyField
                {
                    FieldName = property.Name.ToLowerInvariant(),
                    Direction = DbSortDirection.Ascending,
                    IsHashed = useHashing
                });

                // For non-ID fields, add ID as secondary key for better distribution
                if (propertyName != "_id")
                {
                    fallbackShardKey.Fields.Add(new DbShardKeyField
                    {
                        FieldName = "_id",
                        Direction = DbSortDirection.Ascending,
                        IsHashed = true
                    });
                }

                break;
            }
        }

        return fallbackShardKey.Fields.Any() ? fallbackShardKey : null;
    }

    private static List<string> GenerateRecommendations(
        DbHealthStatus healthStatus,
        DbStatistics dbStats,
        DbShardingStatus shardingStatus)
    {
        var recommendations = new List<string>();

        // Check if sharding would be beneficial
        if (!shardingStatus.IsShardingEnabled && dbStats.CollectionCount > 10)
        {
            recommendations.Add("Consider enabling sharding for better scalability with multiple collections");
        }

        // Check database size
        if (dbStats.SizeInBytes > 100_000_000_000) // 100GB
        {
            recommendations.Add("Database size is large (>100GB). Consider implementing data archiving strategy");
        }

        // Check index efficiency
        var indexRatio = dbStats.IndexSizeInBytes / (double)dbStats.DataSizeInBytes;
        if (indexRatio > 0.5)
        {
            recommendations.Add("Index-to-data ratio is high (>50%). Review indexes for potential optimization");
        }

        // Check collection count for sharding benefits
        if (shardingStatus.IsShardingEnabled && shardingStatus.ShardedCollections.Count == 0)
        {
            recommendations.Add("Sharding is enabled but no collections are sharded. Consider sharding high-volume collections");
        }

        return recommendations;
    }

    #endregion
}


/// <summary>
/// Database status report with recommendations
/// </summary>
public class DbStatusReport
{
    /// <summary>
    /// Whether the status check was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if status check failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Report generation time
    /// </summary>
    public DateTime GeneratedAt { get; set; }

    /// <summary>
    /// Database health status
    /// </summary>
    public DbHealthStatus? HealthStatus { get; set; }

    /// <summary>
    /// Database statistics
    /// </summary>
    public DbStatistics? DatabaseStatistics { get; set; }

    /// <summary>
    /// Sharding status
    /// </summary>
    public DbShardingStatus? ShardingStatus { get; set; }

    /// <summary>
    /// Performance and optimization recommendations
    /// </summary>
    public List<string> Recommendations { get; set; } = new();
}