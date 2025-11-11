# OElite Denormalization System

This document provides comprehensive documentation for the OElite denormalization system, which enables efficient data population and relationship management in MongoDB collections.

## Overview

The denormalization system consists of two main attributes that allow you to automatically populate related data from other MongoDB collections:

- **`DenormalizedFieldAttribute`** - For populating single field values
- **`DenormalizedCollectionAttribute`** - For populating collections of related objects

Both attributes support advanced query capabilities and flexible field mapping through enhanced reference key syntax.

## Core Attributes

### DenormalizedFieldAttribute

Used to populate a single field with data from another collection.

```csharp
[DenormalizedField(string fromCollection, string fromField, string referenceKey, 
    DbSimpleQuery? query = null, string? collectionReference = null)]
```

**Parameters:**

- `fromCollection` - The source MongoDB collection name
- `fromField` - The field to extract from the source document (`"*"` for entire document)
- `referenceKey` - Enhanced reference key syntax (see below)
- `query` - Optional advanced query configuration
- `collectionReference` - Optional grouping key for multiple references to same collection

### DenormalizedCollectionAttribute

Used to populate a collection property with related objects.

```csharp
[DenormalizedCollection(string fromCollection, string referenceKey = "#Id",
    DbSimpleQuery? query = null, string? collectionReference = null)]
```

**Parameters:**

- `fromCollection` - The source MongoDB collection name
- `referenceKey` - Enhanced reference key syntax (see below)
- `query` - Optional advanced query configuration
- `collectionReference` - Optional grouping key for multiple references to same collection

## Enhanced Reference Key Syntax

The reference key syntax has been enhanced to support flexible field mapping and multiple reference types.

### Basic Syntax

#### Current Class Property References (@)

```csharp
"@PropertyName"                    // Uses property's DbField attribute or snake_case conversion
"@CategoryId"                      // References CategoryId property from current class
"@UserId"                          // References UserId property from current class
```


#### Current Property Class References (#)

```csharp
"#PropertyName"                    // Uses property's DbField attribute or snake_case conversion
"#Id"                              // References Id property from current property's class
"#CategoryId"                      // References CategoryId property from current property's class
```


### Enhanced Syntax with Field Remapping

#### Current Class Property References with Remapping

```csharp
"@PropertyName as targetFieldName" // Maps to different field name in MongoDB
"@CategoryId as category_id"       // Maps CategoryId to category_id field
"@UserId as user_ref"              // Maps UserId to user_ref field
"@ProductId as product_reference"  // Maps ProductId to product_reference field
```


#### Current Property Class References with Remapping

```csharp
"#PropertyName as targetFieldName" // Maps to different field name in MongoDB
"#Id as _id"                       // Maps Id to _id field
"#CategoryId as cat_id"            // Maps CategoryId to cat_id field
"#UserId as user_id"               // Maps UserId to user_id field
```


### Field Name Resolution Priority

When determining the actual field name to use in MongoDB queries, the system follows this priority:


1. **Target Field Name** (if specified with `as targetFieldName`)
2. **DbField Attribute** (if property has `[DbField("field_name")]`)
3. **Snake Case Conversion** (default fallback)


## Usage Examples

### Basic Field Denormalization


```csharp
public class Product
{
    public DbObjectId CategoryId { get; set; }
    
    [DenormalizedField(DbSchema.Categories.Name, "name", "@CategoryId")]
    public string CategoryName { get; set; }
    
    [DenormalizedField(DbSchema.Users.Name, "email", "@UserId")]
    public string UserEmail { get; set; }
}
```

### Field Denormalization with Remapping


```csharp
public class Product
{
    public DbObjectId CategoryId { get; set; }
    public DbObjectId UserId { get; set; }
    
    [DenormalizedField(DbSchema.Categories.Name, "display_name", "@CategoryId as category_id")]
    public string CategoryDisplayName { get; set; }
    
    [DenormalizedField(DbSchema.Users.Name, "email", "@UserId as user_ref")]
    public string UserEmail { get; set; }
}
```

### Collection Denormalization


