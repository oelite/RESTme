using MongoDB.Driver;
using MongoDB.Bson;

namespace OElite.Restme.MongoDb;

/// <summary>
/// Extensions for DbCentre to provide MongoDB-free infrastructure operations
/// Replaces direct IMongoCollection usage with abstracted methods
/// </summary>
public static class MongoDbCentreExtensions
{
    /// <summary>
    /// Get a query interface for any collection by name (for infrastructure services)
    /// This replaces direct GetCollection<BsonDocument>(collectionName) usage
    /// </summary>
    public static IMongoCollectionQuery GetCollectionQuery(this MongoDbCentre dbCentre, string collectionName)
    {
        var database = dbCentre.GetDatabase();
        var collection = database.GetCollection<MongoDB.Bson.BsonDocument>(collectionName);
        return new MongoCollectionQuery(collection);
    }

    /// <summary>
    /// Find one document using Dictionary-based filter (extension method for direct collection access)
    /// </summary>
    public static async Task<Dictionary<string, object>?> FindOneAsync(this IMongoCollection<MongoDB.Bson.BsonDocument> collection, Dictionary<string, object> filter, CancellationToken cancellationToken = default)
    {
        var query = new MongoCollectionQuery(collection);
        return await query.FindOneAsync(filter, cancellationToken);
    }

    /// <summary>
    /// Find many documents using Dictionary-based filter (extension method for direct collection access)
    /// </summary>
    public static async Task<List<Dictionary<string, object>>> FindManyAsync(this IMongoCollection<MongoDB.Bson.BsonDocument> collection, Dictionary<string, object> filter, CancellationToken cancellationToken = default)
    {
        var query = new MongoCollectionQuery(collection);
        return await query.FindManyAsync(filter, cancellationToken);
    }

    /// <summary>
    /// Aggregate using Dictionary-based pipeline (extension method for direct collection access)
    /// </summary>
    public static async Task<List<Dictionary<string, object>>> AggregateAsync(this IMongoCollection<MongoDB.Bson.BsonDocument> collection, List<Dictionary<string, object>> pipeline, CancellationToken cancellationToken = default)
    {
        var query = new MongoCollectionQuery(collection);
        return await query.AggregateAsync(pipeline, cancellationToken);
    }

    /// <summary>
    /// Find one entity using Dictionary-based filter (extension method for typed collections)
    /// </summary>
    public static async Task<TEntity?> FindOneAsync<TEntity>(this IMongoCollection<TEntity> collection, Dictionary<string, object> filter, CancellationToken cancellationToken = default) where TEntity : class
    {
        // Convert Dictionary filter to MongoDB filter
        var bsonFilter = new MongoDB.Bson.BsonDocument(filter);
        var result = await collection.Find(bsonFilter).FirstOrDefaultAsync(cancellationToken);
        return result;
    }

