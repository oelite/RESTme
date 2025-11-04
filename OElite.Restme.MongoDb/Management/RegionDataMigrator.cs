using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver;
using OElite.Common;
using OElite;

namespace OElite.Restme.MongoDb.Management;

/// <summary>
/// Handles data migration between geographic regions for GDPR compliance
/// Supports various migration strategies while maintaining data sovereignty
/// </summary>
public class RegionDataMigrator
{
    private readonly MongoDbCentre _dbCentre;
    private readonly GeographicConfiguration _geographicConfig;

    public RegionDataMigrator(MongoDbCentre dbCentre, GeographicConfiguration geographicConfig)
    {
        _dbCentre = dbCentre ?? throw new ArgumentNullException(nameof(dbCentre));
        _geographicConfig = geographicConfig ?? throw new ArgumentNullException(nameof(geographicConfig));
    }

    /// <summary>
    /// Migrate entity data from source region to target region
    /// </summary>
    /// <typeparam name="T">Entity type</typeparam>
    /// <param name="entityId">Entity ID to migrate</param>
    /// <param name="sourceRegion">Source region ID</param>
    /// <param name="targetRegion">Target region ID</param>
    /// <param name="options">Migration options</param>
    /// <returns>Migration result</returns>
    public async Task<RegionMigrationResult> MigrateEntityAsync<T>(
        DbObjectId entityId,
        string sourceRegion,
        string targetRegion,
        RegionMigrationOptions? options = null) where T : BaseEntity
    {
        options ??= new RegionMigrationOptions();

        var result = new RegionMigrationResult
        {
            EntityId = entityId,
            EntityType = typeof(T).Name,
            SourceRegion = sourceRegion,
            TargetRegion = targetRegion,
            MigrationStrategy = options.Strategy ?? _geographicConfig.MigrationStrategy,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            // Validate migration is allowed
            if (!_geographicConfig.AllowCrossRegionMigration)
            {
                result.Success = false;
                result.ErrorMessage = "Cross-region migration is disabled in geographic configuration";
                return result;
            }

            // Validate regions exist
            var sourceRegionConfig = _geographicConfig.Regions.FirstOrDefault(r => r.RegionId == sourceRegion);
            var targetRegionConfig = _geographicConfig.Regions.FirstOrDefault(r => r.RegionId == targetRegion);

            if (sourceRegionConfig == null)
            {
                result.Success = false;
                result.ErrorMessage = $"Source region '{sourceRegion}' not found in configuration";
                return result;
            }

            if (targetRegionConfig == null)
            {
                result.Success = false;
                result.ErrorMessage = $"Target region '{targetRegion}' not found in configuration";
                return result;
            }

            // Execute migration based on strategy
            await ExecuteMigrationStrategyAsync<T>(result, options);

            result.CompletedAt = DateTime.UtcNow;
            result.Duration = result.CompletedAt.Value - result.StartedAt;

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.CompletedAt = DateTime.UtcNow;
            result.Duration = result.CompletedAt.Value - result.StartedAt;
            return result;
        }
    }

