# Attribute-Based Database Configuration Guide

## Overview

The enhanced OElite.Restme.MongoDb now supports **attribute-based database configuration**, allowing you to define sharding, indexing, and collection settings directly at the entity level using C# attributes. This approach provides:

- **Declarative Configuration**: Database schema defined alongside entity code
- **Type Safety**: Compile-time validation of configuration
- **Code Discoverability**: Easy to see database configuration in entity definitions
- **Automatic Scanning**: No manual configuration mapping required
- **OElite Consistency**: Follows established attribute patterns

## Core Attributes

### `[DbCollection]` - Enhanced Collection Configuration

```csharp
[DbCollection("objects",
    EnableSharding = true,
    EnablePreSplitting = true,
    PreSplitChunks = 512,
    TtlExpirationSeconds = 7776000, // 90 days
    BootstrapPriority = 1)]
public class EdgeQ1Object : BaseEntity
{
    // Entity properties...
}
```

**Properties:**
- `EnableSharding`: Whether to enable sharding for this collection
- `EnablePreSplitting`: Whether to pre-split shard chunks during bootstrap
- `PreSplitChunks`: Number of chunks to create (default: 256)
- `TtlExpirationSeconds`: Automatic document expiration in seconds (0 = no TTL)
- `ValidateSchema`: Whether to validate schema during bootstrap
- `BootstrapPriority`: Creation order (lower numbers first)

### `[DbShardKey]` - Shard Key Configuration

```csharp
// Compound shard key (recommended for S3 workloads)
[DbShardKey("Bucket", "KeyHash")]
public class EdgeQ1Object : BaseEntity { }

// Hashed shard key (for even distribution)
[DbShardKey.Hashed("UserId")]
public class UserEvent : BaseEntity { }

// Advanced compound key with mixed sharding
[DbShardKey("TenantId", "EventDate", IsUnique = false)]
public class AnalyticsEvent : BaseEntity { }
```

**Configuration Options:**
- **Fields**: Array of field names for the shard key
- **IsHashed**: Array indicating which fields use hashed sharding
- **Directions**: Sort directions (1 = ascending, -1 = descending)
- **IsUnique**: Whether the shard key enforces uniqueness
- **PreSplitChunks**: Custom chunk count for this collection

**Helper Methods:**
- `DbShardKey.Hashed(field)`: Single hashed field
- `DbShardKey.Compound(bucket, key, hashSecondary)`: Bucket + key pattern

### `[DbIndex]` - Index Configuration

```csharp
[DbIndex.Unique("idx_object_lookup", "Bucket", "Key")]
[DbIndex.Compound("idx_bucket_listing", "Bucket", "Key", "LastModified")]
[DbIndex.Sparse("idx_tenant_objects", "OwnerId", "Bucket")]
[DbIndex.Ttl("idx_cleanup", "CreatedOnUtc", 7776000)] // 90 days
[DbIndex.Text("idx_search", "Name", "Description")]
[DbIndex.Hashed("idx_distribution", "UserId")]
public class MyEntity : BaseEntity { }
```

**Index Types:**
- `DbIndex.Unique()`: Unique constraint index
- `DbIndex.Compound()`: Multi-field compound index
- `DbIndex.Sparse()`: Sparse index (only documents with field)
- `DbIndex.Ttl()`: Time-to-live automatic cleanup index
- `DbIndex.Text()`: Full-text search index
- `DbIndex.Hashed()`: Hashed index for distribution

**Properties:**
- `Priority`: Index creation order (lower numbers first)
- `CreateInBackground`: Whether to create non-blocking (default: true)
- `IsUnique`: Enforce uniqueness
- `IsSparse`: Only index documents with the field
- `TtlExpirationSeconds`: Auto-delete documents after specified seconds

## Complete Entity Example

### EdgeQ1 S3 Storage Entity

```csharp
[DbCollection("objects",
    EnableSharding = true,
    EnablePreSplitting = true,
    PreSplitChunks = 512)]
[DbShardKey("Bucket", "KeyHash")] // Optimal for S3 workloads
[DbIndex("idx_shard_key", "Bucket", "KeyHash", Priority = 1)]
[DbIndex.Unique("idx_object_lookup", "Bucket", "Key")]
[DbIndex.Compound("idx_bucket_listing", "Bucket", "Key", "LastModified")]
[DbIndex.Sparse("idx_tenant_objects", "OwnerId", "Bucket", "LastModified")]
[DbIndex.Compound("idx_storage_class", "StorageClass", "LastModified")]
public class EdgeQ1Object : BaseEntity
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

    [DbField("ownerId")]
    public DbObjectId? OwnerId { get; set; }
}
```