```csharp
public class Order
{
    public DbObjectId Id { get; set; }
    public DbObjectId ProductId { get; set; }
    
    [DenormalizedCollection(DbSchema.OrderItems.Name, "#Id as order_id")]
    public List<OrderItem> Items { get; set; }
    
    [DenormalizedCollection(DbSchema.Products.Name, "@ProductId as product_ref")]
    public List<Product> Products { get; set; }
}
```

### Advanced Query-Based Denormalization


```csharp
public class User
{
    public DbObjectId Id { get; set; }
    
    [DenormalizedCollection(DbSchema.Posts.Name, "#Id as author_id", 
        new DbSimpleQuery { Query = "{ status: 'published' }", Sort = "{ created_at: -1 }", Limit = 10 })]
    public List<Post> RecentPosts { get; set; }
}
```

### Complex Scenarios with Multiple References


```csharp
public class UserProfile
{
    public DbObjectId Id { get; set; }
    public DbObjectId UserId { get; set; }
    
    // Multiple references to same collection with different grouping
    [DenormalizedField(DbSchema.Users.Name, "display_name", "@UserId as user_id", 
        collectionReference: "user_info")]
    public string DisplayName { get; set; }
    
    [DenormalizedField(DbSchema.Users.Name, "email", "@UserId as user_id", 
        collectionReference: "user_info")]
    public string Email { get; set; }
    
    [DenormalizedCollection(DbSchema.Posts.Name, "#Id as author_id", 
        collectionReference: "user_posts")]
    public List<Post> Posts { get; set; }
}
```

## DbSimpleQuery Configuration

The `DbSimpleQuery` class allows you to specify advanced query parameters:

```csharp
public class DbSimpleQuery
{
    public string? Query { get; set; }      // MongoDB query filter
    public string? Sort { get; set; }       // MongoDB sort specification
    public int Limit { get; set; } = 10;    // Maximum number of records
}
```

### Query Examples

```csharp
// Simple filter
new DbSimpleQuery { Query = "{ status: 'active' }" }
```


```csharp
// Complex filter with sorting
new DbSimpleQuery 
{ 
    Query = "{ status: 'published', category_id: '@CategoryId' }",
    Sort = "{ created_at: -1 }",
    Limit = 5
}
```


```csharp
// Parameter substitution in queries
new DbSimpleQuery 
{ 
    Query = "{ user_id: '@UserId', status: 'active' }",
    Sort = "{ priority: -1, created_at: -1 }"
}
```

```

## Parameter Substitution

The system supports parameter substitution in queries using the `@` syntax:

- `@PropertyName` - Substitutes the value of the specified property
- `@PropertyName as fieldName` - Substitutes the value and maps to the specified field name

### Parameter Substitution Examples

```csharp
// In the query string
"{ user_id: '@UserId', status: 'active' }"

// Gets converted to (assuming UserId = "507f1f77bcf86cd799439011")
"{ user_id: ObjectId('507f1f77bcf86cd799439011'), status: 'active' }"
```

## Best Practices

### 1. Use Descriptive Field Names
```csharp
// Good
"@CategoryId as category_id"
"@UserId as user_ref"

// Avoid
"@CategoryId as cat"
"@UserId as u"
```

### 2. Group Related References
```csharp
// When multiple properties reference the same collection
[DenormalizedField(DbSchema.Users.Name, "name", "@UserId", collectionReference: "user_info")]
[DenormalizedField(DbSchema.Users.Name, "email", "@UserId", collectionReference: "user_info")]
```

### 3. Use Appropriate Field Types
```csharp
// For single values
[DenormalizedField(DbSchema.Categories.Name, "name", "@CategoryId")]
public string CategoryName { get; set; }

// For entire documents
[DenormalizedField(DbSchema.Categories.Name, "*", "@CategoryId")]
public Category Category { get; set; }

// For collections
[DenormalizedCollection(DbSchema.Products.Name, "@CategoryId as category_id")]
public List<Product> Products { get; set; }
```

### 4. Handle Null Values
```csharp
public class Product
{
    public DbObjectId? CategoryId { get; set; }  // Nullable for optional relationships
    
    [DenormalizedField(DbSchema.Categories.Name, "name", "@CategoryId")]
    public string? CategoryName { get; set; }    // Nullable to handle missing data
}
```

## Performance Considerations

### 1. Limit Collection Results
```csharp
[DenormalizedCollection(DbSchema.Posts.Name, "#Id as author_id", 
    new DbSimpleQuery { Limit = 10 })]
