using System.Reflection;
using OElite.Common;

namespace OElite.Restme.MongoDb.Management;

/// <summary>
/// Scans entity classes for database management attributes and converts them to configuration objects
/// Provides automatic discovery of sharding and indexing configuration from entity attributes
/// </summary>
public static class EntityAttributeScanner
{
    /// <summary>
    /// Scan entity types and build bootstrap configuration from attributes
    /// </summary>
    /// <param name="entityTypes">Entity types to scan</param>
    /// <param name="globalOptions">Global bootstrap options</param>
    /// <returns>Complete bootstrap configuration</returns>
    public static DbBootstrapConfiguration ScanEntitiesForBootstrapConfiguration(
        Type[] entityTypes,
        DbBootstrapOptions? globalOptions = null)
    {
        globalOptions ??= new DbBootstrapOptions();

        var configuration = new DbBootstrapConfiguration
        {
            EnableSharding = globalOptions.EnableSharding,
            EnablePreSplitting = globalOptions.EnablePreSplitting,
            PreSplitCount = globalOptions.PreSplitCount,
            CreateIndexesInBackground = globalOptions.CreateIndexesInBackground,
            MaxRetryAttempts = globalOptions.MaxRetryAttempts,
            TimeoutSeconds = globalOptions.TimeoutSeconds,
            Collections = new List<DbCollectionConfiguration>()
        };

        // Process each entity type
        foreach (var entityType in entityTypes)
        {
            var collectionConfig = ScanEntityForCollectionConfiguration(entityType, globalOptions);
            if (collectionConfig != null)
            {
                configuration.Collections.Add(collectionConfig);
            }
        }

        // Sort collections by bootstrap priority
        configuration.Collections = configuration.Collections
            .OrderBy(c => c.BootstrapPriority)
            .ToList();

        return configuration;
    }

    /// <summary>
    /// Scan a single entity type for collection configuration
    /// </summary>
    /// <param name="entityType">Entity type to scan</param>
    /// <param name="globalOptions">Global options</param>
    /// <returns>Collection configuration or null if not applicable</returns>
    public static DbCollectionConfiguration? ScanEntityForCollectionConfiguration(
        Type entityType,
        DbBootstrapOptions? globalOptions = null)
    {
        if (!entityType.IsSubclassOf(typeof(BaseEntity)))
        {
            return null; // Only process BaseEntity-derived types
        }

        var collectionAttr = entityType.GetCustomAttribute<DbCollectionAttribute>();
        if (collectionAttr == null)
        {
            // Create default configuration
            collectionAttr = new DbCollectionAttribute(entityType.Name.ToLowerInvariant());
        }

        var collectionName = collectionAttr.GetCollectionName(entityType.Name);
        var config = new DbCollectionConfiguration
        {
            CollectionName = collectionName,
            ValidateSchema = collectionAttr.ValidateSchema,
            BootstrapPriority = collectionAttr.BootstrapPriority
        };

        // Set TTL if specified
        if (collectionAttr.TtlExpirationSeconds > 0)
        {
            config.TtlExpiration = TimeSpan.FromSeconds(collectionAttr.TtlExpirationSeconds);
        }

        // Scan for shard key configuration
        var shardKeyConfig = ScanEntityForShardKey(entityType, collectionAttr, globalOptions);
        if (shardKeyConfig != null)
        {
            config.ShardKey = shardKeyConfig;
        }

        // Scan for index configurations
        var indexConfigs = ScanEntityForIndexes(entityType, globalOptions);
        config.Indexes = indexConfigs;

        return config;
    }

