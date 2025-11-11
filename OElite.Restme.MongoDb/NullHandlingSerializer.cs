using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace OElite.Restme.MongoDb;

/// <summary>
/// Custom serializer that handles null values during deserialization by returning a default value
/// </summary>
public class NullHandlingSerializer<T> : IBsonSerializer<T>
{
    private readonly T _defaultValue;
    private readonly IBsonSerializer<T> _defaultSerializer;

    public NullHandlingSerializer(T defaultValue)
    {
        _defaultValue = defaultValue;
        _defaultSerializer = BsonSerializer.LookupSerializer<T>();
    }

    public Type ValueType => typeof(T);

    public T Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var bsonType = context.Reader.GetCurrentBsonType();

        // If the value is null or missing, return the default value
        if (bsonType == BsonType.Null || bsonType == BsonType.Undefined)
        {
            context.Reader.ReadNull();
            return _defaultValue;
        }

        // Otherwise, use the default serializer
        return _defaultSerializer.Deserialize(context, args);
    }

    public void Serialize(BsonSerializationContext context, BsonSerializationArgs args, T value)
    {
        _defaultSerializer.Serialize(context, args, value);
    }

    object IBsonSerializer.Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        return Deserialize(context, args);
    }

    public void Serialize(BsonSerializationContext context, BsonSerializationArgs args, object value)
    {
        _defaultSerializer.Serialize(context, args, (T)value);
    }
}