public List<Post> RecentPosts { get; set; }
```

### 2. Use Efficient Queries
```csharp
// Good - indexed field
[DenormalizedField(DbSchema.Users.Name, "email", "@UserId as user_id")]

// Avoid - non-indexed field
[DenormalizedField(DbSchema.Users.Name, "email", "@UserId as user_id", 
    new DbSimpleQuery { Query = "{ status: 'active' }" })]  // Additional filter on non-indexed field
```

### 3. Cache Frequently Accessed Data
Consider implementing caching strategies for frequently accessed denormalized data to improve performance.

## Error Handling

The system provides comprehensive error handling and logging:

- **Missing Properties**: Logs warnings when referenced properties don't exist
- **Invalid Queries**: Logs warnings for malformed MongoDB queries
- **Missing Data**: Sets default values when no matching documents are found
- **Type Mismatches**: Validates property types for field denormalization

## Migration and Backward Compatibility

The enhanced syntax is fully backward compatible:

- Existing `@PropertyName` and `#PropertyName` syntax continues to work
- Legacy property name references (without `@` or `#`) are supported
- Gradual migration to enhanced syntax is possible

## Troubleshooting

### Common Issues

1. **Property Not Found**
   ```
   Warning: Referenced property 'CategoryId' not found in 'Product'
   ```
   **Solution**: Ensure the property name is correct and the property exists in the class.

2. **Invalid Query Syntax**
   ```
   Warning: Invalid sort specification: { created_at: -1 }. Skipping sorting.
   ```
   **Solution**: Ensure MongoDB query syntax is correct and properly formatted.

3. **Type Mismatch**
   ```
   Error: Property 'CategoryName' must be a class type when FromField is '*' (entire document)
   ```
   **Solution**: Change the property type to match the expected data type or use a specific field name.

### Debugging Tips

1. **Enable Debug Logging**: Set log level to Debug to see detailed denormalization information
2. **Check Field Names**: Verify that field names in MongoDB match your reference key mappings
3. **Validate Queries**: Test MongoDB queries directly to ensure they work as expected
4. **Monitor Performance**: Use MongoDB profiling to identify slow queries

## Related Classes

- **`DbSimpleQuery`** - Query configuration for advanced denormalization
- **`QueryParameterSubstitutionHelper`** - Parameter substitution in queries
- **`DataPopulationService`** - Service that handles denormalization logic
- **`CascadeUpdateService`** - Service that handles cascade updates for denormalized data
- **`PropertyMappingUtils`** - Utility methods for property mapping and naming convention conversions
- **`AttributeResolver`** - Generic attribute resolver for handling property conflicts and attribute-based logic

## Property Mapping Utilities

The `PropertyMappingUtils` class provides generic utility methods for property mapping and naming convention conversions that are used throughout the OElite platform.

### Core Methods

#### GetNamingConventionFromClass
```csharp
public static DbNamingConvention GetNamingConventionFromClass(Type? type)
```
Gets the naming convention from the `DbCollectionAttribute` of the class, looking up the inheritance hierarchy to find a class with the attribute. Defaults to `SnakeCase` if not found.

#### ConvertToNamingConvention
```csharp
public static string ConvertToNamingConvention(string input, DbNamingConvention convention)
```
Converts a string to the specified naming convention (SnakeCase, CamelCase, or PascalCase).

#### AreTypesCompatible
```csharp
public static bool AreTypesCompatible(Type sourceType, Type targetType)
```
Checks if two types are compatible for property mapping, handling nullable types, inheritance, and convertible types.

### Usage Examples

```csharp
// Get naming convention from a class
var convention = PropertyMappingUtils.GetNamingConventionFromClass(typeof(Product));
// Returns DbNamingConvention.SnakeCase if Product has [DbCollection(..., DbNamingConvention.SnakeCase)]

// Convert property name to snake_case
var fieldName = PropertyMappingUtils.ConvertToNamingConvention("ProductName", DbNamingConvention.SnakeCase);
// Returns "product_name"

// Check type compatibility
var isCompatible = PropertyMappingUtils.AreTypesCompatible(typeof(string), typeof(string?));
// Returns true
```

