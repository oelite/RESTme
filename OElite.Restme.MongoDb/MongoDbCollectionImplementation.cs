using MongoDB.Driver;
using MongoDB.Bson;
using OElite.Common;
using System.Text.Json;

namespace OElite.Restme.MongoDb;

/// <summary>
/// MongoDB-free collection implementation that internally handles MongoDB conversion
/// Provides complete abstraction from MongoDB types while maintaining full functionality
/// </summary>
internal class MongoDbCollectionImplementation : IMongoDbCollection
{
    private readonly IMongoCollection<BsonDocument> _collection;

    public MongoDbCollectionImplementation(IMongoCollection<BsonDocument> collection)
    {
        _collection = collection;
    }

    public async Task<List<MongoDbDocument>> FindAsync(MongoDbDocument filter, CancellationToken cancellationToken = default)
    {
        var bsonFilter = ConvertToMongoFilter(filter);
        var cursor = await _collection.FindAsync(bsonFilter, cancellationToken: cancellationToken);
        var results = await cursor.ToListAsync(cancellationToken);
        return results.Select(ConvertToMongoDbDocument).ToList();
    }

    public async Task<MongoDbDocument?> FindOneAsync(MongoDbDocument filter, CancellationToken cancellationToken = default)
    {
        var bsonFilter = ConvertToMongoFilter(filter);
        var result = await _collection.Find(bsonFilter).FirstOrDefaultAsync(cancellationToken);
        return result != null ? ConvertToMongoDbDocument(result) : null;
    }

    public async Task<List<Dictionary<string, object>>> FindAsync(Dictionary<string, object> filter, CancellationToken cancellationToken = default)
    {
        var bsonFilter = ConvertDictionaryToBsonDocument(filter);
        var cursor = await _collection.FindAsync(bsonFilter, cancellationToken: cancellationToken);
        var results = await cursor.ToListAsync(cancellationToken);
        return results.Select(ConvertBsonToDict).ToList();
    }

    public async Task<Dictionary<string, object>?> FindOneAsync(Dictionary<string, object> filter, CancellationToken cancellationToken = default)
    {
        var bsonFilter = ConvertDictionaryToBsonDocument(filter);
        var result = await _collection.Find(bsonFilter).FirstOrDefaultAsync(cancellationToken);
        return result != null ? ConvertBsonToDict(result) : null;
    }

