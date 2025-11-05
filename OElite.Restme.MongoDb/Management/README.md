# OElite.Restme.MongoDb Database Management

This module provides advanced database management capabilities for MongoDB through the Restme abstraction layer. It includes sharding, indexing, and bootstrap functionality without exposing any MongoDB-specific types to application code.

## Key Features

- **Full MongoDB Encapsulation**: No `MongoDB.*` references required in application code
- **Automatic Sharding**: Intelligent shard key selection and collection sharding
- **Optimized Indexing**: Performance-optimized index creation for various scenarios
- **S3 Storage Optimization**: Pre-configured setups for Q1 S3-compatible storage
- **Entity-Based Configuration**: Automatic configuration based on entity types and attributes
- **Health Monitoring**: Database health checks and performance recommendations
- **Retry Logic**: Robust error handling and retry mechanisms

## Quick Start

### Basic Entity Bootstrap

```csharp
using var dbCentre = new MyDbCentre("mongodb://localhost:27017/mydb");

// Bootstrap for specific entity types
var result = await dbCentre.BootstrapEntitiesAsync<MyEntity1, MyEntity2>();

if (result.Success)
{
    Console.WriteLine("Database bootstrap completed successfully!");
}
else
{
    Console.WriteLine($"Bootstrap failed: {result.ErrorMessage}");
}
```

### Q1 S3 Storage Bootstrap

```csharp
using var dbCentre = new Q1DbCentre("mongodb://localhost:27017/q1");

// Optimized for S3-style object storage
var result = await dbCentre.BootstrapS3StorageAsync(new DbS3StorageOptions
{
    EnableSharding = true,
    EnablePreSplitting = true,
    PreSplitCount = 512,
    ObjectTtl = TimeSpan.FromDays(90) // Optional auto-cleanup
});
```

### Custom Configuration Bootstrap

```csharp
var configuration = new DbBootstrapConfiguration
{
    EnableSharding = true,
    EnablePreSplitting = true,
    PreSplitCount = 256,
    Collections = new List<DbCollectionConfiguration>
    {
        new()
        {
            CollectionName = "objects",
            ShardKey = new DbShardKey
            {
                Fields = new List<DbShardKeyField>
                {
                    new() { FieldName = "bucket", Direction = DbSortDirection.Ascending },
                    new() { FieldName = "keyHash", Direction = DbSortDirection.Ascending }
                }
            },
            Indexes = new List<DbIndexDefinition>
            {
                new()
                {
                    Name = "idx_object_lookup",
                    Fields = new List<DbIndexField>
                    {
                        new() { FieldName = "bucket", Direction = DbSortDirection.Ascending },
                        new() { FieldName = "key", Direction = DbSortDirection.Ascending }
                    },
                    IsUnique = true
                }
            }
        }
    }
};

var result = await dbCentre.InitializeDatabaseAsync(configuration);
```

## Core Components

### IDbManagementProvider

The main interface for database management operations:

```csharp
public interface IDbManagementProvider
{
    Task<DbManagementResult> InitializeDatabaseAsync(DbBootstrapConfiguration configuration);
    Task<DbManagementResult> EnableShardingAsync(string databaseName);
    Task<DbManagementResult> CreateShardedCollectionAsync(string collectionName, DbShardKey shardKey);
    Task<DbManagementResult> CreateIndexesAsync(string collectionName, List<DbIndexDefinition> indexes);
    Task<DbShardingStatus> GetShardingStatusAsync();
    Task<DbStatistics> GetDatabaseStatisticsAsync();
    // ... more methods
}
```

### DbBootstrapService

High-level service for database initialization:

```csharp
var bootstrapService = new DbBootstrapService(dbCentre);

// Entity-based bootstrap
var result = await bootstrapService.BootstrapForEntitiesAsync(
    new[] { typeof(Q1Object), typeof(Q1Bucket) },
    new DbBootstrapOptions { EnableSharding = true });

// S3-optimized bootstrap
var s3Result = await bootstrapService.BootstrapForQ1Async(
    new DbS3StorageOptions { PreSplitCount = 512 });

// Get recommendations
var statusReport = await bootstrapService.GetDatabaseStatusAsync();
Console.WriteLine($"Recommendations: {string.Join(", ", statusReport.Recommendations)}");
```

### MongoDbCentre Extensions

Seamless integration with existing MongoDbCentre patterns:

```csharp
// Get management provider
var managementProvider = dbCentre.GetDbManagementProvider();

// Quick bootstrap
var result = await dbCentre.BootstrapS3StorageAsync();

// Health check
var health = await dbCentre.GetHealthStatusAsync();
```

## Configuration Models

### DbShardKey

