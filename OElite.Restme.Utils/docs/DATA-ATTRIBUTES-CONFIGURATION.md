# Data Attributes & Configuration Guide

This document provides comprehensive guidance on using the enhanced data attributes and configuration system in OElite.Restme.Utils for database schema definition and management.

## Table of Contents

- [Overview](#overview)
- [BaseEntity Enhancements](#baseentity-enhancements)
- [Database Attributes](#database-attributes)
- [Sharding Configuration](#sharding-configuration)
- [Index Configuration](#index-configuration)
- [Field Mapping](#field-mapping)
- [Region-Aware Entities](#region-aware-entities)
- [Best Practices](#best-practices)
- [Examples](#examples)

## Overview

The OElite.Restme.Utils library provides a comprehensive attribute-based system for defining database schema configurations directly on entity classes. This approach eliminates manual configuration while ensuring optimal performance, compliance, and maintainability.

### Key Features

- **Declarative Configuration**: Define database schema using C# attributes
- **Region Awareness**: Built-in support for geographic data placement
- **Type Safety**: Compile-time validation of database configurations
- **Performance Optimization**: Automatic index generation and optimization
- **GDPR Compliance**: Built-in support for regional data sovereignty
- **Zero Configuration**: Automatic discovery and application of settings

## BaseEntity Enhancements

### Enhanced BaseEntity with Region Support

The `BaseEntity` class has been enhanced to include automatic region awareness for GDPR compliance:

```csharp
public abstract class BaseEntity
{
    /// <summary>
    /// Unique identifier for the entity
    /// </summary>
    [DbId]
    public DbObjectId Id { get; set; } = DbObjectId.Empty;

    /// <summary>
    /// When the entity was created (UTC)
    /// </summary>
    [DbField("created_on_utc")]
    public DateTime CreatedOnUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the entity was last updated (UTC)
    /// </summary>
    [DbField("updated_on_utc")]
    public DateTime UpdatedOnUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether the entity is active/enabled
    /// </summary>
    [DbField("is_active")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Which merchant owns this entity (for multi-tenant scenarios)
    /// </summary>
    [DbField("owner_merchant_id")]
    public DbObjectId? OwnerMerchantId { get; set; }

    /// <summary>
    /// Which contact owns this entity (for user-specific data)
    /// </summary>
    [DbField("owner_contact_id")]
    public DbObjectId? OwnerContactId { get; set; }

    /// <summary>
    /// Geographic region for GDPR compliance and data sovereignty
    /// Used for region-aware sharding and data placement
    /// Possible values: "eu", "us", "apac", "ca", "uk", "cn", etc.
    /// </summary>
    [DbField("region")]
    public string? Region { get; set; }
}
```

### Automatic Index Generation

All entities that inherit from `BaseEntity` automatically receive optimized indexes:

```csharp
// Automatically generated indexes for BaseEntity properties:
// - idx_auto_created: CreatedOnUtc (ascending)
// - idx_auto_updated: UpdatedOnUtc (ascending)
// - idx_auto_active: IsActive (ascending)
// - idx_auto_owner_merchant: OwnerMerchantId (sparse, for multi-tenant scenarios)
// - idx_auto_owner_contact: OwnerContactId (sparse, for user-specific data)
// - idx_auto_region: Region (sparse, for GDPR compliance queries)
```

## Database Attributes

### DbCollectionAttribute

Defines collection-level settings including naming, sharding, and validation:

```csharp
[AttributeUsage(AttributeTargets.Class)]
public class DbCollectionAttribute : Attribute
{
    public string? CollectionName { get; set; }
    public DbNamingConvention NamingConvention { get; set; } = DbNamingConvention.SnakeCase;

    // Sharding configuration
    public bool EnableSharding { get; set; } = false;
    public bool EnablePreSplitting { get; set; } = false;
    public int PreSplitChunks { get; set; } = 64;

    // Collection validation
    public bool ValidateSchema { get; set; } = false;

    // TTL configuration for automatic cleanup
    public int TtlExpirationSeconds { get; set; } = 0;

    // Bootstrap ordering
    public int BootstrapPriority { get; set; } = 100;
}
```

#### Usage Examples

```csharp
// Basic collection configuration
[DbCollection("products")]
public class Product : BaseEntity { }

// Advanced configuration with sharding
[DbCollection("high_volume_data",
    EnableSharding = true,
    EnablePreSplitting = true,
    PreSplitChunks = 256,
    ValidateSchema = true)]
public class HighVolumeData : BaseEntity { }

// TTL collection for temporary data
[DbCollection("session_data",
    TtlExpirationSeconds = 3600)] // 1 hour expiration
public class SessionData : BaseEntity { }

// Priority collection (created first during bootstrap)
[DbCollection("configuration",
    BootstrapPriority = 1)]
public class Configuration : BaseEntity { }
```

### DbFieldAttribute

Maps entity properties to database field names:

```csharp
[AttributeUsage(AttributeTargets.Property)]
public class DbFieldAttribute : Attribute
{
    public string FieldName { get; set; }

    public DbFieldAttribute(string fieldName)
    {
        FieldName = fieldName;
    }
}
```

#### Usage Examples

```csharp
public class Product : BaseEntity
{
    // Custom field mapping
    [DbField("product_name")]
    public string Name { get; set; } = string.Empty;

    // Automatic snake_case conversion (no attribute needed)
    public decimal Price { get; set; } // Maps to "price"

    // Complex field mapping
    [DbField("cat_ref_id")]
    public DbObjectId CategoryId { get; set; }
}
```

### DbIdAttribute

Marks the primary key field (automatically maps to MongoDB's `_id`):

```csharp
[AttributeUsage(AttributeTargets.Property)]
public class DbIdAttribute : Attribute { }
```

#### Usage Example

```csharp
public class CustomEntity
{
    [DbId]
    public DbObjectId Id { get; set; } = DbObjectId.Empty;

    public string Name { get; set; } = string.Empty;
}
```

### DbFieldIgnoreAttribute

Excludes properties from database storage:

```csharp
[AttributeUsage(AttributeTargets.Property)]
public class DbFieldIgnoreAttribute : Attribute { }
```

#### Usage Example

```csharp
public class Product : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }

    // Computed property not stored in database
    [DbFieldIgnore]
    public decimal TaxAmount => Price * 0.15m;

    // Runtime-only property
    [DbFieldIgnore]
    public bool IsExpensive => Price > 100;
}
```

## Sharding Configuration

### DbShardKeyAttribute

Defines shard key configuration with advanced region-awareness:

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class DbShardKeyAttribute : Attribute
{
    public string[] Fields { get; set; }
    public bool[] IsHashed { get; set; }
    public int[] Directions { get; set; }
    public bool IsUnique { get; set; } = false;

    // Region-aware sharding
    public bool IncludeRegion { get; set; } = false;
    public RegionShardingStrategy RegionStrategy { get; set; } = RegionShardingStrategy.RegionFirst;
}
```

### Region Sharding Strategies

```csharp
public enum RegionShardingStrategy
{
    RegionFirst,  // { region: 1, ...fields }
    RegionLast,   // { ...fields, region: 1 }
    RegionMiddle  // { field1: 1, region: 1, field2: 1 }
}
```

### Sharding Examples

#### Simple Shard Key

```csharp
[DbCollection("users", EnableSharding = true)]
[DbShardKey("UserId")]
public class User : BaseEntity
{
    public DbObjectId UserId { get; set; }
    public string Username { get; set; } = string.Empty;
}
```

#### Compound Shard Key

```csharp
[DbCollection("orders", EnableSharding = true)]
[DbShardKey("CustomerId", "OrderDate")]
public class Order : BaseEntity
{
    public DbObjectId CustomerId { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal Total { get; set; }
}
```

#### Hashed Shard Key for Even Distribution

```csharp
[DbCollection("analytics", EnableSharding = true)]
[DbShardKey("EventId", IsHashed = new[] { true })]
public class AnalyticsEvent : BaseEntity
{
    public DbObjectId EventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime EventTime { get; set; }
}
```

#### Region-Aware Shard Key (GDPR Compliant)

```csharp
[DbCollection("customer_data", EnableSharding = true)]
[DbShardKey("CustomerId", IncludeRegion = true, RegionStrategy = RegionShardingStrategy.RegionFirst)]
public class CustomerData : BaseEntity
{
    public DbObjectId CustomerId { get; set; }
    public string PersonalData { get; set; } = string.Empty;

    // Effective shard key: { region: 1, customer_id: 1 }
    // Ensures all EU data stays on EU shards
}
```

#### Complex Region-Aware Configuration

```csharp
[DbCollection("transactions", EnableSharding = true, EnablePreSplitting = true)]
[DbShardKey("AccountId", "TransactionDate", IncludeRegion = true, RegionStrategy = RegionShardingStrategy.RegionMiddle)]
public class Transaction : BaseEntity
{
    public DbObjectId AccountId { get; set; }
    public DateTime TransactionDate { get; set; }
    public decimal Amount { get; set; }

    // Effective shard key: { account_id: 1, region: 1, transaction_date: 1 }
}
```

### Factory Methods for Common Patterns

```csharp
// GDPR-compliant shard key
var gdprShardKey = DbShardKeyAttribute.ForGdprCompliance("CustomerId");

// Multi-tenant shard key
var tenantShardKey = DbShardKeyAttribute.ForTenant("TenantId", "EntityId");

// Analytics shard key
var analyticsShardKey = DbShardKeyAttribute.ForAnalytics("EntityId", "EventDate");
```

## Index Configuration

### DbIndexAttribute

Defines comprehensive index configurations:

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class DbIndexAttribute : Attribute
{
    public string Name { get; set; }
    public string[] Fields { get; set; }
    public int[] Directions { get; set; }

    // Index properties
    public bool IsUnique { get; set; } = false;
    public bool IsSparse { get; set; } = false;
    public bool CreateInBackground { get; set; } = false;
    public int Priority { get; set; } = 100;

    // Special index types
    public bool IsTextIndex { get; set; } = false;
    public bool IsHashedIndex { get; set; } = false;

    // TTL configuration
    public int TtlExpirationSeconds { get; set; } = 0;
}
```

### Index Examples

#### Basic Indexes

```csharp
[DbCollection("products")]
[DbIndex("idx_name", "Name")]
[DbIndex("idx_price", "Price")]
public class Product : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
```

#### Compound Indexes

```csharp
[DbCollection("orders")]
[DbIndex("idx_customer_date", "CustomerId", "OrderDate", Directions = new[] { 1, -1 })]
[DbIndex("idx_status_total", "Status", "Total")]
public class Order : BaseEntity
{
    public DbObjectId CustomerId { get; set; }
    public DateTime OrderDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal Total { get; set; }
}
```

#### Unique Constraint Indexes

```csharp
[DbCollection("users")]
[DbIndex("idx_unique_email", "Email", IsUnique = true)]
[DbIndex("idx_unique_username", "Username", IsUnique = true)]
public class User : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
}
```

#### Sparse Indexes for Optional Fields

```csharp
[DbCollection("profiles")]
[DbIndex("idx_social_security", "SocialSecurityNumber", IsSparse = true)]
[DbIndex("idx_phone_number", "PhoneNumber", IsSparse = true)]
public class UserProfile : BaseEntity
{
    public string? SocialSecurityNumber { get; set; }
    public string? PhoneNumber { get; set; }
}
```

#### Text Search Indexes

```csharp
[DbCollection("documents")]
[DbIndex("idx_fulltext_search", "Title", "Content", "Tags", IsTextIndex = true)]
public class Document : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
}
```

#### TTL Indexes for Automatic Cleanup

```csharp
[DbCollection("sessions")]
[DbIndex("idx_ttl_expires", "ExpiresAt", TtlExpirationSeconds = 0)] // Expires when ExpiresAt is reached
public class UserSession : BaseEntity
{
    public string SessionToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

[DbCollection("logs")]
[DbIndex("idx_ttl_auto_cleanup", "CreatedOnUtc", TtlExpirationSeconds = 2592000)] // 30 days retention
public class LogEntry : BaseEntity
{
    public string Message { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
}
```

#### Background Index Creation

```csharp
[DbCollection("large_collection")]
[DbIndex("idx_background_heavy", "LargeTextField", CreateInBackground = true, Priority = 200)]
public class LargeDocument : BaseEntity
{
    public string LargeTextField { get; set; } = string.Empty;
}
```

### Factory Methods for Common Index Patterns

```csharp
// Unique index
var uniqueIndex = DbIndexAttribute.Unique("idx_unique_field", "FieldName");

// Sparse index
var sparseIndex = DbIndexAttribute.Sparse("idx_sparse_field", "OptionalField");

// TTL index
var ttlIndex = DbIndexAttribute.Ttl("idx_ttl_cleanup", "ExpiresAt", 3600); // 1 hour
```

## Field Mapping

### Naming Conventions

The library supports multiple naming conventions for automatic field mapping:

```csharp
public enum DbNamingConvention
{
    SnakeCase,    // ProductName -> product_name
    CamelCase,    // ProductName -> productName
    PascalCase,   // ProductName -> ProductName
    Unchanged     // ProductName -> ProductName
}
```

### Field Mapping Priority

Field names are resolved in the following priority order:

1. **`[DbId]` attribute** → Maps to `_id`
2. **`[DbField]` attribute** → Uses specified `FieldName`
3. **Collection naming convention** → Applies convention from `[DbCollection]`
4. **Default snake_case** → Converts PascalCase to snake_case

### Example Field Mapping

```csharp
[DbCollection("products", NamingConvention = DbNamingConvention.SnakeCase)]
public class Product : BaseEntity
{
    [DbId]
    public DbObjectId Id { get; set; } = DbObjectId.Empty; // Maps to "_id"

    [DbField("product_name")]
    public string Name { get; set; } = string.Empty; // Maps to "product_name"

    public decimal Price { get; set; } // Maps to "price" (snake_case)

    [DbField("category_ref")]
    public DbObjectId CategoryId { get; set; } = DbObjectId.Empty; // Maps to "category_ref"

    [DbFieldIgnore]
    public string ComputedProperty { get; set; } = string.Empty; // Not stored
}
```

## Region-Aware Entities

### GDPR-Compliant Entity Design

```csharp
[DbCollection("gdpr_customers", EnableSharding = true, ValidateSchema = true)]
[DbShardKey("Email", IncludeRegion = true, RegionStrategy = RegionShardingStrategy.RegionFirst)]
[DbIndex("idx_customer_lookup", "Email", "Region", IsUnique = true)]
[DbIndex("idx_consent_tracking", "Region", "ConsentDate", "ConsentStatus")]
[DbIndex("idx_data_retention", "Region", "CreatedOnUtc", TtlExpirationSeconds = 94608000)] // 3 years
public class GdprCustomer : BaseEntity
{
    [DbField("email")]
    public string Email { get; set; } = string.Empty;

    [DbField("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [DbField("last_name")]
    public string LastName { get; set; } = string.Empty;

    [DbField("consent_date")]
    public DateTime ConsentDate { get; set; }

    [DbField("consent_status")]
    public string ConsentStatus { get; set; } = string.Empty; // "granted", "withdrawn", "expired"

    [DbField("data_processing_purposes")]
    public List<string> DataProcessingPurposes { get; set; } = new();

    // Region inherited from BaseEntity ensures geographic compliance
}
```

### EdgeQ1 S3 Storage Entity

```csharp
[DbCollection("edge_objects", EnableSharding = true, EnablePreSplitting = true, PreSplitChunks = 1024)]
[DbShardKey("Bucket", "KeyHash", IncludeRegion = true, RegionStrategy = RegionShardingStrategy.RegionFirst)]
[DbIndex("idx_object_lookup", "Bucket", "Key", "Region", IsUnique = true)]
[DbIndex("idx_region_listing", "Region", "Bucket", "LastModified")]
[DbIndex("idx_tenant_objects", "OwnerId", "Region", "Bucket", IsSparse = true)]
public class EdgeQ1Object : BaseEntity
{
    [DbField("bucket")]
    public string Bucket { get; set; } = string.Empty;

    [DbField("key")]
    public string Key { get; set; } = string.Empty;

    [DbField("key_hash")]
    public string KeyHash { get; set; } = string.Empty;

    [DbField("size")]
    public long Size { get; set; }

    [DbField("content_type")]
    public string ContentType { get; set; } = string.Empty;

    [DbField("owner_id")]
    public DbObjectId? OwnerId { get; set; }

    // Region field ensures objects are stored in correct geographic zone
}
```

## Best Practices

### 1. Entity Design Patterns

```csharp
// GOOD: Clear attribute usage with proper configuration
[DbCollection("products", EnableSharding = true, ValidateSchema = true)]
[DbShardKey("CategoryId", "ProductId", IncludeRegion = true)]
[DbIndex("idx_category_price", "CategoryId", "Price", Directions = new[] { 1, -1 })]
[DbIndex("idx_name_search", "Name", IsTextIndex = true)]
[DbIndex("idx_owner_products", "OwnerMerchantId", IsSparse = true)]
public class Product : BaseEntity
{
    [DbField("category_id")]
    public DbObjectId CategoryId { get; set; }

    [DbField("product_name")]
    public string Name { get; set; } = string.Empty;

    [DbField("price")]
    public decimal Price { get; set; }

    [DbField("description")]
    public string Description { get; set; } = string.Empty;

    [DbFieldIgnore]
    public decimal TaxAmount => Price * 0.15m;
}
```

### 2. Index Strategy

```csharp
// GOOD: Strategic index placement
[DbCollection("orders")]
// Core business indexes
[DbIndex("idx_customer_orders", "CustomerId", "OrderDate", Directions = new[] { 1, -1 }, Priority = 1)]
[DbIndex("idx_status_processing", "Status", "CreatedOnUtc", Priority = 2)]
// Search and reporting indexes
[DbIndex("idx_date_range", "OrderDate", "Status", Priority = 3)]
// Cleanup indexes
[DbIndex("idx_ttl_temp_orders", "ExpiresAt", TtlExpirationSeconds = 0, IsSparse = true)]
public class Order : BaseEntity
{
    public DbObjectId CustomerId { get; set; }
    public DateTime OrderDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; } // Only for draft orders
}
```

### 3. Region Assignment

```csharp
// GOOD: Explicit region handling
public class CustomerService
{
    public async Task<Customer> CreateCustomerAsync(string email, string location)
    {
        var customer = new Customer
        {
            Email = email,
            Region = DetermineRegionFromLocation(location), // Explicit region assignment
            ConsentDate = DateTime.UtcNow,
            ConsentStatus = "granted"
        };

        return await _repository.CreateAsync(customer);
    }

    private string DetermineRegionFromLocation(string location)
    {
        return location switch
        {
            var loc when IsEuCountry(loc) => "EU",
            var loc when IsUkTerritory(loc) => "UK",
            var loc when IsUsTerritory(loc) => "US",
            _ => "US" // Default fallback
        };
    }
}
```

### 4. Configuration Validation

```csharp
// GOOD: Validate configurations at startup
public static void ValidateEntityConfigurations()
{
    var entityTypes = new[]
    {
        typeof(Product),
        typeof(Customer),
        typeof(Order)
    };

    var validationResult = EntityAttributeScanner.ValidateEntityConfigurations(entityTypes);

    if (!validationResult.IsValid)
    {
        throw new InvalidOperationException(
            $"Entity configuration validation failed: {string.Join(", ", validationResult.ValidationErrors)}");
    }
}
```

## Examples

### Complete E-commerce Product Entity

```csharp
[DbCollection("products", EnableSharding = true, EnablePreSplitting = true, ValidateSchema = true)]
[DbShardKey("CategoryId", "ProductId", IncludeRegion = true, RegionStrategy = RegionShardingStrategy.RegionMiddle)]
[DbIndex("idx_category_price", "CategoryId", "Price", Directions = new[] { 1, -1 }, Priority = 1)]
[DbIndex("idx_brand_category", "BrandId", "CategoryId", Priority = 2)]
[DbIndex("idx_search_products", "Name", "Description", "Tags", IsTextIndex = true, Priority = 3)]
[DbIndex("idx_featured_products", "IsFeatured", "DisplayOrder", Priority = 4)]
[DbIndex("idx_inventory_tracking", "Sku", IsUnique = true, Priority = 5)]
[DbIndex("idx_owner_products", "OwnerMerchantId", "IsActive", IsSparse = true, Priority = 6)]
[DbIndex("idx_region_compliance", "Region", "DataClassification", Priority = 7)]
public class Product : BaseEntity
{
    [DbField("product_id")]
    public DbObjectId ProductId { get; set; } = DbObjectId.NewId();

    [DbField("sku")]
    public string Sku { get; set; } = string.Empty;

    [DbField("name")]
    public string Name { get; set; } = string.Empty;

    [DbField("description")]
    public string Description { get; set; } = string.Empty;

    [DbField("price")]
    public decimal Price { get; set; }

    [DbField("category_id")]
    public DbObjectId CategoryId { get; set; }

    [DbField("brand_id")]
    public DbObjectId? BrandId { get; set; }

    [DbField("tags")]
    public List<string> Tags { get; set; } = new();

    [DbField("is_featured")]
    public bool IsFeatured { get; set; } = false;

    [DbField("display_order")]
    public int DisplayOrder { get; set; } = 0;

    [DbField("data_classification")]
    public string DataClassification { get; set; } = "public";

    // Computed properties (not stored)
    [DbFieldIgnore]
    public decimal TaxAmount => Price * 0.15m;

    [DbFieldIgnore]
    public bool IsExpensive => Price > 100;

    [DbFieldIgnore]
    public string FormattedPrice => $"${Price:F2}";
}
```

### Multi-Tenant SaaS Entity

```csharp
[DbCollection("tenant_data", EnableSharding = true)]
[DbShardKey("TenantId", IncludeRegion = true, RegionStrategy = RegionShardingStrategy.RegionFirst)]
[DbIndex("idx_tenant_lookup", "TenantId", "EntityType", "EntityId", IsUnique = true)]
[DbIndex("idx_tenant_search", "TenantId", "SearchableText", IsTextIndex = true)]
[DbIndex("idx_region_compliance", "Region", "TenantId", "DataSensitivity")]
[DbIndex("idx_data_retention", "Region", "CreatedOnUtc", TtlExpirationSeconds = 2592000)] // 30 days default
public class TenantData : BaseEntity
{
    [DbField("tenant_id")]
    public DbObjectId TenantId { get; set; }

    [DbField("entity_type")]
    public string EntityType { get; set; } = string.Empty;

    [DbField("entity_id")]
    public DbObjectId EntityId { get; set; }

    [DbField("data_payload")]
    public Dictionary<string, object> DataPayload { get; set; } = new();

    [DbField("searchable_text")]
    public string SearchableText { get; set; } = string.Empty;

    [DbField("data_sensitivity")]
    public string DataSensitivity { get; set; } = "normal"; // "low", "normal", "high", "restricted"

    [DbField("encryption_status")]
    public string EncryptionStatus { get; set; } = "encrypted_at_rest";

    [DbField("access_log")]
    public List<DataAccessLog> AccessLog { get; set; } = new();
}

public class DataAccessLog
{
    public DateTime AccessTime { get; set; }
    public string AccessedBy { get; set; } = string.Empty;
    public string AccessType { get; set; } = string.Empty; // "read", "write", "delete"
    public string IpAddress { get; set; } = string.Empty;
}
```

This comprehensive documentation covers all aspects of using the enhanced data attributes and configuration system in OElite.Restme.Utils. The examples demonstrate real-world usage patterns and best practices for building scalable, compliant, and performant database schemas.