# OElite.Restme.Utils

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen.svg)](https://github.com/oelite)
[![NuGet](https://img.shields.io/badge/nuget-v2.1.0-blue.svg)](https://www.nuget.org/packages/OElite.Restme.Utils/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

Core utilities and data access abstractions for the OElite platform, providing enhanced BaseEntity classes, database attributes, and region-aware data management capabilities.

## 🚀 Key Features

### **Enhanced BaseEntity with Region Awareness**
- **Geographic Data Placement**: Automatic region support for GDPR compliance
- **Multi-Tenant Support**: Built-in owner tracking for merchant and contact isolation
- **Audit Fields**: Comprehensive creation and modification tracking
- **Type Safety**: Strong typing with DbObjectId and datetime handling

### **Comprehensive Database Attributes**
- **Declarative Schema**: Define database configuration using C# attributes
- **Field Mapping**: Flexible property-to-field mapping with naming conventions
- **Index Configuration**: Performance-optimized index definitions
- **Sharding Support**: Region-aware shard key configuration
- **Validation**: Built-in configuration validation and error checking

### **Data Access Abstractions**
- **Repository Pattern**: Clean abstraction over data access operations
- **Query Builders**: Type-safe query construction and execution
- **Connection Management**: Efficient database connection handling
- **Performance Optimization**: Built-in caching and query optimization

## Table of Contents

- [Installation](#installation)
- [BaseEntity Enhancements](#baseentity-enhancements)
- [Database Attributes](#database-attributes)
- [Data Access Patterns](#data-access-patterns)
- [Region-Aware Data Management](#region-aware-data-management)
- [Configuration Examples](#configuration-examples)
- [Best Practices](#best-practices)
- [API Reference](#api-reference)

## Installation

Add the package reference to your project:

```xml
<PackageReference Include="OElite.Restme.Utils" Version="2.1.0" />
```

## BaseEntity Enhancements

### Enhanced BaseEntity with Region Support

All entities automatically inherit comprehensive base functionality:

```csharp
public class Product : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DbObjectId CategoryId { get; set; }

    // Inherited from BaseEntity:
    // - Id (DbObjectId) - Primary key
    // - CreatedOnUtc (DateTime) - Creation timestamp
    // - UpdatedOnUtc (DateTime) - Last modification timestamp
    // - IsActive (bool) - Soft deletion support
    // - OwnerMerchantId (DbObjectId?) - Multi-tenant support
    // - OwnerContactId (DbObjectId?) - User-specific data
    // - Region (string?) - Geographic data placement for GDPR compliance
}
```

### Automatic Index Generation

BaseEntity properties automatically receive optimized indexes:

```csharp
// Automatically generated indexes:
// - idx_auto_created: CreatedOnUtc (ascending)
// - idx_auto_updated: UpdatedOnUtc (ascending)
// - idx_auto_active: IsActive (ascending)
// - idx_auto_owner_merchant: OwnerMerchantId (sparse)
// - idx_auto_owner_contact: OwnerContactId (sparse)
// - idx_auto_region: Region (sparse) - for GDPR compliance
```

## Database Attributes

### Core Attributes Overview

| Attribute | Purpose | Example |
|-----------|---------|---------|
| `[DbCollection]` | Collection configuration | `[DbCollection("products", EnableSharding = true)]` |
| `[DbShardKey]` | Shard key definition | `[DbShardKey("CategoryId", IncludeRegion = true)]` |
| `[DbIndex]` | Index configuration | `[DbIndex("idx_name_price", "Name", "Price")]` |
| `[DbField]` | Field mapping | `[DbField("product_name")]` |
| `[DbId]` | Primary key marking | `[DbId]` |
| `[DbFieldIgnore]` | Exclude from storage | `[DbFieldIgnore]` |

### Collection Configuration

```csharp
[DbCollection("high_volume_products",
    EnableSharding = true,
    EnablePreSplitting = true,
    PreSplitChunks = 256,
    ValidateSchema = true,
    TtlExpirationSeconds = 7776000, // 90 days
    BootstrapPriority = 1)]
public class Product : BaseEntity
{
    // Entity properties...
}
```

### Sharding Configuration

```csharp
// Simple shard key
[DbShardKey("ProductId")]

// Compound shard key
[DbShardKey("CategoryId", "ProductId")]

// Region-aware shard key for GDPR compliance
[DbShardKey("UserId", IncludeRegion = true, RegionStrategy = RegionShardingStrategy.RegionFirst)]

// Hashed shard key for even distribution
[DbShardKey("EventId", IsHashed = new[] { true })]
```

### Index Configuration

```csharp
// Simple index
[DbIndex("idx_name", "Name")]

// Compound index with sorting
[DbIndex("idx_category_price", "CategoryId", "Price", Directions = new[] { 1, -1 })]

// Unique constraint
[DbIndex("idx_unique_sku", "Sku", IsUnique = true)]

// Text search index
[DbIndex("idx_search", "Name", "Description", IsTextIndex = true)]

// TTL index for automatic cleanup
[DbIndex("idx_expires", "ExpiresAt", TtlExpirationSeconds = 0)]

// Background creation for large collections
[DbIndex("idx_heavy", "LargeField", CreateInBackground = true)]
```

## Data Access Patterns

### Repository Pattern Implementation

```csharp
public class ProductRepository : DataRepository
{
    public MongoQuery<Product> Products => new(_adapter.GetCollection<Product>());

    public async Task<Product?> GetProductByIdAsync(DbObjectId productId)
    {
        return await Products
            .Where(p => p.Id == productId)
            .FirstOrDefaultAsync();
    }

    public async Task<List<Product>> GetActiveProductsAsync(string region = null)
    {
        var query = Products.Where(p => p.IsActive == true);

        if (!string.IsNullOrEmpty(region))
        {
            query = query.Where(p => p.Region == region);
        }

        return await query
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<ProductCollection> GetProductsByCategoryAsync(DbObjectId categoryId, int pageIndex, int pageSize)
    {
        return await Products
            .Where(p => p.CategoryId == categoryId)
            .Where(p => p.IsActive == true)
            .OrderBy(p => p.Name)
            .FetchAsync<Product, ProductCollection>(pageIndex, pageSize);
    }
}
```

### Query Building

```csharp
// Type-safe query construction
var expensiveProducts = await Products
    .Where(p => p.Price > 100)
    .Where(p => p.IsActive == true)
    .OrderByDescending(p => p.Price)
    .Take(10)
    .ToListAsync();

// Region-aware queries for GDPR compliance
var euCustomers = await Customers
    .Where(c => c.Region == "EU")
    .Where(c => c.IsActive == true)
    .ToListAsync();

// Aggregation operations
var categoryStats = await Products
    .Where(p => p.IsActive == true)
    .GroupByAsync(p => p.CategoryId.ToString());
```

## Region-Aware Data Management

### GDPR-Compliant Entity Design

```csharp
[DbCollection("customer_data", EnableSharding = true)]
[DbShardKey("CustomerId", IncludeRegion = true, RegionStrategy = RegionShardingStrategy.RegionFirst)]
[DbIndex("idx_customer_region", "CustomerId", "Region", IsUnique = true)]
[DbIndex("idx_gdpr_compliance", "Region", "ConsentStatus", "ConsentDate")]
public class CustomerData : BaseEntity
{
    public DbObjectId CustomerId { get; set; }
    public string PersonalData { get; set; } = string.Empty;
    public DateTime ConsentDate { get; set; }
    public string ConsentStatus { get; set; } = string.Empty;

    // Region inherited from BaseEntity ensures GDPR compliance
    // Effective shard key: { region: 1, customer_id: 1 }
}
```

### Regional Sharding Strategies

```csharp
public enum RegionShardingStrategy
{
    RegionFirst,  // { region: 1, ...fields } - Best for GDPR compliance
    RegionLast,   // { ...fields, region: 1 } - Best for performance
    RegionMiddle  // { field1: 1, region: 1, field2: 1 } - Balanced approach
}
```

### Region Assignment

```csharp
public static class RegionHelper
{
    public static string DetermineRegionFromLocation(string userLocation)
    {
        return userLocation switch
        {
            var loc when IsEuCountry(loc) => "EU",
            var loc when IsUkTerritory(loc) => "UK",
            var loc when IsUsTerritory(loc) => "US",
            var loc when IsCanadianTerritory(loc) => "CA",
            var loc when IsChineseTerritory(loc) => "CN",
            _ => "US" // Default fallback
        };
    }

    private static bool IsEuCountry(string country) =>
        new[] { "DE", "FR", "IT", "ES", "NL", "BE", "AT", "SE", "DK", "FI" }.Contains(country.ToUpper());
}
```

## Configuration Examples

### E-commerce Product Entity

```csharp
[DbCollection("products", EnableSharding = true, ValidateSchema = true)]
[DbShardKey("CategoryId", "ProductId", IncludeRegion = true, RegionStrategy = RegionShardingStrategy.RegionMiddle)]
[DbIndex("idx_category_price", "CategoryId", "Price", Directions = new[] { 1, -1 })]
[DbIndex("idx_brand_products", "BrandId", "IsActive")]
[DbIndex("idx_search_products", "Name", "Description", IsTextIndex = true)]
[DbIndex("idx_featured", "IsFeatured", "DisplayOrder")]
[DbIndex("idx_inventory", "Sku", IsUnique = true)]
[DbIndex("idx_region_compliance", "Region", "DataClassification")]
public class Product : BaseEntity
{
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

    [DbField("is_featured")]
    public bool IsFeatured { get; set; } = false;

    [DbField("display_order")]
    public int DisplayOrder { get; set; } = 0;

    [DbField("data_classification")]
    public string DataClassification { get; set; } = "public";

    [DbFieldIgnore]
    public decimal TaxAmount => Price * 0.15m;
}
```

### Multi-Tenant SaaS Entity

```csharp
[DbCollection("tenant_data", EnableSharding = true)]
[DbShardKey("TenantId", IncludeRegion = true, RegionStrategy = RegionShardingStrategy.RegionFirst)]
[DbIndex("idx_tenant_lookup", "TenantId", "EntityType", "EntityId")]
[DbIndex("idx_tenant_search", "TenantId", "SearchableText", IsTextIndex = true)]
[DbIndex("idx_data_retention", "Region", "CreatedOnUtc", TtlExpirationSeconds = 2592000)] // 30 days
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
    public string DataSensitivity { get; set; } = "normal";
}
```

## Best Practices

### 1. Entity Design

```csharp
// GOOD: Clear attribute usage and proper inheritance
[DbCollection("customers", EnableSharding = true)]
[DbShardKey("Email", IncludeRegion = true, RegionStrategy = RegionShardingStrategy.RegionFirst)]
[DbIndex("idx_email_region", "Email", "Region", IsUnique = true)]
public class Customer : BaseEntity
{
    [DbField("email")]
    public string Email { get; set; } = string.Empty;

    [DbField("first_name")]
    public string FirstName { get; set; } = string.Empty;

    // Computed properties should be ignored
    [DbFieldIgnore]
    public string FullName => $"{FirstName} {LastName}";
}
```

### 2. Regional Compliance

```csharp
// GOOD: Explicit region handling
public async Task<Customer> CreateCustomerAsync(string email, string location)
{
    var customer = new Customer
    {
        Email = email,
        Region = RegionHelper.DetermineRegionFromLocation(location),
        CreatedOnUtc = DateTime.UtcNow,
        IsActive = true
    };

    return await _repository.CreateAsync(customer);
}
```

### 3. Index Strategy

```csharp
// GOOD: Strategic index placement with priorities
[DbIndex("idx_primary_lookup", "UserId", "EntityId", Priority = 1)]
[DbIndex("idx_search", "SearchText", IsTextIndex = true, Priority = 2)]
[DbIndex("idx_cleanup", "ExpiresAt", TtlExpirationSeconds = 0, Priority = 3)]
```

### 4. Configuration Validation

```csharp
// Validate entity configurations at startup
public static void ValidateConfigurations()
{
    var entityTypes = Assembly.GetExecutingAssembly()
        .GetTypes()
        .Where(t => t.IsSubclassOf(typeof(BaseEntity)))
        .ToArray();

    var validation = EntityAttributeScanner.ValidateEntityConfigurations(entityTypes);

    if (!validation.IsValid)
    {
        throw new InvalidOperationException(
            $"Configuration validation failed: {string.Join(", ", validation.ValidationErrors)}");
    }
}
```

## API Reference

### Core Attributes

#### DbCollectionAttribute
```csharp
[DbCollection(string collectionName,
    bool EnableSharding = false,
    bool EnablePreSplitting = false,
    int PreSplitChunks = 64,
    bool ValidateSchema = false,
    int TtlExpirationSeconds = 0,
    int BootstrapPriority = 100)]
```

#### DbShardKeyAttribute
```csharp
[DbShardKey(params string[] fields,
    bool[] IsHashed = null,
    int[] Directions = null,
    bool IsUnique = false,
    bool IncludeRegion = false,
    RegionShardingStrategy RegionStrategy = RegionShardingStrategy.RegionFirst)]
```

#### DbIndexAttribute
```csharp
[DbIndex(string name, params string[] fields,
    int[] Directions = null,
    bool IsUnique = false,
    bool IsSparse = false,
    bool CreateInBackground = false,
    int Priority = 100,
    bool IsTextIndex = false,
    bool IsHashedIndex = false,
    int TtlExpirationSeconds = 0)]
```

### Factory Methods

```csharp
// GDPR-compliant shard key
DbShardKeyAttribute.ForGdprCompliance("CustomerId");

// Multi-tenant shard key
DbShardKeyAttribute.ForTenant("TenantId", "EntityId");

// Analytics shard key
DbShardKeyAttribute.ForAnalytics("EntityId", "EventDate");

// Unique index
DbIndexAttribute.Unique("idx_unique_field", "FieldName");

// TTL index
DbIndexAttribute.Ttl("idx_cleanup", "ExpiresAt", 3600);
```

## 📚 Detailed Documentation

For comprehensive guides on specific features, see the detailed documentation:

### Core Features Documentation
- **[Data Attributes & Configuration](docs/DATA-ATTRIBUTES-CONFIGURATION.md)** - Complete guide to using database attributes and entity configuration
- **[Region-Aware Sharding](../OElite.Restme.MongoDb/docs/REGION-AWARE-SHARDING.md)** - Geographic data management and GDPR compliance
- **[BaseEntity Enhancements](docs/DATA-ATTRIBUTES-CONFIGURATION.md#baseentity-enhancements)** - Enhanced base entity functionality
- **[Best Practices](docs/DATA-ATTRIBUTES-CONFIGURATION.md#best-practices)** - Recommended patterns and approaches

### Quick Reference
- **[Attribute Reference](docs/DATA-ATTRIBUTES-CONFIGURATION.md#database-attributes)** - Complete attribute documentation
- **[Configuration Examples](docs/DATA-ATTRIBUTES-CONFIGURATION.md#examples)** - Real-world implementation examples
- **[Sharding Strategies](docs/DATA-ATTRIBUTES-CONFIGURATION.md#sharding-configuration)** - Region-aware sharding patterns

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

---

**Version**: 2.1.0
**Last Updated**: 2024
**Compatibility**: .NET 9.0+