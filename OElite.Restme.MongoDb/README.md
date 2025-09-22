# OElite.Restme.MongoDb

A comprehensive MongoDB library for the OElite platform that provides efficient database operations, advanced querying capabilities, and seamless integration with the OElite ecosystem.

## Table of Contents

- [Overview](#overview)
- [Installation](#installation)
- [Core Features](#core-features)
- [Entity Configuration](#entity-configuration)
- [Basic Operations](#basic-operations)
- [Advanced Querying](#advanced-querying)
- [Aggregation Pipelines](#aggregation-pipelines)
- [Denormalization System](#denormalization-system)
- [Performance Optimization](#performance-optimization)
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
<PackageReference Include="OElite.Restme.MongoDb" Version="2.0.9" />
```

## Core Features

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

#### 1. Handle Empty Results Gracefully

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

---

**Version**: 2.0.9  
**Last Updated**: 2024  
**Compatibility**: .NET 9.0+