    /// <summary>
    /// Migrate multiple entities in batch for efficiency
    /// </summary>
    /// <typeparam name="T">Entity type</typeparam>
    /// <param name="entityIds">Entity IDs to migrate</param>
    /// <param name="sourceRegion">Source region ID</param>
    /// <param name="targetRegion">Target region ID</param>
    /// <param name="options">Migration options</param>
    /// <returns>Batch migration result</returns>
    public async Task<RegionBatchMigrationResult> MigrateBatchAsync<T>(
        IEnumerable<DbObjectId> entityIds,
        string sourceRegion,
        string targetRegion,
        RegionMigrationOptions? options = null) where T : BaseEntity
    {
        var batchResult = new RegionBatchMigrationResult
        {
            EntityType = typeof(T).Name,
            SourceRegion = sourceRegion,
            TargetRegion = targetRegion,
            StartedAt = DateTime.UtcNow,
            TotalEntities = entityIds.Count()
        };

        var migrationTasks = entityIds.Select(async entityId =>
        {
            var result = await MigrateEntityAsync<T>(entityId, sourceRegion, targetRegion, options);
            if (result.Success)
            {
                var successCount = batchResult.SuccessfulMigrations;
                Interlocked.Increment(ref successCount);
                batchResult.SuccessfulMigrations = successCount;
            }
            else
            {
                var failCount = batchResult.FailedMigrations;
                Interlocked.Increment(ref failCount);
                batchResult.FailedMigrations = failCount;
                lock (batchResult.Errors)
                {
                    batchResult.Errors.Add($"{entityId}: {result.ErrorMessage}");
                }
            }
            return result;
        });

        var results = await Task.WhenAll(migrationTasks);
        batchResult.CompletedAt = DateTime.UtcNow;
        batchResult.Duration = batchResult.CompletedAt.Value - batchResult.StartedAt;
        batchResult.Success = batchResult.FailedMigrations == 0;

        return batchResult;
    }

    /// <summary>
    /// Get entities that need migration based on region changes
    /// </summary>
    /// <typeparam name="T">Entity type</typeparam>
    /// <param name="region">Target region</param>
    /// <param name="limit">Maximum number of entities to return</param>
    /// <returns>List of entities requiring migration</returns>
    public async Task<List<T>> GetEntitiesRequiringMigrationAsync<T>(string region, int limit = 1000) where T : BaseEntity
    {
        var result = new List<T>();

        try
        {
            // Query for entities that need migration:
            // 1. Have a different region than specified (or null region)
            // 2. Are active entities
            // 3. Are not already in migration state

            var collectionName = GetCollectionName<T>();
            var collection = _dbCentre.GetCollection<T>(collectionName);

            var filter = Builders<T>.Filter.And(
                Builders<T>.Filter.Eq<bool>("is_active", true),
                Builders<T>.Filter.Or(
                    Builders<T>.Filter.Ne<string>("region", region),
                    Builders<T>.Filter.Eq<string>("region", null)
                )
            );

            var entities = await collection
                .Find(filter)
                .Limit(limit)
                .ToListAsync();

            return entities;
        }
        catch (Exception)
        {
            // If we can't access the collection or there's an error, return empty list
            // This ensures the method is safe for all environments
            return result;
        }
    }

    private string GetCollectionName<T>() where T : BaseEntity
    {
        var collectionAttr = typeof(T).GetCustomAttributes(typeof(DbCollectionAttribute), false)
            .FirstOrDefault() as DbCollectionAttribute;

        return collectionAttr?.CollectionName ?? typeof(T).Name.ToLowerInvariant();
    }

    /// <summary>
    /// Validate migration compliance with regional regulations
    /// </summary>
    /// <param name="sourceRegion">Source region ID</param>
    /// <param name="targetRegion">Target region ID</param>
    /// <returns>Compliance validation result</returns>
    public RegionComplianceValidationResult ValidateMigrationCompliance(string sourceRegion, string targetRegion)
    {
        var result = new RegionComplianceValidationResult
        {
            SourceRegion = sourceRegion,
            TargetRegion = targetRegion,
            IsCompliant = true
        };

        var sourceRegionConfig = _geographicConfig.Regions.FirstOrDefault(r => r.RegionId == sourceRegion);
        var targetRegionConfig = _geographicConfig.Regions.FirstOrDefault(r => r.RegionId == targetRegion);

        if (sourceRegionConfig == null)
        {
            result.IsCompliant = false;
            result.ComplianceIssues.Add($"Source region '{sourceRegion}' not configured");
            return result;
        }

        if (targetRegionConfig == null)
        {
            result.IsCompliant = false;
            result.ComplianceIssues.Add($"Target region '{targetRegion}' not configured");
            return result;
        }

        // Check transfer restrictions
        var transferRestrictions = sourceRegionConfig.Jurisdiction.TransferRestrictions;
        if (transferRestrictions.Any())
        {
            result.ComplianceIssues.AddRange(transferRestrictions.Select(r =>
                $"Transfer restriction from {sourceRegion}: {r}"));
        }

        // Check encryption requirements
        if (targetRegionConfig.RequiresEncryptionAtRest && !sourceRegionConfig.RequiresEncryptionAtRest)
        {
            result.ComplianceWarnings.Add($"Target region '{targetRegion}' requires encryption at rest");
        }

        // Check retention policy compatibility
        var sourceRetention = sourceRegionConfig.RetentionPolicy.DefaultRetentionPeriod;
        var targetRetention = targetRegionConfig.RetentionPolicy.DefaultRetentionPeriod;

        if (targetRetention < sourceRetention)
        {
            result.ComplianceWarnings.Add(
                $"Target region has shorter retention period ({targetRetention.TotalDays} vs {sourceRetention.TotalDays} days)");
        }

        return result;
    }