## Attribute Resolution Utilities

The `AttributeResolver` class provides generic attribute resolution logic for handling property conflicts and attribute-based operations.

### Core Methods

#### GetExcludedProperties
```csharp
public static IEnumerable<string> GetExcludedProperties(Type type)
```
Gets all properties that should be excluded from serialization based on attributes like `[DenormalizedAttribute]` and `[DbFieldIgnore]`.

#### GetPropertyConflicts
```csharp
public static IEnumerable<PropertyConflict> GetPropertyConflicts(Type type)
```
Gets properties that have conflicts with base class properties (using the 'new' keyword).

#### GetPropertiesWithAttribute
```csharp
public static IEnumerable<PropertyInfo> GetPropertiesWithAttribute<TAttribute>(Type type) where TAttribute : Attribute
```
Gets all properties with specific attribute types.

#### GetInheritanceChain
```csharp
public static IEnumerable<Type> GetInheritanceChain(Type type)
```
Gets the inheritance chain for a type (excluding object).

### Usage Examples

```csharp
// Get excluded properties
var excludedProps = AttributeResolver.GetExcludedProperties(typeof(Product));
// Returns properties marked with [DenormalizedAttribute] or [DbFieldIgnore]

// Get property conflicts
var conflicts = AttributeResolver.GetPropertyConflicts(typeof(DerivedProduct));
// Returns properties that hide base class properties with 'new' keyword

// Get properties with specific attributes
var denormalizedProps = AttributeResolver.GetPropertiesWithAttribute<DenormalizedAttribute>(typeof(Product));

// Get inheritance chain
var inheritanceChain = AttributeResolver.GetInheritanceChain(typeof(DerivedProduct));
// Returns [BaseProduct, BaseEntity] (most derived first)
```

### PropertyConflict Class

```csharp
public class PropertyConflict
{
    public string PropertyName { get; set; } = string.Empty;
    public PropertyInfo DerivedProperty { get; set; } = null!;
    public PropertyInfo BaseProperty { get; set; } = null!;
    public PropertyConflictType ConflictType { get; set; }
}

public enum PropertyConflictType
{
    HiddenBaseProperty,
    DuplicateFieldName,
    TypeMismatch
}
```

## MongoDB Serialization Issues and Resolutions

### Issue: Denormalized Properties Not Being Saved to Database

#### Problem Description

When using denormalized properties in entities, you may encounter situations where:

1. **Denormalized properties are correctly populated** by `DataPopulationService` during runtime
2. **The target entity contains all expected values** after transformation
3. **But the denormalized properties are missing** when the entity is saved to the MongoDB database

#### Root Cause

This issue occurs due to MongoDB serialization configuration in the `RestmeDbAttributeConvention`. The convention was previously configured to exclude **ALL** denormalized properties from MongoDB serialization, regardless of whether they should be persisted to the database.

**The problematic code was:**
```csharp
// Skip denormalized properties - they are populated by DataPopulationService, not MongoDB serialization
if (denormalizedAttr != null)
{
    classMap.UnmapProperty(property.Name);
    continue;
}
```

This approach was too aggressive and excluded denormalized properties that should be saved to the database.

#### Solution

The fix involves two key changes:

1. **Modified `RestmeDbAttributeConvention`** to only exclude denormalized properties that are explicitly marked with `[DbFieldIgnore]`:

```csharp
// Skip denormalized properties that are marked with [DbFieldIgnore] - they are populated by DataPopulationService, not MongoDB serialization
if (denormalizedAttr != null && ignoreAttr != null)
{
    classMap.UnmapProperty(property.Name);
    continue;
}
```

2. **Added `[DbFieldIgnore]` attributes** to denormalized properties in **source entities** (legacy entities) that should not be persisted to the database:

