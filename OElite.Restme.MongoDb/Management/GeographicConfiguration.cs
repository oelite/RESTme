namespace OElite.Restme.MongoDb.Management;

/// <summary>
/// Geographic configuration options for GDPR compliance and data sovereignty
/// Provides region-aware database management capabilities
/// </summary>
public class GeographicConfiguration
{
    /// <summary>
    /// List of supported regions with their configurations
    /// </summary>
    public List<RegionConfiguration> Regions { get; set; } = new();

    /// <summary>
    /// Default region for entities without region specified
    /// </summary>
    public string DefaultRegion { get; set; } = "DEFAULT";

    /// <summary>
    /// Whether to enable automatic region detection from entity data
    /// </summary>
    public bool EnableAutoRegionDetection { get; set; } = true;

    /// <summary>
    /// Whether to allow data migration between regions
    /// </summary>
    public bool AllowCrossRegionMigration { get; set; } = true;

    /// <summary>
    /// Strategy for handling region changes in existing data
    /// </summary>
    public RegionMigrationStrategy MigrationStrategy { get; set; } = RegionMigrationStrategy.CopyAndDelete;

    /// <summary>
    /// Maximum time to keep data in source region during migration
    /// </summary>
    public TimeSpan MigrationRetentionPeriod { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Whether to create region-specific indexes
    /// </summary>
    public bool CreateRegionSpecificIndexes { get; set; } = true;
}

/// <summary>
/// Configuration for a specific geographic region
/// </summary>
public class RegionConfiguration
{
    /// <summary>
    /// Region identifier (e.g., "EU", "US", "APAC")
    /// </summary>
    public string RegionId { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable region name
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// MongoDB connection string for this region (if using region-specific clusters)
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Database name suffix for this region
    /// </summary>
    public string? DatabaseSuffix { get; set; }

    /// <summary>
    /// Legal jurisdiction information
    /// </summary>
    public JurisdictionInfo Jurisdiction { get; set; } = new();

    /// <summary>
    /// Data retention policies for this region
    /// </summary>
    public DataRetentionPolicy RetentionPolicy { get; set; } = new();

    /// <summary>
    /// Whether this region requires additional encryption
    /// </summary>
    public bool RequiresEncryptionAtRest { get; set; } = false;

    /// <summary>
    /// Custom shard tag for MongoDB zone sharding
    /// </summary>
    public string? ShardTag { get; set; }
}

/// <summary>
/// Legal jurisdiction information for compliance
/// </summary>
public class JurisdictionInfo
{
    /// <summary>
    /// Primary legal jurisdiction (e.g., "GDPR", "CCPA", "PIPEDA")
    /// </summary>
    public string PrimaryRegulation { get; set; } = string.Empty;

    /// <summary>
    /// List of applicable data protection regulations
    /// </summary>
    public List<string> ApplicableRegulations { get; set; } = new();

    /// <summary>
    /// Data processing lawful basis
    /// </summary>
    public string LawfulBasis { get; set; } = string.Empty;

    /// <summary>
    /// Data transfer restrictions
    /// </summary>
    public List<string> TransferRestrictions { get; set; } = new();
}

/// <summary>
/// Data retention policy for a region
/// </summary>
public class DataRetentionPolicy
{
    /// <summary>
    /// Default retention period for user data
    /// </summary>
    public TimeSpan DefaultRetentionPeriod { get; set; } = TimeSpan.FromDays(2555); // ~7 years

    /// <summary>
    /// Retention period for analytics data
    /// </summary>
    public TimeSpan AnalyticsRetentionPeriod { get; set; } = TimeSpan.FromDays(1095); // 3 years

    /// <summary>
    /// Retention period for audit logs
    /// </summary>
    public TimeSpan AuditLogRetentionPeriod { get; set; } = TimeSpan.FromDays(3653); // 10 years

    /// <summary>
    /// Whether to automatically delete data after retention period
    /// </summary>
    public bool AutoDeleteExpiredData { get; set; } = false;

    /// <summary>
    /// Grace period before permanent deletion
    /// </summary>
    public TimeSpan DeletionGracePeriod { get; set; } = TimeSpan.FromDays(30);
}

/// <summary>
/// Strategy for handling region migrations
/// </summary>
public enum RegionMigrationStrategy
{
    /// <summary>
    /// Copy data to new region then delete from source
    /// </summary>
    CopyAndDelete = 0,

