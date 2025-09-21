using System;

namespace OElite.Restme.MongoDb;

/// <summary>
/// Custom MongoDB field attribute - equivalent to RestmeDbColumn but for MongoDB
/// Specifies field mapping and options for MongoDB documents
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class DbFieldAttribute : Attribute
{
    public string? FieldName { get; set; }
    public bool Ignore { get; set; }
    public bool Required { get; set; }
    public object? DefaultValue { get; set; }
    public RestmeDbType FieldType { get; set; } = RestmeDbType.Auto;

    public DbFieldAttribute()
    {
    }

    public DbFieldAttribute(string fieldName)
    {
        FieldName = fieldName;
    }
}

/// <summary>
/// MongoDB field type enumeration
/// </summary>
public enum RestmeDbType
{
    Auto,
    String,
    Int32,
    Int64,
    Double,
    Boolean,
    DateTime,
    ObjectId,
    Array,
    Object,
    Binary
}