    public async Task<bool> ReplaceOneAsync(MongoDbDocument filter, MongoDbDocument replacement, CancellationToken cancellationToken = default)
    {
        var bsonFilter = ConvertToMongoFilter(filter);
        var bsonReplacement = ConvertToMongoDocument(replacement);
        var result = await _collection.ReplaceOneAsync(bsonFilter, bsonReplacement, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> ReplaceOneAsync(Dictionary<string, object> filter, Dictionary<string, object> replacement, CancellationToken cancellationToken = default)
    {
        var bsonFilter = ConvertDictionaryToBsonDocument(filter);
        var bsonReplacement = ConvertDictionaryToBsonDocument(replacement);
        var result = await _collection.ReplaceOneAsync(bsonFilter, bsonReplacement, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task<long> UpdateManyAsync(MongoDbDocument filter, MongoDbDocument update, CancellationToken cancellationToken = default)
    {
        var bsonFilter = ConvertToMongoFilter(filter);
        var bsonUpdate = ConvertToMongoDocument(update);
        var result = await _collection.UpdateManyAsync(bsonFilter, bsonUpdate, cancellationToken: cancellationToken);
        return result.ModifiedCount;
    }

    public async Task<long> UpdateManyAsync(Dictionary<string, object> filter, Dictionary<string, object> update, CancellationToken cancellationToken = default)
    {
        var bsonFilter = ConvertDictionaryToBsonDocument(filter);
        var bsonUpdate = ConvertDictionaryToBsonDocument(update);
        var result = await _collection.UpdateManyAsync(bsonFilter, bsonUpdate, cancellationToken: cancellationToken);
        return result.ModifiedCount;
    }

    public async Task<long> DeleteManyAsync(MongoDbDocument filter, CancellationToken cancellationToken = default)
    {
        var bsonFilter = ConvertToMongoFilter(filter);
        var result = await _collection.DeleteManyAsync(bsonFilter, cancellationToken);
        return result.DeletedCount;
    }

    public async Task<long> DeleteManyAsync(Dictionary<string, object> filter, CancellationToken cancellationToken = default)
    {
        var bsonFilter = ConvertDictionaryToBsonDocument(filter);
        var result = await _collection.DeleteManyAsync(bsonFilter, cancellationToken);
        return result.DeletedCount;
    }

    public async Task InsertOneAsync(MongoDbDocument document, CancellationToken cancellationToken = default)
    {
        var bsonDocument = ConvertToMongoDocument(document);
        await _collection.InsertOneAsync(bsonDocument, cancellationToken: cancellationToken);
    }

    public async Task InsertOneAsync(Dictionary<string, object> document, CancellationToken cancellationToken = default)
    {
        var bsonDocument = ConvertDictionaryToBsonDocument(document);
        await _collection.InsertOneAsync(bsonDocument, cancellationToken: cancellationToken);
    }

    public async Task<long> CountDocumentsAsync(MongoDbDocument filter, CancellationToken cancellationToken = default)
    {
        var bsonFilter = ConvertToMongoFilter(filter);
        return await _collection.CountDocumentsAsync(bsonFilter, cancellationToken: cancellationToken);
    }

    public async Task<long> CountDocumentsAsync(Dictionary<string, object> filter, CancellationToken cancellationToken = default)
    {
        var bsonFilter = ConvertDictionaryToBsonDocument(filter);
        return await _collection.CountDocumentsAsync(bsonFilter, cancellationToken: cancellationToken);
    }

    public async Task<bool> ExistsAsync(MongoDbDocument filter, CancellationToken cancellationToken = default)
    {
        var count = await CountDocumentsAsync(filter, cancellationToken);
        return count > 0;
    }

    public async Task<bool> ExistsAsync(Dictionary<string, object> filter, CancellationToken cancellationToken = default)
    {
        var count = await CountDocumentsAsync(filter, cancellationToken);
        return count > 0;
    }

    public async Task<List<MongoDbDocument>> AggregateAsync(List<MongoDbDocument> pipeline, CancellationToken cancellationToken = default)
    {
        var bsonPipeline = pipeline.Select(ConvertToMongoDocument).ToArray();
        var cursor = await _collection.AggregateAsync<BsonDocument>(bsonPipeline, cancellationToken: cancellationToken);
        var results = await cursor.ToListAsync(cancellationToken);
        return results.Select(ConvertToMongoDbDocument).ToList();
    }

    public async Task<List<Dictionary<string, object>>> AggregateAsync(List<Dictionary<string, object>> pipeline, CancellationToken cancellationToken = default)
    {
        var bsonPipeline = pipeline.Select(ConvertDictionaryToBsonDocument).ToArray();
        var cursor = await _collection.AggregateAsync<BsonDocument>(bsonPipeline, cancellationToken: cancellationToken);
        var results = await cursor.ToListAsync(cancellationToken);
        return results.Select(ConvertBsonToDict).ToList();
    }

    /// <summary>
    /// Converts MongoDbDocument to BsonDocument for internal MongoDB operations
    /// </summary>
    private static BsonDocument ConvertToMongoDocument(MongoDbDocument document)
    {
        var bsonDoc = new BsonDocument();
        foreach (var kvp in document)
        {
            bsonDoc[kvp.Key] = ConvertToBsonValue(kvp.Value);
        }
        return bsonDoc;
    }

    /// <summary>
    /// Converts MongoDbDocument to FilterDefinition for MongoDB queries
    /// </summary>
    private static FilterDefinition<BsonDocument> ConvertToMongoFilter(MongoDbDocument document)
    {
        return new BsonDocumentFilterDefinition<BsonDocument>(ConvertToMongoDocument(document));
    }

    /// <summary>
    /// Converts .NET object to BsonValue
    /// </summary>
    internal static BsonValue ConvertToBsonValue(object? value)
    {
        return value switch
        {
            null => BsonNull.Value,
            string s => new BsonString(s),
            int i => new BsonInt32(i),
            long l => new BsonInt64(l),
            double d => new BsonDouble(d),
            decimal dec => new BsonDecimal128(dec),
            bool b => new BsonBoolean(b),
            DateTime dt => new BsonDateTime(dt),
            DbObjectId objectId => new BsonObjectId(new ObjectId(objectId.ToString())),
            JsonElement jsonElement => ConvertJsonElementToBsonValue(jsonElement),
            MongoDbDocument doc => ConvertToMongoDocument(doc),
            Dictionary<string, object> dict => ConvertDictionaryToBsonDocument(dict),
            IEnumerable<object> array => new BsonArray(array.Select(ConvertToBsonValue)),
            _ => new BsonString(value?.ToString() ?? "")
        };
    }

    /// <summary>
    /// Converts JsonElement to BsonValue based on its ValueKind
    /// </summary>
    private static BsonValue ConvertJsonElementToBsonValue(JsonElement jsonElement)
    {
        return jsonElement.ValueKind switch
        {
            JsonValueKind.String => new BsonString(jsonElement.GetString() ?? ""),
            JsonValueKind.Number => jsonElement.TryGetInt32(out var intValue) ? new BsonInt32(intValue) :
                                   jsonElement.TryGetInt64(out var longValue) ? new BsonInt64(longValue) :
                                   new BsonDouble(jsonElement.GetDouble()),
            JsonValueKind.True => new BsonBoolean(true),
            JsonValueKind.False => new BsonBoolean(false),
            JsonValueKind.Null => BsonNull.Value,
            JsonValueKind.Object => new BsonDocument(
                jsonElement.EnumerateObject()
                    .ToDictionary(prop => prop.Name, prop => ConvertJsonElementToBsonValue(prop.Value))),
            JsonValueKind.Array => new BsonArray(
                jsonElement.EnumerateArray()
                    .Select(ConvertJsonElementToBsonValue)),
            _ => BsonNull.Value
        };
    }

    /// <summary>
    /// Converts Dictionary to BsonDocument using proper type conversion
    /// </summary>
    internal static BsonDocument ConvertDictionaryToBsonDocument(Dictionary<string, object> dict)
    {
        var bsonDoc = new BsonDocument();
        foreach (var kvp in dict)
        {
            bsonDoc[kvp.Key] = ConvertToBsonValue(kvp.Value);
        }
        return bsonDoc;
    }

    /// <summary>
    /// Converts BsonDocument to MongoDbDocument
    /// </summary>
    private static MongoDbDocument ConvertToMongoDbDocument(BsonDocument bsonDoc)
    {
        var document = new MongoDbDocument();
        foreach (var element in bsonDoc.Elements)
        {
            document[element.Name] = ConvertBsonValue(element.Value);
        }
        return document;
    }

    /// <summary>
    /// Converts BsonDocument to Dictionary<string, object> (legacy support)
    /// </summary>
    private static Dictionary<string, object> ConvertBsonToDict(BsonDocument bsonDoc)
    {
        var dict = new Dictionary<string, object>();
        foreach (var element in bsonDoc.Elements)
        {
            var value = ConvertBsonValue(element.Value);
            if (value != null)
            {
                dict[element.Name] = value;
            }
        }
        return dict;
    }

    /// <summary>
    /// Converts BsonValue to standard .NET object
    /// </summary>
    private static object? ConvertBsonValue(BsonValue bsonValue)
    {
        return bsonValue.BsonType switch
        {
            BsonType.String => bsonValue.AsString,
            BsonType.Int32 => bsonValue.AsInt32,
            BsonType.Int64 => bsonValue.AsInt64,
            BsonType.Double => bsonValue.AsDouble,
            BsonType.Decimal128 => bsonValue.AsDecimal,
            BsonType.Boolean => bsonValue.AsBoolean,
            BsonType.DateTime => bsonValue.ToUniversalTime(),
            BsonType.ObjectId => bsonValue.AsObjectId.ToString(),
            BsonType.Array => bsonValue.AsBsonArray.Select(ConvertBsonValue).ToArray(),
            BsonType.Document => ConvertToMongoDbDocument(bsonValue.AsBsonDocument),
            BsonType.Null => null,
            _ => bsonValue.ToString()
        };
    }
}

/// <summary>
/// MongoDB-free typed collection implementation
/// </summary>
internal class MongoDbCollectionImplementation<T> : IMongoDbCollection<T> where T : BaseEntity
{
    private readonly IMongoCollection<T> _collection;

    public MongoDbCollectionImplementation(IMongoCollection<T> collection)
    {
        _collection = collection;
    }

    public async Task<List<T>> FindAsync(System.Linq.Expressions.Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
    {
        var cursor = await _collection.FindAsync(filter, cancellationToken: cancellationToken);
        return await cursor.ToListAsync(cancellationToken);
    }

    public async Task<T?> FindOneAsync(System.Linq.Expressions.Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<T>> FindAsync(Dictionary<string, object> filter, CancellationToken cancellationToken = default)
    {
        var bsonFilter = MongoDbCollectionImplementation.ConvertDictionaryToBsonDocument(filter);
        var cursor = await _collection.FindAsync(bsonFilter, cancellationToken: cancellationToken);
        return await cursor.ToListAsync(cancellationToken);
    }

    public async Task<T?> FindOneAsync(Dictionary<string, object> filter, CancellationToken cancellationToken = default)
    {
        var bsonFilter = MongoDbCollectionImplementation.ConvertDictionaryToBsonDocument(filter);
        return await _collection.Find(bsonFilter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> ReplaceOneAsync(System.Linq.Expressions.Expression<Func<T, bool>> filter, T replacement, CancellationToken cancellationToken = default)
    {
        var result = await _collection.ReplaceOneAsync(filter, replacement, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task<long> DeleteManyAsync(System.Linq.Expressions.Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
    {
        var result = await _collection.DeleteManyAsync(filter, cancellationToken);
        return result.DeletedCount;
    }

    public async Task InsertOneAsync(T document, CancellationToken cancellationToken = default)
    {
        await _collection.InsertOneAsync(document, cancellationToken: cancellationToken);
    }

    public async Task<long> CountDocumentsAsync(System.Linq.Expressions.Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
    {
        return await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
    }

    public async Task<bool> ExistsAsync(System.Linq.Expressions.Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
    {
        var count = await CountDocumentsAsync(filter, cancellationToken);
        return count > 0;
    }
}