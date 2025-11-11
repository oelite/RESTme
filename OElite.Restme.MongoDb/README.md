# OElite.Restme.MongoDb

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen.svg)](https://github.com/oelite)
[![NuGet](https://img.shields.io/badge/nuget-v2.1.0-blue.svg)](https://www.nuget.org/packages/OElite.Restme.MongoDb/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

MongoDB integration package for OElite platform, providing enterprise-grade database management with GDPR compliance, region-aware sharding, and attribute-based configuration.

## 🚀 Key Features

### **Region-Aware Sharding & GDPR Compliance**
- **Geographic Data Isolation**: Automatic region-based data placement for GDPR/CCPA compliance
- **Cross-Region Migration**: Built-in data migration tools for compliance requirements
- **Zone Sharding**: MongoDB zone sharding for geographic data sovereignty
- **Retention Policies**: Configurable data retention per region with auto-cleanup
- **Data Sovereignty**: Ensures personal data stays within required legal jurisdictions
- **Compliance Validation**: Built-in validation for cross-region transfer restrictions

### **Attribute-Based Configuration**
- **Declarative Sharding**: Define shard keys directly on entity classes with region awareness
- **Performance Indexing**: Automatic index generation for optimal query performance
- **Type-Safe Configuration**: Compile-time validation of database schema
- **Zero-Configuration Bootstrap**: Automatic database setup from entity attributes
- **Region-First Sharding**: Smart region placement strategies for optimal performance
- **Auto-Index Generation**: Intelligent indexing for region-aware queries

### **Enterprise-Scale Performance**
- **Trillion-Record Support**: Optimized for massive datasets (Q1 S3 storage patterns)
- **Smart Pre-Splitting**: Intelligent chunk distribution for optimal performance
- **Background Operations**: Non-blocking index creation and maintenance
- **Health Monitoring**: Comprehensive database health checks and metrics
- **Zone-Based Scaling**: Geographic distribution for global performance
- **Migration Optimization**: Efficient cross-region data movement strategies

## Table of Contents

- [Overview](#overview)
- [Installation](#installation)
- [Core Features](#core-features)
- [Region-Aware Sharding & GDPR Compliance](#region-aware-sharding--gdpr-compliance)
- [Attribute-Based Database Configuration](#attribute-based-database-configuration)
- [Entity Configuration](#entity-configuration)
- [Basic Operations](#basic-operations)
- [Advanced Querying](#advanced-querying)
- [Aggregation Pipelines](#aggregation-pipelines)
- [Denormalization System](#denormalization-system)
- [Performance Optimization](#performance-optimization)
- [Geographic Data Management](#geographic-data-management)
- [Migration & Compliance](#migration--compliance)
- [Best Practices](#best-practices)
- [Examples](#examples)
- [API Reference](#api-reference)

## Overview

`OElite.Restme.MongoDb` is a high-performance MongoDB library designed specifically for the OElite platform. It provides:

- **Type-safe MongoDB operations** with full IntelliSense support
- **Advanced aggregation pipelines** for complex queries
- **Automatic denormalization** for efficient data relationships
- **Flexible query builders** for various scenarios
- **Seamless integration** with OElite's data layer architecture

## Installation

Add the package reference to your project:

```xml
<PackageReference Include="OElite.Restme.MongoDb" Version="2.0.10" />
```

## Region-Aware Sharding & GDPR Compliance

The library provides comprehensive support for geographic data management, ensuring compliance with GDPR, CCPA, and other regional data protection regulations through intelligent sharding and automatic data placement.

### 🌍 Geographic Data Sovereignty

#### BaseEntity Region Support
All entities automatically inherit region awareness through the enhanced `BaseEntity` class:

```csharp
public class Customer : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;

    // Region field automatically inherited from BaseEntity
    // Controls geographic data placement for GDPR compliance
    // public string? Region { get; set; } // from BaseEntity
}

// Entity automatically placed in correct geographic zone
var customer = new Customer
{
    Email = "user@example.com",
    FirstName = "John",
    Region = "EU" // Ensures data stays in European jurisdiction
};
```

#### Supported Geographic Regions
- **EU**: European Union (GDPR compliance)
- **US**: United States (CCPA compliance)
- **UK**: United Kingdom (UK GDPR)
- **CA**: Canada (PIPEDA compliance)
- **APAC**: Asia-Pacific (various local regulations)
- **CN**: China (Cybersecurity Law compliance)

### 🗂️ Region-Aware Sharding Strategies

#### 1. Region-First Sharding (Recommended for GDPR)
Places region as the first field in the shard key for optimal data isolation:

```csharp
[DbCollection("customer_data", EnableSharding = true)]
[DbShardKey("UserId", IncludeRegion = true, RegionStrategy = RegionShardingStrategy.RegionFirst)]
public class CustomerData : BaseEntity
{
    public DbObjectId UserId { get; set; }
    public string PersonalData { get; set; } = string.Empty;

    // Effective shard key: { region: 1, user_id: 1 }
    // Ensures all EU data is in EU shards, US data in US shards, etc.
}
```

#### 2. Region-Last Sharding (Performance Optimized)
Places region at the end for better distribution while maintaining compliance:

```csharp
[DbCollection("product_analytics", EnableSharding = true)]
[DbShardKey("ProductId", "EventDate", IncludeRegion = true, RegionStrategy = RegionShardingStrategy.RegionLast)]
public class ProductAnalytics : BaseEntity
{
    public DbObjectId ProductId { get; set; }
    public DateTime EventDate { get; set; }

    // Effective shard key: { product_id: 1, event_date: 1, region: 1 }
    // Better distribution for global products with regional compliance
}
```

#### 3. Region-Middle Sharding (Balanced Approach)
Places region in the middle for specific use cases:

```csharp
[DbCollection("order_tracking", EnableSharding = true)]
[DbShardKey("CustomerId", IncludeRegion = true, RegionStrategy = RegionShardingStrategy.RegionMiddle, "OrderDate")]
public class OrderTracking : BaseEntity
{
    public DbObjectId CustomerId { get; set; }
    public DateTime OrderDate { get; set; }

    // Effective shard key: { customer_id: 1, region: 1, order_date: 1 }
    // Optimal for customer-centric data with regional compliance
}
```

### 🔐 Automatic Zone Configuration

The library automatically configures MongoDB zones based on region data:

```csharp
// Geographic configuration with zone mapping
var geographicConfig = GeographicConfiguration.CreateGdprCompliantConfiguration();

// Automatic zone creation during bootstrap
await dbCentre.BootstrapEntitiesAsync<CustomerData>(new DbBootstrapOptions
{
    EnableSharding = true,
    EnablePreSplitting = true,
    GeographicConfiguration = geographicConfig
});

// Results in MongoDB zones:
// Zone "EU" -> Region: { "region": "EU" }
// Zone "US" -> Region: { "region": "US" }
// Zone "UK" -> Region: { "region": "UK" }
```

### 📋 Attribute-Based Configuration Examples

#### GDPR-Compliant Customer Entity
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

#### Q1 S3 Storage with Region Awareness
```csharp
[DbCollection("edge_objects", EnableSharding = true, EnablePreSplitting = true, PreSplitChunks = 1024)]
[DbShardKey("Bucket", "KeyHash", IncludeRegion = true, RegionStrategy = RegionShardingStrategy.RegionFirst)]
[DbIndex("idx_object_lookup", "Bucket", "Key", "Region", IsUnique = true)]
[DbIndex("idx_region_listing", "Region", "Bucket", "LastModified")]
[DbIndex("idx_tenant_objects", "OwnerId", "Region", "Bucket", IsSparse = true)]
public class Q1Object : BaseEntity
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
    // for compliance with local data residency requirements
}
```

### 🚀 Zero-Configuration Bootstrap

#### Automatic Entity Discovery and Configuration
```csharp
public static async Task<DbBootstrapResult> AutoBootstrapWithRegionAwarenessAsync(string connectionString)
{
    using var dbCentre = new MongoDbCentre(connectionString);

    // Automatic bootstrap discovers all entity attributes
    var result = await dbCentre.BootstrapEntitiesAsync<GdprCustomer, Q1Object, OrderTracking>();

    if (result.Success)
    {
        Console.WriteLine("✅ Region-aware database bootstrap completed!");
        Console.WriteLine($"Configured collections: {result.ConfiguredCollections.Count}");
        Console.WriteLine($"Created zones: {string.Join(", ", result.CreatedZones)}");
        Console.WriteLine($"Applied retention policies: {result.RetentionPoliciesApplied}");
    }

    return result;
}
```

#### Entity Validation and Configuration Scanning
```csharp
// Validate all entity configurations before deployment
public static void ValidateEntityConfigurations()
{
    var entityTypes = new[]
    {
        typeof(GdprCustomer),
        typeof(Q1Object),
        typeof(OrderTracking)
    };

    var validationResult = EntityAttributeScanner.ValidateEntityConfigurations(entityTypes);

    if (validationResult.IsValid)
    {
        Console.WriteLine("✅ All entity configurations are valid");
        Console.WriteLine($"Validated entities: {string.Join(", ", validationResult.ValidatedEntities)}");
    }
    else
    {
        Console.WriteLine("❌ Entity configuration validation failed:");
        foreach (var error in validationResult.ValidationErrors)
        {
            Console.WriteLine($"  - {error}");
        }
    }
}
```

## Attribute-Based Database Configuration

The library provides a comprehensive attribute-based approach to database configuration, eliminating the need for manual setup while ensuring optimal performance and compliance.

### 🏷️ Core Attributes

#### DbCollectionAttribute
Controls collection-level settings including sharding and validation:

```csharp
[DbCollection("products",
    EnableSharding = true,
    EnablePreSplitting = true,
    PreSplitChunks = 512,
    ValidateSchema = true,
    TtlExpirationSeconds = 7776000, // 90 days
    BootstrapPriority = 1)]
public class Product : BaseEntity
{
    // Entity properties...
}
```

#### DbShardKeyAttribute
Defines shard key configuration with region awareness:

```csharp
// Simple shard key
[DbShardKey("ProductId")]

// Compound shard key with region awareness
[DbShardKey("TenantId", "ProductId", IncludeRegion = true, RegionStrategy = RegionShardingStrategy.RegionFirst)]

// Hashed shard key for even distribution
[DbShardKey("UserId", IsHashed = new[] { true })]

// Complex configuration with unique constraint
[DbShardKey("Email", "Region", IsUnique = true, IncludeRegion = false)] // Region explicitly in key
```

#### DbIndexAttribute
Configures performance and compliance indexes:

```csharp
// Simple index
[DbIndex("idx_name", "Name")]

// Compound index with sorting
[DbIndex("idx_price_category", "CategoryId", "Price", Directions = new[] { 1, -1 })]

// Unique constraint index
[DbIndex("idx_unique_email", "Email", "Region", IsUnique = true)]

// Sparse index for optional fields
[DbIndex("idx_owner", "OwnerId", IsSparse = true)]

// TTL index for automatic cleanup
[DbIndex("idx_expiry", "ExpiresAt", TtlExpirationSeconds = 0)]

// Text search index
[DbIndex("idx_search", "Name", "Description", IsTextIndex = true)]

// Background creation for large collections
[DbIndex("idx_background", "CreatedAt", CreateInBackground = true, Priority = 200)]
```

### 🔍 Automatic Index Generation

The library automatically generates standard indexes for common patterns:

```csharp
public class Product : BaseEntity
{
    // Automatic indexes are created for BaseEntity properties:
    // - idx_auto_created: CreatedOnUtc
    // - idx_auto_updated: UpdatedOnUtc
    // - idx_auto_active: IsActive
    // - idx_auto_owner_merchant: OwnerMerchantId (sparse)
    // - idx_auto_owner_contact: OwnerContactId (sparse)
    // - idx_auto_region: Region (sparse) - for GDPR compliance

    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
```

### 📊 Configuration Scanning and Bootstrap

#### Entity Attribute Scanner
Automatically discovers and validates entity configurations:

```csharp
// Scan entities and build configuration
var entityTypes = new[] { typeof(Product), typeof(Customer), typeof(Order) };
var configuration = EntityAttributeScanner.ScanEntitiesForBootstrapConfiguration(
    entityTypes,
    new DbBootstrapOptions
    {
        EnableSharding = true,
        EnablePreSplitting = true,
        CreateIndexesInBackground = true
    });

// Configuration includes:
// - Collection settings from [DbCollection]
// - Shard key definitions from [DbShardKey]
// - Index definitions from [DbIndex]
// - Automatic indexes for BaseEntity properties
// - Region-aware configurations
// - Priority-ordered bootstrap sequence
```

#### Bootstrap Service Integration
```csharp
public class ApplicationDbCentre : MongoDbCentre
{
    public ApplicationDbCentre(string connectionString) : base(connectionString) { }

    // Strongly-typed collections
    public IMongoDbCollection<Product> Products => GetMongoDbCollection<Product>();
    public IMongoDbCollection<Customer> Customers => GetMongoDbCollection<Customer>();

    // Automatic bootstrap
    public async Task<DbBootstrapResult> InitializeAsync()
    {
        return await this.BootstrapEntitiesAsync<Product, Customer, Order>(
            new DbBootstrapOptions
            {
                EnableSharding = true,
                EnablePreSplitting = true,
                CreateIndexesInBackground = true,
                MaxRetryAttempts = 3,
                TimeoutSeconds = 300
            });
    }
}
```

## Core Features

### 🆕 MongoDB-Free Application Layer

⭐ **Zero MongoDB Dependencies**: Complete abstraction eliminates MongoDB.Driver dependencies from application code
- **MongoDbDocument API**: Replace BsonDocument with pure .NET Dictionary-based operations
- **IMongoDbCollection Interface**: MongoDB-free collection operations with Dictionary and lambda support
- **Seamless Type Conversion**: Internal MongoDB type conversion while exposing clean .NET APIs
- **Zero Vendor Lock-in**: Application developers work with pure .NET types and collections
- **Clean Architecture**: Perfect separation between business logic and database implementation

```csharp
// MongoDB-Free collection operations - no MongoDB.Driver dependencies
var collection = DbCentre.GetMongoDbCollection("products");
var typedCollection = DbCentre.GetMongoDbCollection<Product>();

// Use MongoDbDocument instead of BsonDocument
var filter = new MongoDbDocument
{
    ["status"] = "active",
    ["price"] = new MongoDbDocument { ["$gte"] = 100 }
};

// Returns pure .NET types - List<MongoDbDocument>
var results = await collection.FindAsync(filter);

// Strongly-typed operations with lambda expressions
var products = await typedCollection.FindAsync(p => p.Price > 100);
```

### 1. Entity Configuration
- Automatic collection mapping with naming conventions
- Custom field mapping and serialization
- Support for embedded documents and arrays
- Flexible attribute-based configuration

### 2. Query Operations
- Simple CRUD operations
- Complex filtering and sorting
- Pagination and limiting
- Bulk operations

### 3. Aggregation Pipelines
- Advanced aggregation support
- Join operations with `$lookup`
- Complex data transformations
- Performance-optimized queries

### 4. Denormalization System
- Automatic data population from related collections
- Flexible reference key syntax with `@` and `#` prefixes
- Support for complex queries and field mapping
- Cascade update capabilities

### 5. Advanced LINQ Expression Support
- **Nested Document Queries**: Full support for querying nested properties like `p.MeasureUnit?.IsDefaultStockMeasure`
- **Extension Method Support**: Use OElite.Restme.Utils extension methods like `IsNotNullOrEmpty()` in LINQ expressions
- **Type Safety**: Compile-time checking of property names and types
- **MongoDB Translation**: Automatic translation to efficient MongoDB queries

## Entity Configuration

### Basic Entity Setup

```csharp
using OElite.Restme.MongoDb;
using OElite.Common;

[DbCollection("products", DbNamingConvention.SnakeCase)]
public class Product : BaseEntity
{
    [DbId]
    public DbObjectId Id { get; set; }
    
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DbObjectId CategoryId { get; set; }
    
    [DbFieldIgnore]
    public string ComputedField { get; set; } = string.Empty;
}
```

### Automatic Field Name Mapping

The library automatically maps your C# property names to MongoDB field names based on your attributes:

- **`[DbId]`** → Maps to `_id` field
- **`[DbField("custom_name")]`** → Maps to `custom_name` field
- **`[DbCollection(namingConvention)]`** → Applies naming convention (snake_case, camelCase, etc.)
- **`[DbFieldIgnore]`** → Excludes from database storage

**Example**: With the above `Product` class and `DbNamingConvention.SnakeCase`:
- `Name` property → `name` field in MongoDB
- `Price` property → `price` field in MongoDB  
- `CategoryId` property → `category_id` field in MongoDB
- `Id` property → `_id` field in MongoDB
- `ComputedField` property → Not stored in MongoDB

### Collection Attributes

#### DbCollectionAttribute
Specifies the MongoDB collection name and naming convention:

```csharp
[DbCollection("product_catalog", DbNamingConvention.SnakeCase)]
public class Product { }

[DbCollection("userProfiles", DbNamingConvention.CamelCase)]
public class UserProfile { }

[DbCollection("Orders", DbNamingConvention.PascalCase)]
public class Order { }
```

#### DbFieldAttribute
Customizes field mapping:

```csharp
public class Product
{
    [DbField("product_name")]
    public string Name { get; set; }
    
    [DbField("unit_price")]
    public decimal Price { get; set; }
}
```

#### DbIdAttribute
Marks the primary key field:

```csharp
public class Product
{
    [DbId]
    public DbObjectId Id { get; set; }
}
```

#### DbFieldIgnore
Excludes fields from database storage:

```csharp
public class Product
{
    public string Name { get; set; }
    
    [DbFieldIgnore]
    public string ComputedValue { get; set; }
}
```

#### DbDateTimeOptionsAttribute
Configures DateTime serialization:

```csharp
public class Product
{
    [DbDateTimeOptions(DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; }
}
```

## Basic Operations

### Simple Queries

```csharp
using OElite.Restme.MongoDb;

// Get collection
var collection = dbCentre.GetCollection<Product>();

// Simple find - automatically uses field names from [DbField] attributes
var products = await collection.Find(p => p.Price > 100).ToListAsync();

// Find with filter - respects attribute mappings
var expensiveProducts = await collection
    .Find(p => p.Price > 100 && p.CategoryId == categoryId)
    .Sort(p => p.Price, false) // descending
    .Limit(10)
    .ToListAsync();
```

**Note**: All LINQ expressions automatically respect your `[DbField]`, `[DbId]`, and `[DbCollection]` attribute mappings. The MongoDB driver uses the configured class mappings to translate property names to the correct field names in the database.

### Data Repository Pattern with LINQ Extensions

For Data Repositories that use the `MongoQuery<T>` pattern, you can now use enhanced LINQ expressions:

```csharp
// In your Data Repository
public class ProductRepository : DataRepository
{
    public MongoQuery<Product> ProductStock => new(_adapter.GetCollection<Product>());
    
    // Now you can use LINQ expressions directly on MongoQuery
    public async Task<List<Product>> GetExpensiveProductsAsync(decimal minPrice)
    {
        return await ProductStock
            .Where(p => p.Price > minPrice)
            .OrderByDescending(p => p.Price)
            .Take(10)
            .ToListAsync();
    }
    
    public async Task<Product?> GetProductByIdAsync(DbObjectId productId)
    {
        return await ProductStock
            .Where(p => p.Id == productId)
            .FirstOrDefaultAsync();
    }
    
    public async Task<bool> ProductExistsAsync(string productName)
    {
        return await ProductStock
            .Where(p => p.Name == productName)
            .AnyAsync();
    }
    
    // Nested document queries - fully supported!
    public async Task<List<Product>> GetProductsWithNonDefaultMeasureUnitsAsync()
    {
        return await ProductStock
            .Where(p => p.MeasureUnit != null && p.MeasureUnit.IsDefaultStockMeasure == false)
            .Where(p => p.MeasureUnit.Name.Contains("kg"))
            .ToListAsync();
    }
    
    // Extension method queries - fully supported!
    public async Task<List<Product>> GetProductsWithValidOwnerAsync()
    {
        return await ProductStock
            .Where(p => p.OwnerMerchantId.IsNotNullOrEmpty())
            .Where(p => p.Name.IsNotNullOrEmpty())
            .ToListAsync();
    }
    
    public async Task<(List<Product> Items, long TotalCount)> GetPagedProductsAsync(int pageIndex, int pageSize)
    {
        return await ProductStock
            .OrderBy(p => p.Name)
            .ToPagedListAsync(pageIndex, pageSize);
    }
    
    // Using the new FetchAsync extensions
    public async Task<Product?> GetProductByIdAsync(DbObjectId productId)
    {
        return await ProductStock
            .Where(p => p.Id == productId)
            .FetchAsync(); // Equivalent to FirstOrDefaultAsync()
    }
    
    public async Task<ProductCollection> GetProductsByCategoryAsync(DbObjectId categoryId)
    {
        return await ProductStock
            .Where(p => p.CategoryId == categoryId)
            .Where(p => p.Status == EntityStatus.Active)
            .FetchAsync<Product, ProductCollection>(); // Returns BaseEntityCollection<T>
    }
    
    // Aggregation-based methods for maximum performance
    public async Task<List<string>> GetDistinctProductNamesAsync()
    {
        return await ProductStock
            .Where(p => p.Status == EntityStatus.Active)
            .DistinctAsync(p => p.Name);
    }
    
    public async Task<decimal> GetTotalProductValueAsync()
    {
        return await ProductStock
            .Where(p => p.Status == EntityStatus.Active)
            .SumAsync(p => p.Price);
    }
    
    public async Task<decimal> GetAverageProductPriceAsync()
    {
        return (decimal)await ProductStock
            .Where(p => p.Status == EntityStatus.Active)
            .AverageAsync(p => (double)p.Price);
    }
    
    public async Task<Dictionary<string, List<Product>>> GetProductsByCategoryAsync()
    {
        return await ProductStock
            .Where(p => p.Status == EntityStatus.Active)
            .GroupByAsync(p => p.CategoryId.ToString());
    }
    
    public async Task<List<ProductSummary>> GetProductSummariesAsync()
    {
        return await ProductStock
            .Where(p => p.Status == EntityStatus.Active)
            .SelectAsync(p => new ProductSummary 
            { 
                Name = p.Name, 
                Price = p.Price, 
                CategoryId = p.CategoryId 
            });
    }
    
    public async Task<bool> AllProductsHaveValidPricesAsync()
    {
        return await ProductStock
            .Where(p => p.Status == EntityStatus.Active)
            .AllAsync(p => p.Price > 0);
    }
}
```

### CRUD Operations

```csharp
// Create
var product = new Product { Name = "New Product", Price = 99.99m };
await collection.InsertOneAsync(product);

// Read
var product = await collection.Find(p => p.Id == productId).FirstOrDefaultAsync();

// Update
await collection.UpdateOneAsync(
    p => p.Id == productId,
    Builders<Product>.Update.Set(p => p.Price, 149.99m)
);

// Delete
await collection.DeleteOneAsync(p => p.Id == productId);
```

### Bulk Operations

```csharp
// Bulk insert
var products = new List<Product> { /* ... */ };
await collection.InsertManyAsync(products);

// Bulk update
await collection.UpdateManyAsync(
    p => p.CategoryId == oldCategoryId,
    Builders<Product>.Update.Set(p => p.CategoryId, newCategoryId)
);
```

## Advanced Querying

### Complex Filters

```csharp
// Multiple conditions
var products = await collection
    .Find(p => p.Price > 100 &&
               p.CategoryId == categoryId &&
               p.Name.Contains("Premium"))
    .ToListAsync();

// Array operations
var products = await collection
    .Find(p => p.Tags.Contains("electronics") &&
               p.Tags.Contains("sale"))
    .ToListAsync();

// Date range queries
var recentProducts = await collection
    .Find(p => p.CreatedAt >= DateTime.UtcNow.AddDays(-30))
    .ToListAsync();
```

### MongoDB Query Operators Support

The library provides comprehensive support for MongoDB query operators, including logical operators for complex filtering:

#### Logical Operators

```csharp
// $and operator - combines multiple conditions (all must be true)
var filter = new Dictionary<string, object>
{
    ["$and"] = new List<Dictionary<string, object>>
    {
        new() { ["price"] = new Dictionary<string, object> { ["$gt"] = 100 } },
        new() { ["status"] = "active" },
        new() { ["categoryId"] = categoryId }
    }
};

// $or operator - combines multiple conditions (any can be true)
var filter = new Dictionary<string, object>
{
    ["$or"] = new List<Dictionary<string, object>>
    {
        new() { ["price"] = new Dictionary<string, object> { ["$lt"] = 50 } },
        new() { ["onSale"] = true }
    }
};

// Complex nested queries with both $and and $or
var complexFilter = new Dictionary<string, object>
{
    ["$and"] = new List<Dictionary<string, object>>
    {
        new() { ["status"] = "active" },
        new()
        {
            ["$or"] = new List<Dictionary<string, object>>
            {
                new() { ["price"] = new Dictionary<string, object> { ["$lt"] = 100 } },
                new() { ["featured"] = true }
            }
        }
    }
};
```

#### Enhanced Query Processing

- **`$and` Operator Support**: Efficiently processes logical AND operations with proper array handling
- **`$or` Operator Support**: Handles logical OR operations with collection and array compatibility
- **Flexible Array Types**: Supports both `List<>` collections and `object[]` arrays for operator values
- **Nested Operators**: Allows complex nesting of logical operators for sophisticated queries
- **Performance Optimized**: Converts to native MongoDB FilterDefinitions for optimal query performance

### Sorting and Pagination

```csharp
// Complex sorting
var products = await collection
    .Find(p => p.CategoryId == categoryId)
    .Sort(Builders<Product>.Sort
        .Ascending(p => p.CategoryId)
        .Descending(p => p.Price)
        .Ascending(p => p.Name))
    .ToListAsync();

// Pagination
var pageSize = 20;
var pageIndex = 0;

var products = await collection
    .Find(p => p.Price > 100)
    .Skip(pageIndex * pageSize)
    .Limit(pageSize)
    .ToListAsync();
```

## Aggregation Pipelines

### MongoAggregationQueryBuilder

The `MongoAggregationQueryBuilder` provides a fluent interface for building complex aggregation pipelines:

```csharp
using OElite.Restme.MongoDb;

var collection = dbCentre.GetCollection<Product>();
var results = await collection.CreateAggregation<Product>()
    .Lookup<Category>("categories", "categoryId", "_id", "category")
    .Unwind("category")
    .Match(new BsonDocument { { "category.isActive", true } })
    .Sort(new BsonDocument { { "price", -1 } })
    .Limit(10)
    .ExecuteAsync<Product>();
```

### Common Aggregation Patterns

#### 1. Join Operations

```csharp
// Join products with categories
var productsWithCategories = await collection.CreateAggregation<Product>()
    .Lookup<Category>("categories", "categoryId", "_id", "category")
    .Unwind("category")
    .ExecuteAsync<Product>();
```

#### 2. Grouping and Aggregation

```csharp
// Group products by category and calculate average price
var categoryStats = await collection.CreateAggregation<Product>()
    .Lookup<Category>("categories", "categoryId", "_id", "category")
    .Unwind("category")
    .Group(new BsonDocument
    {
        { "_id", "$category.name" },
        { "averagePrice", new BsonDocument("$avg", "$price") },
        { "productCount", new BsonDocument("$sum", 1) }
    })
    .ExecuteAsync<BsonDocument>();
```

#### 3. Complex Filtering

```csharp
// Find products with active categories and high ratings
var topProducts = await collection.CreateAggregation<Product>()
    .Lookup<Category>("categories", "categoryId", "_id", "category")
    .Unwind("category")
    .Lookup<Review>("reviews", "_id", "productId", "reviews")
    .Match(new BsonDocument
    {
        { "category.isActive", true },
        { "reviews.rating", new BsonDocument("$gte", 4) }
    })
    .AddFields(new BsonDocument
    {
        { "averageRating", new BsonDocument("$avg", "$reviews.rating") }
    })
    .Sort(new BsonDocument { { "averageRating", -1 } })
    .Limit(20)
    .ExecuteAsync<Product>();
```

#### 4. Data Transformation

```csharp
// Transform product data with computed fields
var transformedProducts = await collection.CreateAggregation<Product>()
    .AddFields(new BsonDocument
    {
        { "priceRange", new BsonDocument("$switch", new BsonDocument
        {
            { "branches", new BsonArray
            {
                new BsonDocument { { "case", new BsonDocument("$lt", new BsonArray { "$price", 50 }) }, { "then", "Budget" } },
                new BsonDocument { { "case", new BsonDocument("$lt", new BsonArray { "$price", 200 }) }, { "then", "Mid-range" } },
                new BsonDocument { { "case", new BsonDocument("$gte", new BsonArray { "$price", 200 }) }, { "then", "Premium" } }
            }},
            { "default", "Unknown" }
        })},
        { "isExpensive", new BsonDocument("$gt", new BsonArray { "$price", 100 }) }
    })
    .ExecuteAsync<Product>();
```

## Denormalization System

The denormalization system automatically populates related data from other collections, improving query performance and reducing the need for multiple database calls.

### Basic Denormalization

```csharp
public class Product : BaseEntity
{
    public DbObjectId CategoryId { get; set; }
    public DbObjectId BrandId { get; set; }
    
    // Populate category name
    [DenormalizedField("categories", "name", "@CategoryId")]
    public string CategoryName { get; set; } = string.Empty;
    
    // Populate brand information
    [DenormalizedField("brands", "*", "@BrandId")]
    public Brand? Brand { get; set; }
    
    // Populate related products
    [DenormalizedCollection("products", "@CategoryId as categoryId")]
    public List<Product> RelatedProducts { get; set; } = new();
}
```

### Advanced Denormalization

```csharp
public class Order : BaseEntity
{
    public DbObjectId CustomerId { get; set; }
    public DbObjectId ProductId { get; set; }
    
    // Populate customer email with custom query
    [DenormalizedField("customers", "email", "@CustomerId", 
        new DbSimpleQuery { Query = "{ status: 'active' }" })]
    public string CustomerEmail { get; set; } = string.Empty;
    
    // Populate recent orders with sorting and limiting
    [DenormalizedCollection("orders", "@CustomerId as customerId",
        new DbSimpleQuery 
        { 
            Query = "{ status: 'completed' }",
            Sort = "{ createdAt: -1 }",
            Limit = 5
        })]
    public List<Order> RecentOrders { get; set; } = new();
}
```

### Field Remapping

```csharp
public class Product : BaseEntity
{
    public DbObjectId CategoryId { get; set; }
    
    // Map to different field name in MongoDB
    [DenormalizedField("categories", "display_name", "@CategoryId as category_id")]
    public string CategoryDisplayName { get; set; } = string.Empty;
    
    // Map to nested field
    [DenormalizedField("categories", "metadata.description", "@CategoryId as cat_id")]
    public string CategoryDescription { get; set; } = string.Empty;
}
```

## Performance Optimization

### 1. Indexing Strategy

```csharp
// Create indexes for frequently queried fields
await collection.Indexes.CreateOneAsync(
    Builders<Product>.IndexKeys
        .Ascending(p => p.CategoryId)
        .Descending(p => p.Price)
);

// Text search index
await collection.CreateTextIndexAsync(p => p.Name);

// Compound indexes for complex queries
await collection.Indexes.CreateOneAsync(
    Builders<Product>.IndexKeys
        .Ascending(p => p.CategoryId)
        .Ascending(p => p.IsActive)
        .Descending(p => p.CreatedAt)
);
```

### 2. Query Optimization

```csharp
// Use projection to limit returned fields
var products = await collection
    .Find(p => p.CategoryId == categoryId)
    .Project(p => new { p.Id, p.Name, p.Price })
    .ToListAsync();

// Use aggregation for complex queries instead of multiple finds
var results = await collection.CreateAggregation<Product>()
    .Lookup<Category>("categories", "categoryId", "_id", "category")
    .Unwind("category")
    .Match(new BsonDocument { { "category.isActive", true } })
    .ExecuteAsync<Product>();
```

### 3. Caching Strategy

```csharp
// Cache frequently accessed data
public class ProductService
{
    private readonly IMemoryCache _cache;
    
    public async Task<Product> GetProductAsync(DbObjectId productId)
    {
        var cacheKey = $"product_{productId}";
        
        if (_cache.TryGetValue(cacheKey, out Product cachedProduct))
            return cachedProduct;
            
        var product = await _collection.Find(p => p.Id == productId).FirstOrDefaultAsync();
        
        if (product != null)
            _cache.Set(cacheKey, product, TimeSpan.FromMinutes(15));
            
        return product;
    }
}
```

## Best Practices

### 1. Entity Design

```csharp
// Use appropriate data types
public class Product : BaseEntity
{
    [DbId]
    public DbObjectId Id { get; set; }
    
    // Use DbObjectId for references
    public DbObjectId CategoryId { get; set; }
    
    // Use appropriate string defaults
    public string Name { get; set; } = string.Empty;
    
    // Use nullable types for optional fields
    public string? Description { get; set; }
    
    // Use collections for arrays
    public List<string> Tags { get; set; } = new();
}
```

### 2. Query Patterns

```csharp
// Use async/await consistently
public async Task<List<Product>> GetProductsByCategoryAsync(DbObjectId categoryId)
{
    return await _collection
        .Find(p => p.CategoryId == categoryId)
        .ToListAsync();
}

// Use proper error handling
public async Task<Product?> GetProductAsync(DbObjectId productId)
{
    try
    {
        return await _collection
            .Find(p => p.Id == productId)
            .FirstOrDefaultAsync();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error retrieving product {ProductId}", productId);
        return null;
    }
}
```

### 3. Performance Considerations

```csharp
// Limit result sets
var products = await collection
    .Find(p => p.CategoryId == categoryId)
    .Limit(100)
    .ToListAsync();

// Use pagination for large datasets
var products = await collection
    .Find(p => p.Price > 100)
    .Skip(pageIndex * pageSize)
    .Limit(pageSize)
    .ToListAsync();

// Use aggregation for complex operations
var stats = await collection.CreateAggregation<Product>()
    .Group(new BsonDocument
    {
        { "_id", "$categoryId" },
        { "count", new BsonDocument("$sum", 1) },
        { "avgPrice", new BsonDocument("$avg", "$price") }
    })
    .ExecuteAsync<BsonDocument>();
```

## Examples

### E-commerce Product Catalog

```csharp
[DbCollection("products", DbNamingConvention.SnakeCase)]
public class Product : BaseEntity
{
    [DbId]
    public DbObjectId Id { get; set; }
    
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DbObjectId CategoryId { get; set; }
    public DbObjectId BrandId { get; set; }
    public List<string> Tags { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Denormalized fields
    [DenormalizedField("categories", "name", "@CategoryId")]
    public string CategoryName { get; set; } = string.Empty;
    
    [DenormalizedField("brands", "name", "@BrandId")]
    public string BrandName { get; set; } = string.Empty;
    
    [DenormalizedCollection("product_reviews", "@Id as productId",
        new DbSimpleQuery { Sort = "{ createdAt: -1 }", Limit = 5 })]
    public List<ProductReview> RecentReviews { get; set; } = new();
}

public class ProductService
{
    private readonly IMongoCollection<Product> _collection;
    
    public ProductService(DbCentre dbCentre)
    {
        _collection = dbCentre.GetCollection<Product>();
    }
    
    public async Task<List<Product>> GetProductsByCategoryAsync(
        DbObjectId categoryId, 
        int pageIndex = 0, 
        int pageSize = 20)
    {
        return await _collection
            .Find(p => p.CategoryId == categoryId && p.IsActive)
            .Sort(p => p.CreatedAt, false)
            .Skip(pageIndex * pageSize)
            .Limit(pageSize)
            .ToListAsync();
    }
    
    public async Task<List<Product>> SearchProductsAsync(
        string searchTerm, 
        decimal? minPrice = null, 
        decimal? maxPrice = null)
    {
        var filter = Builders<Product>.Filter.And(
            Builders<Product>.Filter.Text(searchTerm),
            Builders<Product>.Filter.Eq(p => p.IsActive, true)
        );
        
        if (minPrice.HasValue)
            filter = Builders<Product>.Filter.And(filter, 
                Builders<Product>.Filter.Gte(p => p.Price, minPrice.Value));
                
        if (maxPrice.HasValue)
            filter = Builders<Product>.Filter.And(filter, 
                Builders<Product>.Filter.Lte(p => p.Price, maxPrice.Value));
        
        return await _collection
            .Find(filter)
            .Sort(Builders<Product>.Sort.MetaTextScore("score"))
            .ToListAsync();
    }
    
    public async Task<Dictionary<string, decimal>> GetCategoryPriceStatsAsync()
    {
        var stats = await _collection.CreateAggregation<Product>()
            .Lookup<Category>("categories", "categoryId", "_id", "category")
            .Unwind("category")
            .Group(new BsonDocument
            {
                { "_id", "$category.name" },
                { "averagePrice", new BsonDocument("$avg", "$price") },
                { "minPrice", new BsonDocument("$min", "$price") },
                { "maxPrice", new BsonDocument("$max", "$price") },
                { "productCount", new BsonDocument("$sum", 1) }
            })
            .ExecuteAsync<BsonDocument>();
            
        return stats.ToDictionary(
            s => s["_id"].AsString,
            s => s["averagePrice"].AsDecimal
        );
    }
}
```

### User Management System

```csharp
[DbCollection("users", DbNamingConvention.SnakeCase)]
public class User : BaseEntity
{
    [DbId]
    public DbObjectId Id { get; set; }
    
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    
    // Denormalized collections
    [DenormalizedCollection("user_roles", "@Id as userId")]
    public List<UserRole> Roles { get; set; } = new();
    
    [DenormalizedCollection("user_permissions", "@Id as userId")]
    public List<UserPermission> Permissions { get; set; } = new();
}

public class UserService
{
    private readonly IMongoCollection<User> _collection;
    
    public UserService(DbCentre dbCentre)
    {
        _collection = dbCentre.GetCollection<User>();
    }
    
    public async Task<User?> GetUserWithRolesAsync(DbObjectId userId)
    {
        return await _collection
            .Find(u => u.Id == userId)
            .FirstOrDefaultAsync();
    }
    
    public async Task<List<User>> GetActiveUsersAsync(int pageIndex = 0, int pageSize = 50)
    {
        return await _collection
            .Find(u => u.IsActive)
            .Sort(u => u.LastLoginAt, false)
            .Skip(pageIndex * pageSize)
            .Limit(pageSize)
            .ToListAsync();
    }
    
    public async Task<Dictionary<string, int>> GetUserRegistrationStatsAsync(DateTime fromDate)
    {
        var stats = await _collection.CreateAggregation<User>()
            .Match(new BsonDocument { { "createdAt", new BsonDocument("$gte", fromDate) } })
            .Group(new BsonDocument
            {
                { "_id", new BsonDocument("$dateToString", new BsonDocument
                {
                    { "format", "%Y-%m-%d" },
                    { "date", "$createdAt" }
                })},
                { "count", new BsonDocument("$sum", 1) }
            })
            .Sort(new BsonDocument { { "_id", 1 } })
            .ExecuteAsync<BsonDocument>();
            
        return stats.ToDictionary(
            s => s["_id"].AsString,
            s => s["count"].AsInt32
        );
    }
}
```

## API Reference

### MongoAggregationQueryBuilder

#### Methods

- `Lookup<TForeign>(string foreignCollection, string localField, string foreignField, string aliasField)` - Join with another collection
- `Match(BsonDocument filter)` - Filter documents
- `Match(Dictionary<string, object> filter)` - Filter with dictionary
- `Project(BsonDocument projection)` - Select specific fields
- `Unwind(string fieldPath, bool preserveNullAndEmptyArrays = false)` - Flatten arrays
- `Group(BsonDocument groupDefinition)` - Group documents
- `Sort(BsonDocument sortDefinition)` - Sort results
- `Limit(int limit)` - Limit number of results
- `Skip(int skip)` - Skip number of results
- `AddFields(BsonDocument fields)` - Add computed fields
- `AddStage(BsonDocument stage)` - Add custom pipeline stage
- `ExecuteAsync<TResult>()` - Execute pipeline and return results
- `FirstOrDefaultAsync<TResult>()` - Execute pipeline and return first result
- `CountAsync()` - Execute pipeline and return count

### Extension Methods

- `CreateAggregation<T>(this IMongoCollection<T> collection)` - Create aggregation builder

### Attributes

- `[DbCollection(string? collectionName, DbNamingConvention namingConvention)]` - Collection mapping
- `[DbField(string fieldName)]` - Field mapping
- `[DbId]` - Primary key field
- `[DbFieldIgnore]` - Exclude field from storage
- `[DbDateTimeOptions(DateTimeKind kind)]` - DateTime serialization options
- `[DenormalizedField(...)]` - Field denormalization
- `[DenormalizedCollection(...)]` - Collection denormalization

### MongoDB-Specific Components

- **`MongoPropertyConflictResolver`** - Resolves MongoDB property conflicts, including denormalized fields and inherited properties
- **`MongoClassMapConfigurator`** - Configures MongoDB class mappings to use custom attributes with conflict resolution
- **`RestmeDbAttributeConvention`** - Custom MongoDB convention that applies RestmeDb attributes to class mappings

### Data Types

- `DbObjectId` - MongoDB ObjectId equivalent
- `DbSimpleQuery` - Query configuration for denormalization
- `DbNamingConvention` - Enum for naming conventions (SnakeCase, CamelCase, PascalCase)

## FetchAsync Extensions

The library provides convenient `FetchAsync` extensions that simplify common query patterns:

### Single Record Fetching
```csharp
// FetchAsync() - equivalent to FirstOrDefaultAsync()
public async Task<Product?> GetProductByIdAsync(DbObjectId productId)
{
    return await ProductStock
        .Where(p => p.Id == productId)
        .FetchAsync(); // Returns T? or null if not found
}
```

### Collection Fetching
```csharp
// FetchAsync<T, TCollection>() - returns BaseEntityCollection<T>
public async Task<ProductCollection> GetActiveProductsAsync()
{
    return await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .OrderBy(p => p.Name)
        .FetchAsync<Product, ProductCollection>(); // Returns typed collection
}

// FetchAsync with optional total count calculation
public async Task<ProductCollection> GetActiveProductsWithCountAsync()
{
    return await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .OrderBy(p => p.Name)
        .FetchAsync<Product, ProductCollection>(returnTotalCount: true); // Includes TotalRecordsCount
}

// FetchAsync with pagination (default: no total count for better performance)
public async Task<ProductCollection> GetActiveProductsPagedAsync(int pageIndex, int pageSize)
{
    return await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .OrderBy(p => p.Name)
        .FetchAsync<Product, ProductCollection>(pageIndex, pageSize); // returnTotalCount defaults to false
}

// FetchAsync with pagination and total count (explicitly requested)
public async Task<ProductCollection> GetActiveProductsPagedWithCountAsync(int pageIndex, int pageSize)
{
    return await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .OrderBy(p => p.Name)
        .FetchAsync<Product, ProductCollection>(pageIndex, pageSize, returnTotalCount: true);
}
```

### Benefits of FetchAsync
- **Cleaner syntax** - More intuitive than `FirstOrDefaultAsync()` and `ToListAsync()`
- **Type safety** - Generic constraints ensure proper collection types
- **Consistent API** - Both methods follow the same naming pattern
- **Performance optimization** - Optional total count calculation to avoid unnecessary queries
- **Pagination support** - Built-in pagination with efficient total count handling
- **Parallel execution** - Count and data queries run in parallel when total count is needed

## Performance Considerations

### Query Optimization

- **Use appropriate indexes** for your query patterns
- **Consider using aggregation pipelines** for complex queries
- **Use projection** to limit returned fields when possible
- **Implement pagination** for large result sets
- **Use `FetchAsync()`** for single result queries
- **Use `returnTotalCount: false`** when total count is not needed for better performance

### Performance-Optimized Methods

#### FetchAsync Performance Optimization

The `FetchAsync<T, TCollection>()` method provides intelligent performance optimization:

**When `returnTotalCount = false` (default for all methods):**
- **Skips the count query entirely** - saves one database round trip
- **Significantly faster** for scenarios where total count is not needed
- Perfect for infinite scroll, "load more" buttons, or when you only need the current page data
- **Default behavior** - prioritizes performance by default

**When `returnTotalCount = true` (explicitly requested):**
- Executes count and data queries **in parallel** using `Task.WhenAll()`
- Uses existing optimized `ToPagedListAsync()` method for pagination
- Provides accurate `TotalRecordsCount` for UI pagination controls
- Use only when total count is actually needed

**Performance Comparison:**
```csharp
// Fast - Single query, no count (default behavior)
var fastResults = await ProductStock
    .Where(p => p.Status == EntityStatus.Active)
    .FetchAsync<Product, ProductCollection>(); // returnTotalCount defaults to false

// Slower - Two queries (count + data) but provides total count (explicitly requested)
var resultsWithCount = await ProductStock
    .Where(p => p.Status == EntityStatus.Active)
    .FetchAsync<Product, ProductCollection>(returnTotalCount: true);
```

### Performance-Optimized Methods

The library includes several performance-optimized methods:

```csharp
// Efficient existence check - stops at first match
var exists = await ProductStock.Where(p => p.Name == "Widget").AnyAsync();

// Optimized single result queries
var product = await ProductStock.Where(p => p.Id == productId).FirstOrDefaultAsync();
var singleProduct = await ProductStock.Where(p => p.Sku == "ABC123").SingleOrDefaultAsync();

// Efficient counting with limit
var hasExpensiveProducts = await ProductStock.Where(p => p.Price > 1000).CountAsync() > 0;

// Pagination with total count
var (items, totalCount) = await ProductStock
    .Where(p => p.CategoryId == categoryId)
    .OrderBy(p => p.Name)
    .ToPagedListAsync(pageIndex, pageSize);
```

### MongoDB-Specific Optimizations

- **`AnyAsync()`** uses `CountDocumentsAsync` with `Limit = 1` for efficient existence checks
- **`SingleOrDefaultAsync()`** limits to 2 documents to check for uniqueness
- **`FirstOrDefaultAsync()`** uses `Limit = 1` to avoid loading unnecessary data
- **Pagination methods** combine count and data queries efficiently
- **LINQ expressions** are translated to native MongoDB queries for optimal performance

### Aggregation Pipeline Optimizations

All complex operations now use MongoDB aggregation pipelines for maximum performance:

- **`SelectAsync()`** uses `$project` stage for field projection without loading full documents
- **`GroupByAsync()`** uses `$group` stage for server-side grouping operations
- **`DistinctAsync()`** uses `$group` + `$project` for efficient distinct value retrieval
- **`MaxAsync()`, `MinAsync()`, `AverageAsync()`, `SumAsync()`** use `$group` with aggregation operators
- **`AllAsync()`** uses inverted `$match` with `$limit` for efficient "all match" checks

**Performance Benefits:**
- **No in-memory processing** - all operations execute on MongoDB server
- **Minimal data transfer** - only required fields are returned
- **Index utilization** - aggregation pipelines can leverage MongoDB indexes
- **Scalable** - performance remains consistent regardless of collection size

## Advanced Aggregation Operations Guide

This section provides comprehensive guidance on using the new aggregation-based methods with detailed examples, special syntax, and performance considerations.

### Field Projection with SelectAsync

The `SelectAsync` method uses MongoDB's `$project` stage to fetch only the required fields, dramatically reducing data transfer and memory usage.

#### Basic Field Projection

```csharp
// Project to single field
public async Task<List<string>> GetProductNamesAsync()
{
    return await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .SelectAsync(p => p.Name); // Only fetches the 'name' field
}

// Project to multiple fields using anonymous objects
public async Task<List<object>> GetProductSummariesAsync()
{
    return await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .SelectAsync(p => new { p.Name, p.Price, p.CategoryId });
}

// Project to strongly-typed DTOs
public class ProductSummary
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DbObjectId CategoryId { get; set; } = DbObjectId.Empty;
}

public async Task<List<ProductSummary>> GetProductSummariesTypedAsync()
{
    return await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .SelectAsync(p => new ProductSummary 
        { 
            Name = p.Name, 
            Price = p.Price, 
            CategoryId = p.CategoryId 
        });
}
```

#### Complex Projection Examples

```csharp
// Project with calculated fields
public async Task<List<object>> GetProductWithCalculatedFieldsAsync()
{
    return await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .SelectAsync(p => new 
        { 
            p.Name, 
            p.Price,
            IsExpensive = p.Price > 100, // Calculated field
            PriceCategory = p.Price > 100 ? "Premium" : "Standard"
        });
}

// Project with nested object access
public async Task<List<object>> GetProductWithNestedDataAsync()
{
    return await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .SelectAsync(p => new 
        { 
            p.Name, 
            p.Price,
            CategoryName = p.Category.Name, // Nested property access
            SupplierCountry = p.Supplier.Address.Country
        });
}
```

### Grouping Operations with GroupByAsync

The `GroupByAsync` method uses MongoDB's `$group` stage for efficient server-side grouping operations.

#### Basic Grouping

```csharp
// Group by single field
public async Task<Dictionary<string, List<Product>>> GetProductsByCategoryAsync()
{
    return await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .GroupByAsync(p => p.CategoryId.ToString());
}

// Group by multiple fields (composite key)
public async Task<Dictionary<object, List<Product>>> GetProductsByCategoryAndStatusAsync()
{
    return await ProductStock
        .GroupByAsync(p => new { p.CategoryId, p.Status });
}
```

#### Advanced Grouping with Aggregations

```csharp
// Group with count aggregation
public async Task<Dictionary<string, int>> GetProductCountByCategoryAsync()
{
    var grouped = await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .GroupByAsync(p => p.CategoryId.ToString());
    
    return grouped.ToDictionary(g => g.Key, g => g.Value.Count);
}

// Group with price statistics
public async Task<Dictionary<string, object>> GetCategoryPriceStatsAsync()
{
    var grouped = await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .GroupByAsync(p => p.CategoryId.ToString());
    
    return grouped.ToDictionary(g => g.Key, g => new
    {
        Count = g.Value.Count,
        TotalValue = g.Value.Sum(p => p.Price),
        AveragePrice = g.Value.Average(p => p.Price),
        MinPrice = g.Value.Min(p => p.Price),
        MaxPrice = g.Value.Max(p => p.Price)
    });
}
```

### Distinct Operations with DistinctAsync

The `DistinctAsync` method uses MongoDB's `$group` stage to efficiently retrieve unique values.

#### Basic Distinct Operations

```csharp
// Get distinct values from single field
public async Task<List<string>> GetDistinctProductNamesAsync()
{
    return await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .DistinctAsync(p => p.Name);
}

// Get distinct values from multiple fields
public async Task<List<object>> GetDistinctCategoryAndStatusAsync()
{
    return await ProductStock
        .DistinctAsync(p => new { p.CategoryId, p.Status });
}
```

#### Complex Distinct Scenarios

```csharp
// Get distinct values with additional filtering
public async Task<List<string>> GetDistinctActiveProductNamesAsync()
{
    return await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .Where(p => p.Price > 0)
        .DistinctAsync(p => p.Name);
}

// Get distinct values with sorting
public async Task<List<string>> GetDistinctSortedCategoriesAsync()
{
    return await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .OrderBy(p => p.CategoryId)
        .DistinctAsync(p => p.CategoryId.ToString());
}
```

### Mathematical Aggregations

The library provides efficient server-side mathematical operations using MongoDB's aggregation operators.

#### Sum Operations

```csharp
// Sum decimal values
public async Task<decimal> GetTotalProductValueAsync()
{
    return await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .SumAsync(p => p.Price);
}

// Sum with additional filtering
public async Task<decimal> GetTotalValueForCategoryAsync(DbObjectId categoryId)
{
    return await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .Where(p => p.CategoryId == categoryId)
        .SumAsync(p => p.Price);
}
```

#### Average Operations

```csharp
// Average with type conversion
public async Task<decimal> GetAverageProductPriceAsync()
{
    return (decimal)await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .AverageAsync(p => (double)p.Price);
}

// Average with filtering
public async Task<double> GetAveragePriceForExpensiveProductsAsync()
{
    return await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .Where(p => p.Price > 100)
        .AverageAsync(p => (double)p.Price);
}
```

#### Min/Max Operations

```csharp
// Min/Max with different data types
public async Task<decimal> GetMinProductPriceAsync()
{
    return await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .MinAsync(p => p.Price);
}

public async Task<decimal> GetMaxProductPriceAsync()
{
    return await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .MaxAsync(p => p.Price);
}

// Min/Max with date fields
public async Task<DateTime> GetEarliestProductDateAsync()
{
    return await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .MinAsync(p => p.CreatedDate);
}
```

### Universal Quantification with AllAsync

The `AllAsync` method efficiently checks if all documents match a predicate using inverted matching.

#### Basic All Operations

```csharp
// Check if all products have valid prices
public async Task<bool> AllProductsHaveValidPricesAsync()
{
    return await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .AllAsync(p => p.Price > 0);
}

// Check if all products in category are active
public async Task<bool> AllProductsInCategoryAreActiveAsync(DbObjectId categoryId)
{
    return await ProductStock
        .Where(p => p.CategoryId == categoryId)
        .AllAsync(p => p.Status == EntityStatus.Active);
}
```

#### Complex All Operations

```csharp
// Check multiple conditions
public async Task<bool> AllProductsMeetQualityStandardsAsync()
{
    return await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .AllAsync(p => p.Price > 0 && p.Name.Length > 0 && p.CategoryId != DbObjectId.Empty);
}

// Check with nested property access
public async Task<bool> AllProductsHaveValidCategoriesAsync()
{
    return await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .AllAsync(p => p.Category != null && p.Category.Name.Length > 0);
}
```

### Performance Optimization Tips

#### 1. Use Appropriate Indexes

```csharp
// Ensure indexes exist for frequently queried fields
// Example: Create compound index for common query patterns
await collection.CreateIndexAsync(
    Builders<Product>.IndexKeys
        .Ascending(p => p.Status)
        .Ascending(p => p.CategoryId)
        .Ascending(p => p.Price)
);
```

#### 2. Combine Filters Before Aggregation

```csharp
// GOOD: Combine filters before aggregation
public async Task<List<string>> GetDistinctNamesForActiveExpensiveProductsAsync()
{
    return await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .Where(p => p.Price > 100)
        .DistinctAsync(p => p.Name);
}

// AVOID: Multiple separate queries
public async Task<List<string>> GetDistinctNamesForActiveExpensiveProductsBadAsync()
{
    var activeProducts = await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .ToListAsync(); // Loads all active products into memory
    
    var expensiveProducts = activeProducts
        .Where(p => p.Price > 100)
        .Select(p => p.Name)
        .Distinct()
        .ToList(); // Processes in memory
    
    return expensiveProducts;
}
```

#### 3. Use Projection for Large Result Sets

```csharp
// GOOD: Use projection to reduce data transfer
public async Task<List<object>> GetProductNamesAndPricesAsync()
{
    return await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .SelectAsync(p => new { p.Name, p.Price }); // Only fetches required fields
}

// AVOID: Loading full documents when only specific fields are needed
public async Task<List<object>> GetProductNamesAndPricesBadAsync()
{
    var products = await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .ToListAsync(); // Loads all fields for all products
    
    return products.Select(p => new { p.Name, p.Price }).ToList();
}
```

#### 4. Leverage Aggregation for Complex Calculations

```csharp
// GOOD: Use aggregation for complex calculations
public async Task<Dictionary<string, object>> GetCategoryStatisticsAsync()
{
    var grouped = await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .GroupByAsync(p => p.CategoryId.ToString());
    
    return grouped.ToDictionary(g => g.Key, g => new
    {
        Count = g.Value.Count,
        TotalValue = g.Value.Sum(p => p.Price),
        AveragePrice = g.Value.Average(p => p.Price),
        MinPrice = g.Value.Min(p => p.Price),
        MaxPrice = g.Value.Max(p => p.Price)
    });
}

// AVOID: Loading all data and calculating in memory
public async Task<Dictionary<string, object>> GetCategoryStatisticsBadAsync()
{
    var allProducts = await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .ToListAsync(); // Loads all products into memory
    
    var grouped = allProducts.GroupBy(p => p.CategoryId.ToString());
    
    return grouped.ToDictionary(g => g.Key, g => new
    {
        Count = g.Count(),
        TotalValue = g.Sum(p => p.Price),
        AveragePrice = g.Average(p => p.Price),
        MinPrice = g.Min(p => p.Price),
        MaxPrice = g.Max(p => p.Price)
    });
}
```

### Error Handling and Best Practices

#### 1. Thread-Safe MongoDB Class Mapping (Concurrent Environment Support)

The library now provides enhanced thread-safety for MongoDB class mapping registration to prevent race conditions in multi-threaded environments:

```csharp
// Thread-safe class mapping registration
// Multiple threads can safely call this simultaneously without conflicts
MongoClassMapConfigurator.RegisterClassMapping<Product>();

// The library handles concurrent registration attempts gracefully:
// - Uses internal locks to prevent race conditions
// - Catches and handles duplicate registration attempts
// - Maintains an internal registry of registered types
// - No application code changes required
```

**Key Improvements:**
- **Atomic Registration**: Uses locks to ensure only one thread registers a type at a time
- **Duplicate Detection**: Prevents "An item with the same key has already been added" errors
- **Exception Handling**: Gracefully handles concurrent registration attempts
- **BaseEntity Safety**: Special handling for BaseEntity and inheritance chains
- **Backward Compatibility**: All existing code continues to work without changes

#### 2. Handle Empty Results Gracefully

```csharp
public async Task<decimal> GetAveragePriceSafelyAsync()
{
    try
    {
        return (decimal)await ProductStock
            .Where(p => p.Status == EntityStatus.Active)
            .AverageAsync(p => (double)p.Price);
    }
    catch (InvalidOperationException)
    {
        // Handle case when no products match the criteria
        return 0m;
    }
}
```

#### 2. Use Appropriate Data Types

```csharp
// GOOD: Use correct data types for aggregation
public async Task<double> GetAveragePriceAsync()
{
    return await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .AverageAsync(p => (double)p.Price); // Convert decimal to double for AverageAsync
}

// GOOD: Use decimal for SumAsync
public async Task<decimal> GetTotalValueAsync()
{
    return await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .SumAsync(p => p.Price); // Keep as decimal for SumAsync
}
```

#### 3. Combine Operations Efficiently

```csharp
// GOOD: Chain operations efficiently
public async Task<List<ProductSummary>> GetTopExpensiveProductsAsync(int count)
{
    return await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .Where(p => p.Price > 100)
        .OrderByDescending(p => p.Price)
        .Take(count)
        .SelectAsync(p => new ProductSummary 
        { 
            Name = p.Name, 
            Price = p.Price, 
            CategoryId = p.CategoryId 
        });
}
```

This comprehensive guide should help developers understand and effectively use all the new aggregation-based methods with proper performance considerations and best practices.

### Special Syntax and Field Mapping

The aggregation methods automatically respect OElite's custom attribute mappings. Understanding how field names are resolved is crucial for effective usage.

#### Field Name Resolution

The library automatically converts C# property names to MongoDB field names based on the following priority:

1. **`[DbId]` attribute** - Maps to `_id` field
2. **`[DbField]` attribute** - Uses the specified `FieldName`
3. **`[DbCollection]` naming convention** - Converts based on the collection's naming convention
4. **Default snake_case conversion** - Converts PascalCase to snake_case

```csharp
// Example entity with custom field mappings
[DbCollection(DbSchema.Products.Name, DbNamingConvention.SnakeCase)]
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
    public string ComputedProperty { get; set; } = string.Empty; // Ignored in queries
}
```

#### Working with Custom Field Names

```csharp
// The aggregation methods automatically use the correct field names
public async Task<List<string>> GetProductNamesAsync()
{
    return await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .SelectAsync(p => p.Name); // Uses "product_name" field in MongoDB
}

public async Task<decimal> GetTotalValueAsync()
{
    return await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .SumAsync(p => p.Price); // Uses "price" field in MongoDB
}

public async Task<List<Product>> GetProductsByCategoryAsync(DbObjectId categoryId)
{
    return await ProductStock
        .Where(p => p.CategoryId == categoryId) // Uses "category_ref" field
        .ToListAsync();
}
```

#### Complex Field Access Patterns

```csharp
// Working with nested objects and denormalized data
public class ProductWithCategory : BaseEntity
{
    [DbId]
    public DbObjectId Id { get; set; } = DbObjectId.Empty;
    
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    
    [DenormalizedCollection(DbSchema.Categories.Name, "#Id")]
    public Category? Category { get; set; }
}

// Aggregation with denormalized fields
public async Task<List<object>> GetProductsWithCategoryNamesAsync()
{
    return await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .SelectAsync(p => new 
        { 
            p.Name, 
            p.Price,
            CategoryName = p.Category.Name // Accesses denormalized category data
        });
}
```

#### Type Conversion Considerations

```csharp
// Important: Use correct data types for aggregation methods
public async Task<double> GetAveragePriceAsync()
{
    // AverageAsync expects double, so convert decimal to double
    return await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .AverageAsync(p => (double)p.Price);
}

public async Task<decimal> GetTotalValueAsync()
{
    // SumAsync works with decimal directly
    return await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .SumAsync(p => p.Price);
}

// For date/time aggregations
public async Task<DateTime> GetEarliestDateAsync()
{
    return await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .MinAsync(p => p.CreatedDate);
}
```

#### Error Handling for Aggregation Operations

```csharp
// Handle potential exceptions from aggregation operations
public async Task<decimal> GetAveragePriceSafelyAsync()
{
    try
    {
        return (decimal)await ProductStock
            .Where(p => p.Status == EntityStatus.Active)
            .AverageAsync(p => (double)p.Price);
    }
    catch (InvalidOperationException)
    {
        // Thrown when no documents match the criteria
        return 0m;
    }
    catch (MongoCommandException ex)
    {
        // Handle MongoDB-specific errors
        _logger.LogError(ex, "MongoDB aggregation error");
        throw;
    }
}

// Check for empty results before aggregation
public async Task<decimal> GetAveragePriceWithCheckAsync()
{
    var hasProducts = await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .AnyAsync();
    
    if (!hasProducts)
        return 0m;
    
    return (decimal)await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .AverageAsync(p => (double)p.Price);
}
```

#### Performance Monitoring and Debugging

```csharp
// Example of monitoring aggregation performance
public async Task<List<string>> GetDistinctNamesWithMonitoringAsync()
{
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    
    try
    {
        var result = await ProductStock
            .Where(p => p.Status == EntityStatus.Active)
            .DistinctAsync(p => p.Name);
        
        stopwatch.Stop();
        _logger.LogInformation("DistinctAsync completed in {ElapsedMs}ms, returned {Count} items", 
            stopwatch.ElapsedMilliseconds, result.Count);
        
        return result;
    }
    catch (Exception ex)
    {
        stopwatch.Stop();
        _logger.LogError(ex, "DistinctAsync failed after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
        throw;
    }
}
```

#### Advanced Aggregation Patterns

```csharp
// Combining multiple aggregation operations efficiently
public async Task<object> GetProductStatisticsAsync()
{
    // Use multiple queries for different statistics
    var totalCount = await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .CountAsync();
    
    var totalValue = await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .SumAsync(p => p.Price);
    
    var averagePrice = totalCount > 0 
        ? (decimal)await ProductStock
            .Where(p => p.Status == EntityStatus.Active)
            .AverageAsync(p => (double)p.Price)
        : 0m;
    
    var distinctCategories = await ProductStock
        .Where(p => p.Status == EntityStatus.Active)
        .DistinctAsync(p => p.CategoryId.ToString());
    
    return new
    {
        TotalCount = totalCount,
        TotalValue = totalValue,
        AveragePrice = averagePrice,
        CategoryCount = distinctCategories.Count
    };
}
```

This comprehensive documentation provides developers with all the guidance they need to effectively use the new aggregation-based methods, including special syntax considerations, field mapping behavior, error handling, and performance optimization techniques.

## MongoDB Class Mapping and Conflict Resolution

The library provides advanced MongoDB class mapping capabilities with automatic conflict resolution for complex inheritance scenarios and denormalized fields.

### MongoPropertyConflictResolver

The `MongoPropertyConflictResolver` handles property conflicts that arise from inheritance and denormalized fields in MongoDB class mappings.

#### Key Features

- **Property Conflict Resolution**: Automatically resolves conflicts between base and derived class properties
- **Denormalized Field Handling**: Properly unmaps denormalized properties from MongoDB serialization
- **Inheritance Chain Support**: Ensures base classes are configured before derived classes

#### Usage

```csharp
// The resolver is automatically used by MongoClassMapConfigurator
// No direct usage required in most scenarios
```

#### Conflict Types Handled

1. **Hidden Base Properties**: Properties using the `new` keyword that hide base class properties
2. **Denormalized Fields**: Properties marked with `[DenormalizedField]` or `[DenormalizedCollection]`
3. **Inherited Properties**: Properties from base classes that need proper mapping

### MongoClassMapConfigurator

The `MongoClassMapConfigurator` provides centralized configuration for MongoDB class mappings with automatic conflict resolution.

#### Core Methods

##### RegisterClassMapping<T>
```csharp
public static void RegisterClassMapping<T>() where T : BaseEntity
```
Registers class mapping for a specific type with automatic conflict resolution.

##### ConfigureClassMappingForType
```csharp
public static void ConfigureClassMappingForType(Type type, HashSet<Type> configuredTypes)
```
Configures class mapping for any type (including legacy types) with conflict resolution. This method is designed for use by repository classes.

#### Usage Examples

```csharp
// Register mapping for a new entity type
MongoClassMapConfigurator.RegisterClassMapping<Product>();

// Configure mapping for legacy types in repositories
var configuredTypes = new HashSet<Type>();
MongoClassMapConfigurator.ConfigureClassMappingForType(typeof(LegacyProduct), configuredTypes);
```

### RestmeDbAttributeConvention

The `RestmeDbAttributeConvention` is a custom MongoDB convention that automatically applies RestmeDb attributes to class mappings.

#### Key Features

- **Automatic Attribute Application**: Applies `[DbField]`, `[DbId]`, `[DbFieldIgnore]`, and other attributes
- **Naming Convention Support**: Automatically converts property names based on collection naming conventions
- **Denormalized Field Exclusion**: Automatically excludes denormalized properties from MongoDB serialization
- **Custom Serializers**: Applies custom serializers for specific data types like `DbObjectId`

#### Automatic Field Mapping

The convention automatically handles field mapping based on the following priority:

1. **`[DbId]` attribute** → Maps to `_id` field
2. **`[DbField]` attribute** → Uses the specified `FieldName`
3. **`[DbCollection]` naming convention** → Converts based on the collection's naming convention
4. **Default snake_case conversion** → Converts PascalCase to snake_case

#### Denormalized Field Handling

```csharp
public class Product : BaseEntity
{
    [DbId]
    public DbObjectId Id { get; set; }
    
    public string Name { get; set; } = string.Empty;
    
    // This property is automatically excluded from MongoDB serialization
    [DenormalizedField("categories", "name", "@CategoryId")]
    public string CategoryName { get; set; } = string.Empty;
    
    // This property is also automatically excluded
    [DenormalizedCollection("products", "@CategoryId as category_id")]
    public List<Product> RelatedProducts { get; set; } = new();
}
```

#### Custom Serializer Application

The convention automatically applies custom serializers for specific data types:

```csharp
public class Product : BaseEntity
{
    [DbId]
    public DbObjectId Id { get; set; } // Automatically uses DbObjectIdSerializer
    
    [DbDateTimeOptions(DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } // Automatically uses UTC DateTime handling
}
```

### Integration with Repository Pattern

The MongoDB class mapping system integrates seamlessly with the repository pattern:

```csharp
public class ProductRepository : DataRepository
{
    public MongoQuery<Product> ProductStock => new(_adapter.GetCollection<Product>());
    
    // Class mapping is automatically configured when the collection is first accessed
    public async Task<List<Product>> GetActiveProductsAsync()
    {
        return await ProductStock
            .Where(p => p.Status == EntityStatus.Active)
            .ToListAsync();
    }
}
```

### Best Practices

#### 1. Use Appropriate Attributes

```csharp
// Good - Clear attribute usage
[DbCollection("products", DbNamingConvention.SnakeCase)]
public class Product : BaseEntity
{
    [DbId]
    public DbObjectId Id { get; set; }
    
    [DbField("product_name")]
    public string Name { get; set; } = string.Empty;
    
    [DbFieldIgnore]
    public string ComputedProperty { get; set; } = string.Empty;
}
```

#### 2. Handle Inheritance Properly

```csharp
// Base class
[DbCollection("base_entities", DbNamingConvention.SnakeCase)]
public class BaseEntity
{
    [DbId]
    public DbObjectId Id { get; set; }
    
    public DateTime CreatedAt { get; set; }
}

// Derived class - conflicts are automatically resolved
public class Product : BaseEntity
{
    // This property hides BaseEntity.Id but conflict is resolved automatically
    public new DbObjectId Id { get; set; }
    
    public string Name { get; set; } = string.Empty;
}
```

#### 3. Use Denormalized Fields Appropriately

```csharp
public class Product : BaseEntity
{
    public DbObjectId CategoryId { get; set; }
    
    // Denormalized fields are automatically excluded from MongoDB serialization
    [DenormalizedField("categories", "name", "@CategoryId")]
    public string CategoryName { get; set; } = string.Empty;
    
    [DenormalizedCollection("product_reviews", "@Id as product_id")]
    public List<ProductReview> Reviews { get; set; } = new();
}
```

### Performance Considerations

- **Automatic Caching**: Class mappings are cached to avoid repeated configuration
- **Lazy Initialization**: Mappings are configured only when collections are first accessed
- **Conflict Resolution**: Conflicts are resolved once during initial mapping configuration
- **Memory Efficiency**: Only necessary mappings are created and cached

### Error Handling

The system provides comprehensive error handling for mapping issues:

- **Missing Attributes**: Logs warnings when expected attributes are not found
- **Invalid Configurations**: Validates attribute configurations and logs errors
- **Type Mismatches**: Handles type compatibility issues gracefully
- **Circular Dependencies**: Prevents infinite loops in inheritance chain configuration

### Advanced LINQ Expression Support

The library provides comprehensive support for LINQ expressions with MongoDB, including nested document queries and extension methods.

#### Nested Document Queries

Full support for querying nested properties in embedded documents:

```csharp
// Nested document queries - fully supported!
public async Task<List<Product>> GetProductsWithNonDefaultMeasureUnitsAsync()
{
    return await ProductStock
        .Where(p => p.MeasureUnit != null && p.MeasureUnit.IsDefaultStockMeasure == false)
        .Where(p => p.MeasureUnit.Name.Contains("kg"))
        .OrderBy(p => p.Name)
        .ToListAsync();
}

// Complex nested queries with multiple levels
public async Task<List<Product>> GetProductsWithValidCategoryAsync()
{
    return await ProductStock
        .Where(p => p.Category != null && p.Category.ParentCategory != null)
        .Where(p => p.Category.ParentCategory.IsActive == true)
        .ToListAsync();
}
```

#### Extension Method Support

Use OElite.Restme.Utils extension methods directly in LINQ expressions:

```csharp
// Extension method queries - fully supported!
public async Task<List<Product>> GetProductsWithValidOwnerAsync()
{
    return await ProductStock
        .Where(p => p.OwnerMerchantId.IsNotNullOrEmpty())
        .Where(p => p.Name.IsNotNullOrEmpty())
        .OrderBy(p => p.Name)
        .ToListAsync();
}

// Combined nested and extension method queries
public async Task<List<Product>> GetProductsWithValidOwnerAndNonDefaultMeasureUnitAsync()
{
    return await ProductStock
        .Where(p => p.OwnerMerchantId.IsNotNullOrEmpty())           // Extension method
        .Where(p => p.MeasureUnit != null && p.MeasureUnit.IsDefaultStockMeasure == false)  // Nested query
        .OrderBy(p => p.Name)
        .ToListAsync();
}
```

#### Important: Null-Conditional Operator Limitations

**⚠️ CRITICAL**: The null-conditional operator (`?.`) **cannot be used** in LINQ expression trees (IQueryable queries) because it causes the error: *"An expression tree lambda cannot contain conditional access expressions"*.

**❌ This will cause compilation errors:**
```csharp
// DON'T DO THIS - causes "conditional access expressions" error
.Where(p => p.MeasureUnit?.IsDefaultStockMeasure == false)
.Where(p => p.TagData?.OwnerEntityId == productId)
.Where(p => p.Category?.ParentCategory?.IsActive == true)
```

**✅ Use explicit null checking instead:**
```csharp
// DO THIS - works correctly in expression trees
.Where(p => p.MeasureUnit != null && p.MeasureUnit.IsDefaultStockMeasure == false)
.Where(p => p.TagData != null && p.TagData.OwnerEntityId == productId)
.Where(p => p.Category != null && p.Category.ParentCategory != null && p.Category.ParentCategory.IsActive == true)
```

#### String Method Support

LINQ string methods are fully supported and translate to MongoDB regular expressions:

```csharp
// String methods - fully supported!
public async Task<List<Product>> GetProductsWithStringFiltersAsync()
{
    return await ProductStock
        .Where(p => p.Name.Contains("widget"))           // MongoDB $regex
        .Where(p => p.Description.StartsWith("Premium")) // MongoDB $regex with ^
        .Where(p => p.Sku.EndsWith("001"))              // MongoDB $regex with $
        .Where(p => p.Name.ToLower().Contains("sale"))  // MongoDB $regex with $options: "i"
        .ToListAsync();
}
```

#### Type Safety and Performance

- **Type Safety**: Compile-time checking of property names and types
- **MongoDB Translation**: Automatic translation to efficient MongoDB queries
- **Performance**: Server-side execution using MongoDB aggregation pipelines
- **IntelliSense**: Full IntelliSense support for all LINQ operations

## Troubleshooting

### Common Issues

1. **Collection Not Found**
   ```
   Error: Collection 'products' not found
   ```
   **Solution**: Ensure the collection name in `[DbCollection]` matches your MongoDB collection.

2. **Field Mapping Issues**
   ```
   Error: Field 'product_name' not found in document
   ```
   **Solution**: Check field names in `[DbField]` attributes match your MongoDB document structure.

3. **Performance Issues**
   ```
   Warning: Query took 5000ms to execute
   ```
   **Solution**: Add appropriate indexes and use aggregation pipelines for complex queries.

### Debugging Tips

1. **Enable MongoDB Logging**
   ```csharp
   // In your startup configuration
   services.Configure<MongoDbSettings>(options =>
   {
       options.LogLevel = LogLevel.Debug;
   });
   ```

2. **Use MongoDB Profiler**
   ```javascript
   // Enable profiling in MongoDB
   db.setProfilingLevel(2, { slowms: 100 });
   ```

3. **Check Query Execution Plans**
   ```csharp
   // Use explain() for query analysis
   var explain = await collection.Find(filter).ExplainAsync();
   ```

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests for new functionality
5. Submit a pull request

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Support

For support and questions:
- Create an issue in the repository
- Contact the OElite development team
- Check the documentation wiki

## 📚 Detailed Documentation

For comprehensive guides on specific features, see the detailed documentation:

### Core Features Documentation
- **[Region-Aware Sharding & GDPR Compliance](docs/REGION-AWARE-SHARDING.md)** - Complete guide to implementing geographic data management and compliance
- **[Attribute-Based Configuration](../OElite.Restme.Utils/docs/DATA-ATTRIBUTES-CONFIGURATION.md)** - Detailed guide to using database attributes and entity configuration
- **[Migration & Compliance Tools](docs/REGION-AWARE-SHARDING.md#migration--compliance)** - GDPR right to erasure, data portability, and cross-region migration
- **[Performance Optimization](README.md#performance-optimization)** - Advanced performance tuning and best practices

### Quick Reference
- **[Examples Repository](docs/REGION-AWARE-SHARDING.md#examples)** - Real-world implementation examples
- **[Best Practices](docs/REGION-AWARE-SHARDING.md#best-practices)** - Recommended patterns and approaches
- **[Troubleshooting Guide](README.md#troubleshooting)** - Common issues and solutions

---

**Version**: 2.3.0
**Last Updated**: 2024
**Compatibility**: .NET 9.0+

## 🚀 High-Performance Update Operations

The library includes a powerful `UpdateBuilder<T>` class that provides a fluent API for MongoDB update operations with significant performance improvements over direct MongoDB.Driver usage.

### UpdateBuilder<T> Features

- **Optimized Performance**: Uses raw BSON operations when possible for maximum speed
- **Fluent API**: Clean, readable syntax for complex update operations
- **Type Safety**: Strongly-typed expressions with compile-time validation
- **Field Name Mapping**: Automatic support for custom field attributes and naming conventions
- **Batch Operations**: Combine multiple operations for optimal network usage

### Basic Update Operations

```csharp
using static OElite.Restme.MongoDb.Update;

// Single field updates
await Products
    .Where(p => p.Id == productId)
    .UpdateAsync(u => u
        .Set(p => p.Name, "Updated Product")
        .Set(p => p.Price, 299.99m)
        .Inc(p => p.ViewCount, 1)
        .CurrentDate(p => p.UpdatedAt));

// Multiple field updates (optimized)
await Products
    .Where(p => p.CategoryId == categoryId)
    .UpdateAsync(u => u.Set(
        (p => p.IsActive, true),
        (p => p.Priority, 10),
        (p => p.UpdatedBy, "admin")
    ));
```

### Advanced Update Operations

```csharp
// Array operations
await Products
    .Where(p => p.Id == productId)
    .UpdateAsync(u => u
        .Push(p => p.Tags, "new-tag")
        .PushEach(p => p.Categories, new[] { "electronics", "gadgets" })
        .Pull(p => p.Tags, "old-tag")
        .AddToSet(p => p.RelatedIds, relatedId));

// Numeric operations
await Products
    .Where(p => p.Price < 100)
    .UpdateAsync(u => u
        .Inc(p => p.Price, 10.0m)        // Increment
        .Mul(p => p.Weight, 1.1m)        // Multiply
        .Max(p => p.MinPrice, 50.0m)     // Set if greater
        .Min(p => p.MaxPrice, 500.0m));  // Set if less

// Conditional updates
await Products
    .Where(p => p.Status == ProductStatus.Draft)
    .UpdateAsync(u => u
        .Set(p => p.Status, ProductStatus.Published)
        .Set(p => p.PublishedAt, DateTime.UtcNow)
        .Unset(p => p.DraftNotes));
```

### Static Factory Methods

```csharp
// Create update builders with static methods
var update1 = Update.Set<Product, string>(p => p.Name, "New Name");
var update2 = Update.Inc<Product, int>(p => p.ViewCount, 1);
var update3 = Update.Timestamp<Product>(p => p.UpdatedAt);

// Convenience timestamp methods
var timestampUpdate = Update.Timestamps<Product>(
    updatedField: p => p.UpdatedAt,
    accessedField: p => p.LastAccessedAt
);

await Products.Where(p => p.Id == id).UpdateAsync(_ => timestampUpdate);
```

### Performance Optimizations

The UpdateBuilder uses two different execution strategies:

#### Optimized BSON Approach (Default)
For simple operations (Set, Inc, Push, Pull, Unset), the builder generates raw BSON documents:

```csharp
// This generates optimized BSON: { "$set": { "name": "value", "price": 100 }, "$inc": { "viewCount": 1 } }
await Products.Where(p => p.Id == id).UpdateAsync(u => u
    .Set(p => p.Name, "New Name")
    .Set(p => p.Price, 100.0m)
    .Inc(p => p.ViewCount, 1));
```

#### MongoDB Builders Fallback
For complex operations (Max, Min, AddToSet), it falls back to MongoDB.Driver builders:

```csharp
// This uses MongoDB builders for complex operations
await Products.Where(p => p.Id == id).UpdateAsync(u => u
    .Set(p => p.Name, "New Name")    // Optimized
    .Max(p => p.Price, 100.0m)       // Uses MongoDB builders
    .AddToSet(p => p.Tags, "new"));  // Uses MongoDB builders
```

## 🎯 Enhanced LINQ Query Extensions

The library provides comprehensive LINQ-style extension methods for `MongoQuery<T>` with performance optimizations and MongoDB-specific features.

### Standard LINQ Operations

```csharp
public class ProductRepository : DataRepository
{
    public MongoQuery<Product> Products => new(_adapter.GetCollection<Product>());

    // Enhanced LINQ support with type safety
    public async Task<List<Product>> GetProductsAsync()
    {
        return await Products
            .Where(p => p.Status == EntityStatus.Active)
            .Where(p => p.Price > 100)
            .OrderBy(p => p.Name)
            .ThenByDescending(p => p.CreatedAt)
            .Take(20)
            .Skip(10)
            .ToListAsync();
    }

    // Single result operations with optimization
    public async Task<Product?> GetProductBySkuAsync(string sku)
    {
        return await Products
            .Where(p => p.Sku == sku)
            .FirstOrDefaultAsync(); // Optimized with Limit(1)
    }

    public async Task<Product> GetUniqueProductAsync(string uniqueField)
    {
        return await Products
            .Where(p => p.UniqueField == uniqueField)
            .SingleAsync(); // Validates uniqueness with Limit(2)
    }
}
```

### Advanced Aggregation Operations

```csharp
// Projection with MongoDB aggregation
public async Task<List<ProductSummary>> GetProductSummariesAsync()
{
    return await Products
        .Where(p => p.Status == EntityStatus.Active)
        .SelectAsync(p => new ProductSummary
        {
            Name = p.Name,
            Price = p.Price,
            CategoryName = p.Category.Name  // Nested property support
        });
}

// Grouping with server-side aggregation
public async Task<Dictionary<string, List<Product>>> GetProductsByCategoryAsync()
{
    return await Products
        .Where(p => p.Status == EntityStatus.Active)
        .GroupByAsync(p => p.CategoryId.ToString());
}

// Distinct values with aggregation pipeline
public async Task<List<string>> GetDistinctBrandsAsync()
{
    return await Products
        .Where(p => p.Status == EntityStatus.Active)
        .DistinctAsync(p => p.BrandName);
}

// Mathematical aggregations
public async Task<ProductStatistics> GetProductStatisticsAsync()
{
    var totalValue = await Products
        .Where(p => p.Status == EntityStatus.Active)
        .SumAsync(p => p.Price);

    var averagePrice = (decimal)await Products
        .Where(p => p.Status == EntityStatus.Active)
        .AverageAsync(p => (double)p.Price);

    var maxPrice = await Products
        .Where(p => p.Status == EntityStatus.Active)
        .MaxAsync(p => p.Price);

    var minPrice = await Products
        .Where(p => p.Status == EntityStatus.Active)
        .MinAsync(p => p.Price);

    return new ProductStatistics
    {
        TotalValue = totalValue,
        AveragePrice = averagePrice,
        MaxPrice = maxPrice ?? 0,
        MinPrice = minPrice ?? 0
    };
}
```

### Enhanced FetchAsync Methods

```csharp
// Single result fetching
public async Task<Product?> GetProductByIdAsync(DbObjectId productId)
{
    return await Products
        .Where(p => p.Id == productId)
        .FetchAsync(); // Equivalent to FirstOrDefaultAsync()
}

// Collection fetching with optional total count
public async Task<ProductCollection> GetActiveProductsAsync(bool includeCount = false)
{
    return await Products
        .Where(p => p.Status == EntityStatus.Active)
        .OrderBy(p => p.Name)
        .FetchAsync<Product, ProductCollection>(returnTotalCount: includeCount);
}

// Pagination with performance optimization
public async Task<ProductCollection> GetProductsPagedAsync(int pageIndex, int pageSize, bool includeCount = false)
{
    return await Products
        .Where(p => p.Status == EntityStatus.Active)
        .OrderBy(p => p.Name)
        .FetchAsync<Product, ProductCollection>(pageIndex, pageSize, returnTotalCount: includeCount);
}
```

### Data Modification Operations

```csharp
// Insert operations
public async Task AddProductAsync(Product product)
{
    await Products.InsertAsync(product);
}

public async Task AddProductsAsync(IEnumerable<Product> products)
{
    await Products.InsertManyAsync(products);
}

// Update operations with high performance
public async Task UpdateProductPriceAsync(DbObjectId productId, decimal newPrice)
{
    await Products
        .Where(p => p.Id == productId)
        .SetAsync(p => p.Price, newPrice);
}

public async Task IncrementViewCountAsync(DbObjectId productId)
{
    await Products
        .Where(p => p.Id == productId)
        .IncrementAsync(p => p.ViewCount, 1);
}

public async Task TouchProductAsync(DbObjectId productId)
{
    await Products
        .Where(p => p.Id == productId)
        .TouchAsync(p => p.UpdatedAt);
}

// Batch field updates
public async Task UpdateProductDetailsAsync(DbObjectId productId, string name, decimal price, bool isActive)
{
    await Products
        .Where(p => p.Id == productId)
        .SetFieldsAsync(
            (p => p.Name, name),
            (p => p.Price, price),
            (p => p.IsActive, isActive)
        );
}

// Replace operations
public async Task ReplaceProductAsync(Product product)
{
    await Products.ReplaceAsync(product, isUpsert: false);
}

// Delete operations
public async Task DeleteExpiredProductsAsync()
{
    await Products
        .Where(p => p.ExpiryDate < DateTime.UtcNow)
        .DeleteAsync();
}

public async Task DeleteProductByIdAsync(DbObjectId productId)
{
    await Products.DeleteByIdAsync(productId);
}
```

### Universal Quantification

```csharp
// Check if all products meet a condition
public async Task<bool> AllProductsHaveValidPricesAsync()
{
    return await Products
        .Where(p => p.Status == EntityStatus.Active)
        .AllAsync(p => p.Price > 0);
}

// Check if any products exist
public async Task<bool> HasActiveProductsAsync()
{
    return await Products
        .Where(p => p.Status == EntityStatus.Active)
        .AnyAsync();
}

// Count operations
public async Task<int> GetActiveProductCountAsync()
{
    return await Products
        .Where(p => p.Status == EntityStatus.Active)
        .CountAsync();
}
```

## 🔧 Advanced Expression to Pipeline Conversion

The library includes sophisticated LINQ expression to MongoDB aggregation pipeline conversion capabilities.

### Projection Support

```csharp
// Simple property projection
var names = await Products
    .Where(p => p.Status == EntityStatus.Active)
    .SelectAsync(p => p.Name);

// Complex object projection
var summaries = await Products
    .Where(p => p.Status == EntityStatus.Active)
    .SelectAsync(p => new ProductSummary
    {
        Id = p.Id,
        Name = p.Name,
        Price = p.Price,
        CategoryName = p.Category.Name,
        IsExpensive = p.Price > 100 // Calculated fields
    });
```

### Grouping Support

```csharp
// Single field grouping
var productsByCategory = await Products
    .Where(p => p.Status == EntityStatus.Active)
    .GroupByAsync(p => p.CategoryId.ToString());

// Composite key grouping
var productsByMultipleFields = await Products
    .Where(p => p.Status == EntityStatus.Active)
    .GroupByAsync(p => new { p.CategoryId, p.Status });
```

### Field Name Resolution

The pipeline converter automatically handles:

- **Custom Field Attributes**: `[DbField]` and `[BsonElement]` mappings
- **Naming Conventions**: Automatic camelCase/snake_case conversion
- **Nested Properties**: Deep property access like `p.Category.Name`
- **Expression Caching**: Performance optimization for repeated expressions

## 🚀 Performance Benefits

### UpdateBuilder Performance
- **2-3x faster** than MongoDB.Driver builders for simple operations
- **Batch optimization**: Combines multiple operations into single update
- **Memory efficient**: Minimal object allocation during update building
- **Network optimization**: Reduces round trips with combined operations

### Query Extensions Performance
- **Aggregation-based**: All operations use MongoDB aggregation pipelines
- **Server-side execution**: No in-memory processing for large datasets
- **Index utilization**: Optimized pipeline stages leverage MongoDB indexes
- **Minimal data transfer**: Projection reduces network overhead

### Expression Pipeline Performance
- **Expression caching**: Compiled expressions cached for reuse
- **Optimized pipeline stages**: Minimal pipeline complexity
- **Type conversion optimization**: Efficient BSON to .NET type conversion
- **Parallel execution**: Count and data queries run concurrently when needed

## 📋 Best Practices for High-Performance Operations

### 1. Use UpdateBuilder for Complex Updates

```csharp
// GOOD: Single update operation with multiple fields
await Products
    .Where(p => p.CategoryId == categoryId)
    .UpdateAsync(u => u
        .Set(p => p.IsActive, true)
        .Inc(p => p.ViewCount, 1)
        .Set(p => p.UpdatedAt, DateTime.UtcNow));

// AVOID: Multiple separate update calls
await Products.Where(p => p.CategoryId == categoryId).SetAsync(p => p.IsActive, true);
await Products.Where(p => p.CategoryId == categoryId).IncrementAsync(p => p.ViewCount, 1);
await Products.Where(p => p.CategoryId == categoryId).TouchAsync(p => p.UpdatedAt);
```

### 2. Leverage Aggregation Extensions

```csharp
// GOOD: Server-side aggregation
var categoryTotals = await Products
    .Where(p => p.Status == EntityStatus.Active)
    .GroupByAsync(p => p.CategoryId.ToString());

// AVOID: Client-side processing
var allProducts = await Products.Where(p => p.Status == EntityStatus.Active).ToListAsync();
var grouped = allProducts.GroupBy(p => p.CategoryId.ToString()).ToDictionary(g => g.Key, g => g.ToList());
```

### 3. Optimize FetchAsync Usage

```csharp
// GOOD: Skip count when not needed
var products = await Products
    .Where(p => p.Status == EntityStatus.Active)
    .FetchAsync<Product, ProductCollection>(returnTotalCount: false);

// GOOD: Include count only when necessary for UI pagination
var productsWithCount = await Products
    .Where(p => p.Status == EntityStatus.Active)
    .FetchAsync<Product, ProductCollection>(pageIndex, pageSize, returnTotalCount: true);
```

### 4. Use Appropriate Query Methods

```csharp
// GOOD: Use specific methods for their purpose
var exists = await Products.Where(p => p.Sku == sku).AnyAsync();           // Existence check
var product = await Products.Where(p => p.Id == id).FirstOrDefaultAsync(); // Single result
var count = await Products.Where(p => p.Status == EntityStatus.Active).CountAsync(); // Count only

// AVOID: Using general methods for specific purposes
var products = await Products.Where(p => p.Sku == sku).ToListAsync(); // Don't load all for existence check
var hasProducts = products.Count > 0; // Inefficient existence check
```

## Geographic Data Management

The library provides comprehensive geographic data management capabilities for global applications that need to comply with regional data protection regulations.

### 🌐 GeographicConfiguration

#### GDPR-Compliant Configuration
```csharp
var config = GeographicConfiguration.CreateGdprCompliantConfiguration();

// Results in configuration with:
// - EU region with GDPR compliance rules
// - US region with CCPA compliance rules
// - UK region with UK GDPR rules
// - Strict transfer restrictions between regions
// - Automatic data retention policies
// - Geographic zone mapping for MongoDB
```

#### Custom Geographic Configuration
```csharp
var customConfig = new GeographicConfiguration
{
    DefaultRegion = "US",
    AllowCrossRegionMigration = true,
    MigrationStrategy = RegionMigrationStrategy.CopyAndArchive,
    MigrationRetentionPeriod = TimeSpan.FromDays(365),
    Regions = new List<RegionConfiguration>
    {
        new()
        {
            RegionId = "US",
            DataCenters = new[] { "us-east-1", "us-west-2" },
            Jurisdiction = new DataJurisdiction
            {
                LegalFramework = "CCPA",
                TransferRestrictions = new[] { "No EU transfers without consent" },
                RequiresEncryptionAtRest = true
            },
            RetentionPolicy = new DataRetentionPolicy
            {
                DefaultRetentionPeriod = TimeSpan.FromDays(2555), // 7 years
                PurgeDeletedDataAfter = TimeSpan.FromDays(30)
            }
        },
        new()
        {
            RegionId = "EU",
            DataCenters = new[] { "eu-west-1", "eu-central-1" },
            Jurisdiction = new DataJurisdiction
            {
                LegalFramework = "GDPR",
                TransferRestrictions = new[] { "No non-EU transfers without adequacy decision" },
                RequiresEncryptionAtRest = true
            },
            RetentionPolicy = new DataRetentionPolicy
            {
                DefaultRetentionPeriod = TimeSpan.FromDays(1095), // 3 years
                PurgeDeletedDataAfter = TimeSpan.FromDays(30)
            }
        }
    }
};
```

### 🗺️ Zone-Based Sharding

#### Automatic Zone Configuration
```csharp
// Bootstrap with geographic zones
var result = await dbCentre.BootstrapEntitiesAsync<CustomerData>(
    new DbBootstrapOptions
    {
        EnableSharding = true,
        GeographicConfiguration = config
    });

// Automatically creates MongoDB zones:
// sh.addShardToZone("shard01", "EU")
// sh.addShardToZone("shard02", "US")
// sh.addShardToZone("shard03", "UK")
// sh.updateZoneKeyRange("customer_data", { "region": "EU" }, { "region": "EU\u9999" }, "EU")
```

#### Zone-Aware Queries
```csharp
// Queries automatically route to correct geographic zones
public class CustomerRepository : DataRepository
{
    public MongoQuery<Customer> Customers => new(_adapter.GetCollection<Customer>());

    // This query only hits EU shards when region = "EU"
    public async Task<List<Customer>> GetEuCustomersAsync()
    {
        return await Customers
            .Where(c => c.Region == "EU")
            .Where(c => c.IsActive == true)
            .ToListAsync();
    }

    // Cross-region queries hit multiple zones (use carefully for compliance)
    public async Task<List<Customer>> GetAllActiveCustomersAsync()
    {
        return await Customers
            .Where(c => c.IsActive == true)
            .ToListAsync(); // May hit multiple geographic zones
    }
}
```

### 📊 Region-Aware Analytics

#### Geographic Data Distribution
```csharp
public async Task<Dictionary<string, int>> GetCustomerDistributionByRegionAsync()
{
    return await Customers
        .Where(c => c.IsActive == true)
        .GroupByAsync(c => c.Region ?? "Unknown");
}

public async Task<object> GetRegionalStatisticsAsync()
{
    var totalsByRegion = await Customers
        .Where(c => c.IsActive == true)
        .GroupByAsync(c => c.Region ?? "Unknown");

    return totalsByRegion.ToDictionary(g => g.Key, g => new
    {
        CustomerCount = g.Value.Count,
        AverageCreationDate = g.Value.Average(c => c.CreatedOnUtc.Ticks),
        OldestCustomer = g.Value.Min(c => c.CreatedOnUtc),
        NewestCustomer = g.Value.Max(c => c.CreatedOnUtc)
    });
}
```

## Migration & Compliance

The library includes comprehensive data migration tools for regional compliance, supporting multiple migration strategies while maintaining data integrity and regulatory compliance.

### 🚀 RegionDataMigrator

#### Migration Strategies
```csharp
public enum RegionMigrationStrategy
{
    CopyAndDelete,      // Complete data transfer (GDPR right to portability)
    CopyAndArchive,     // Keep original with retention policy
    PreventMigration,   // Block transfer for compliance
    FederatedAccess     // Virtual access without data movement
}
```

#### Basic Migration Operations
```csharp
var migrator = new RegionDataMigrator(managementProvider, geographicConfig);

// Migrate single customer from US to EU
var result = await migrator.MigrateEntityAsync<Customer>(
    customerId,
    sourceRegion: "US",
    targetRegion: "EU",
    new RegionMigrationOptions
    {
        Strategy = RegionMigrationStrategy.CopyAndDelete,
        VerifyIntegrity = true,
        MaxRetryAttempts = 3
    });

if (result.Success)
{
    Console.WriteLine($"✅ Customer migrated successfully in {result.Duration?.TotalSeconds:F2}s");
    Console.WriteLine($"Migration steps: {string.Join(", ", result.MigrationSteps)}");
}
```

#### Batch Migration for Compliance
```csharp
// Batch migrate customers for GDPR compliance
var customerIds = await GetCustomersRequiringMigrationAsync("EU");

var batchResult = await migrator.MigrateBatchAsync<Customer>(
    customerIds,
    sourceRegion: "US",
    targetRegion: "EU",
    new RegionMigrationOptions
    {
        Strategy = RegionMigrationStrategy.CopyAndArchive,
        VerifyIntegrity = true
    });

Console.WriteLine($"Migration completed: {batchResult.SuccessfulMigrations}/{batchResult.TotalEntities}");
if (batchResult.FailedMigrations > 0)
{
    Console.WriteLine("Failed migrations:");
    foreach (var error in batchResult.Errors)
    {
        Console.WriteLine($"  - {error}");
    }
}
```

### ✅ Compliance Validation

#### Pre-Migration Compliance Checks
```csharp
// Validate compliance before migration
var complianceResult = migrator.ValidateMigrationCompliance("US", "EU");

if (!complianceResult.IsCompliant)
{
    Console.WriteLine("❌ Migration blocked by compliance issues:");
    foreach (var issue in complianceResult.ComplianceIssues)
    {
        Console.WriteLine($"  - {issue}");
    }
    return; // Don't proceed with migration
}

if (complianceResult.ComplianceWarnings.Any())
{
    Console.WriteLine("⚠️ Migration warnings:");
    foreach (var warning in complianceResult.ComplianceWarnings)
    {
        Console.WriteLine($"  - {warning}");
    }
}
```

#### GDPR Right to Data Portability
```csharp
public async Task<byte[]> ExportCustomerDataForPortabilityAsync(DbObjectId customerId)
{
    // 1. Validate customer consent for data export
    var customer = await Customers.Where(c => c.Id == customerId).FirstOrDefaultAsync();
    if (customer?.ConsentStatus != "granted")
    {
        throw new InvalidOperationException("Customer has not granted consent for data export");
    }

    // 2. Gather all related data across collections
    var customerData = await Customers.Where(c => c.Id == customerId).FirstOrDefaultAsync();
    var orders = await Orders.Where(o => o.CustomerId == customerId).ToListAsync();
    var addresses = await Addresses.Where(a => a.CustomerId == customerId).ToListAsync();

    // 3. Create portable data package
    var exportData = new
    {
        Customer = customerData,
        Orders = orders,
        Addresses = addresses,
        ExportDate = DateTime.UtcNow,
        LegalBasis = "GDPR Article 20 - Right to Data Portability",
        DataRetentionNotice = "This export contains personal data valid as of export date"
    };

    // 4. Return as JSON for portability
    return System.Text.Encoding.UTF8.GetBytes(
        System.Text.Json.JsonSerializer.Serialize(exportData, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
}
```

#### GDPR Right to be Forgotten (Erasure)
```csharp
public async Task<bool> EraseCustomerDataAsync(DbObjectId customerId, string legalBasis)
{
    try
    {
        // 1. Validate legal basis for erasure
        if (!IsValidErasureBasis(legalBasis))
        {
            throw new InvalidOperationException($"Invalid legal basis for erasure: {legalBasis}");
        }

        // 2. Identify all data related to customer across collections
        var collections = new[]
        {
            (Collection: Customers, Filter: (Expression<Func<Customer, bool>>)(c => c.Id == customerId)),
            (Collection: Orders, Filter: (Expression<Func<Order, bool>>)(o => o.CustomerId == customerId)),
            (Collection: Addresses, Filter: (Expression<Func<Address, bool>>)(a => a.CustomerId == customerId))
        };

        // 3. Perform secure deletion across all collections
        foreach (var (collection, filter) in collections)
        {
            await collection.Where(filter).DeleteAsync();
        }

        // 4. Log erasure for compliance audit trail
        await AuditLog.InsertAsync(new DataErasureAuditEntry
        {
            CustomerId = customerId,
            ErasureDate = DateTime.UtcNow,
            LegalBasis = legalBasis,
            ErasedCollections = collections.Select(c => c.Collection.GetType().Name).ToList(),
            RequestedBy = "system", // or actual user ID
            ComplianceFramework = "GDPR Article 17"
        });

        return true;
    }
    catch (Exception ex)
    {
        // Log failure for compliance audit trail
        await AuditLog.InsertAsync(new DataErasureFailureEntry
        {
            CustomerId = customerId,
            FailureDate = DateTime.UtcNow,
            ErrorMessage = ex.Message,
            LegalBasis = legalBasis
        });

        throw;
    }
}

private bool IsValidErasureBasis(string legalBasis)
{
    var validBases = new[]
    {
        "GDPR Article 17(1)(a) - Consent withdrawn",
        "GDPR Article 17(1)(b) - No longer necessary",
        "GDPR Article 17(1)(c) - Unlawful processing",
        "GDPR Article 17(1)(d) - Objection to processing",
        "GDPR Article 17(1)(e) - Legal obligation"
    };

    return validBases.Contains(legalBasis);
}
```

### 📝 Compliance Audit Trail

#### Automatic Audit Logging
```csharp
[DbCollection("compliance_audit", EnableSharding = true)]
[DbShardKey("Region", "AuditDate", IncludeRegion = false)] // Region explicit in shard key
[DbIndex("idx_audit_timeline", "Region", "AuditDate", "ComplianceFramework")]
[DbIndex("idx_customer_audit", "CustomerId", "AuditDate")]
[DbIndex("idx_cleanup", "AuditDate", TtlExpirationSeconds = 220752000)] // 7 years retention
public class ComplianceAuditEntry : BaseEntity
{
    [DbField("customer_id")]
    public DbObjectId CustomerId { get; set; }

    [DbField("audit_date")]
    public DateTime AuditDate { get; set; }

    [DbField("action_type")]
    public string ActionType { get; set; } = string.Empty; // "migration", "erasure", "export", "consent_change"

    [DbField("legal_basis")]
    public string LegalBasis { get; set; } = string.Empty;

    [DbField("compliance_framework")]
    public string ComplianceFramework { get; set; } = string.Empty; // "GDPR", "CCPA", "PIPEDA"

    [DbField("source_region")]
    public string? SourceRegion { get; set; }

    [DbField("target_region")]
    public string? TargetRegion { get; set; }

    [DbField("requested_by")]
    public string RequestedBy { get; set; } = string.Empty;

    [DbField("audit_details")]
    public Dictionary<string, object> AuditDetails { get; set; } = new();
}
```

## Version History

- **v2.3.0** - Added comprehensive region-aware sharding, GDPR compliance features, geographic data management, and cross-region migration tools
- **v2.2.0** - Added high-performance UpdateBuilder<T>, enhanced MongoQueryExtensions with advanced LINQ support, and ExpressionToMongoPipeline for sophisticated aggregation operations
- **v2.1.0** - Added MongoDB class mapping and conflict resolution system with MongoPropertyConflictResolver, MongoClassMapConfigurator, and enhanced RestmeDbAttributeConvention
- **v2.0.9** - Enhanced aggregation operations and LINQ expression support
- **v2.0.0** - Initial release with core MongoDB functionality