    /// <summary>
    /// Find many entities using Dictionary-based filter (extension method for typed collections)
    /// </summary>
    public static async Task<List<TEntity>> FindManyAsync<TEntity>(this IMongoCollection<TEntity> collection, Dictionary<string, object> filter, CancellationToken cancellationToken = default) where TEntity : class
    {
        // Convert Dictionary filter to MongoDB filter
        var bsonFilter = new MongoDB.Bson.BsonDocument(filter);
        var cursor = await collection.FindAsync(bsonFilter, cancellationToken: cancellationToken);
        return await cursor.ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Find one entity using lambda expression filter (extension method for typed collections)
    /// </summary>
    public static async Task<TEntity?> FindOneAsync<TEntity>(this IMongoCollection<TEntity> collection, System.Linq.Expressions.Expression<Func<TEntity, bool>> filter, CancellationToken cancellationToken = default) where TEntity : class
    {
        var result = await collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
        return result;
    }

    /// <summary>
    /// Find many entities using lambda expression filter (extension method for typed collections)
    /// </summary>
    public static async Task<List<TEntity>> FindManyAsync<TEntity>(this IMongoCollection<TEntity> collection, System.Linq.Expressions.Expression<Func<TEntity, bool>> filter, CancellationToken cancellationToken = default) where TEntity : class
    {
        var cursor = await collection.FindAsync(filter, cancellationToken: cancellationToken);
        return await cursor.ToListAsync(cancellationToken);
    }
}

/// <summary>
/// MongoDB-free interface for collection operations
/// Provides infrastructure services with complete abstraction from MongoDB types
/// </summary>
public interface IMongoCollectionQuery
{
    /// <summary>
    /// Find documents using Dictionary-based filter
    /// </summary>
    Task<List<Dictionary<string, object>>> FindAsync(Dictionary<string, object> filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find first document using Dictionary-based filter
    /// </summary>
    Task<Dictionary<string, object>?> FindFirstAsync(Dictionary<string, object> filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find one document using Dictionary-based filter (alias for FindFirstAsync)
    /// </summary>
    Task<Dictionary<string, object>?> FindOneAsync(Dictionary<string, object> filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find many documents using Dictionary-based filter (alias for FindAsync)
    /// </summary>
    Task<List<Dictionary<string, object>>> FindManyAsync(Dictionary<string, object> filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replace document using Dictionary-based filter and replacement
    /// </summary>
    Task<bool> ReplaceOneAsync(Dictionary<string, object> filter, Dictionary<string, object> replacement, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update multiple documents using Dictionary-based filter and update operations
    /// </summary>
    Task<long> UpdateManyAsync(Dictionary<string, object> filter, Dictionary<string, object> update, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete documents using Dictionary-based filter
    /// </summary>
    Task<long> DeleteManyAsync(Dictionary<string, object> filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Insert document using Dictionary-based document
    /// </summary>
    Task InsertOneAsync(Dictionary<string, object> document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Count documents using Dictionary-based filter
    /// </summary>
    Task<long> CountDocumentsAsync(Dictionary<string, object> filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if documents exist using Dictionary-based filter
    /// </summary>
    Task<bool> ExistsAsync(Dictionary<string, object> filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aggregate using Dictionary-based pipeline (List overload)
    /// </summary>
    Task<List<Dictionary<string, object>>> AggregateAsync(List<Dictionary<string, object>> pipeline, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aggregate using Dictionary-based pipeline (Array overload)
    /// </summary>
    Task<List<Dictionary<string, object>>> AggregateAsync(Dictionary<string, object>[] pipeline, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of MongoDB-free collection operations
/// Handles internal MongoDB conversion while exposing clean Dictionary-based API
/// </summary>
internal class MongoCollectionQuery : IMongoCollectionQuery
{
    private readonly IMongoCollection<MongoDB.Bson.BsonDocument> _collection;

    public MongoCollectionQuery(IMongoCollection<MongoDB.Bson.BsonDocument> collection)
    {
        _collection = collection;
    }

    public async Task<List<Dictionary<string, object>>> FindAsync(Dictionary<string, object> filter, CancellationToken cancellationToken = default)
    {
        var bsonFilter = new MongoDB.Bson.BsonDocument(filter);
        var cursor = await _collection.FindAsync(bsonFilter, cancellationToken: cancellationToken);
        var results = await cursor.ToListAsync(cancellationToken);
        return results.Select(ConvertBsonToDict).ToList();
    }

    public async Task<Dictionary<string, object>?> FindFirstAsync(Dictionary<string, object> filter, CancellationToken cancellationToken = default)
    {
        var bsonFilter = new MongoDB.Bson.BsonDocument(filter);
        var result = await _collection.Find(bsonFilter).FirstOrDefaultAsync(cancellationToken);
        return result != null ? ConvertBsonToDict(result) : null;
    }

    public async Task<Dictionary<string, object>?> FindOneAsync(Dictionary<string, object> filter, CancellationToken cancellationToken = default)
    {
        return await FindFirstAsync(filter, cancellationToken);
    }

    public async Task<List<Dictionary<string, object>>> FindManyAsync(Dictionary<string, object> filter, CancellationToken cancellationToken = default)
    {
        return await FindAsync(filter, cancellationToken);
    }

    public async Task<bool> ReplaceOneAsync(Dictionary<string, object> filter, Dictionary<string, object> replacement, CancellationToken cancellationToken = default)
    {
        var bsonFilter = new MongoDB.Bson.BsonDocument(filter);
        var bsonReplacement = new MongoDB.Bson.BsonDocument(replacement);
        var result = await _collection.ReplaceOneAsync(bsonFilter, bsonReplacement, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task<long> UpdateManyAsync(Dictionary<string, object> filter, Dictionary<string, object> update, CancellationToken cancellationToken = default)
    {
        var bsonFilter = new MongoDB.Bson.BsonDocument(filter);
        var bsonUpdate = new MongoDB.Bson.BsonDocument(update);
        var result = await _collection.UpdateManyAsync(bsonFilter, bsonUpdate, cancellationToken: cancellationToken);
        return result.ModifiedCount;
    }

    public async Task<long> DeleteManyAsync(Dictionary<string, object> filter, CancellationToken cancellationToken = default)
    {
        var bsonFilter = new MongoDB.Bson.BsonDocument(filter);
        var result = await _collection.DeleteManyAsync(bsonFilter, cancellationToken);
        return result.DeletedCount;
    }

    public async Task InsertOneAsync(Dictionary<string, object> document, CancellationToken cancellationToken = default)
    {
        var bsonDocument = new MongoDB.Bson.BsonDocument(document);
        await _collection.InsertOneAsync(bsonDocument, cancellationToken: cancellationToken);
    }

    public async Task<long> CountDocumentsAsync(Dictionary<string, object> filter, CancellationToken cancellationToken = default)
    {
        var bsonFilter = new MongoDB.Bson.BsonDocument(filter);
        return await _collection.CountDocumentsAsync(bsonFilter, cancellationToken: cancellationToken);
    }

    public async Task<bool> ExistsAsync(Dictionary<string, object> filter, CancellationToken cancellationToken = default)
    {
        var count = await CountDocumentsAsync(filter, cancellationToken);
        return count > 0;
    }

    public async Task<List<Dictionary<string, object>>> AggregateAsync(List<Dictionary<string, object>> pipeline, CancellationToken cancellationToken = default)
    {
        var bsonPipeline = pipeline.Select(dict => new MongoDB.Bson.BsonDocument(dict)).ToArray();
        var cursor = await _collection.AggregateAsync<MongoDB.Bson.BsonDocument>(bsonPipeline, cancellationToken: cancellationToken);
        var results = await cursor.ToListAsync(cancellationToken);
        return results.Select(ConvertBsonToDict).ToList();
    }

    public async Task<List<Dictionary<string, object>>> AggregateAsync(Dictionary<string, object>[] pipeline, CancellationToken cancellationToken = default)
    {
        var bsonPipeline = pipeline.Select(dict => new MongoDB.Bson.BsonDocument(dict)).ToArray();
        var cursor = await _collection.AggregateAsync<MongoDB.Bson.BsonDocument>(bsonPipeline, cancellationToken: cancellationToken);
        var results = await cursor.ToListAsync(cancellationToken);
        return results.Select(ConvertBsonToDict).ToList();
    }

    /// <summary>
    /// Converts BsonDocument to Dictionary without exposing MongoDB types (internal use only)
    /// </summary>
    private static Dictionary<string, object> ConvertBsonToDict(MongoDB.Bson.BsonDocument bsonDoc)
    {
        var dict = new Dictionary<string, object>();
        foreach (var element in bsonDoc.Elements)
        {
            dict[element.Name] = ConvertBsonValue(element.Value);
        }
        return dict;
    }

    /// <summary>
    /// Converts BsonValue to standard .NET object (internal use only)
    /// </summary>
    private static object ConvertBsonValue(MongoDB.Bson.BsonValue bsonValue)
    {
        return bsonValue.BsonType switch
        {
            MongoDB.Bson.BsonType.String => bsonValue.AsString,
            MongoDB.Bson.BsonType.Int32 => bsonValue.AsInt32,
            MongoDB.Bson.BsonType.Int64 => bsonValue.AsInt64,
            MongoDB.Bson.BsonType.Double => bsonValue.AsDouble,
            MongoDB.Bson.BsonType.Decimal128 => bsonValue.AsDecimal,
            MongoDB.Bson.BsonType.Boolean => bsonValue.AsBoolean,
            MongoDB.Bson.BsonType.DateTime => bsonValue.ToUniversalTime(),
            MongoDB.Bson.BsonType.ObjectId => bsonValue.AsObjectId.ToString(),
            MongoDB.Bson.BsonType.Array => bsonValue.AsBsonArray.Select(ConvertBsonValue).ToArray(),
            MongoDB.Bson.BsonType.Document => ConvertBsonToDict(bsonValue.AsBsonDocument),
            MongoDB.Bson.BsonType.Null => null!,
            _ => bsonValue.ToString()
        };
    }
}