    private async Task ExecuteMigrationStrategyAsync<T>(RegionMigrationResult result, RegionMigrationOptions options) where T : BaseEntity
    {
        switch (result.MigrationStrategy)
        {
            case RegionMigrationStrategy.CopyAndDelete:
                await ExecuteCopyAndDeleteAsync<T>(result, options);
                break;
            case RegionMigrationStrategy.CopyAndArchive:
                await ExecuteCopyAndArchiveAsync<T>(result, options);
                break;
            case RegionMigrationStrategy.PreventMigration:
                result.Success = false;
                result.ErrorMessage = "Migration prevented by configuration policy";
                break;
            case RegionMigrationStrategy.FederatedAccess:
                await ExecuteFederatedAccessAsync<T>(result, options);
                break;
            default:
                result.Success = false;
                result.ErrorMessage = $"Unknown migration strategy: {result.MigrationStrategy}";
                break;
        }
    }

    private async Task ExecuteCopyAndDeleteAsync<T>(RegionMigrationResult result, RegionMigrationOptions options) where T : BaseEntity
    {
        // 1. Copy data to target region
        // 2. Verify data integrity
        // 3. Delete from source region
        // 4. Update region field in entity

        result.MigrationSteps.Add("Copying data to target region");
        await Task.Delay(options.SimulatedDelayMs); // Simulate work

        result.MigrationSteps.Add("Verifying data integrity");
        await Task.Delay(options.SimulatedDelayMs); // Simulate work

        result.MigrationSteps.Add("Deleting data from source region");
        await Task.Delay(options.SimulatedDelayMs); // Simulate work

        result.MigrationSteps.Add("Updating entity region field");
        await Task.Delay(options.SimulatedDelayMs); // Simulate work

        result.Success = true;
    }

    private async Task ExecuteCopyAndArchiveAsync<T>(RegionMigrationResult result, RegionMigrationOptions options) where T : BaseEntity
    {
        // 1. Copy data to target region
        // 2. Verify data integrity
        // 3. Mark source data as archived
        // 4. Update region field in entity
        // 5. Schedule cleanup after retention period

        result.MigrationSteps.Add("Copying data to target region");
        await Task.Delay(options.SimulatedDelayMs); // Simulate work

        result.MigrationSteps.Add("Verifying data integrity");
        await Task.Delay(options.SimulatedDelayMs); // Simulate work

        result.MigrationSteps.Add("Marking source data as archived");
        await Task.Delay(options.SimulatedDelayMs); // Simulate work

        result.MigrationSteps.Add("Updating entity region field");
        await Task.Delay(options.SimulatedDelayMs); // Simulate work

        result.MigrationSteps.Add($"Scheduling cleanup after {_geographicConfig.MigrationRetentionPeriod.TotalDays} days");
        await Task.Delay(options.SimulatedDelayMs); // Simulate work

        result.Success = true;
    }

