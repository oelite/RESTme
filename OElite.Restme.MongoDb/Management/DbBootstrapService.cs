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
    /// Bootstrap database for EdgeQ1 S3 storage with optimized settings
    /// </summary>
    /// <param name="options">S3 storage options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Bootstrap result</returns>
    public async Task<DbBootstrapResult> BootstrapForEdgeQ1Async(
        DbS3StorageOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new DbS3StorageOptions();

        // Create optimized configuration for S3 storage
        var configuration = new DbBootstrapConfiguration
        {
            EnableSharding = options.EnableSharding,
            EnablePreSplitting = options.EnablePreSplitting,
            PreSplitCount = options.PreSplitCount,
            CreateIndexesInBackground = true,
            MaxRetryAttempts = 3,
            TimeoutSeconds = 600, // Extended timeout for S3 bootstrap
            Collections = CreateEdgeQ1Collections(options)
        };

        return await BootstrapAsync(configuration, cancellationToken);
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

    private async Task<DbManagementResult> ExecuteBootstrapWithRetryAsync(
        DbBootstrapConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var maxAttempts = configuration.MaxRetryAttempts;
        var delay = TimeSpan.FromSeconds(2);

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var result = await _managementProvider.InitializeDatabaseAsync(configuration, cancellationToken);

                if (result.Success)
                {
                    result.Messages.Add($"Bootstrap completed successfully on attempt {attempt}");
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
                    return new DbManagementResult
                    {
                        Success = false,
                        ErrorMessage = ex.Message,
                        Messages = { $"Bootstrap failed after {maxAttempts} attempts: {ex.Message}" }
                    };
                }
            }
        }

        return new DbManagementResult
        {
            Success = false,
            ErrorMessage = "Bootstrap failed after all retry attempts"
        };
    }

    private static List<DbCollectionConfiguration> CreateEdgeQ1Collections(DbS3StorageOptions options)
    {
        return new List<DbCollectionConfiguration>
        {
            // Objects collection - optimized for S3 object storage
            new()
            {
                CollectionName = options.ObjectsCollectionName,
                ShardKey = options.EnableSharding ? new DbShardKey
                {
                    Fields = new List<DbShardKeyField>
                    {
                        new() { FieldName = "bucket", Direction = DbSortDirection.Ascending },
                        new() { FieldName = "keyHash", Direction = DbSortDirection.Ascending }
                    }
                } : null,
                Indexes = new List<DbIndexDefinition>
                {
                    // Shard key index (created automatically but explicit is better)
                    new()
                    {
                        Name = "idx_shard_key",
                        Fields = new List<DbIndexField>
                        {
                            new() { FieldName = "bucket", Direction = DbSortDirection.Ascending },
                            new() { FieldName = "keyHash", Direction = DbSortDirection.Ascending }
                        },
                        CreateInBackground = true
                    },
                    // Object lookup (exact key match)
                    new()
                    {
                        Name = "idx_object_lookup",
                        Fields = new List<DbIndexField>
                        {
                            new() { FieldName = "bucket", Direction = DbSortDirection.Ascending },
                            new() { FieldName = "key", Direction = DbSortDirection.Ascending }
                        },
                        IsUnique = true,
                        CreateInBackground = true
                    },
                    // Bucket listing (with prefix support)
                    new()
                    {
                        Name = "idx_bucket_listing",
                        Fields = new List<DbIndexField>
                        {
                            new() { FieldName = "bucket", Direction = DbSortDirection.Ascending },
                            new() { FieldName = "key", Direction = DbSortDirection.Ascending },
                            new() { FieldName = "lastModified", Direction = DbSortDirection.Descending }
                        },
                        CreateInBackground = true
                    },
                    // Owner isolation (tenant queries)
                    new()
                    {
                        Name = "idx_tenant_objects",
                        Fields = new List<DbIndexField>
                        {
                            new() { FieldName = "ownerId", Direction = DbSortDirection.Ascending },
                            new() { FieldName = "bucket", Direction = DbSortDirection.Ascending },
                            new() { FieldName = "lastModified", Direction = DbSortDirection.Descending }
                        },
                        CreateInBackground = true,
                        IsSparse = true
                    },
                    // Cleanup queries (by last modified)
                    new()
                    {
                        Name = "idx_cleanup",
                        Fields = new List<DbIndexField>
                        {
                            new() { FieldName = "lastModified", Direction = DbSortDirection.Ascending }
                        },
                        CreateInBackground = true,
                        TtlExpiration = options.ObjectTtl
                    }
                }
            },

            // Uploads collection - for multipart uploads
            new()
            {
                CollectionName = options.UploadsCollectionName,
                ShardKey = options.EnableSharding ? new DbShardKey
                {
                    Fields = new List<DbShardKeyField>
                    {
                        new() { FieldName = "uploadId", Direction = DbSortDirection.Ascending, IsHashed = true }
                    }
                } : null,
                Indexes = new List<DbIndexDefinition>
                {
                    new()
                    {
                        Name = "idx_upload_lookup",
                        Fields = new List<DbIndexField>
                        {
                            new() { FieldName = "bucket", Direction = DbSortDirection.Ascending },
                            new() { FieldName = "key", Direction = DbSortDirection.Ascending }
                        },
                        CreateInBackground = true
                    },
                    new()
                    {
                        Name = "idx_upload_cleanup",
                        Fields = new List<DbIndexField>
                        {
                            new() { FieldName = "expires", Direction = DbSortDirection.Ascending }
                        },
                        CreateInBackground = true,
                        TtlExpiration = TimeSpan.Zero // Auto-cleanup expired uploads
                    }
                }
            },

            // Buckets collection - metadata only
            new()
            {
                CollectionName = options.BucketsCollectionName,
                Indexes = new List<DbIndexDefinition>
                {
                    new()
                    {
                        Name = "idx_tenant_buckets",
                        Fields = new List<DbIndexField>
                        {
                            new() { FieldName = "ownerMerchantId", Direction = DbSortDirection.Ascending },
                            new() { FieldName = "bucketName", Direction = DbSortDirection.Ascending }
                        },
                        CreateInBackground = true,
                        IsSparse = true
                    },
                    new()
                    {
                        Name = "idx_contact_buckets",
                        Fields = new List<DbIndexField>
                        {
                            new() { FieldName = "ownerContactId", Direction = DbSortDirection.Ascending },
                            new() { FieldName = "bucketName", Direction = DbSortDirection.Ascending }
                        },
                        CreateInBackground = true,
                        IsSparse = true
                    }
                }
            }
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
            }

            collections.Add(config);
        }

        return collections;
    }

    private static List<DbIndexDefinition> CreateEntityIndexes(Type entityType, DbBootstrapOptions options)
    {
        var indexes = new List<DbIndexDefinition>();
        var properties = entityType.GetProperties();

        // Standard indexes for BaseEntity-derived types
        if (entityType.IsSubclassOf(typeof(BaseEntity)))
        {
            // Common indexable properties
            var indexableProperties = new Dictionary<string, (string indexName, bool isUnique, bool isSparse)>
            {
                { "CreatedOnUtc", ("idx_created", false, false) },
                { "UpdatedOnUtc", ("idx_updated", false, false) },
                { "IsActive", ("idx_active", false, false) },
                { "Status", ("idx_status", false, false) },
                { "OwnerMerchantId", ("idx_owner_merchant", false, true) },
                { "OwnerContactId", ("idx_owner_contact", false, true) },
                { "TenantId", ("idx_tenant", false, true) }
            };

            foreach (var (propertyName, (indexName, isUnique, isSparse)) in indexableProperties)
            {
                if (properties.Any(p => p.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase)))
                {
                    indexes.Add(new DbIndexDefinition
                    {
                        Name = indexName,
                        Fields = new List<DbIndexField>
                        {
                            new() { FieldName = propertyName.ToLowerInvariant(), Direction = DbSortDirection.Ascending }
                        },
                        IsUnique = isUnique,
                        IsSparse = isSparse,
                        CreateInBackground = options.CreateIndexesInBackground
                    });
                }
            }
        }

        return indexes;
    }

    private static bool ShouldShardEntity(Type entityType)
    {
        var properties = entityType.GetProperties();

        // Entities with owner isolation, bucket patterns, or high volume indicators should be sharded
        return properties.Any(p =>
            p.Name.Contains("Owner", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Bucket", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Tenant", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Partition", StringComparison.OrdinalIgnoreCase));
    }

    private static DbShardKey CreateShardKeyForEntity(Type entityType)
    {
        var properties = entityType.GetProperties();
        var shardKey = new DbShardKey();

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
                shardKey.Fields.Add(new DbShardKeyField
                {
                    FieldName = property.Name.ToLowerInvariant(),
                    Direction = DbSortDirection.Ascending,
                    IsHashed = useHashing
                });

                // For non-ID fields, add ID as secondary key for better distribution
                if (propertyName != "_id")
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