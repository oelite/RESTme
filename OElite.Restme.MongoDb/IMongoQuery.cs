using System.Linq.Expressions;
using MongoDB.Driver;

namespace OElite.Restme.MongoDb;

/// <summary>
/// MongoDB query interface - equivalent to IRestmeDbQuery
/// </summary>
public interface IMongoQuery<T> where T : BaseEntity
{
    IMongoQuery<T> Query(string filter);
    IMongoQuery<T> Query(Expression<Func<T, bool>> filter);
    IMongoQuery<T> Query(Dictionary<string, object> filters);
    IMongoQuery<T> Params(object parameters);
    IMongoQuery<T> Paginated(int pageIndex, int pageSize, string? sort = null);
    IMongoQuery<T> Sort(string sortExpression);
    IMongoQuery<T> Sort(Expression<Func<T, object>> sortExpression, bool ascending = true);
    IMongoQuery<T> Limit(int limit);
    IMongoQuery<T> Skip(int skip);

    // Transaction support
    IMongoQuery<T> WithTransaction(IClientSessionHandle session);

    Task<TCollection> FetchAsync<TCollection>() where TCollection : EntityCollection<T>, new();
    Task<T?> FirstOrDefaultAsync();
    Task<List<T>> ToListAsync();
    Task<long> CountAsync();
    Task<bool> AnyAsync();
    Task<List<TResult>> AggregateAsync<TResult>(Dictionary<string, object>[] pipeline);

    // Write operations
    Task<T> InsertOneAsync(T document);
    Task<List<T>> InsertManyAsync(IEnumerable<T> documents);
    Task<UpdateResult> UpdateOneAsync(Dictionary<string, object> update);
    Task<UpdateResult> UpdateManyAsync(Dictionary<string, object> update);
    Task<DeleteResult> DeleteOneAsync();
    Task<DeleteResult> DeleteManyAsync();

    // Aggregation pipeline methods
    IMongoQuery<T> Pipeline(params object[] stages);

    IMongoQuery<T> Lookup<TForeign>(string foreignCollection, string localField, string foreignField,
        string aliasField);

    IMongoQuery<T> Match(Expression<Func<T, bool>> filter);
    IMongoQuery<T> Project<TProjection>(Expression<Func<T, TProjection>> projection);
    IMongoQuery<T> Group<TKey>(Expression<Func<T, TKey>> groupBy, Expression<Func<IGrouping<TKey, T>, object>> group);
    IMongoQuery<T> Unwind<TItem>(Expression<Func<T, IEnumerable<TItem>>> arrayField);
}