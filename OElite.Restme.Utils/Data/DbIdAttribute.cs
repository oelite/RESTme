using System;

namespace OElite;

/// <summary>
/// Custom MongoDB ID attribute - equivalent to BsonId
/// Marks a property as the MongoDB document ID
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class DbIdAttribute : Attribute
{
    public DbIdType IdType { get; set; } = DbIdType.ObjectId;
    public bool AutoGenerate { get; set; } = true;

    public DbIdAttribute()
    {
    }

    public DbIdAttribute(DbIdType idType)
    {
        IdType = idType;
    }
}

/// <summary>
/// MongoDB ID type enumeration
/// </summary>
public enum DbIdType
{
    ObjectId,
    DbObjectId,  // Custom ObjectId type for OElite.Common
    String,
    Int32,
    Int64,
    Guid
}