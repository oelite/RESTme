using System;

namespace OElite;

/// <summary>
/// Custom MongoDB ignore attribute - equivalent to BsonIgnore
/// Marks a property to be ignored during MongoDB serialization
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class DbFieldIgnore : Attribute
{
    public DbFieldIgnoreType IgnoreType { get; set; } = DbFieldIgnoreType.Always;

    public DbFieldIgnore()
    {
    }

    public DbFieldIgnore(DbFieldIgnoreType ignoreType)
    {
        IgnoreType = ignoreType;
    }
}

/// <summary>
/// MongoDB ignore type enumeration
/// </summary>
public enum DbFieldIgnoreType
{
    Always,
    IfNull,
    IfDefault
}