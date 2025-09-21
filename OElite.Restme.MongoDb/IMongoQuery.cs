using System.Linq.Expressions;
using MongoDB.Bson;
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

    Task<TCollection> FetchAsync<TCollection>() where TCollection : BaseEntityCollection<T>, new();
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

/// <summary>
/// MongoDB query implementation
/// </summary>
public class MongoQuery<T> : IMongoQuery<T> where T : BaseEntity
{
    private readonly IMongoCollection<T> _collection;
    private readonly List<FilterDefinition<T>> _filters = new();
    private readonly List<SortDefinition<T>> _sorts = new();
    private readonly List<object> _pipelineStages = new();
    private readonly Dictionary<string, object> _parameters = new();
    private int? _limit;
    private int? _skip;
    private bool _useAggregation = false;
    private IClientSessionHandle? _session;

    public MongoQuery(IMongoCollection<T> collection)
    {
        _collection = collection;
    }

    public IMongoQuery<T> Query(string filter)
    {
        var mongoFilter = ConvertStringFilterToMongoFilter(filter);
        _filters.Add(mongoFilter);
        return this;
    }

    public IMongoQuery<T> Query(Expression<Func<T, bool>> filter)
    {
        _filters.Add(Builders<T>.Filter.Where(filter));
        return this;
    }

    public IMongoQuery<T> Query(Dictionary<string, object> filters)
    {
        var mongoFilters = ConvertDictionaryToMongoFilters(filters);
        _filters.AddRange(mongoFilters);
        return this;
    }

    public IMongoQuery<T> Params(object parameters)
    {
        if (parameters != null)
        {
            var properties = parameters.GetType().GetProperties();
            foreach (var prop in properties)
            {
                _parameters[prop.Name] = prop.GetValue(parameters);
            }
        }

        return this;
    }

    public IMongoQuery<T> Paginated(int pageIndex, int pageSize, string? sort = null)
    {
        _skip = pageIndex * pageSize;
        _limit = pageSize;

        if (!string.IsNullOrEmpty(sort))
        {
            Sort(sort);
        }

        return this;
    }

    public IMongoQuery<T> Sort(string sortExpression)
    {
        var sortDefinitions = ParseSortExpression(sortExpression);
        _sorts.AddRange(sortDefinitions);
        return this;
    }

    public IMongoQuery<T> Sort(Expression<Func<T, object>> sortExpression, bool ascending = true)
    {
        var fieldDefinition = new ExpressionFieldDefinition<T, object>(sortExpression);
        if (ascending)
        {
            _sorts.Add(Builders<T>.Sort.Ascending(fieldDefinition));
        }
        else
        {
            _sorts.Add(Builders<T>.Sort.Descending(fieldDefinition));
        }

        return this;
    }

    public IMongoQuery<T> Limit(int limit)
    {
        _limit = limit;
        return this;
    }

    public IMongoQuery<T> Skip(int skip)
    {
        _skip = skip;
        return this;
    }

    public IMongoQuery<T> WithTransaction(IClientSessionHandle session)
    {
        _session = session;
        return this;
    }

    public async Task<TCollection> FetchAsync<TCollection>() where TCollection : BaseEntityCollection<T>, new()
    {
        var results = await ToListAsync();
        var collection = new TCollection();
        collection.AddRange(results);
        return collection;
    }

    public async Task<T?> FirstOrDefaultAsync()
    {
        var options = new FindOptions<T>
        {
            Limit = 1
        };

        if (_skip.HasValue)
            options.Skip = _skip.Value;

        var combinedFilter = CombineFilters();
        var combinedSort = CombineSorts();

        if (combinedSort != null)
            options.Sort = combinedSort;

        if (_session != null)
        {
            var cursor = await _collection.FindAsync(_session, combinedFilter, options);
            return await cursor.FirstOrDefaultAsync();
        }
        else
        {
            var cursor = await _collection.FindAsync(combinedFilter, options);
            return await cursor.FirstOrDefaultAsync();
        }
    }

    public async Task<List<T>> ToListAsync()
    {
        var options = new FindOptions<T>();

        if (_limit.HasValue)
            options.Limit = _limit.Value;
        if (_skip.HasValue)
            options.Skip = _skip.Value;

        var combinedSort = CombineSorts();
        if (combinedSort != null)
            options.Sort = combinedSort;

        var combinedFilter = CombineFilters();

        if (_session != null)
        {
            var cursor = await _collection.FindAsync(_session, combinedFilter, options);
            return await cursor.ToListAsync();
        }
        else
        {
            var cursor = await _collection.FindAsync(combinedFilter, options);
            return await cursor.ToListAsync();
        }
    }