    /// <summary>
    /// Scan entity for shard key configuration
    /// </summary>
    /// <param name="entityType">Entity type</param>
    /// <param name="collectionAttr">Collection attribute</param>
    /// <param name="globalOptions">Global options</param>
    /// <returns>Shard key configuration or null</returns>
    public static DbShardKey? ScanEntityForShardKey(
        Type entityType,
        DbCollectionAttribute collectionAttr,
        DbBootstrapOptions? globalOptions = null)
    {
        var shardKeyAttr = entityType.GetCustomAttribute<DbShardKeyAttribute>();

        // Check if sharding should be enabled
        var enableSharding = collectionAttr.EnableSharding ||
                           (globalOptions?.EnableSharding == true && shardKeyAttr != null);

        if (!enableSharding || shardKeyAttr == null)
        {
            return null;
        }

        if (!shardKeyAttr.IsValid())
        {
            throw new InvalidOperationException(
                $"Invalid shard key configuration on entity {entityType.Name}");
        }

        var shardKey = new DbShardKey
        {
            Fields = new List<DbShardKeyField>(),
            IsUnique = shardKeyAttr.IsUnique
        };

        // Check if region-aware sharding is enabled
        if (shardKeyAttr.IncludeRegion)
        {
            // Use region-aware shard key configuration
            var allFields = shardKeyAttr.GetShardKeyFieldsWithRegion();
            var allHashingConfig = shardKeyAttr.GetHashingConfigurationWithRegion();
            var allDirections = shardKeyAttr.GetDirectionsWithRegion();

            for (int i = 0; i < allFields.Length; i++)
            {
                var fieldName = allFields[i];
                var isHashed = i < allHashingConfig.Length ? allHashingConfig[i] : false;
                var direction = i < allDirections.Length ? allDirections[i] : 1;

                // Convert field name using property mapping (handles both region and business fields)
                var mappedFieldName = MapPropertyNameToDbField(entityType, fieldName);

                shardKey.Fields.Add(new DbShardKeyField
                {
                    FieldName = mappedFieldName,
                    Direction = direction == 1 ? DbSortDirection.Ascending : DbSortDirection.Descending,
                    IsHashed = isHashed
                });
            }
        }
        else
        {
            // Use standard shard key configuration (existing behavior)
            for (int i = 0; i < shardKeyAttr.Fields.Length; i++)
            {
                var (fieldName, isHashed, direction) = shardKeyAttr.GetFieldConfig(i);

                // Convert field name using property mapping
                var mappedFieldName = MapPropertyNameToDbField(entityType, fieldName);

                shardKey.Fields.Add(new DbShardKeyField
                {
                    FieldName = mappedFieldName,
                    Direction = direction == 1 ? DbSortDirection.Ascending : DbSortDirection.Descending,
                    IsHashed = isHashed
                });
            }
        }

        return shardKey;
    }

    /// <summary>
    /// Scan entity for index configurations
    /// </summary>
    /// <param name="entityType">Entity type</param>
    /// <param name="globalOptions">Global options</param>
    /// <returns>List of index configurations</returns>
    public static List<DbIndexDefinition> ScanEntityForIndexes(
        Type entityType,
        DbBootstrapOptions? globalOptions = null)
    {
        var indexes = new List<DbIndexDefinition>();

        // Get all DbIndex attributes
        var indexAttrs = entityType.GetCustomAttributes<DbIndexAttribute>().ToArray();

        foreach (var indexAttr in indexAttrs)
        {
            if (!indexAttr.IsValid())
            {
                throw new InvalidOperationException(
                    $"Invalid index configuration '{indexAttr.Name}' on entity {entityType.Name}");
            }

            var indexDef = new DbIndexDefinition
            {
                Name = indexAttr.Name,
                Fields = new List<DbIndexField>(),
                IsUnique = indexAttr.IsUnique,
                IsSparse = indexAttr.IsSparse,
                CreateInBackground = globalOptions?.CreateIndexesInBackground ?? indexAttr.CreateInBackground
            };

            // Set TTL if specified
            if (indexAttr.TtlExpirationSeconds > 0)
            {
                indexDef.TtlExpiration = TimeSpan.FromSeconds(indexAttr.TtlExpirationSeconds);
            }

            // Build field list
            for (int i = 0; i < indexAttr.Fields.Length; i++)
            {
                var (fieldName, direction) = indexAttr.GetFieldConfig(i);

                // Convert field name using property mapping
                var mappedFieldName = MapPropertyNameToDbField(entityType, fieldName);

                var indexField = new DbIndexField
                {
                    FieldName = mappedFieldName,
                    Direction = direction == 1 ? DbSortDirection.Ascending : DbSortDirection.Descending,
                    IsText = indexAttr.IsTextIndex,
                    IsHashed = indexAttr.IsHashedIndex
                };

                indexDef.Fields.Add(indexField);
            }

            indexes.Add(indexDef);
        }

        // Add automatic indexes for common BaseEntity patterns
        var autoIndexes = GenerateAutomaticIndexes(entityType, globalOptions);
        indexes.AddRange(autoIndexes);

        // Sort by priority
        return indexes
            .OrderBy(idx => GetIndexPriority(idx, indexAttrs))
            .ToList();
    }