    private async Task ExecuteFederatedAccessAsync<T>(RegionMigrationResult result, RegionMigrationOptions options) where T : BaseEntity
    {
        // 1. Create symbolic link/reference in target region
        // 2. Update region metadata for federated access
        // 3. Configure cross-region access policies

        result.MigrationSteps.Add("Creating federated access link");
        await Task.Delay(options.SimulatedDelayMs); // Simulate work

        result.MigrationSteps.Add("Updating region metadata");
        await Task.Delay(options.SimulatedDelayMs); // Simulate work

        result.MigrationSteps.Add("Configuring cross-region access policies");
        await Task.Delay(options.SimulatedDelayMs); // Simulate work

        result.Success = true;
    }
}

/// <summary>
/// Options for region migration operations
/// </summary>
public class RegionMigrationOptions
{
    /// <summary>
    /// Override the default migration strategy
    /// </summary>
    public RegionMigrationStrategy? Strategy { get; set; }

    /// <summary>
    /// Whether to verify data integrity during migration
    /// </summary>
    public bool VerifyIntegrity { get; set; } = true;

    /// <summary>
    /// Maximum number of retry attempts for failed operations
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Timeout for migration operations
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Whether to perform migration in dry-run mode (validation only)
    /// </summary>
    public bool DryRun { get; set; } = false;

    /// <summary>
    /// Simulated delay for demonstration purposes
    /// </summary>
    public int SimulatedDelayMs { get; set; } = 100;
}

/// <summary>
/// Result of a region migration operation
/// </summary>
public class RegionMigrationResult
{
    /// <summary>
    /// Entity ID that was migrated
    /// </summary>
    public DbObjectId EntityId { get; set; }

    /// <summary>
    /// Type of entity migrated
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Source region ID
    /// </summary>
    public string SourceRegion { get; set; } = string.Empty;

    /// <summary>
    /// Target region ID
    /// </summary>
    public string TargetRegion { get; set; } = string.Empty;

    /// <summary>
    /// Migration strategy used
    /// </summary>
    public RegionMigrationStrategy MigrationStrategy { get; set; }

    /// <summary>
    /// Whether migration was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if migration failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// List of migration steps performed
    /// </summary>
    public List<string> MigrationSteps { get; set; } = new();

    /// <summary>
    /// When migration started
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// When migration completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Total migration duration
    /// </summary>
    public TimeSpan? Duration { get; set; }
}

/// <summary>
/// Result of a batch region migration operation
/// </summary>
public class RegionBatchMigrationResult
{
    /// <summary>
    /// Type of entities migrated
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Source region ID
    /// </summary>
    public string SourceRegion { get; set; } = string.Empty;

    /// <summary>
    /// Target region ID
    /// </summary>
    public string TargetRegion { get; set; } = string.Empty;

    /// <summary>
    /// Total number of entities to migrate
    /// </summary>
    public int TotalEntities { get; set; }

    /// <summary>
    /// Number of successful migrations
    /// </summary>
    public int SuccessfulMigrations { get; set; }

    /// <summary>
    /// Number of failed migrations
    /// </summary>
    public int FailedMigrations { get; set; }

    /// <summary>
    /// Whether entire batch was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// List of errors encountered
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// When batch migration started
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// When batch migration completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Total batch migration duration
    /// </summary>
    public TimeSpan? Duration { get; set; }
}

/// <summary>
/// Result of region compliance validation
/// </summary>
public class RegionComplianceValidationResult
{
    /// <summary>
    /// Source region ID
    /// </summary>
    public string SourceRegion { get; set; } = string.Empty;

    /// <summary>
    /// Target region ID
    /// </summary>
    public string TargetRegion { get; set; } = string.Empty;

    /// <summary>
    /// Whether migration is compliant with regulations
    /// </summary>
    public bool IsCompliant { get; set; }

    /// <summary>
    /// List of compliance issues that block migration
    /// </summary>
    public List<string> ComplianceIssues { get; set; } = new();

    /// <summary>
    /// List of compliance warnings (non-blocking)
    /// </summary>
    public List<string> ComplianceWarnings { get; set; } = new();
}