```csharp
var shardKey = new DbShardKey
{
    Fields = new List<DbShardKeyField>
    {
        new() { FieldName = "bucket", Direction = DbSortDirection.Ascending },
        new() { FieldName = "keyHash", Direction = DbSortDirection.Ascending }
    },
    IsUnique = false
};
```

### DbIndexDefinition

```csharp
var index = new DbIndexDefinition
{
    Name = "idx_bucket_listing",
    Fields = new List<DbIndexField>
    {
        new() { FieldName = "bucket", Direction = DbSortDirection.Ascending },
        new() { FieldName = "key", Direction = DbSortDirection.Ascending },
        new() { FieldName = "lastModified", Direction = DbSortDirection.Descending }
    },
    IsUnique = false,
    IsSparse = false,
    CreateInBackground = true,
    TtlExpiration = TimeSpan.FromDays(90) // Optional TTL
};
```

## Entity Attribute Integration

The system automatically recognizes OElite entity attributes:

```csharp
[DbCollection("objects")]
public class Q1Object : BaseEntity
{
    [DbField("bucket")]
    public string Bucket { get; set; }

    [DbField("key")]
    public string Key { get; set; }

    [DbField("keyHash")]
    public string KeyHash { get; set; }

    [DbField("ownerId")]
    public DbObjectId? OwnerId { get; set; }

    // ... other properties
}
```

## S3 Storage Optimization

For S3-compatible storage scenarios (like Q1), use the pre-optimized configurations with GDPR region compliance:

### Production Q1 Object Storage Implementation

#### 1. **Entity Definitions with Region-Aware Sharding**

```csharp
[DbCollection("objects", EnableSharding = true, EnablePreSplitting = true)]
[DbShardKey("bucket", "keyHash", IncludeRegion = true, RegionStrategy = RegionShardingStrategy.RegionFirst)]
[DbIndex("idx_object_lookup", "bucket", "key", IsUnique = true)]
[DbIndex("idx_bucket_listing", "bucket", "key", "lastModified")]
[DbIndex("idx_region_compliance", "region", "bucket", "lastModified", IsSparse = true)]
[DbIndex("idx_tenant_objects", "ownerId", "region", "bucket", IsSparse = true)]
public class Q1Object : BaseEntity
{
    [DbField("bucket")]
    public string Bucket { get; set; } = string.Empty;

    [DbField("key")]
    public string Key { get; set; } = string.Empty;

    [DbField("keyHash")]
    public string KeyHash { get; set; } = string.Empty;

    [DbField("size")]
    public long Size { get; set; }

    [DbField("etag")]
    public string ETag { get; set; } = string.Empty;

    [DbField("lastModified")]
    public DateTime LastModified { get; set; }

    [DbField("contentType")]
    public string ContentType { get; set; } = string.Empty;

    [DbField("storageClass")]
    public string StorageClass { get; set; } = "STANDARD";

    [DbField("versionId")]
    public string? VersionId { get; set; }

    [DbField("ownerId")]
    public DbObjectId? OwnerId { get; set; }

    [DbField("userMetadata")]
    public Dictionary<string, string>? UserMetadata { get; set; }
}

[DbCollection("uploads", EnableSharding = true)]
[DbShardKey("uploadId", IsHashed = true, IncludeRegion = true)]
[DbIndex("idx_upload_lookup", "bucket", "key")]
[DbIndex("idx_upload_cleanup", "expires", TtlExpiration = "PT0S")]
[DbIndex("idx_region_uploads", "region", "initiated", IsSparse = true)]
public class Q1Upload : BaseEntity
{
    [DbField("uploadId")]
    public string UploadId { get; set; } = string.Empty;

    [DbField("bucket")]
    public string Bucket { get; set; } = string.Empty;

    [DbField("key")]
    public string Key { get; set; } = string.Empty;

    [DbField("initiated")]
    public DateTime Initiated { get; set; }

    [DbField("expires")]
    public DateTime? Expires { get; set; }

    [DbField("contentType")]
    public string ContentType { get; set; } = string.Empty;

    [DbField("partCount")]
    public int PartCount { get; set; }

    [DbField("totalSize")]
    public long TotalSize { get; set; }

    [DbField("ownerId")]
    public DbObjectId? OwnerId { get; set; }
}

[DbCollection("buckets")]
[DbIndex("idx_bucket_name", "bucketName", IsUnique = true)]
[DbIndex("idx_tenant_buckets", "ownerMerchantId", "bucketName", IsSparse = true)]
[DbIndex("idx_region_buckets", "region", "bucketName", IsSparse = true)]
public class Q1Bucket : BaseEntity
{
    [DbField("bucketName")]
    public string BucketName { get; set; } = string.Empty;

    [DbField("ownerMerchantId")]
    public DbObjectId? OwnerMerchantId { get; set; }

    [DbField("ownerContactId")]
    public DbObjectId? OwnerContactId { get; set; }

    [DbField("createdOnUtc")]
    public DateTime CreatedOnUtc { get; set; }

    [DbField("isActive")]
    public bool IsActive { get; set; } = true;
}
```

