using System.Linq.Expressions;

namespace OElite.Restme.MongoDb;

/// <summary>
/// Provider-neutral server-side query options for a typed collection.
/// Kept intentionally narrow: expression-based ordering with optional paging.
/// Driver types remain private to the provider implementation.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public sealed class MongoDbQueryOptions<T> where T : BaseEntity
{
    /// <summary>Expression-based sort definition (ascending unless <see cref="Ascending"/> is false).</summary>
    public Expression<Func<T, object>>? SortExpression { get; set; }

    /// <summary>Whether the sort should be ascending. Defaults to true.</summary>
    public bool Ascending { get; set; } = true;

    /// <summary>Number of documents to skip before returning results.</summary>
    public int Skip { get; set; }

    /// <summary>Maximum number of documents to return. Zero means no limit.</summary>
    public int Limit { get; set; }

    /// <summary>Creates options for ascending ordering by an expression.</summary>
    public static MongoDbQueryOptions<T> OrderBy(Expression<Func<T, object>> sortExpression) =>
        new() { SortExpression = sortExpression, Ascending = true };

    /// <summary>Creates options for descending ordering by an expression.</summary>
    public static MongoDbQueryOptions<T> OrderByDescending(Expression<Func<T, object>> sortExpression) =>
        new() { SortExpression = sortExpression, Ascending = false };

    /// <summary>Adds paging to the options.</summary>
    public MongoDbQueryOptions<T> Page(int skip, int limit)
    {
        Skip = skip;
        Limit = limit;
        return this;
    }

    /// <summary>Adds ascending ordering to the options.</summary>
    public MongoDbQueryOptions<T> ThenBy(Expression<Func<T, object>> sortExpression)
    {
        SortExpression = sortExpression;
        Ascending = true;
        return this;
    }

    /// <summary>Adds descending ordering to the options.</summary>
    public MongoDbQueryOptions<T> ThenByDescending(Expression<Func<T, object>> sortExpression)
    {
        SortExpression = sortExpression;
        Ascending = false;
        return this;
    }
}

/// <summary>Typed paged result. Never exposes MongoDB driver types.</summary>
/// <typeparam name="T">The entity type.</typeparam>
public sealed class MongoDbPagedResult<T> where T : BaseEntity
{
    /// <summary>Documents on the requested page.</summary>
    public IReadOnlyList<T> Documents { get; init; } = Array.Empty<T>();

    /// <summary>Total number of documents matching the filter (before paging).</summary>
    public long TotalCount { get; init; }

    /// <summary>Zero-based offset represented by this result.</summary>
    public int Skip { get; init; }

    /// <summary>Zero-based page index when Skip is page-aligned; otherwise zero.</summary>
    public int PageIndex { get; init; }

    /// <summary>Number of documents requested per page.</summary>
    public int PageSize { get; init; }

    /// <summary>True if there is at least one more matching document after this page.</summary>
    public bool HasNextPage => PageSize > 0 && Skip + Documents.Count < TotalCount;
}

/// <summary>
/// Provider-neutral result of an ordered bulk insert operation. Never exposes MongoDB driver types.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public sealed record MongoDbBulkInsertResult<T>(int InsertedCount, int DuplicateCount, List<T> Duplicates)
    where T : BaseEntity
{
    /// <summary>True if every document was inserted with no duplicates.</summary>
    public bool AllInserted => DuplicateCount == 0;

    /// <summary>True if at least one document was inserted.</summary>
    public bool AnyInserted => InsertedCount > 0;

    /// <summary>True if any duplicate key conflicts occurred.</summary>
    public bool AnyDuplicates => DuplicateCount > 0;
}
