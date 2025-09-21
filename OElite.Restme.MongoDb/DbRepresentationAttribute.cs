using System;

namespace OElite.Restme.MongoDb;

/// <summary>
/// Custom MongoDB representation attribute - equivalent to BsonRepresentation
/// Specifies how a value should be represented in MongoDB
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class DbRepresentationAttribute(RestmeDbType dbType) : Attribute
{
    public RestmeDbType DbType { get; } = dbType;
}