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
    /// Atomically inserts a document using a unique index. Returns DuplicateKey for any unique-index conflict.
    /// Requires a unique index for the caller's idempotency/identity key. Does not read before inserting.
    /// OutcomeUnknown requires reconciliation before retrying. Existing InsertOneAsync is unchanged.
    /// </summary>
    Task<MongoDbInsertResult<T>> InsertOneIfAbsentAsync(
        T document,
        CancellationToken cancellationToken = default)
        => throw new MongoDbWriteException(MongoDbWriteFailure.Unsupported);

    /// <summary>
    /// Atomically replaces the document identified by replacement.Id only if its persisted long version equals
    /// expectedVersion and condition (when supplied) matches. Persists expectedVersion + 1 in the same write.
    /// version must select a direct, writable, serialized top-level long property. Versions must be nonnegative
    /// and less than long.MaxValue. The replacement is snapshotted; neither winner nor loser mutates the caller's version.
    /// An acknowledged zero-match returns generic Conflict because this atomic operation cannot safely distinguish
    /// a missing identity from a stale version or failed condition. Updated means one acknowledged write with
    /// VersionAfter == expectedVersion + 1. All writers to this aggregate must use this contract. Do not delete/recreate
    /// an identity or reset its version.
    /// This is single-record atomicity only, not multi-document transaction atomicity.
    /// </summary>
    Task<MongoDbCasResult<T>> ReplaceOneIfVersionAsync(
        T replacement,
        System.Linq.Expressions.Expression<Func<T, long>> version,
        long expectedVersion,
        System.Linq.Expressions.Expression<Func<T, bool>>? condition = null,
        CancellationToken cancellationToken = default)
        => throw new MongoDbWriteException(MongoDbWriteFailure.Unsupported);

    /// <summary>
    /// Version-aware request form of ReplaceOneIfVersionAsync. Use this when the caller has a provider-owned
    /// version property name instead of a lambda. It performs the same single-record CAS and never uses an
    /// unversioned update.
    /// </summary>
    Task<MongoDbCasResult<T>> ReplaceOneIfVersionAsync(
        MongoDbVersionedWrite<T> request,
        CancellationToken cancellationToken = default)
        => throw new MongoDbWriteException(MongoDbWriteFailure.Unsupported);

    /// <summary>
    /// Executes an operation within a provider-managed transaction/session. This collection exposes the contract only;
    /// the current MongoDB implementation explicitly does not support multi-document transactions. Callers must use
    /// ReplaceOneIfVersionAsync for a single aggregate and fail closed before writing when multi-document atomicity is required.
    /// </summary>
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
        => throw new MongoDbWriteException(MongoDbWriteFailure.Unsupported);

    /// <summary>
    /// Multi-document transactions are not supported by this provider. Use ReplaceOneIfVersionAsync as the reviewed
    /// single-document CAS fallback; it is not multi-document atomicity.
    /// </summary>
    MongoDbTransactionSupport TransactionSupport => MongoDbTransactionSupport.Unsupported;

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
    /// Replace document using lambda expression filter
    /// </summary>
    Task<bool> ReplaceOneAsync(System.Linq.Expressions.Expression<Func<T, bool>> filter, T replacement, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replace document using Dictionary-based filter
    /// </summary>
    Task<bool> ReplaceOneAsync(Dictionary<string, object> filter, T replacement, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update single document using lambda expression filter
    /// </summary>
    Task<bool> UpdateOneAsync(System.Linq.Expressions.Expression<Func<T, bool>> filter, Dictionary<string, object> update, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update single document using Dictionary-based filter
    /// </summary>
    Task<bool> UpdateOneAsync(Dictionary<string, object> filter, Dictionary<string, object> update, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update multiple documents using lambda expression filter
    /// </summary>
    Task<long> UpdateManyAsync(System.Linq.Expressions.Expression<Func<T, bool>> filter, Dictionary<string, object> update, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update multiple documents using Dictionary-based filter
    /// </summary>
    Task<long> UpdateManyAsync(Dictionary<string, object> filter, Dictionary<string, object> update, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete documents using lambda expression filter
    /// </summary>
    Task<long> DeleteManyAsync(System.Linq.Expressions.Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete documents using Dictionary-based filter
    /// </summary>
    Task<long> DeleteManyAsync(Dictionary<string, object> filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete single document using lambda expression filter
    /// </summary>
    Task<long> DeleteOneAsync(System.Linq.Expressions.Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete single document using Dictionary-based filter
    /// </summary>
    Task<long> DeleteOneAsync(Dictionary<string, object> filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Insert document
    /// </summary>
    Task InsertOneAsync(T document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Count documents using lambda expression filter
    /// </summary>
    Task<long> CountDocumentsAsync(System.Linq.Expressions.Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Count documents using Dictionary-based filter
    /// </summary>
    Task<long> CountDocumentsAsync(Dictionary<string, object> filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if documents exist using lambda expression filter
    /// </summary>
    Task<bool> ExistsAsync(System.Linq.Expressions.Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if documents exist using Dictionary-based filter
    /// </summary>
    Task<bool> ExistsAsync(Dictionary<string, object> filter, CancellationToken cancellationToken = default);
}