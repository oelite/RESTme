using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace OElite.Restme.MongoDb;

/// <summary>
/// Extension methods for MongoDB deserialization that respect class mapping conventions
/// </summary>
public static class MongoDeserializationExtensions
{
    private static readonly object _classMapLock = new object();
    private static readonly HashSet<Type> _configuredTypes = new HashSet<Type>();

    /// <summary>
    /// Deserializes a dictionary document to a strongly-typed BaseEntity using MongoDB BSON serialization
    /// This respects the MongoDB class mappings configured by MongoClassMapConfigurator
    /// </summary>
    public static T? DeserializeDbRecord<T>(this Dictionary<string, object> dbDocumentRecord) where T : BaseEntity
    {
        try
        {
            // Ensure class mapping is configured for the target type
            ConfigureClassMapForType<T>();

            // Convert Dictionary to BsonDocument and then deserialize using MongoDB's BSON serialization
            var bsonDoc = new BsonDocument();
            foreach (var kvp in dbDocumentRecord)
            {
                bsonDoc[kvp.Key] = MongoDbCollectionImplementation.ConvertToBsonValue(kvp.Value);
            }

            return BsonSerializer.Deserialize<T>(bsonDoc);
        }
        catch (Exception)
        {
            // Return null on error - let caller handle logging
            return null;
        }
    }

    /// <summary>
    /// Configures MongoDB class mapping for the given type to handle property conflicts
    /// </summary>
    private static void ConfigureClassMapForType<T>()
    {
        lock (_classMapLock)
        {
            var type = typeof(T);
            MongoClassMapConfigurator.ConfigureClassMappingForType(type, _configuredTypes);
        }
    }
}