    public async Task<long> CountAsync()
    {
        var combinedFilter = CombineFilters();
        var options = new CountOptions();

        if (_session != null)
        {
            return await _collection.CountDocumentsAsync(_session, combinedFilter, options);
        }
        else
        {
            return await _collection.CountDocumentsAsync(combinedFilter, options);
        }
    }

    public async Task<bool> AnyAsync()
    {
        return await CountAsync() > 0;
    }

    public async Task<List<TResult>> AggregateAsync<TResult>(Dictionary<string, object>[] pipeline)
    {
        var bsonPipeline = pipeline.Select(stage =>
            new BsonDocument(stage.Select(kvp => new BsonElement(kvp.Key, BsonValue.Create(kvp.Value))))).ToArray();
        var aggregationPipeline = PipelineDefinition<T, TResult>.Create(bsonPipeline);

        if (_session != null)
        {
            var cursor = await _collection.AggregateAsync(_session, aggregationPipeline);
            return await cursor.ToListAsync();
        }
        else
        {
            var cursor = await _collection.AggregateAsync(aggregationPipeline);
            return await cursor.ToListAsync();
        }
    }

    // Write operations
    public async Task<T> InsertOneAsync(T document)
    {
        if (_session != null)
        {
            await _collection.InsertOneAsync(_session, document);
        }
        else
        {
            await _collection.InsertOneAsync(document);
        }

        return document;
    }

    public async Task<List<T>> InsertManyAsync(IEnumerable<T> documents)
    {
        var documentList = documents.ToList();
        if (_session != null)
        {
            await _collection.InsertManyAsync(_session, documentList);
        }
        else
        {
            await _collection.InsertManyAsync(documentList);
        }

        return documentList;
    }

    public async Task<UpdateResult> UpdateOneAsync(Dictionary<string, object> update)
    {
        var combinedFilter = CombineFilters();
        var updateBuilder = Builders<T>.Update;
        var updateDefinitions = new List<UpdateDefinition<T>>();

        foreach (var kvp in update)
        {
            updateDefinitions.Add(updateBuilder.Set(kvp.Key, kvp.Value));
        }

        var updateDef = updateBuilder.Combine(updateDefinitions);

        if (_session != null)
        {
            return await _collection.UpdateOneAsync(_session, combinedFilter, updateDef);
        }
        else
        {
            return await _collection.UpdateOneAsync(combinedFilter, updateDef);
        }
    }

    public async Task<UpdateResult> UpdateManyAsync(Dictionary<string, object> update)
    {
        var combinedFilter = CombineFilters();
        var updateBuilder = Builders<T>.Update;
        var updateDefinitions = new List<UpdateDefinition<T>>();

        foreach (var kvp in update)
        {
            updateDefinitions.Add(updateBuilder.Set(kvp.Key, kvp.Value));
        }

        var updateDef = updateBuilder.Combine(updateDefinitions);

        if (_session != null)
        {
            return await _collection.UpdateManyAsync(_session, combinedFilter, updateDef);
        }
        else
        {
            return await _collection.UpdateManyAsync(combinedFilter, updateDef);
        }
    }

    public async Task<DeleteResult> DeleteOneAsync()
    {
        var combinedFilter = CombineFilters();

        if (_session != null)
        {
            return await _collection.DeleteOneAsync(_session, combinedFilter);
        }
        else
        {
            return await _collection.DeleteOneAsync(combinedFilter);
        }
    }

    public async Task<DeleteResult> DeleteManyAsync()
    {
        var combinedFilter = CombineFilters();

        if (_session != null)
        {
            return await _collection.DeleteManyAsync(_session, combinedFilter);
        }
        else
        {
            return await _collection.DeleteManyAsync(combinedFilter);
        }
    }

    // Aggregation pipeline methods
    public IMongoQuery<T> Pipeline(params object[] stages)
    {
        _useAggregation = true;
        _pipelineStages.AddRange(stages);
        return this;
    }

    public IMongoQuery<T> Lookup<TForeign>(string foreignCollection, string localField, string foreignField,
        string aliasField)
    {
        _useAggregation = true;
        var lookupStage = PipelineStageDefinitionBuilder.Lookup<T, TForeign, T>(
            _collection.Database.GetCollection<TForeign>(foreignCollection),
            localField,
            foreignField,
            aliasField);
        _pipelineStages.Add(lookupStage);
        return this;
    }

