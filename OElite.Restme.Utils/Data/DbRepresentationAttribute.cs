using System;

namespace OElite;

/// <summary>
/// Custom MongoDB representation attribute - equivalent to BsonRepresentation
/// Specifies how a value should be represented in MongoDB
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class DbRepresentationAttribute(RestmeDbType dbType) : Attribute
{
    public RestmeDbType DbType { get; } = dbType;
}