    /// <summary>
    /// Generate automatic indexes for common BaseEntity patterns
    /// </summary>
    /// <param name="entityType">Entity type</param>
    /// <param name="globalOptions">Global options</param>
    /// <returns>List of automatic index definitions</returns>
    public static List<DbIndexDefinition> GenerateAutomaticIndexes(
        Type entityType,
        DbBootstrapOptions? globalOptions = null)
    {
        var indexes = new List<DbIndexDefinition>();
        var properties = entityType.GetProperties();

        // Auto-index common BaseEntity properties
        var autoIndexProperties = new Dictionary<string, (string indexName, bool isSparse)>
        {
            { "CreatedOnUtc", ("idx_auto_created", false) },
            { "UpdatedOnUtc", ("idx_auto_updated", false) },
            { "IsActive", ("idx_auto_active", false) },
            { "OwnerMerchantId", ("idx_auto_owner_merchant", true) },
            { "OwnerContactId", ("idx_auto_owner_contact", true) },
            { "Region", ("idx_auto_region", true) } // For GDPR compliance and region-aware queries
        };

        foreach (var (propertyName, (indexName, isSparse)) in autoIndexProperties)
        {
            var property = properties.FirstOrDefault(p => p.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase));
            if (property != null)
            {
                // Check if manual index already exists for this field
                var existingManualIndex = entityType.GetCustomAttributes<DbIndexAttribute>()
                    .Any(attr => attr.Fields.Contains(propertyName, StringComparer.OrdinalIgnoreCase));

                if (!existingManualIndex)
                {
                    var mappedFieldName = MapPropertyNameToDbField(entityType, propertyName);

                    indexes.Add(new DbIndexDefinition
                    {
                        Name = indexName,
                        Fields = new List<DbIndexField>
                        {
                            new() { FieldName = mappedFieldName, Direction = DbSortDirection.Ascending }
                        },
                        IsSparse = isSparse,
                        CreateInBackground = globalOptions?.CreateIndexesInBackground ?? true
                    });
                }
            }
        }