    public IMongoQuery<T> Match(Expression<Func<T, bool>> filter)
    {
        _useAggregation = true;
        var filterDefinition = Builders<T>.Filter.Where(filter);
        var matchStage = PipelineStageDefinitionBuilder.Match<T>(filterDefinition);
        _pipelineStages.Add(matchStage);
        return this;
    }

    public IMongoQuery<T> Project<TProjection>(Expression<Func<T, TProjection>> projection)
    {
        _useAggregation = true;
        var projectionDefinition = Builders<T>.Projection.Expression(projection);
        var projectStage = PipelineStageDefinitionBuilder.Project<T, TProjection>(projectionDefinition);
        _pipelineStages.Add(projectStage);
        return this;
    }

    public IMongoQuery<T> Group<TKey>(Expression<Func<T, TKey>> groupBy,
        Expression<Func<IGrouping<TKey, T>, object>> group)
    {
        _useAggregation = true;
        // Create a simple group stage using BsonDocument
        var groupDoc = new BsonDocument();
        groupDoc["_id"] = "$" + GetFieldName(groupBy);
        groupDoc["count"] = new BsonDocument("$sum", 1);

        var groupStage = new BsonDocument("$group", groupDoc);
        _pipelineStages.Add(groupStage);
        return this;
    }

    private string GetFieldName<TField>(Expression<Func<T, TField>> expression)
    {
        if (expression.Body is MemberExpression memberExpression)
        {
            return memberExpression.Member.Name.ToLowerInvariant();
        }

        return "field";
    }

    public IMongoQuery<T> Unwind<TItem>(Expression<Func<T, IEnumerable<TItem>>> arrayField)
    {
        _useAggregation = true;
        var fieldDefinition = new ExpressionFieldDefinition<T, IEnumerable<TItem>>(arrayField);
        var unwindStage = PipelineStageDefinitionBuilder.Unwind<T, TItem>(fieldDefinition);
        _pipelineStages.Add(unwindStage);
        return this;
    }

    private FilterDefinition<T> CombineFilters()
    {
        if (_filters.Count == 0)
            return Builders<T>.Filter.Empty;

        if (_filters.Count == 1)
            return _filters[0];

        return Builders<T>.Filter.And((IEnumerable<FilterDefinition<T>>)_filters);
    }

    private SortDefinition<T>? CombineSorts()
    {
        if (_sorts.Count == 0)
            return null;

        if (_sorts.Count == 1)
            return _sorts[0];

        return Builders<T>.Sort.Combine((IEnumerable<SortDefinition<T>>)_sorts);
    }

    private FilterDefinition<T> ConvertStringFilterToMongoFilter(string filter)
    {
        // Simple filter conversion - can be enhanced for more complex SQL-like syntax
        if (string.IsNullOrEmpty(filter))
            return Builders<T>.Filter.Empty;

        // Handle basic equality filters like "Id = @Id"
        if (filter.Contains("="))
        {
            var parts = filter.Split('=');
            if (parts.Length == 2)
            {
                var field = parts[0].Trim();
                var value = parts[1].Trim();

                // Remove @ prefix from parameter
                if (value.StartsWith("@"))
                {
                    var paramName = value.Substring(1);
                    if (_parameters.ContainsKey(paramName))
                    {
                        value = _parameters[paramName]?.ToString() ?? "";
                    }
                }

                // Remove quotes if present
                if (value.StartsWith("'") && value.EndsWith("'"))
                {
                    value = value.Substring(1, value.Length - 2);
                }

                return Builders<T>.Filter.Eq(field, value);
            }
        }

        // Handle IN filters like "Id IN (@Ids)"
        if (filter.Contains(" IN "))
        {
            var parts = filter.Split(new[] { " IN " }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                var field = parts[0].Trim();
                var valuePart = parts[1].Trim();

                if (valuePart.StartsWith("(") && valuePart.EndsWith(")"))
                {
                    valuePart = valuePart.Substring(1, valuePart.Length - 2);
                    var values = valuePart.Split(',');
                    var convertedValues = values.Select(v => v.Trim().Trim('\'')).ToArray();
                    return Builders<T>.Filter.In(field, convertedValues);
                }
            }
        }

        // Handle LIKE filters (convert to regex)
        if (filter.Contains(" LIKE "))
        {
            var parts = filter.Split(new[] { " LIKE " }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                var field = parts[0].Trim();
                var pattern = parts[1].Trim().Trim('\'');

                // Convert SQL LIKE pattern to MongoDB regex
                pattern = pattern.Replace("%", ".*").Replace("_", ".");
                var regex = new BsonRegularExpression(pattern, "i");
                return Builders<T>.Filter.Regex(field, regex);
            }
        }

        // Default to text search if no specific pattern matches
        return Builders<T>.Filter.Text(filter);
    }

