using System.Linq.Expressions;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace OElite.Restme.MongoDb;

/// <summary>
/// Extension methods for IMongoCollection to provide fluent querying capabilities for any entity type
/// These extensions work with entities that don't require BaseEntity inheritance, making them suitable for
/// any entities, DTOs, and other domain-specific models
/// </summary>
public static class DbCollectionExtensions
{
    /// <summary>
    /// Replace or insert document (upsert operation)
    /// </summary>
    public static async Task ReplaceAsync<T>(this IMongoCollection<T> collection, T document, bool isUpsert = false)
    {
        // Assume the document has an "Id" property for filtering
        var idProperty = typeof(T).GetProperty("Id");
        if (idProperty == null)
        {
            throw new InvalidOperationException($"Type {typeof(T).Name} must have an 'Id' property for ReplaceAsync operation");
        }

        var idValue = idProperty.GetValue(document);
        var filter = Builders<T>.Filter.Eq("_id", idValue);

        await collection.ReplaceOneAsync(filter, document, new ReplaceOptions { IsUpsert = isUpsert });
    }

    /// <summary>
    /// Add Where filter using LINQ expression
    /// </summary>
    public static IMongoQueryable<T> Where<T>(this IMongoCollection<T> collection, Expression<Func<T, bool>> filter)
    {
        return collection.AsQueryable().Where(filter);
    }

    /// <summary>
    /// Add OrderBy using LINQ expression
    /// </summary>
    public static IOrderedMongoQueryable<T> OrderBy<T, TKey>(this IMongoCollection<T> collection, Expression<Func<T, TKey>> keySelector)
    {
        return collection.AsQueryable().OrderBy(keySelector);
    }

    /// <summary>
    /// Add OrderByDescending using LINQ expression
    /// </summary>
    public static IOrderedMongoQueryable<T> OrderByDescending<T, TKey>(this IMongoCollection<T> collection, Expression<Func<T, TKey>> keySelector)
    {
        return collection.AsQueryable().OrderByDescending(keySelector);
    }

    /// <summary>
    /// Delete document by filter expression
    /// </summary>
    public static async Task<DeleteResult> DeleteAsync<T>(this IMongoCollection<T> collection, Expression<Func<T, bool>> filter)
    {
        var filterDefinition = Builders<T>.Filter.Where(filter);
        return await collection.DeleteOneAsync(filterDefinition);
    }

    /// <summary>
    /// Get first or default result using LINQ expression filter
    /// </summary>
    public static async Task<T?> FirstOrDefaultAsync<T>(this IMongoCollection<T> collection, Expression<Func<T, bool>> filter)
    {
        return await collection.AsQueryable().Where(filter).FirstOrDefaultAsync();
    }

    /// <summary>
    /// Count documents using LINQ expression filter
    /// </summary>
    public static async Task<long> CountAsync<T>(this IMongoCollection<T> collection, Expression<Func<T, bool>> filter)
    {
        return await collection.CountDocumentsAsync(Builders<T>.Filter.Where(filter));
    }

    /// <summary>
    /// Count all documents
    /// </summary>
    public static async Task<long> CountAsync<T>(this IMongoCollection<T> collection)
    {
        return await collection.CountDocumentsAsync(Builders<T>.Filter.Empty);
    }
}