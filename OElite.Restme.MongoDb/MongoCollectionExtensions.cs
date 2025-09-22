using System.Linq.Expressions;
using MongoDB.Bson;
using MongoDB.Driver;
using OElite.Common;

namespace OElite.Restme.MongoDb;

/// <summary>
/// Extension methods for IMongoCollection to provide fluent querying capabilities
/// </summary>
public static class MongoCollectionExtensions
{
    /// <summary>
    /// Creates a fluent query builder for the collection
    /// </summary>
    public static IMongoQuery<T> CreateQuery<T>(this IMongoCollection<T> collection) where T : BaseEntity
    {
        return new MongoQuery<T>(collection);
    }

    /// <summary>
    /// Creates an aggregation query builder for the collection
    /// </summary>
    public static MongoAggregationQueryBuilder<T> CreateAggregation<T>(this IMongoCollection<T> collection) where T : BaseEntity
    {
        return new MongoAggregationQueryBuilder<T>(collection);
    }

    /// <summary>
    /// Finds documents matching the specified filter expression
    /// </summary>
    public static IFindFluent<T, T> Find<T>(this IMongoCollection<T> collection, Expression<Func<T, bool>> filter) where T : BaseEntity
    {
        return collection.Find(Builders<T>.Filter.Where(filter));
    }

    /// <summary>
    /// Finds documents matching the specified filter expression with options
    /// </summary>
    public static IFindFluent<T, T> Find<T>(this IMongoCollection<T> collection, Expression<Func<T, bool>> filter, FindOptions options) where T : BaseEntity
    {
        return collection.Find(Builders<T>.Filter.Where(filter), options);
    }

    /// <summary>
    /// Finds documents matching the specified filter expression with session
    /// </summary>
    public static IFindFluent<T, T> Find<T>(this IMongoCollection<T> collection, IClientSessionHandle session, Expression<Func<T, bool>> filter) where T : BaseEntity
    {
        return collection.Find(session, Builders<T>.Filter.Where(filter));
    }

    /// <summary>
    /// Finds documents matching the specified filter expression with session and options
    /// </summary>
    public static IFindFluent<T, T> Find<T>(this IMongoCollection<T> collection, IClientSessionHandle session, Expression<Func<T, bool>> filter, FindOptions options) where T : BaseEntity
    {
        return collection.Find(session, Builders<T>.Filter.Where(filter), options);
    }

    /// <summary>
    /// Finds documents matching the specified filter expression and returns them as a list
    /// </summary>
    public static async Task<List<T>> FindAsync<T>(this IMongoCollection<T> collection, Expression<Func<T, bool>> filter) where T : BaseEntity
    {
        var cursor = await collection.FindAsync(Builders<T>.Filter.Where(filter));
        return await cursor.ToListAsync();
    }

    /// <summary>
    /// Finds documents matching the specified filter expression with session and returns them as a list
    /// </summary>
    public static async Task<List<T>> FindAsync<T>(this IMongoCollection<T> collection, IClientSessionHandle session, Expression<Func<T, bool>> filter) where T : BaseEntity
    {
        var cursor = await collection.FindAsync(session, Builders<T>.Filter.Where(filter));
        return await cursor.ToListAsync();
    }

    /// <summary>
    /// Finds the first document matching the specified filter expression
    /// </summary>
    public static async Task<T?> FirstOrDefaultAsync<T>(this IMongoCollection<T> collection, Expression<Func<T, bool>> filter) where T : BaseEntity
    {
        var cursor = await collection.FindAsync(Builders<T>.Filter.Where(filter));
        return await cursor.FirstOrDefaultAsync();
    }

    /// <summary>
    /// Finds the first document matching the specified filter expression with session
    /// </summary>
    public static async Task<T?> FirstOrDefaultAsync<T>(this IMongoCollection<T> collection, IClientSessionHandle session, Expression<Func<T, bool>> filter) where T : BaseEntity
    {
        var cursor = await collection.FindAsync(session, Builders<T>.Filter.Where(filter));
        return await cursor.FirstOrDefaultAsync();
    }