    private List<SortDefinition<T>> ParseSortExpression(string sortExpression)
    {
        var sorts = new List<SortDefinition<T>>();

        if (string.IsNullOrEmpty(sortExpression))
            return sorts;

        var sortParts = sortExpression.Split(',');
        foreach (var part in sortParts)
        {
            var trimmed = part.Trim();
            if (trimmed.EndsWith(" DESC", StringComparison.OrdinalIgnoreCase))
            {
                var field = trimmed.Substring(0, trimmed.Length - 5).Trim();
                sorts.Add(Builders<T>.Sort.Descending(field));
            }
            else if (trimmed.EndsWith(" ASC", StringComparison.OrdinalIgnoreCase))
            {
                var field = trimmed.Substring(0, trimmed.Length - 4).Trim();
                sorts.Add(Builders<T>.Sort.Ascending(field));
            }
            else
            {
                // Default to ascending
                sorts.Add(Builders<T>.Sort.Ascending(trimmed));
            }
        }

        return sorts;
    }

    private List<FilterDefinition<T>> ConvertDictionaryToMongoFilters(Dictionary<string, object> filters)
    {
        var mongoFilters = new List<FilterDefinition<T>>();

        foreach (var filter in filters)
        {
            var field = filter.Key;
            var value = filter.Value;

            if (value is Dictionary<string, object> operatorValue)
            {
                // Handle MongoDB operators like $in, $nin, $regex, etc.
                foreach (var op in operatorValue)
                {
                    var operatorName = op.Key;
                    var operatorVal = op.Value;

                    switch (operatorName)
                    {
                        case "$in":
                            if (operatorVal is IEnumerable<object> inValues)
                                mongoFilters.Add(Builders<T>.Filter.In(field, inValues));
                            break;
                        case "$nin":
                            if (operatorVal is IEnumerable<object> ninValues)
                                mongoFilters.Add(Builders<T>.Filter.Nin(field, ninValues));
                            break;
                        case "$regex":
                            var regexOptions = operatorValue.ContainsKey("$options")
                                ? operatorValue["$options"].ToString()
                                : "i";
                            var regex = new BsonRegularExpression(operatorVal.ToString(), regexOptions);
                            mongoFilters.Add(Builders<T>.Filter.Regex(field, regex));
                            break;
                        case "$exists":
                            if (operatorVal is bool existsValue)
                                mongoFilters.Add(Builders<T>.Filter.Exists(field, existsValue));
                            break;
                        case "$gt":
                            mongoFilters.Add(Builders<T>.Filter.Gt(field, operatorVal));
                            break;
                        case "$gte":
                            mongoFilters.Add(Builders<T>.Filter.Gte(field, operatorVal));
                            break;
                        case "$lt":
                            mongoFilters.Add(Builders<T>.Filter.Lt(field, operatorVal));
                            break;
                        case "$lte":
                            mongoFilters.Add(Builders<T>.Filter.Lte(field, operatorVal));
                            break;
                        case "$ne":
                            mongoFilters.Add(Builders<T>.Filter.Ne(field, operatorVal));
                            break;
                        default:
                            mongoFilters.Add(Builders<T>.Filter.Eq(field, operatorVal));
                            break;
                    }
                }
            }
            else if (field == "$or" && value is object[] orArray)
            {
                // Handle $or operator
                var orFilters = new List<FilterDefinition<T>>();
                foreach (var orItem in orArray)
                {
                    if (orItem is Dictionary<string, object> orDict)
                    {
                        var orMongoFilters = ConvertDictionaryToMongoFilters(orDict);
                        orFilters.AddRange(orMongoFilters);
                    }
                }

                if (orFilters.Count > 0)
                {
                    mongoFilters.Add(Builders<T>.Filter.Or((IEnumerable<FilterDefinition<T>>)orFilters));
                }
            }
            else
            {
                // Simple equality filter
                mongoFilters.Add(Builders<T>.Filter.Eq(field, value));
            }
        }

        return mongoFilters;
    }
}