# Attribute Mapping Guide for MongoDB Serialization

This guide explains how to properly use attributes for MongoDB serialization in the OElite platform, particularly for migration scenarios.

## Attribute Overview

### Core Attributes

1. **`[DbField("fieldName")]`** - Maps a C# property to a specific MongoDB field name
2. **`[DenormalizedField]`** - Indicates a property is populated by `DataPopulationService` at runtime
3. **`[DenormalizedCollection]`** - Indicates a collection property is populated by `DataPopulationService` at runtime
4. **`[DbFieldIgnore]`** - Excludes a property from MongoDB serialization entirely

## Migration Scenario: Source vs Target Entities

### Source/Legacy Entities (e.g., `LegacyMerchantAccount`, `LegacyContact`)

**Rule**: Add `[DbFieldIgnore]` to ALL denormalized properties in source entities.

**Why**: Source entities are used for data transformation, not direct MongoDB persistence. Denormalized properties in source entities:
- Are populated by `DataPopulationService` for transformation purposes
- Should NOT be serialized to MongoDB (to avoid conflicts with legacy fields)
- Are intermediate holders for reference resolution

**Example**:
```csharp
public class LegacyMerchantAccount : LegacyBaseEntity
{
    [DbField("defaultCurrencyId")] 
    public long DefaultCurrencyLegacyId { get; set; }

    [DenormalizedField(DbSchema.Legacy.Currencies.Name, DbSchema.ObjectId, referenceKey: "@DefaultCurrencyLegacyId as id")]
    [DbFieldIgnore] // ✅ REQUIRED: Prevents serialization conflict with DefaultCurrencyLegacyId
    public DbObjectId? DefaultCurrencyId { get; set; }

    [DenormalizedField(DbSchema.Currencies.Name, referenceKey: "@DefaultCurrencyId as _id")]
    [DbFieldIgnore] // ✅ REQUIRED: This is a denormalized object, not directly stored
    public Currency? DefaultCurrency { get; set; }
}
```

### Target Entities (e.g., `Merchant`, `Contact`)

**Rule**: Do NOT add `[DbFieldIgnore]` to denormalized properties in target entities.

**Why**: Target entities are intended for persistence in the new unified MongoDB collections. Denormalized properties in target entities:
- Are populated by `DataPopulationService` during migration
- SHOULD be serialized to MongoDB for persistence
- Are part of the final data structure

**Example**:
```csharp
public class Merchant : BaseEntity
{
    [DenormalizedField(DbSchema.Currencies.Name, referenceKey: "@DefaultCurrencyId as _id")]
    // ❌ NO [DbFieldIgnore] - This should be saved to the database
    public Currency? DefaultCurrency { get; set; }
}
```

## Attribute Resolution Hierarchy

The `RestmeDbAttributeConvention` processes attributes in this order:

1. **`[DbFieldIgnore]`** - If present, property is excluded from MongoDB serialization
2. **`[DbId]`** - Maps to `_id` field with custom serialization
3. **`[DbField("name")]`** - Maps to specific MongoDB field name
4. **Naming Convention** - Applies class-level naming convention (camelCase, snake_case, etc.)

## Common Patterns

### Pattern 1: Legacy Entity with Denormalized Fields
```csharp
public class LegacyEntity : LegacyBaseEntity
{
    [DbField("legacyField")] 
    public long LegacyField { get; set; }

    [DenormalizedField(collection, field, "@LegacyField as id")]
    [DbFieldIgnore] // ✅ Always add this to source entities
    public DbObjectId? DenormalizedId { get; set; }

    [DenormalizedField(collection, referenceKey: "@DenormalizedId as _id")]
    [DbFieldIgnore] // ✅ Always add this to source entities
    public SomeEntity? DenormalizedEntity { get; set; }
}
```

### Pattern 2: Target Entity with Denormalized Fields
```csharp
public class TargetEntity : BaseEntity
{
    [DenormalizedField(collection, referenceKey: "@SomeId as _id")]
    // ❌ NO [DbFieldIgnore] - This should be persisted
    public SomeEntity? DenormalizedEntity { get; set; }
}
```

### Pattern 3: Mixed Properties
```csharp
public class MixedEntity : BaseEntity
{
    // Direct property - no special attributes needed
    public string Name { get; set; } = string.Empty;

    // Denormalized property for persistence
    [DenormalizedField(collection, referenceKey: "@SomeId as _id")]
    public SomeEntity? DenormalizedEntity { get; set; }

    // Computed property - should not be serialized
    [DbFieldIgnore]
    public string ComputedProperty => $"{Name}-computed";
}
```

## Troubleshooting

### Issue: "MongoDB.Bson.BsonSerializationException: The property 'X' cannot use element name 'Y' because it is already being used"

**Cause**: Two properties are trying to map to the same MongoDB field name.

**Solution**: Add `[DbFieldIgnore]` to the denormalized property in the source entity.

### Issue: Denormalized properties not being saved to database

**Cause**: The denormalized property has `[DbFieldIgnore]` in a target entity.

**Solution**: Remove `[DbFieldIgnore]` from denormalized properties in target entities.

### Issue: Properties not populating during migration

**Cause**: Missing `[DenormalizedField]` or `[DenormalizedCollection]` attributes.

**Solution**: Add the appropriate denormalized attribute with correct reference keys.

## Best Practices

1. **Source Entities**: Always add `[DbFieldIgnore]` to denormalized properties
2. **Target Entities**: Never add `[DbFieldIgnore]` to denormalized properties intended for persistence
3. **Computed Properties**: Always add `[DbFieldIgnore]` to computed/calculated properties
4. **Legacy Fields**: Use `[DbField("legacyName")]` to map legacy field names
5. **ID Fields**: Use `[DbId]` for primary key fields that should map to `_id`

## Migration Checklist

When creating or updating migration strategies:

- [ ] Source entity denormalized properties have `[DbFieldIgnore]`
- [ ] Target entity denormalized properties do NOT have `[DbFieldIgnore]`
- [ ] Legacy field mappings use `[DbField("legacyName")]`
- [ ] Reference keys in `[DenormalizedField]` are correct
- [ ] No conflicting field name mappings
- [ ] Test migration with specific entity IDs
- [ ] Verify denormalized properties are populated and saved