#### 2. **Production DbCentre Implementation**

```csharp
public class Q1DbCentre : MongoDbCentre
{
    public Q1DbCentre(string connectionString) : base(connectionString) { }

    public IMongoDbCollection<Q1Object> Objects =>
        GetMongoDbCollection<Q1Object>("objects");

    public IMongoDbCollection<Q1Upload> Uploads =>
        GetMongoDbCollection<Q1Upload>("uploads");

    public IMongoDbCollection<Q1Bucket> Buckets =>
        GetMongoDbCollection<Q1Bucket>("buckets");

    /// <summary>
    /// Initialize Q1 storage with GDPR region compliance
    /// </summary>
    public async Task<bool> InitializeAsync(GeographicConfiguration geoConfig)
    {
        var bootstrapService = new DbBootstrapService(this);
        var entityTypes = new[] { typeof(Q1Object), typeof(Q1Upload), typeof(Q1Bucket) };

        var result = await bootstrapService.BootstrapForEntitiesAsync(entityTypes, new DbBootstrapOptions
        {
            EnableSharding = true,
            EnablePreSplitting = true,
            PreSplitCount = 512,
            CreateIndexesInBackground = true,
            GeographicConfiguration = geoConfig
        });

        return result.Success;
    }
}
```

#### 3. **GDPR-Compliant Application Startup**

```csharp
public static async Task<Q1DbCentre> InitializeProductionStorageAsync(
    string connectionString,
    string region = "eu")
{
    // Geographic configuration for GDPR compliance
    var geoConfig = new GeographicConfiguration
    {
        DefaultRegion = region,
        EnableRegionAwareSharding = true,
        EnableCrossRegionQueries = false, // GDPR strict mode
        RegionMigrationStrategy = RegionMigrationStrategy.PreventMigration,
        EnableRegionValidation = true,
        AllowedRegions = new List<string> { "eu", "us", "ca", "uk", "apac" }
    };

    var dbCentre = new Q1DbCentre(connectionString);

    var success = await dbCentre.InitializeAsync(geoConfig);
    if (!success)
    {
        throw new InvalidOperationException("Failed to initialize Q1 storage");
    }

    return dbCentre;
}
```

#### 4. **GDPR Data Operations**

```csharp
// Store object with explicit region for GDPR compliance
public async Task<string> StoreObjectAsync(string bucket, string key, Stream data, string region = "eu")
{
    var obj = new Q1Object
    {
        Id = DbObjectId.NewId(),
        Bucket = bucket,
        Key = key,
        KeyHash = GenerateKeyHash(key),
        Size = data.Length,
        ETag = GenerateETag(data),
        LastModified = DateTime.UtcNow,
        ContentType = "application/octet-stream",
        Region = region, // GDPR compliance - explicit region
        OwnerId = GetCurrentTenantId()
    };

    await Objects.InsertOneAsync(obj);
    return obj.ETag;
}

// GDPR-compliant object listing (region-filtered)
public async Task<List<Q1Object>> ListObjectsAsync(string bucket, string region)
{
    return await Objects
        .Where(o => o.Bucket == bucket && o.Region == region && o.IsActive)
        .OrderByDescending(o => o.LastModified)
        .ToListAsync();
}

// GDPR right to erasure implementation
public async Task<bool> ErasePersonalDataAsync(DbObjectId userId, string region)
{
    var filter = new { OwnerId = userId, Region = region };

    // Mark for deletion rather than immediate delete for audit trail
    var result = await Objects
        .Where(o => o.OwnerId == userId && o.Region == region)
        .UpdateAsync(u => u
            .Set(o => o.IsActive, false)
            .Set(o => o.UserMetadata, new Dictionary<string, string>
            {
                ["gdpr_erasure"] = DateTime.UtcNow.ToString("O"),
                ["gdpr_request_id"] = Guid.NewGuid().ToString()
            }));

    return result.ModifiedCount > 0;
}
```

### Recommended Architecture with GDPR Regions

- **Objects Collection**: Sharded by `region + bucket + keyHash` for optimal GDPR compliance
- **Uploads Collection**: Sharded by `region + uploadId` (hashed) for multipart upload management
- **Buckets Collection**: Indexed by region for jurisdiction-specific bucket management

### Pre-configured Indexes for GDPR Compliance

