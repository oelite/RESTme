using System;
using System.Collections.Generic;
using System.Linq;

namespace OElite;

/// <summary>
/// Specifies shard key configuration for MongoDB collections
/// Used to define how a collection should be sharded for horizontal scaling
/// Supports region-aware sharding for GDPR compliance and data sovereignty
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class DbShardKeyAttribute : Attribute
{
    /// <summary>
    /// Field names that comprise the shard key, in order
    /// </summary>
    public string[] Fields { get; }

    /// <summary>
    /// Whether fields should use hashed sharding (true) or range sharding (false)
    /// </summary>
    public bool[] IsHashed { get; set; } = Array.Empty<bool>();

    /// <summary>
    /// Sort directions for each field (1 for ascending, -1 for descending)
    /// </summary>
    public int[] Directions { get; set; } = Array.Empty<int>();

    /// <summary>
    /// Whether the shard key should enforce uniqueness
    /// </summary>
    public bool IsUnique { get; set; } = false;

    /// <summary>
    /// Number of chunks to pre-split when creating the sharded collection
    /// </summary>
    public int PreSplitChunks { get; set; } = 256;

    /// <summary>
    /// Whether to automatically include region in shard key for GDPR compliance
    /// </summary>
    public bool IncludeRegion { get; set; } = false;

    /// <summary>
    /// Strategy for incorporating region into shard key
    /// </summary>
    public RegionShardingStrategy RegionStrategy { get; set; } = RegionShardingStrategy.RegionFirst;

    /// <summary>
    /// Whether to use hashed region for better distribution across regions
    /// </summary>
    public bool HashRegion { get; set; } = false;

    /// <summary>
    /// Fallback region for entities without region specified
    /// </summary>
    public string DefaultRegion { get; set; } = "DEFAULT";

    /// <summary>
    /// Whether region changes are allowed (affects data migration strategy)
    /// </summary>
    public bool AllowRegionMigration { get; set; } = true;

    /// <summary>
    /// Creates a shard key attribute with specified fields
    /// </summary>
    /// <param name="fields">Field names for the shard key</param>
    public DbShardKeyAttribute(params string[] fields)
    {
        if (fields == null || fields.Length == 0)
            throw new ArgumentException("Shard key must have at least one field", nameof(fields));

        Fields = fields;

        // Default to ascending, non-hashed for all fields
        Directions = new int[fields.Length];
        IsHashed = new bool[fields.Length];

        for (int i = 0; i < fields.Length; i++)
        {
            Directions[i] = 1; // Ascending
            IsHashed[i] = false; // Range sharding
        }
    }

    /// <summary>
    /// Creates a shard key with a single hashed field (common for ID-based sharding)
    /// </summary>
    /// <param name="field">Field name to use for hashed sharding</param>
    /// <returns>Configured shard key attribute</returns>
    public static DbShardKeyAttribute Hashed(string field)
    {
        return new DbShardKeyAttribute(field)
        {
            IsHashed = new[] { true }
        };
    }

    /// <summary>
    /// Creates a compound shard key for bucket-style sharding (common in S3 scenarios)
    /// </summary>
    /// <param name="bucketField">Primary bucket/partition field</param>
    /// <param name="keyField">Secondary key field</param>
    /// <param name="hashSecondary">Whether to hash the secondary field</param>
    /// <returns>Configured compound shard key attribute</returns>
    public static DbShardKeyAttribute Compound(string bucketField, string keyField, bool hashSecondary = false)
    {
        return new DbShardKeyAttribute(bucketField, keyField)
        {
            IsHashed = new[] { false, hashSecondary },
            Directions = new[] { 1, 1 } // Both ascending
        };
    }

    /// <summary>
    /// Creates a region-aware shard key optimized for tenant isolation
    /// </summary>
    /// <param name="tenantField">Primary tenant field (e.g., "TenantId", "OwnerMerchantId")</param>
    /// <param name="additionalFields">Additional fields for compound key</param>
    /// <returns>Configured region-aware attribute</returns>
    public static DbShardKeyAttribute ForTenant(string tenantField, params string[] additionalFields)
    {
        var allFields = new[] { tenantField }.Concat(additionalFields).ToArray();
        return new DbShardKeyAttribute(allFields)
        {
            IncludeRegion = true,
            RegionStrategy = RegionShardingStrategy.RegionFirst,
            HashRegion = false, // Keep region readable for compliance queries
            AllowRegionMigration = true
        };
    }

    /// <summary>
    /// Creates a region-aware shard key optimized for S3-style object storage
    /// </summary>
    /// <param name="bucketField">Bucket/container field</param>
    /// <param name="keyField">Object key field</param>
    /// <param name="hashKey">Whether to hash the key field</param>
    /// <returns>Configured region-aware attribute</returns>
    public static DbShardKeyAttribute ForObjectStorage(string bucketField, string keyField, bool hashKey = false)
    {
        return new DbShardKeyAttribute(bucketField, keyField)
        {
            IncludeRegion = true,
            RegionStrategy = RegionShardingStrategy.RegionFirst,
            HashRegion = false, // Readable region for compliance
            AllowRegionMigration = true,
            IsHashed = new[] { false, hashKey }
        };
    }

    /// <summary>
    /// Creates a region-aware shard key optimized for time-series data
    /// </summary>
    /// <param name="timeField">Time/date field for chronological sharding</param>
    /// <param name="entityField">Entity identifier field</param>
    /// <returns>Configured region-aware attribute</returns>
    public static DbShardKeyAttribute ForTimeSeries(string timeField, string entityField)
    {
        return new DbShardKeyAttribute(timeField, entityField)
        {
            IncludeRegion = true,
            RegionStrategy = RegionShardingStrategy.RegionFirst,
            HashRegion = false,
            AllowRegionMigration = false // Time-series data typically doesn't migrate
        };
    }

    /// <summary>
    /// Creates a region-aware shard key with hashed region for maximum distribution
    /// </summary>
    /// <param name="businessFields">Business fields for the shard key</param>
    /// <returns>Configured region-aware attribute with hashed region</returns>
    public static DbShardKeyAttribute WithHashedRegion(params string[] businessFields)
    {
        return new DbShardKeyAttribute(businessFields)
        {
            IncludeRegion = true,
            RegionStrategy = RegionShardingStrategy.RegionFirst,
            HashRegion = true, // Better distribution but less readable
            AllowRegionMigration = true
        };
    }

    /// <summary>
    /// Validates the shard key configuration
    /// </summary>
    /// <returns>True if configuration is valid</returns>
    public virtual bool IsValid()
    {
        if (Fields == null || Fields.Length == 0)
            return false;

        if (Fields.Any(string.IsNullOrEmpty))
            return false;

        // Validate array lengths match
        if (IsHashed.Length != 0 && IsHashed.Length != Fields.Length)
            return false;

        if (Directions.Length != 0 && Directions.Length != Fields.Length)
            return false;

        // Validate direction values
        if (Directions.Any(d => d != 1 && d != -1))
            return false;

        // Validate region-aware configuration
        if (IncludeRegion)
        {
            if (string.IsNullOrWhiteSpace(DefaultRegion))
                return false;

            // Validate region field constraints for middle placement
            if (HashRegion && RegionStrategy == RegionShardingStrategy.RegionMiddle && Fields.Length < 2)
                return false; // Need at least 2 fields for middle placement
        }

        return true;
    }

    /// <summary>
    /// Gets the field configuration at the specified index
    /// </summary>
    /// <param name="index">Field index</param>
    /// <returns>Field configuration tuple</returns>
    public (string FieldName, bool IsHashed, int Direction) GetFieldConfig(int index)
    {
        if (index < 0 || index >= Fields.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        var isHashed = IsHashed.Length > index ? IsHashed[index] : false;
        var direction = Directions.Length > index ? Directions[index] : 1;

        return (Fields[index], isHashed, direction);
    }

    /// <summary>
    /// Gets the complete shard key fields including region placement when IncludeRegion is true
    /// </summary>
    /// <returns>Array of all shard key fields with region incorporated</returns>
    public string[] GetShardKeyFieldsWithRegion()
    {
        if (!IncludeRegion)
            return Fields;

        var regionField = HashRegion ? "regionHash" : "region";

        return RegionStrategy switch
        {
            RegionShardingStrategy.RegionFirst => new[] { regionField }.Concat(Fields).ToArray(),
            RegionShardingStrategy.RegionLast => Fields.Concat(new[] { regionField }).ToArray(),
            RegionShardingStrategy.RegionMiddle => InsertRegionInMiddle(regionField),
            _ => new[] { regionField }.Concat(Fields).ToArray()
        };
    }

    /// <summary>
    /// Gets the hashing configuration for all fields including region when IncludeRegion is true
    /// </summary>
    /// <returns>Array indicating which fields should be hashed</returns>
    public bool[] GetHashingConfigurationWithRegion()
    {
        if (!IncludeRegion)
            return IsHashed.Length > 0 ? IsHashed : Fields.Select(_ => false).ToArray();

        var regionHashing = HashRegion;
        var existingHashing = IsHashed.Length > 0 ? IsHashed : Fields.Select(_ => false).ToArray();

        return RegionStrategy switch
        {
            RegionShardingStrategy.RegionFirst => new[] { regionHashing }.Concat(existingHashing).ToArray(),
            RegionShardingStrategy.RegionLast => existingHashing.Concat(new[] { regionHashing }).ToArray(),
            RegionShardingStrategy.RegionMiddle => InsertRegionHashingInMiddle(regionHashing, existingHashing),
            _ => new[] { regionHashing }.Concat(existingHashing).ToArray()
        };
    }

    /// <summary>
    /// Gets the direction configuration for all fields including region when IncludeRegion is true
    /// </summary>
    /// <returns>Array of sort directions with region incorporated</returns>
    public int[] GetDirectionsWithRegion()
    {
        if (!IncludeRegion)
            return Directions.Length > 0 ? Directions : Fields.Select(_ => 1).ToArray();

        var regionDirection = 1; // Always ascending for region
        var existingDirections = Directions.Length > 0 ? Directions : Fields.Select(_ => 1).ToArray();

        return RegionStrategy switch
        {
            RegionShardingStrategy.RegionFirst => new[] { regionDirection }.Concat(existingDirections).ToArray(),
            RegionShardingStrategy.RegionLast => existingDirections.Concat(new[] { regionDirection }).ToArray(),
            RegionShardingStrategy.RegionMiddle => InsertRegionDirectionInMiddle(regionDirection, existingDirections),
            _ => new[] { regionDirection }.Concat(existingDirections).ToArray()
        };
    }

    private string[] InsertRegionInMiddle(string regionField)
    {
        if (Fields.Length < 2)
            return new[] { regionField }.Concat(Fields).ToArray(); // Fallback to first

        var middleIndex = Fields.Length / 2;
        var result = new List<string>();
        result.AddRange(Fields.Take(middleIndex));
        result.Add(regionField);
        result.AddRange(Fields.Skip(middleIndex));
        return result.ToArray();
    }

    private bool[] InsertRegionHashingInMiddle(bool regionHashing, bool[] existingHashing)
    {
        if (existingHashing.Length < 2)
            return new[] { regionHashing }.Concat(existingHashing).ToArray(); // Fallback to first

        var middleIndex = existingHashing.Length / 2;
        var result = new List<bool>();
        result.AddRange(existingHashing.Take(middleIndex));
        result.Add(regionHashing);
        result.AddRange(existingHashing.Skip(middleIndex));
        return result.ToArray();
    }

    private int[] InsertRegionDirectionInMiddle(int regionDirection, int[] existingDirections)
    {
        if (existingDirections.Length < 2)
            return new[] { regionDirection }.Concat(existingDirections).ToArray(); // Fallback to first

        var middleIndex = existingDirections.Length / 2;
        var result = new List<int>();
        result.AddRange(existingDirections.Take(middleIndex));
        result.Add(regionDirection);
        result.AddRange(existingDirections.Skip(middleIndex));
        return result.ToArray();
    }
}

/// <summary>
/// Strategy for incorporating region into shard key
/// </summary>
public enum RegionShardingStrategy
{
    /// <summary>
    /// Region is the first field in shard key (recommended for compliance queries)
    /// Pattern: { region: 1, businessField1: 1, businessField2: 1 }
    /// </summary>
    RegionFirst = 0,

    /// <summary>
    /// Region is the last field in shard key (for business-field-first queries)
    /// Pattern: { businessField1: 1, businessField2: 1, region: 1 }
    /// </summary>
    RegionLast = 1,

    /// <summary>
    /// Region is placed in the middle of compound shard key
    /// Pattern: { businessField1: 1, region: 1, businessField2: 1 }
    /// </summary>
    RegionMiddle = 2
}