    /// <summary>
    /// Copy data to new region and mark source as archived
    /// </summary>
    CopyAndArchive = 1,

    /// <summary>
    /// Prevent region changes (data sovereignty enforcement)
    /// </summary>
    PreventMigration = 2,

    /// <summary>
    /// Create symbolic links between regions (for federated access)
    /// </summary>
    FederatedAccess = 3
}

/// <summary>
/// Geographic bootstrap options for region-aware database setup
/// </summary>
public class GeographicBootstrapOptions : DbBootstrapOptions
{
    /// <summary>
    /// Geographic configuration
    /// </summary>
    public GeographicConfiguration GeographicConfig { get; set; } = new();

    /// <summary>
    /// Whether to create region-specific collections
    /// </summary>
    public bool CreateRegionSpecificCollections { get; set; } = false;

    /// <summary>
    /// Whether to use zone sharding for geographic isolation
    /// </summary>
    public bool UseZoneSharding { get; set; } = true;

    /// <summary>
    /// Prefix for region-specific collection names
    /// </summary>
    public string RegionCollectionPrefix { get; set; } = string.Empty;
}

/// <summary>
/// Helper class for creating common geographic configurations
/// </summary>
public static class GeographicConfigurationHelper
{
    /// <summary>
    /// Create a standard GDPR-compliant configuration
    /// </summary>
    /// <returns>GDPR-compliant geographic configuration</returns>
    public static GeographicConfiguration CreateGdprCompliantConfiguration()
    {
        return new GeographicConfiguration
        {
            DefaultRegion = "EU",
            EnableAutoRegionDetection = true,
            AllowCrossRegionMigration = true,
            MigrationStrategy = RegionMigrationStrategy.CopyAndArchive,
            MigrationRetentionPeriod = TimeSpan.FromDays(30),
            CreateRegionSpecificIndexes = true,
            Regions = new List<RegionConfiguration>
            {
                new()
                {
                    RegionId = "EU",
                    DisplayName = "European Union",
                    DatabaseSuffix = "_eu",
                    ShardTag = "EU",
                    RequiresEncryptionAtRest = true,
                    Jurisdiction = new JurisdictionInfo
                    {
                        PrimaryRegulation = "GDPR",
                        ApplicableRegulations = { "GDPR", "ePrivacy" },
                        LawfulBasis = "Consent",
                        TransferRestrictions = { "Adequacy Decision Required", "Standard Contractual Clauses" }
                    },
                    RetentionPolicy = new DataRetentionPolicy
                    {
                        DefaultRetentionPeriod = TimeSpan.FromDays(2555), // 7 years
                        AnalyticsRetentionPeriod = TimeSpan.FromDays(1095), // 3 years
                        AutoDeleteExpiredData = true,
                        DeletionGracePeriod = TimeSpan.FromDays(30)
                    }
                },
                new()
                {
                    RegionId = "US",
                    DisplayName = "United States",
                    DatabaseSuffix = "_us",
                    ShardTag = "US",
                    RequiresEncryptionAtRest = false,
                    Jurisdiction = new JurisdictionInfo
                    {
                        PrimaryRegulation = "CCPA",
                        ApplicableRegulations = { "CCPA", "COPPA", "HIPAA" },
                        LawfulBasis = "Legitimate Interest",
                        TransferRestrictions = { "Privacy Shield", "Standard Contractual Clauses" }
                    }
                },
                new()
                {
                    RegionId = "APAC",
                    DisplayName = "Asia Pacific",
                    DatabaseSuffix = "_apac",
                    ShardTag = "APAC",
                    RequiresEncryptionAtRest = true,
                    Jurisdiction = new JurisdictionInfo
                    {
                        PrimaryRegulation = "PDPA",
                        ApplicableRegulations = { "PDPA", "PIPEDA", "Privacy Act" },
                        LawfulBasis = "Consent"
                    }
                }
            }
        };
    }