## Usage Examples

### Automatic Bootstrap

```csharp
// All configuration is defined in attributes - no manual setup needed!
using var dbCentre = new MyDbCentre("mongodb://localhost:27017/mydb");

var result = await dbCentre.BootstrapEntitiesAsync<EdgeQ1Object, EdgeQ1Bucket>();

if (result.Success)
{
    Console.WriteLine("✅ Database configured automatically from entity attributes!");
}
```

### Configuration Validation

```csharp
var entityTypes = new[] { typeof(EdgeQ1Object), typeof(EdgeQ1Bucket) };
var validation = EntityAttributeScanner.ValidateEntityConfigurations(entityTypes);

if (validation.IsValid)
{
    Console.WriteLine($"✅ All {validation.ValidatedEntities.Count} entities are valid");
}
else
{
    foreach (var error in validation.ValidationErrors)
    {
        Console.WriteLine($"❌ {error}");
    }
}
```

### Custom Bootstrap Options

```csharp
var options = new DbBootstrapOptions
{
    EnableSharding = true,          // Global sharding enable
    EnablePreSplitting = true,      // Global pre-splitting
    CreateIndexesInBackground = true, // Non-blocking index creation
    MaxRetryAttempts = 5,           // Retry on failures
    TimeoutSeconds = 600            // Extended timeout
};

var result = await dbCentre.BootstrapEntitiesAsync(entityTypes, options);
```

## Advanced Patterns

### Multi-Tenant Sharding

```csharp
[DbCollection("user_events", EnableSharding = true)]
[DbShardKey("TenantId", "UserId")] // Tenant isolation with user distribution
[DbIndex.Compound("idx_tenant_timeline", "TenantId", "EventDate")]
[DbIndex.Sparse("idx_user_events", "UserId", "EventDate")]
public class UserEvent : BaseEntity
{
    [DbField("tenantId")]
    public DbObjectId TenantId { get; set; }

    [DbField("userId")]
    public DbObjectId UserId { get; set; }

    [DbField("eventDate")]
    public DateTime EventDate { get; set; }
}
```

### Time-Series Data with TTL

```csharp
[DbCollection("metrics", TtlExpirationSeconds = 2592000)] // 30 days
[DbShardKey("MetricType", "Timestamp")]
[DbIndex.Compound("idx_metric_timeline", "MetricType", "Timestamp")]
[DbIndex.Ttl("idx_auto_cleanup", "Timestamp", 2592000)]
public class Metric : BaseEntity
{
    [DbField("metricType")]
    public string MetricType { get; set; } = string.Empty;

    [DbField("timestamp")]
    public DateTime Timestamp { get; set; }

    [DbField("value")]
    public double Value { get; set; }
}
```

### Search-Optimized Entity

```csharp
[DbCollection("products")]
[DbIndex.Text("idx_product_search", "Name", "Description", "Tags")]
[DbIndex.Compound("idx_category_price", "CategoryId", "Price")]
[DbIndex.Sparse("idx_merchant_products", "MerchantId", "IsActive")]
public class Product : BaseEntity
{
    [DbField("name")]
    public string Name { get; set; } = string.Empty;

    [DbField("description")]
    public string Description { get; set; } = string.Empty;

    [DbField("tags")]
    public string Tags { get; set; } = string.Empty;

    [DbField("categoryId")]
    public DbObjectId CategoryId { get; set; }

    [DbField("merchantId")]
    public DbObjectId? MerchantId { get; set; }

    [DbField("price")]
    public decimal Price { get; set; }
}
```

## Attribute Scanning Process

The system automatically:

1. **Scans Entity Attributes**: Discovers `DbCollection`, `DbShardKey`, and `DbIndex` attributes
2. **Maps Property Names**: Uses `DbField` attributes to map to database field names
3. **Validates Configuration**: Checks for conflicts and invalid settings
4. **Generates Configuration**: Creates `DbBootstrapConfiguration` automatically
5. **Applies Priorities**: Creates collections and indexes in the correct order
6. **Adds Auto-Indexes**: Generates common indexes for `BaseEntity` properties