        return indexes;
    }

    /// <summary>
    /// Map property name to database field name using DbField attributes
    /// Handles special region-aware field mapping for GDPR compliance
    /// </summary>
    /// <param name="entityType">Entity type</param>
    /// <param name="propertyName">Property name</param>
    /// <returns>Mapped field name</returns>
    public static string MapPropertyNameToDbField(Type entityType, string propertyName)
    {
        // Handle special region-aware field names
        if (propertyName.Equals("region", StringComparison.OrdinalIgnoreCase))
        {
            // Map to the actual Region property field name from BaseEntity
            var regionProperty = entityType.GetProperty("Region", BindingFlags.Public | BindingFlags.Instance);
            if (regionProperty != null)
            {
                var regionDbFieldAttr = regionProperty.GetCustomAttribute<DbFieldAttribute>();
                if (regionDbFieldAttr != null && !string.IsNullOrEmpty(regionDbFieldAttr.FieldName))
                {
                    return regionDbFieldAttr.FieldName;
                }
            }
            return "region"; // Default region field name
        }

        if (propertyName.Equals("regionHash", StringComparison.OrdinalIgnoreCase))
        {
            // For hashed region, we typically use a computed field
            return "regionHash";
        }

        var property = entityType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property == null)
        {
            // Return as-is if property not found (might be a direct database field name)
            return propertyName.ToLowerInvariant();
        }

        var dbFieldAttr = property.GetCustomAttribute<DbFieldAttribute>();
        if (dbFieldAttr != null && !string.IsNullOrEmpty(dbFieldAttr.FieldName))
        {
            return dbFieldAttr.FieldName;
        }

        // Use property name with snake_case conversion
        return ConvertToSnakeCase(propertyName);
    }

    /// <summary>
    /// Get index priority for sorting
    /// </summary>
    /// <param name="indexDef">Index definition</param>
    /// <param name="indexAttrs">Index attributes from entity</param>
    /// <returns>Priority value</returns>
    private static int GetIndexPriority(DbIndexDefinition indexDef, DbIndexAttribute[] indexAttrs)
    {
        var attr = indexAttrs.FirstOrDefault(a => a.Name == indexDef.Name);
        if (attr != null)
        {
            return attr.Priority;
        }

        // Default priorities for auto-generated indexes
        if (indexDef.Name.StartsWith("idx_auto_"))
            return 200; // Lower priority for auto indexes

        return 100; // Default priority
    }

    /// <summary>
    /// Convert PascalCase to snake_case
    /// </summary>
    /// <param name="pascalCase">PascalCase string</param>
    /// <returns>snake_case string</returns>
    private static string ConvertToSnakeCase(string pascalCase)
    {
        if (string.IsNullOrEmpty(pascalCase))
        {
            return pascalCase;
        }

        var result = new System.Text.StringBuilder();

        for (int i = 0; i < pascalCase.Length; i++)
        {
            char currentChar = pascalCase[i];

            // If this is an uppercase character and not the first character, add underscore
            if (char.IsUpper(currentChar) && i > 0)
            {
                result.Append('_');
            }

            // Convert to lowercase
            result.Append(char.ToLowerInvariant(currentChar));
        }

        return result.ToString();
    }

    /// <summary>
    /// Validate all entity configurations
    /// </summary>
    /// <param name="entityTypes">Entity types to validate</param>
    /// <returns>Validation results</returns>
    public static EntityValidationResult ValidateEntityConfigurations(Type[] entityTypes)
    {
        var result = new EntityValidationResult();

        foreach (var entityType in entityTypes)
        {
            try
            {
                ValidateEntityConfiguration(entityType);
                result.ValidatedEntities.Add(entityType.Name);
            }
            catch (Exception ex)
            {
                result.ValidationErrors.Add($"{entityType.Name}: {ex.Message}");
            }
        }

        result.IsValid = !result.ValidationErrors.Any();
        return result;
    }

    /// <summary>
    /// Validate a single entity configuration
    /// </summary>
    /// <param name="entityType">Entity type to validate</param>
    private static void ValidateEntityConfiguration(Type entityType)
    {
        // Validate shard key
        var shardKeyAttr = entityType.GetCustomAttribute<DbShardKeyAttribute>();
        if (shardKeyAttr != null && !shardKeyAttr.IsValid())
        {
            throw new InvalidOperationException($"Invalid shard key configuration");
        }

        // Validate indexes
        var indexAttrs = entityType.GetCustomAttributes<DbIndexAttribute>();
        foreach (var indexAttr in indexAttrs)
        {
            if (!indexAttr.IsValid())
            {
                throw new InvalidOperationException($"Invalid index configuration: {indexAttr.Name}");
            }
        }

        // Check for duplicate index names
        var indexNames = indexAttrs.Select(a => a.Name).ToList();
        var duplicateNames = indexNames.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key);
        if (duplicateNames.Any())
        {
            throw new InvalidOperationException($"Duplicate index names: {string.Join(", ", duplicateNames)}");
        }
    }
}

/// <summary>
/// Result of entity configuration validation
/// </summary>
public class EntityValidationResult
{
    /// <summary>
    /// Whether all entities are valid
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// List of validated entity names
    /// </summary>
    public List<string> ValidatedEntities { get; set; } = new();

    /// <summary>
    /// List of validation errors
    /// </summary>
    public List<string> ValidationErrors { get; set; } = new();
}