    /// <summary>
    /// Counts documents matching the specified filter expression
    /// </summary>
    public static async Task<long> CountDocumentsAsync<T>(this IMongoCollection<T> collection, Expression<Func<T, bool>> filter) where T : BaseEntity
    {
        return await collection.CountDocumentsAsync(Builders<T>.Filter.Where(filter));
    }

    /// <summary>
    /// Counts documents matching the specified filter expression with session
    /// </summary>
    public static async Task<long> CountDocumentsAsync<T>(this IMongoCollection<T> collection, IClientSessionHandle session, Expression<Func<T, bool>> filter) where T : BaseEntity
    {
        return await collection.CountDocumentsAsync(session, Builders<T>.Filter.Where(filter));
    }

    /// <summary>
    /// Checks if any documents match the specified filter expression
    /// </summary>
    public static async Task<bool> AnyAsync<T>(this IMongoCollection<T> collection, Expression<Func<T, bool>> filter) where T : BaseEntity
    {
        return await collection.CountDocumentsAsync(Builders<T>.Filter.Where(filter)) > 0;
    }

    /// <summary>
    /// Checks if any documents match the specified filter expression with session
    /// </summary>
    public static async Task<bool> AnyAsync<T>(this IMongoCollection<T> collection, IClientSessionHandle session, Expression<Func<T, bool>> filter) where T : BaseEntity
    {
        return await collection.CountDocumentsAsync(session, Builders<T>.Filter.Where(filter)) > 0;
    }

    /// <summary>
    /// Deletes documents matching the specified filter expression
    /// </summary>
    public static async Task<DeleteResult> DeleteManyAsync<T>(this IMongoCollection<T> collection, Expression<Func<T, bool>> filter) where T : BaseEntity
    {
        return await collection.DeleteManyAsync(Builders<T>.Filter.Where(filter));
    }

    /// <summary>
    /// Deletes documents matching the specified filter expression with session
    /// </summary>
    public static async Task<DeleteResult> DeleteManyAsync<T>(this IMongoCollection<T> collection, IClientSessionHandle session, Expression<Func<T, bool>> filter) where T : BaseEntity
    {
        return await collection.DeleteManyAsync(session, Builders<T>.Filter.Where(filter));
    }

    /// <summary>
    /// Deletes the first document matching the specified filter expression
    /// </summary>
    public static async Task<DeleteResult> DeleteOneAsync<T>(this IMongoCollection<T> collection, Expression<Func<T, bool>> filter) where T : BaseEntity
    {
        return await collection.DeleteOneAsync(Builders<T>.Filter.Where(filter));
    }

    /// <summary>
    /// Deletes the first document matching the specified filter expression with session
    /// </summary>
    public static async Task<DeleteResult> DeleteOneAsync<T>(this IMongoCollection<T> collection, IClientSessionHandle session, Expression<Func<T, bool>> filter) where T : BaseEntity
    {
        return await collection.DeleteOneAsync(session, Builders<T>.Filter.Where(filter));
    }

    /// <summary>
    /// Updates documents matching the specified filter expression
    /// </summary>
    public static async Task<UpdateResult> UpdateManyAsync<T>(this IMongoCollection<T> collection, Expression<Func<T, bool>> filter, UpdateDefinition<T> update) where T : BaseEntity
    {
        return await collection.UpdateManyAsync(Builders<T>.Filter.Where(filter), update);
    }

    /// <summary>
    /// Updates documents matching the specified filter expression with session
    /// </summary>
    public static async Task<UpdateResult> UpdateManyAsync<T>(this IMongoCollection<T> collection, IClientSessionHandle session, Expression<Func<T, bool>> filter, UpdateDefinition<T> update) where T : BaseEntity
    {
        return await collection.UpdateManyAsync(session, Builders<T>.Filter.Where(filter), update);
    }

    /// <summary>
    /// Updates the first document matching the specified filter expression
    /// </summary>
    public static async Task<UpdateResult> UpdateOneAsync<T>(this IMongoCollection<T> collection, Expression<Func<T, bool>> filter, UpdateDefinition<T> update) where T : BaseEntity
    {
        return await collection.UpdateOneAsync(Builders<T>.Filter.Where(filter), update);
    }