## Automatic Index Generation

The system automatically creates indexes for common `BaseEntity` properties:

- `CreatedOnUtc` → `idx_auto_created`
- `UpdatedOnUtc` → `idx_auto_updated`
- `IsActive` → `idx_auto_active`
- `OwnerMerchantId` → `idx_auto_owner_merchant` (sparse)
- `OwnerContactId` → `idx_auto_owner_contact` (sparse)

These are only created if no manual index exists for the same field.

## Best Practices

### Shard Key Design

```csharp
// ✅ Good: Compound key with high cardinality + distribution
[DbShardKey("TenantId", "UserId")]

// ✅ Good: Bucket pattern for S3-style workloads
[DbShardKey("Bucket", "KeyHash")]

// ✅ Good: Hashed ID for even distribution
[DbShardKey.Hashed("_id")]

// ❌ Poor: Low cardinality field
[DbShardKey("Status")] // Only a few possible values

// ❌ Poor: Monotonically increasing field
[DbShardKey("CreatedOnUtc")] // Creates hotspots
```

### Index Design

```csharp
// ✅ Good: Query-aligned compound indexes
[DbIndex.Compound("idx_user_timeline", "UserId", "CreatedOnUtc")]

// ✅ Good: Sparse indexes for optional fields
[DbIndex.Sparse("idx_tenant_data", "TenantId", "DataType")]

// ✅ Good: Unique constraints where appropriate
[DbIndex.Unique("idx_username", "Username")]

// ❌ Poor: Too many single-field indexes
[DbIndex("idx_field1", "Field1")]
[DbIndex("idx_field2", "Field2")]
[DbIndex("idx_field3", "Field3")] // Consider compound indexes instead
```

### Collection Organization

```csharp
// ✅ Good: High-priority collections created first
[DbCollection("buckets", BootstrapPriority = 1)]

// ✅ Good: Appropriate TTL for temporary data
[DbCollection("sessions", TtlExpirationSeconds = 3600)] // 1 hour

// ✅ Good: Sharding for high-volume collections
[DbCollection("events", EnableSharding = true, PreSplitChunks = 1024)]
```

## Migration from Manual Configuration

### Before (Manual Configuration)

```csharp
var configuration = new DbBootstrapConfiguration
{
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

### After (Attribute-Based)

```csharp
[DbCollection("objects", EnableSharding = true)]
[DbShardKey("Bucket", "KeyHash")]
[DbIndex.Unique("idx_object_lookup", "Bucket", "Key")]
public class EdgeQ1Object : BaseEntity
{
    [DbField("bucket")]
    public string Bucket { get; set; } = string.Empty;

    [DbField("keyHash")]
    public string KeyHash { get; set; } = string.Empty;

    [DbField("key")]
    public string Key { get; set; } = string.Empty;
}

// Usage is much simpler
var result = await dbCentre.BootstrapEntitiesAsync<EdgeQ1Object>();
```

## Troubleshooting

### Validation Errors

```csharp
var validation = EntityAttributeScanner.ValidateEntityConfigurations(entityTypes);
if (!validation.IsValid)
{
    foreach (var error in validation.ValidationErrors)
    {
        Console.WriteLine($"Configuration Error: {error}");
    }
}
```

### Common Issues

1. **Missing DbField Attributes**: Properties without `[DbField]` use snake_case conversion
2. **Duplicate Index Names**: Each index name must be unique within a collection
3. **Invalid Shard Keys**: Must have at least one field with valid property names
4. **TTL Index Fields**: TTL indexes must target a single DateTime field

### Debugging Configuration

```csharp
// Inspect what configuration is generated
var config = EntityAttributeScanner.ScanEntityForCollectionConfiguration(typeof(MyEntity));
Console.WriteLine($"Collection: {config.CollectionName}");
Console.WriteLine($"Shard Key: {string.Join(", ", config.ShardKey?.Fields.Select(f => f.FieldName) ?? [])}");
Console.WriteLine($"Indexes: {config.Indexes.Count}");
```

This attribute-based approach significantly simplifies database configuration while maintaining full control and type safety. The configuration lives alongside your entity code, making it easy to understand and maintain the database schema as your application evolves.