- **Object Lookup**: `{ bucket: 1, key: 1 }` (unique)
- **Bucket Listing**: `{ bucket: 1, key: 1, lastModified: -1 }`
- **Region Compliance**: `{ region: 1, bucket: 1, lastModified: -1 }` (sparse)
- **Tenant Isolation**: `{ ownerId: 1, region: 1, bucket: 1 }` (sparse)
- **GDPR Erasure**: `{ ownerId: 1, region: 1, isActive: 1 }` (for data subject requests)

## Performance Considerations

### Sharding Strategy

- **Bucket + KeyHash**: Best for S3 workloads with even key distribution
- **Hashed ID**: Good for general-purpose collections with uniform access
- **Tenant ID**: Ideal for multi-tenant applications with tenant isolation

### Index Optimization

- Use sparse indexes for optional fields (like `ownerId`)
- Create compound indexes for common query patterns
- Use TTL indexes for automatic cleanup
- Create indexes in background mode for production environments

### Pre-splitting

```csharp
var options = new DbS3StorageOptions
{
    PreSplitCount = 512, // Creates 512 initial chunks for better distribution
    EnablePreSplitting = true
};
```

## Health Monitoring

### Database Health Status

```csharp
var health = await dbCentre.GetHealthStatusAsync();

if (health.IsAccessible)
{
    Console.WriteLine($"Database: {health.DatabaseStatistics.DatabaseName}");
    Console.WriteLine($"Collections: {health.DatabaseStatistics.CollectionCount}");
    Console.WriteLine($"Shards: {health.ShardingStatus.ShardCount}");
}
```

### Performance Recommendations

```csharp
var statusReport = await bootstrapService.GetDatabaseStatusAsync();

foreach (var recommendation in statusReport.Recommendations)
{
    Console.WriteLine($"Recommendation: {recommendation}");
}
```

## Error Handling and Retry Logic

The system includes built-in retry logic for resilient bootstrapping:

```csharp
var configuration = new DbBootstrapConfiguration
{
    MaxRetryAttempts = 5,
    TimeoutSeconds = 600
};

var result = await dbCentre.InitializeDatabaseAsync(configuration);

if (!result.Success)
{
    Console.WriteLine($"Bootstrap failed after {configuration.MaxRetryAttempts} attempts");
    Console.WriteLine($"Error: {result.ErrorMessage}");

    foreach (var message in result.Messages)
    {
        Console.WriteLine($"Detail: {message}");
    }
}
```

## Production Deployment Guidelines

### Development Environment

```csharp
var options = new DbBootstrapOptions
{
    EnableSharding = false, // Single node
    CreateIndexesInBackground = false // Faster for dev
};
```

### Production Environment

```csharp
var options = new DbBootstrapOptions
{
    EnableSharding = true,
    EnablePreSplitting = true,
    PreSplitCount = 512, // Higher for production scale
    CreateIndexesInBackground = true, // Non-blocking
    MaxRetryAttempts = 5,
    TimeoutSeconds = 1200 // Extended for large-scale operations
};
```

### Staging Environment

```csharp
var options = new DbBootstrapOptions
{
    EnableSharding = true,
    EnablePreSplitting = false, // Smaller scale
    CreateIndexesInBackground = true
};
```

## Integration with Existing Code

This module extends existing OElite.Restme.MongoDb functionality without breaking changes:

- All existing `MongoDbCentre` methods continue to work
- No changes required to existing entity definitions
- Backward compatible with existing `IMongoDbCollection` usage
- Seamless integration with existing dependency injection patterns

## Advanced Scenarios

### Custom Entity Analysis

The system can automatically analyze entities for optimal sharding and indexing:

```csharp
// Automatically detects owner fields for tenant isolation
public class MyEntity : BaseEntity
{
    public DbObjectId? OwnerMerchantId { get; set; } // Triggers tenant-aware sharding
    public string BucketName { get; set; } // Triggers bucket-based sharding
}
```

### Multi-Collection Operations

```csharp
var entityTypes = new[]
{
    typeof(Q1Object),
    typeof(Q1Bucket),
    typeof(Q1Upload),
    typeof(MyCustomEntity)
};

var result = await dbCentre.BootstrapEntitiesAsync(entityTypes);
```

### Configuration Validation

```csharp
var bootstrapService = new DbBootstrapService(dbCentre);
var validationResult = await bootstrapService.ValidateConfigurationAsync(configuration);

if (!validationResult.Success)
{
    foreach (var message in validationResult.Messages)
    {
        Console.WriteLine($"Validation Error: {message}");
    }
}
```

This comprehensive database management system provides enterprise-grade MongoDB capabilities while maintaining the clean, encapsulated Restme patterns that OElite applications depend on.