```csharp
public class LegacyMerchantAccount : LegacyBaseEntity
{
    [DbField("defaultCurrencyId")] 
    public long DefaultCurrencyLegacyId { get; set; }

    [DenormalizedField(DbSchema.Legacy.Currencies.Name, DbSchema.ObjectId, referenceKey: "@DefaultCurrencyLegacyId as id")]
    [DbFieldIgnore]  // ← This prevents MongoDB serialization conflicts
    public DbObjectId? DefaultCurrencyId { get; set; }

    [DenormalizedField(DbSchema.Currencies.Name, referenceKey: "@DefaultCurrencyId as _id")]
    [DbFieldIgnore]  // ← This prevents MongoDB serialization conflicts
    public Currency? DefaultCurrency { get; set; }
}
```

#### When to Use `[DbFieldIgnore]`

Use `[DbFieldIgnore]` on denormalized properties in the following scenarios:

1. **Source/Legacy Entities**: Denormalized properties that are populated by `DataPopulationService` but should not be persisted to the database
2. **Computed Properties**: Properties that are calculated at runtime and should not be stored
3. **Temporary Properties**: Properties used for migration or transformation purposes only

#### When NOT to Use `[DbFieldIgnore]`

Do **NOT** use `[DbFieldIgnore]` on denormalized properties in:

1. **Target Entities**: Properties that should be persisted to the database after migration
2. **Business Entities**: Properties that represent actual business data that needs to be stored
3. **Queryable Properties**: Properties that need to be indexed or queried in MongoDB

#### Example: Migration Scenario

**Source Entity (LegacyMerchantAccount):**
```csharp
public class LegacyMerchantAccount : LegacyBaseEntity
{
    [DbField("defaultCurrencyId")] 
    public long DefaultCurrencyLegacyId { get; set; }

    // These denormalized properties are populated by DataPopulationService
    // but should NOT be saved to the legacy database
    [DenormalizedField(DbSchema.Legacy.Currencies.Name, DbSchema.ObjectId, referenceKey: "@DefaultCurrencyLegacyId as id")]
    [DbFieldIgnore]  // ← Exclude from MongoDB serialization
    public DbObjectId? DefaultCurrencyId { get; set; }

    [DenormalizedField(DbSchema.Currencies.Name, referenceKey: "@DefaultCurrencyId as _id")]
    [DbFieldIgnore]  // ← Exclude from MongoDB serialization
    public Currency? DefaultCurrency { get; set; }
}
```

**Target Entity (Merchant):**
```csharp
public class Merchant : BaseEntity
{
    public DbObjectId? DefaultCurrencyId { get; set; }

    // These denormalized properties SHOULD be saved to the database
    // No [DbFieldIgnore] attribute needed
    [DenormalizedField(DbSchema.Currencies.Name, referenceKey: "@DefaultCurrencyId as _id")]
    public Currency? DefaultCurrency { get; set; }
}
```

#### Common Error Messages

If you encounter this issue, you may see error messages like:

```
MongoDB.Bson.BsonSerializationException: The property 'DefaultCurrencyId' of type 'LegacyMerchantAccount' 
cannot use element name 'defaultCurrencyId' because it is already being used by property 'DefaultCurrencyLegacyId'.
```

This indicates that two properties are trying to use the same MongoDB field name, which is resolved by adding `[DbFieldIgnore]` to the denormalized property.

#### Best Practices

1. **Always add `[DbFieldIgnore]`** to denormalized properties in source/legacy entities
2. **Never add `[DbFieldIgnore]`** to denormalized properties in target entities that should be persisted
3. **Test thoroughly** after making changes to ensure properties are correctly saved to the database
4. **Use specific entity IDs** for testing to verify the fix works correctly

#### Testing the Fix

To verify that denormalized properties are correctly saved:

1. Run a migration with a specific entity ID
2. Check the database to ensure all expected properties are present
3. Verify that the migration completes without MongoDB serialization errors

Example test command:
```bash
dotnet run sync --s MerchantMigration --entity-id 68e15c45eccc15c6b35f84e1
```

Expected output should show successful migration without serialization conflicts.

## Version History

- **v2.2** - Fixed MongoDB serialization issue with denormalized properties; added comprehensive documentation for serialization conflicts and resolutions
- **v2.1** - Added PropertyMappingUtils and AttributeResolver for generic property mapping and attribute resolution
- **v2.0** - Enhanced reference key syntax with field remapping support
- **v1.0** - Initial implementation with basic `@` and `#` syntax
