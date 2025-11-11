using System;
using System.Collections.Generic;
using System.Linq;

namespace OElite;

/// <summary>
/// Specifies index configuration for MongoDB collections
/// Used to define performance-optimized indexes at the entity level
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class DbIndexAttribute : Attribute
{
    /// <summary>
    /// Index name (must be unique within the collection)
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Field names that comprise the index, in order
    /// </summary>
    public string[] Fields { get; }

    /// <summary>
    /// Sort directions for each field (1 for ascending, -1 for descending)
    /// </summary>
    public int[] Directions { get; set; } = Array.Empty<int>();

    /// <summary>
    /// Whether this is a unique index
    /// </summary>
    public bool IsUnique { get; set; } = false;

    /// <summary>
    /// Whether this is a sparse index (only index documents that have the indexed field)
    /// </summary>
    public bool IsSparse { get; set; } = false;

    /// <summary>
    /// Whether to create the index in background mode (non-blocking)
    /// </summary>
    public bool CreateInBackground { get; set; } = true;

    /// <summary>
    /// TTL expiration in seconds (for TTL indexes that auto-delete documents)
    /// </summary>
    public int TtlExpirationSeconds { get; set; } = 0;

    /// <summary>
    /// Whether any field in this index uses text indexing
    /// </summary>
    public bool IsTextIndex { get; set; } = false;

    /// <summary>
    /// Whether any field in this index uses hashed indexing
    /// </summary>
    public bool IsHashedIndex { get; set; } = false;

    /// <summary>
    /// Priority for index creation (lower numbers created first)
    /// </summary>
    public int Priority { get; set; } = 100;

    /// <summary>
    /// Creates an index attribute with specified name and fields
    /// </summary>
    /// <param name="name">Index name</param>
    /// <param name="fields">Field names for the index</param>
    public DbIndexAttribute(string name, params string[] fields)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("Index name cannot be null or empty", nameof(name));

        if (fields == null || fields.Length == 0)
            throw new ArgumentException("Index must have at least one field", nameof(fields));

        Name = name;
        Fields = fields;

        // Default to ascending for all fields
        Directions = new int[fields.Length];
        for (int i = 0; i < fields.Length; i++)
        {
            Directions[i] = 1; // Ascending
        }
    }

    /// <summary>
    /// Creates a unique index
    /// </summary>
    /// <param name="name">Index name</param>
    /// <param name="fields">Field names</param>
    /// <returns>Configured unique index attribute</returns>
    public static DbIndexAttribute Unique(string name, params string[] fields)
    {
        return new DbIndexAttribute(name, fields)
        {
            IsUnique = true,
            Priority = 10 // High priority for unique indexes
        };
    }

    /// <summary>
    /// Creates a compound index for common query patterns
    /// </summary>
    /// <param name="name">Index name</param>
    /// <param name="fields">Field names in query order</param>
    /// <returns>Configured compound index attribute</returns>
    public static DbIndexAttribute Compound(string name, params string[] fields)
    {
        return new DbIndexAttribute(name, fields)
        {
            Priority = 20 // Medium-high priority for compound indexes
        };
    }

    /// <summary>
    /// Creates a sparse index for optional fields
    /// </summary>
    /// <param name="name">Index name</param>
    /// <param name="fields">Field names</param>
    /// <returns>Configured sparse index attribute</returns>
    public static DbIndexAttribute Sparse(string name, params string[] fields)
    {
        return new DbIndexAttribute(name, fields)
        {
            IsSparse = true,
            Priority = 30 // Medium priority for sparse indexes
        };
    }

    /// <summary>
    /// Creates a TTL index for automatic document expiration
    /// </summary>
    /// <param name="name">Index name</param>
    /// <param name="field">Date field for TTL</param>
    /// <param name="expirationSeconds">Expiration time in seconds</param>
    /// <returns>Configured TTL index attribute</returns>
    public static DbIndexAttribute Ttl(string name, string field, int expirationSeconds)
    {
        return new DbIndexAttribute(name, field)
        {
            TtlExpirationSeconds = expirationSeconds,
            Priority = 40 // Lower priority for TTL indexes
        };
    }

    /// <summary>
    /// Creates a text search index
    /// </summary>
    /// <param name="name">Index name</param>
    /// <param name="fields">Text fields</param>
    /// <returns>Configured text index attribute</returns>
    public static DbIndexAttribute Text(string name, params string[] fields)
    {
        return new DbIndexAttribute(name, fields)
        {
            IsTextIndex = true,
            Priority = 50 // Lower priority for text indexes
        };
    }

    /// <summary>
    /// Creates a hashed index for even distribution
    /// </summary>
    /// <param name="name">Index name</param>
    /// <param name="field">Field to hash</param>
    /// <returns>Configured hashed index attribute</returns>
    public static DbIndexAttribute Hashed(string name, string field)
    {
        return new DbIndexAttribute(name, field)
        {
            IsHashedIndex = true,
            Priority = 25 // Medium priority for hashed indexes
        };
    }

    /// <summary>
    /// Validates the index configuration
    /// </summary>
    /// <returns>True if configuration is valid</returns>
    public bool IsValid()
    {
        if (string.IsNullOrEmpty(Name))
            return false;

        if (Fields == null || Fields.Length == 0)
            return false;

        if (Fields.Any(string.IsNullOrEmpty))
            return false;

        // Validate array lengths match
        if (Directions.Length != 0 && Directions.Length != Fields.Length)
            return false;

        // Validate direction values
        if (Directions.Any(d => d != 1 && d != -1))
            return false;

        // TTL indexes should only have one field
        if (TtlExpirationSeconds > 0 && Fields.Length > 1)
            return false;

        return true;
    }

    /// <summary>
    /// Gets the field configuration at the specified index
    /// </summary>
    /// <param name="index">Field index</param>
    /// <returns>Field configuration tuple</returns>
    public (string FieldName, int Direction) GetFieldConfig(int index)
    {
        if (index < 0 || index >= Fields.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        var direction = Directions.Length > index ? Directions[index] : 1;
        return (Fields[index], direction);
    }
}