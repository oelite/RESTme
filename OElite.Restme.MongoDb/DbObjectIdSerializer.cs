using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace OElite.Restme.MongoDb;

/// <summary>
/// Custom serializer for DbObjectId that converts to/from MongoDB ObjectId
/// </summary>
public class DbObjectIdSerializer : SerializerBase<DbObjectId>
{
    /// <summary>
    /// Deserializes an DbObjectId from MongoDB ObjectId
    /// </summary>
    public override DbObjectId Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var bsonType = context.Reader.GetCurrentBsonType();

        switch (bsonType)
        {
            case BsonType.ObjectId:
                var objectId = context.Reader.ReadObjectId();
                var objectIdString = objectId.ToString();
                if (string.IsNullOrEmpty(objectIdString))
                    return default(DbObjectId);
                return new DbObjectId(objectIdString);

            case BsonType.String:
                var stringValue = context.Reader.ReadString();
                if (string.IsNullOrEmpty(stringValue))
                    return default(DbObjectId);
                return new DbObjectId(stringValue);

            case BsonType.Null:
                context.Reader.ReadNull();
                return default(DbObjectId);

            default:
                throw new FormatException($"Cannot deserialize a {bsonType} to RestmeObjectId.");
        }
    }

    /// <summary>
    /// Serializes an DbObjectId to MongoDB ObjectId
    /// </summary>
    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, DbObjectId value)
    {
        if (value.IsEmpty)
        {
            context.Writer.WriteNull();
        }
        else if (ObjectId.TryParse(value.Value, out var objectId))
        {
            context.Writer.WriteObjectId(objectId);
        }
        else
        {
            // Fallback to string if parsing fails
            context.Writer.WriteString(value.Value);
        }
    }
}

/// <summary>
/// Custom serializer provider that registers DbObjectId serializer
/// </summary>
public static class DbObjectIdSerializerProvider
{
    private static bool _isRegistered = false;
    private static readonly object _lock = new object();

    /// <summary>
    /// Registers the DbObjectId serializer with MongoDB
    /// </summary>
    public static void RegisterSerializer()
    {
        if (_isRegistered) return;

        lock (_lock)
        {
            if (_isRegistered) return;

            // Register the serializer for DbObjectId
            BsonSerializer.RegisterSerializer(typeof(DbObjectId), new DbObjectIdSerializer());

            _isRegistered = true;
        }
    }
}