    /// <summary>
    /// Create a configuration for global SaaS with data residency requirements
    /// </summary>
    /// <returns>Global SaaS geographic configuration</returns>
    public static GeographicConfiguration CreateGlobalSaasConfiguration()
    {
        return new GeographicConfiguration
        {
            DefaultRegion = "GLOBAL",
            EnableAutoRegionDetection = true,
            AllowCrossRegionMigration = true,
            MigrationStrategy = RegionMigrationStrategy.CopyAndDelete,
            MigrationRetentionPeriod = TimeSpan.FromDays(90),
            CreateRegionSpecificIndexes = true,
            Regions = new List<RegionConfiguration>
            {
                new()
                {
                    RegionId = "EU",
                    DisplayName = "Europe",
                    ShardTag = "EU_ZONE",
                    RequiresEncryptionAtRest = true
                },
                new()
                {
                    RegionId = "US",
                    DisplayName = "North America",
                    ShardTag = "US_ZONE",
                    RequiresEncryptionAtRest = false
                },
                new()
                {
                    RegionId = "APAC",
                    DisplayName = "Asia Pacific",
                    ShardTag = "APAC_ZONE",
                    RequiresEncryptionAtRest = true
                },
                new()
                {
                    RegionId = "GLOBAL",
                    DisplayName = "Global (No Restrictions)",
                    ShardTag = "GLOBAL_ZONE",
                    RequiresEncryptionAtRest = false
                }
            }
        };
    }

    /// <summary>
    /// Create EdgeQ1 S3 storage configuration with geographic isolation
    /// </summary>
    /// <returns>EdgeQ1-optimized geographic configuration</returns>
    public static GeographicConfiguration CreateEdgeQ1Configuration()
    {
        return new GeographicConfiguration
        {
            DefaultRegion = "US",
            EnableAutoRegionDetection = true,
            AllowCrossRegionMigration = true,
            MigrationStrategy = RegionMigrationStrategy.CopyAndDelete,
            MigrationRetentionPeriod = TimeSpan.FromDays(7), // Faster migration for object storage
            CreateRegionSpecificIndexes = true,
            Regions = new List<RegionConfiguration>
            {
                new()
                {
                    RegionId = "US",
                    DisplayName = "US East",
                    ShardTag = "US_EAST",
                    RetentionPolicy = new DataRetentionPolicy
                    {
                        DefaultRetentionPeriod = TimeSpan.FromDays(2555),
                        AutoDeleteExpiredData = false // Manual lifecycle management
                    }
                },
                new()
                {
                    RegionId = "EU",
                    DisplayName = "EU West",
                    ShardTag = "EU_WEST",
                    RequiresEncryptionAtRest = true,
                    RetentionPolicy = new DataRetentionPolicy
                    {
                        DefaultRetentionPeriod = TimeSpan.FromDays(2555),
                        AutoDeleteExpiredData = true // GDPR compliance
                    }
                },
                new()
                {
                    RegionId = "APAC",
                    DisplayName = "APAC Singapore",
                    ShardTag = "APAC_SG",
                    RequiresEncryptionAtRest = true
                }
            }
        };
    }

    /// <summary>
    /// Validate geographic configuration
    /// </summary>
    /// <param name="config">Configuration to validate</param>
    /// <returns>Validation result</returns>
    public static GeographicValidationResult ValidateConfiguration(GeographicConfiguration config)
    {
        var result = new GeographicValidationResult();

        // Validate regions
        if (!config.Regions.Any())
        {
            result.Errors.Add("At least one region must be configured");
        }

        // Check for duplicate region IDs
        var duplicateRegions = config.Regions
            .GroupBy(r => r.RegionId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var duplicate in duplicateRegions)
        {
            result.Errors.Add($"Duplicate region ID: {duplicate}");
        }

        // Validate default region exists
        if (!string.IsNullOrEmpty(config.DefaultRegion) &&
            !config.Regions.Any(r => r.RegionId.Equals(config.DefaultRegion, StringComparison.OrdinalIgnoreCase)))
        {
            result.Errors.Add($"Default region '{config.DefaultRegion}' not found in region list");
        }

        // Validate each region
        foreach (var region in config.Regions)
        {
            if (string.IsNullOrWhiteSpace(region.RegionId))
            {
                result.Errors.Add("Region ID cannot be empty");
            }

            if (string.IsNullOrWhiteSpace(region.DisplayName))
            {
                result.Warnings.Add($"Region '{region.RegionId}' has no display name");
            }
        }

        result.IsValid = !result.Errors.Any();
        return result;
    }
}

/// <summary>
/// Result of geographic configuration validation
/// </summary>
public class GeographicValidationResult
{
    /// <summary>
    /// Whether the configuration is valid
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Validation errors that must be fixed
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Validation warnings (non-blocking)
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}