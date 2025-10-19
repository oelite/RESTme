using OElite.Common;

namespace OElite.Restme.MongoDb;

/// <summary>
/// MongoDB-free collection interface that completely abstracts database operations
/// Provides all necessary operations without exposing any MongoDB types
/// </summary>
public interface IMongoDbCollection
{
    /// <summary>
    /// Find documents using MongoDbDocument filter
    /// </summary>
    Task<List<MongoDbDocument>> FindAsync(MongoDbDocument filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find first document using MongoDbDocument filter
    /// </summary>
    Task<MongoDbDocument?> FindOneAsync(MongoDbDocument filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find documents using Dictionary-based filter
    /// </summary>
    Task<List<Dictionary<string, object>>> FindAsync(Dictionary<string, object> filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find first document using Dictionary-based filter
    /// </summary>
    Task<Dictionary<string, object>?> FindOneAsync(Dictionary<string, object> filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replace document using MongoDbDocument filter and replacement
    /// </summary>
    Task<bool> ReplaceOneAsync(MongoDbDocument filter, MongoDbDocument replacement, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replace document using Dictionary-based filter and replacement
    /// </summary>
    Task<bool> ReplaceOneAsync(Dictionary<string, object> filter, Dictionary<string, object> replacement, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update multiple documents using MongoDbDocument filter and update operations
    /// </summary>
    Task<long> UpdateManyAsync(MongoDbDocument filter, MongoDbDocument update, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update multiple documents using Dictionary-based filter and update operations
    /// </summary>
    Task<long> UpdateManyAsync(Dictionary<string, object> filter, Dictionary<string, object> update, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete documents using MongoDbDocument filter
    /// </summary>
    Task<long> DeleteManyAsync(MongoDbDocument filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete documents using Dictionary-based filter
    /// </summary>
    Task<long> DeleteManyAsync(Dictionary<string, object> filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Insert document using MongoDbDocument
    /// </summary>
    Task InsertOneAsync(MongoDbDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Insert document using Dictionary-based document
    /// </summary>
    Task InsertOneAsync(Dictionary<string, object> document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Count documents using MongoDbDocument filter
    /// </summary>
    Task<long> CountDocumentsAsync(MongoDbDocument filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Count documents using Dictionary-based filter
    /// </summary>
    Task<long> CountDocumentsAsync(Dictionary<string, object> filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if documents exist using MongoDbDocument filter
    /// </summary>
    Task<bool> ExistsAsync(MongoDbDocument filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if documents exist using Dictionary-based filter
    /// </summary>
    Task<bool> ExistsAsync(Dictionary<string, object> filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aggregate using MongoDbDocument pipeline
    /// </summary>
    Task<List<MongoDbDocument>> AggregateAsync(List<MongoDbDocument> pipeline, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aggregate using Dictionary-based pipeline
    /// </summary>
    Task<List<Dictionary<string, object>>> AggregateAsync(List<Dictionary<string, object>> pipeline, CancellationToken cancellationToken = default);
}

/// <summary>
/// MongoDB-free typed collection interface for strongly-typed operations
/// </summary>
public interface IMongoDbCollection<T> where T : BaseEntity
{
    /// <summary>
    /// Find documents using lambda expression filter
    /// </summary>
    Task<List<T>> FindAsync(System.Linq.Expressions.Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find first document using lambda expression filter
    /// </summary>
    Task<T?> FindOneAsync(System.Linq.Expressions.Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find documents using Dictionary-based filter
    /// </summary>
    Task<List<T>> FindAsync(Dictionary<string, object> filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find first document using Dictionary-based filter
    /// </summary>
    Task<T?> FindOneAsync(Dictionary<string, object> filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replace document
    /// </summary>
    Task<bool> ReplaceOneAsync(System.Linq.Expressions.Expression<Func<T, bool>> filter, T replacement, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete documents using lambda expression filter
    /// </summary>
    Task<long> DeleteManyAsync(System.Linq.Expressions.Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Insert document
    /// </summary>
    Task InsertOneAsync(T document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Count documents using lambda expression filter
    /// </summary>
    Task<long> CountDocumentsAsync(System.Linq.Expressions.Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if documents exist using lambda expression filter
    /// </summary>
    Task<bool> ExistsAsync(System.Linq.Expressions.Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default);
}