    /// <summary>
    /// Updates the first document matching the specified filter expression with session
    /// </summary>
    public static async Task<UpdateResult> UpdateOneAsync<T>(this IMongoCollection<T> collection, IClientSessionHandle session, Expression<Func<T, bool>> filter, UpdateDefinition<T> update) where T : BaseEntity
    {
        return await collection.UpdateOneAsync(session, Builders<T>.Filter.Where(filter), update);
    }

    /// <summary>
    /// Replaces the first document matching the specified filter expression
    /// </summary>
    public static async Task<ReplaceOneResult> ReplaceOneAsync<T>(this IMongoCollection<T> collection, Expression<Func<T, bool>> filter, T replacement) where T : BaseEntity
    {
        return await collection.ReplaceOneAsync(Builders<T>.Filter.Where(filter), replacement);
    }

    /// <summary>
    /// Replaces the first document matching the specified filter expression with session
    /// </summary>
    public static async Task<ReplaceOneResult> ReplaceOneAsync<T>(this IMongoCollection<T> collection, IClientSessionHandle session, Expression<Func<T, bool>> filter, T replacement) where T : BaseEntity
    {
        return await collection.ReplaceOneAsync(session, Builders<T>.Filter.Where(filter), replacement);
    }

    /// <summary>
    /// Finds documents and returns them as a list with sorting
    /// </summary>
    public static async Task<List<T>> FindAsync<T>(this IMongoCollection<T> collection, Expression<Func<T, bool>> filter, Expression<Func<T, object>> sortBy, bool ascending = true) where T : BaseEntity
    {
        var sortDefinition = ascending 
            ? Builders<T>.Sort.Ascending(sortBy)
            : Builders<T>.Sort.Descending(sortBy);
            
        var cursor = await collection.FindAsync(Builders<T>.Filter.Where(filter), new FindOptions<T> { Sort = sortDefinition });
        return await cursor.ToListAsync();
    }

    /// <summary>
    /// Finds documents and returns them as a list with sorting and limiting
    /// </summary>
    public static async Task<List<T>> FindAsync<T>(this IMongoCollection<T> collection, Expression<Func<T, bool>> filter, Expression<Func<T, object>> sortBy, bool ascending, int limit) where T : BaseEntity
    {
        var sortDefinition = ascending 
            ? Builders<T>.Sort.Ascending(sortBy)
            : Builders<T>.Sort.Descending(sortBy);
            
        var cursor = await collection.FindAsync(Builders<T>.Filter.Where(filter), new FindOptions<T> { Sort = sortDefinition, Limit = limit });
        return await cursor.ToListAsync();
    }

    /// <summary>
    /// Finds documents and returns them as a list with pagination
    /// </summary>
    public static async Task<List<T>> FindAsync<T>(this IMongoCollection<T> collection, Expression<Func<T, bool>> filter, int skip, int limit) where T : BaseEntity
    {
        var cursor = await collection.FindAsync(Builders<T>.Filter.Where(filter), new FindOptions<T> { Skip = skip, Limit = limit });
        return await cursor.ToListAsync();
    }

    /// <summary>
    /// Finds documents and returns them as a list with pagination and sorting
    /// </summary>
    public static async Task<List<T>> FindAsync<T>(this IMongoCollection<T> collection, Expression<Func<T, bool>> filter, Expression<Func<T, object>> sortBy, bool ascending, int skip, int limit) where T : BaseEntity
    {
        var sortDefinition = ascending 
            ? Builders<T>.Sort.Ascending(sortBy)
            : Builders<T>.Sort.Descending(sortBy);
            
        var cursor = await collection.FindAsync(Builders<T>.Filter.Where(filter), new FindOptions<T> { Sort = sortDefinition, Skip = skip, Limit = limit });
        return await cursor.ToListAsync();
    }

    /// <summary>
    /// Creates a text search index on the specified field
    /// </summary>
    public static async Task<string> CreateTextIndexAsync<T>(this IMongoCollection<T> collection, Expression<Func<T, object>> field) where T : BaseEntity
    {
        var fieldDefinition = new ExpressionFieldDefinition<T, object>(field);
        var indexModel = new CreateIndexModel<T>(Builders<T>.IndexKeys.Text(fieldDefinition));
        return await collection.Indexes.CreateOneAsync(indexModel);
    }

}
