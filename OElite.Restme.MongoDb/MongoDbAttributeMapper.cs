using System.Reflection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace OElite.Restme.MongoDb;

/// <summary>
/// Maps our custom OElite MongoDB attributes to actual MongoDB attributes
/// </summary>
public static class MongoDbAttributeMapper
{
    /// <summary>
    /// Gets the MongoDB collection name from our custom attribute
    /// </summary>
    public static string? GetCollectionName(Type type)
    {
        var attr = type.GetCustomAttribute<DbCollectionAttribute>();
        return attr?.CollectionName;
    }

    /// <summary>
    /// Gets MongoDB field attributes for a property
    /// </summary>
    public static List<Attribute> GetMongoDbAttributes(PropertyInfo property)
    {
        var attributes = new List<Attribute>();

        // Check for OeMongoId attribute
        var idAttr = property.GetCustomAttribute<DbIdAttribute>();
        if (idAttr != null)
        {
            attributes.Add(new BsonIdAttribute());

            // Add representation if specified
            if (idAttr.IdType != DbIdType.ObjectId)
            {
                var bsonType = MapToBsonType(idAttr.IdType);
                attributes.Add(new BsonRepresentationAttribute(bsonType));
            }
        }

        // Check for OeMongoField attribute
        var fieldAttr = property.GetCustomAttribute<DbFieldAttribute>();
        if (fieldAttr != null)
        {
            if (!string.IsNullOrEmpty(fieldAttr.FieldName))
            {
                attributes.Add(new BsonElementAttribute(fieldAttr.FieldName));
            }

            // Add representation if specified
            if (fieldAttr.FieldType != RestmeDbType.Auto)
            {
                var bsonType = MapToBsonType(fieldAttr.FieldType);
                attributes.Add(new BsonRepresentationAttribute(bsonType));
            }
        }

        // Check for OeMongoRepresentation attribute
        var reprAttr = property.GetCustomAttribute<DbRepresentationAttribute>();
        if (reprAttr != null)
        {
            var bsonType = MapToBsonType(reprAttr.DbType);
            attributes.Add(new BsonRepresentationAttribute(bsonType));
        }

        // Check for OeMongoIgnore attribute
        var ignoreAttr = property.GetCustomAttribute<DbFieldIgnore>();
        if (ignoreAttr != null)
        {
            attributes.Add(new BsonIgnoreAttribute());
        }

        return attributes;
    }

    /// <summary>
    /// Maps our custom field types to MongoDB BsonType
    /// </summary>
    private static BsonType MapToBsonType(RestmeDbType fieldType)
    {
        return fieldType switch
        {
            RestmeDbType.String => BsonType.String,
            RestmeDbType.Int32 => BsonType.Int32,
            RestmeDbType.Int64 => BsonType.Int64,
            RestmeDbType.Double => BsonType.Double,
            RestmeDbType.Boolean => BsonType.Boolean,
            RestmeDbType.DateTime => BsonType.DateTime,
            RestmeDbType.ObjectId => BsonType.ObjectId,
            RestmeDbType.Array => BsonType.Array,
            RestmeDbType.Object => BsonType.Document,
            RestmeDbType.Binary => BsonType.Binary,
            _ => BsonType.String
        };
    }

    /// <summary>
    /// Maps our custom ID types to MongoDB BsonType
    /// </summary>
    private static BsonType MapToBsonType(DbIdType idType)
    {
        return idType switch
        {
            DbIdType.ObjectId => BsonType.ObjectId,
            DbIdType.DbObjectId => BsonType.ObjectId, // Custom ObjectId maps to MongoDB ObjectId
            DbIdType.String => BsonType.String,
            DbIdType.Int32 => BsonType.Int32,
            DbIdType.Int64 => BsonType.Int64,
            DbIdType.Guid => BsonType.String, // GUIDs are typically stored as strings in MongoDB
            _ => BsonType.ObjectId
        };
    }

    /// <summary>
    /// Gets all properties with MongoDB field mappings
    /// </summary>
    public static Dictionary<string, PropertyInfo> GetMongoFieldMappings(Type type)
    {
        var mappings = new Dictionary<string, PropertyInfo>();

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            var fieldAttr = property.GetCustomAttribute<DbFieldAttribute>();
            var idAttr = property.GetCustomAttribute<DbIdAttribute>();
            var ignoreAttr = property.GetCustomAttribute<DbFieldIgnore>();

            // Skip ignored properties
            if (ignoreAttr != null)
                continue;

            string fieldName;

            if (idAttr != null)
            {
                fieldName = "_id";
            }
            else if (fieldAttr != null && !string.IsNullOrEmpty(fieldAttr.FieldName))
            {
                fieldName = fieldAttr.FieldName;
            }
            else
            {
                fieldName = property.Name;
            }

            mappings[fieldName] = property;
        }

        return mappings;
    }
}