using System.Linq.Expressions;
using System.Reflection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using OElite.Common;

namespace OElite.Restme.MongoDb;

/// <summary>
/// Extension methods for MongoQuery to provide enhanced LINQ support and performance optimizations
/// </summary>
public static class MongoQueryExtensions
{
    /// <summary>
    /// Adds a filter using LINQ expression - more efficient than string-based queries
    /// </summary>
    public static IMongoQuery<T> Where<T>(this IMongoQuery<T> query, Expression<Func<T, bool>> filter) where T : BaseEntity
    {
        return query.Query(filter);
    }

    /// <summary>
    /// Adds multiple filters using LINQ expressions - combines them with AND logic
    /// </summary>
    public static IMongoQuery<T> Where<T>(this IMongoQuery<T> query, params Expression<Func<T, bool>>[] filters) where T : BaseEntity
    {
        foreach (var filter in filters)
        {
            query = query.Query(filter);
        }
        return query;
    }

    /// <summary>
    /// Adds sorting using LINQ expression - more type-safe than string-based sorting
    /// </summary>
    public static IMongoQuery<T> OrderBy<T, TKey>(this IMongoQuery<T> query, Expression<Func<T, TKey>> keySelector) where T : BaseEntity
    {
        // Convert Expression<Func<T, TKey>> to Expression<Func<T, object>>
        var objectSelector = Expression.Lambda<Func<T, object>>(
            Expression.Convert(keySelector.Body, typeof(object)),
            keySelector.Parameters);
        return query.Sort(objectSelector, true);
    }

    /// <summary>
    /// Adds descending sorting using LINQ expression
    /// </summary>
    public static IMongoQuery<T> OrderByDescending<T, TKey>(this IMongoQuery<T> query, Expression<Func<T, TKey>> keySelector) where T : BaseEntity
    {
        // Convert Expression<Func<T, TKey>> to Expression<Func<T, object>>
        var objectSelector = Expression.Lambda<Func<T, object>>(
            Expression.Convert(keySelector.Body, typeof(object)),
            keySelector.Parameters);
        return query.Sort(objectSelector, false);
    }

    /// <summary>
    /// Adds secondary sorting using LINQ expression
    /// </summary>
    public static IMongoQuery<T> ThenBy<T, TKey>(this IMongoQuery<T> query, Expression<Func<T, TKey>> keySelector) where T : BaseEntity
    {
        // Convert Expression<Func<T, TKey>> to Expression<Func<T, object>>
        var objectSelector = Expression.Lambda<Func<T, object>>(
            Expression.Convert(keySelector.Body, typeof(object)),
            keySelector.Parameters);
        return query.Sort(objectSelector, true);
    }

    /// <summary>
    /// Adds secondary descending sorting using LINQ expression
    /// </summary>
    public static IMongoQuery<T> ThenByDescending<T, TKey>(this IMongoQuery<T> query, Expression<Func<T, TKey>> keySelector) where T : BaseEntity
    {
        // Convert Expression<Func<T, TKey>> to Expression<Func<T, object>>
        var objectSelector = Expression.Lambda<Func<T, object>>(
            Expression.Convert(keySelector.Body, typeof(object)),
            keySelector.Parameters);
        return query.Sort(objectSelector, false);
    }

    /// <summary>
    /// Limits the number of results - more efficient than using Skip/Limit separately
    /// </summary>
    public static IMongoQuery<T> Take<T>(this IMongoQuery<T> query, int count) where T : BaseEntity
    {
        return query.Limit(count);
    }

    /// <summary>
    /// Skips the specified number of results - use with Take for pagination
    /// </summary>
    public static IMongoQuery<T> Skip<T>(this IMongoQuery<T> query, int count) where T : BaseEntity
    {
        return query.Skip(count);
    }

    /// <summary>
    /// Executes the query and returns the first result or null - optimized for single result queries
    /// </summary>
    public static async Task<T?> FirstOrDefaultAsync<T>(this IMongoQuery<T> query) where T : BaseEntity
    {
        return await query.FirstOrDefaultAsync();
    }

    /// <summary>
    /// Executes the query and returns the first result - throws if no results found
    /// </summary>
    public static async Task<T> FirstAsync<T>(this IMongoQuery<T> query) where T : BaseEntity
    {
        var result = await query.FirstOrDefaultAsync();
        if (result == null)
            throw new InvalidOperationException("Sequence contains no elements");
        return result;
    }

    /// <summary>
    /// Executes the query and returns the single result or null - optimized for single result queries
    /// </summary>
    public static async Task<T?> SingleOrDefaultAsync<T>(this IMongoQuery<T> query) where T : BaseEntity
    {
        // For SingleOrDefault, we need to ensure we get at most 2 results to check for uniqueness
        var results = await query.Limit(2).ToListAsync();
        return results.Count switch
        {
            0 => default(T),
            1 => results[0],
            _ => throw new InvalidOperationException("Sequence contains more than one element")
        };
    }

    /// <summary>
    /// Executes the query and returns the single result - throws if no results or multiple results
    /// </summary>
    public static async Task<T> SingleAsync<T>(this IMongoQuery<T> query) where T : BaseEntity
    {
        var result = await query.SingleOrDefaultAsync();
        if (result == null)
            throw new InvalidOperationException("Sequence contains no elements");
        return result;
    }

    /// <summary>
    /// Executes the query and returns all results as a list
    /// </summary>
    public static async Task<List<T>> ToListAsync<T>(this IMongoQuery<T> query) where T : BaseEntity
    {
        return await query.ToListAsync();
    }

    /// <summary>
    /// Executes the query and returns the count of matching documents - optimized for counting
    /// </summary>
    public static async Task<int> CountAsync<T>(this IMongoQuery<T> query) where T : BaseEntity
    {
        var count = await query.CountAsync();
        return count > int.MaxValue ? int.MaxValue : (int)count;
    }

    /// <summary>
    /// Executes the query and returns the count of matching documents as long
    /// </summary>
    public static async Task<long> LongCountAsync<T>(this IMongoQuery<T> query) where T : BaseEntity
    {
        return await query.CountAsync();
    }

    /// <summary>
    /// Checks if any documents match the query - optimized for existence checks
    /// </summary>
    public static async Task<bool> AnyAsync<T>(this IMongoQuery<T> query) where T : BaseEntity
    {
        return await query.AnyAsync();
    }

    /// <summary>
    /// Checks if all documents match the additional filter using MongoDB aggregation
    /// </summary>
    public static async Task<bool> AllAsync<T>(this IMongoQuery<T> query, Expression<Func<T, bool>> predicate) where T : BaseEntity
    {
        var collection = GetCollectionFromQuery(query);
        
        // Build aggregation pipeline to check if any documents don't match the predicate
        var pipeline = new List<BsonDocument>();
        
        // Add match stage for existing filters
        var matchStage = GetMatchStageFromQuery(query);
        if (matchStage != null)
        {
            pipeline.Add(matchStage);
        }
        
        // Add match stage for the predicate (inverted - find documents that DON'T match)
        var predicateFilter = Builders<T>.Filter.Not(Builders<T>.Filter.Where(predicate));
        var predicateFilterDoc = predicateFilter.Render(
            BsonSerializer.SerializerRegistry.GetSerializer<T>(),
            BsonSerializer.SerializerRegistry);
        pipeline.Add(new BsonDocument("$match", predicateFilterDoc));
        
        // Add limit 1 to stop at first non-matching document
        pipeline.Add(new BsonDocument("$limit", 1));
        
        // Add count stage
        pipeline.Add(new BsonDocument("$count", "nonMatchingCount"));
        
        // Execute aggregation
        var cursor = await collection.AggregateAsync<BsonDocument>(pipeline);
        var result = await cursor.FirstOrDefaultAsync();
        
        // If we found any non-matching documents, return false
        return result == null;
    }

    /// <summary>
    /// Projects the query results to a different type - deferred execution version
    /// Returns a wrapper that defers execution until FetchAsync() or other execution methods are called
    /// </summary>
    public static ProjectionQuery<T, TResult> Select<T, TResult>(this IMongoQuery<T> query, Expression<Func<T, TResult>> selector) where T : BaseEntity
    {
        return new ProjectionQuery<T, TResult>(query, selector);
    }

    /// <summary>
    /// Projects the query results to a different type using MongoDB aggregation pipeline
    /// </summary>
    public static async Task<List<TResult>> SelectAsync<T, TResult>(this IMongoQuery<T> query, Expression<Func<T, TResult>> selector) where T : BaseEntity
    {
        // Get the underlying collection from the query
        var collection = GetCollectionFromQuery(query);
        
        // Build aggregation pipeline
        var pipeline = new List<BsonDocument>();
        
        // Add match stage if there are filters
        var matchStage = GetMatchStageFromQuery(query);
        if (matchStage != null)
        {
            pipeline.Add(matchStage);
        }
        
        // Add sort stage if there are sorts
        var sortStage = GetSortStageFromQuery(query);
        if (sortStage != null)
        {
            pipeline.Add(sortStage);
        }
        
        // Add projection stage
        var projectStage = ExpressionToMongoPipeline.CreateProjectStage(selector);
        pipeline.Add(projectStage);
        
        // Add limit and skip stages
        var limitSkipStages = ExpressionToMongoPipeline.CreateLimitSkipStages(GetLimitFromQuery(query), GetSkipFromQuery(query));
        pipeline.AddRange(limitSkipStages);
        
        // Execute aggregation
        var cursor = await collection.AggregateAsync<BsonDocument>(pipeline);
        var bsonResults = await cursor.ToListAsync();
        
        // Convert BsonDocument results to TResult
        var results = new List<TResult>();
        foreach (var bsonDoc in bsonResults)
        {
            if (bsonDoc.Contains("_value"))
            {
                // Single field projection
                var value = bsonDoc["_value"];
                results.Add(ConvertBsonValue<TResult>(value));
            }
            else
            {
                // Object projection - convert to anonymous object first, then to TResult
                var anonymousObj = ConvertBsonToAnonymous(bsonDoc);
                results.Add(ConvertAnonymousToTResult<TResult>(anonymousObj));
            }
        }
        
        return results;
    }

    /// <summary>
    /// Groups the query results by a key selector using MongoDB aggregation pipeline
    /// </summary>
    public static async Task<Dictionary<TKey, List<T>>> GroupByAsync<T, TKey>(this IMongoQuery<T> query, Expression<Func<T, TKey>> keySelector) where T : BaseEntity where TKey : notnull
    {
        // Get the underlying collection from the query
        var collection = GetCollectionFromQuery(query);
        
        // Build aggregation pipeline
        var pipeline = new List<BsonDocument>();
        
        // Add match stage if there are filters
        var matchStage = GetMatchStageFromQuery(query);
        if (matchStage != null)
        {
            pipeline.Add(matchStage);
        }
        
        // Add group stage
        var groupStage = ExpressionToMongoPipeline.CreateGroupStage(keySelector);
        pipeline.Add(groupStage);
        
        // Add sort stage if there are sorts
        var sortStage = GetSortStageFromQuery(query);
        if (sortStage != null)
        {
            pipeline.Add(sortStage);
        }
        
        // Add limit and skip stages
        var limitSkipStages = ExpressionToMongoPipeline.CreateLimitSkipStages(GetLimitFromQuery(query), GetSkipFromQuery(query));
        pipeline.AddRange(limitSkipStages);
        
        // Execute aggregation
        var cursor = await collection.AggregateAsync<BsonDocument>(pipeline);
        var bsonResults = await cursor.ToListAsync();
        
        // Convert results to dictionary
        var result = new Dictionary<TKey, List<T>>();
        foreach (var bsonDoc in bsonResults)
        {
            var key = ConvertBsonValue<TKey>(bsonDoc["_id"]);
            var items = bsonDoc["items"].AsBsonArray.Select(item => BsonSerializer.Deserialize<T>(item.AsBsonDocument)).ToList();
            result[key] = items;
        }
        
        return result;
    }

    /// <summary>
    /// Executes the query with pagination - optimized for large datasets
    /// </summary>
    public static async Task<(List<T> Items, long TotalCount)> ToPagedListAsync<T>(this IMongoQuery<T> query, int pageIndex, int pageSize) where T : BaseEntity
    {
        var totalCount = await query.CountAsync();
        var items = await query.Skip(pageIndex * pageSize).Take(pageSize).ToListAsync();
        return (items, totalCount);
    }

    /// <summary>
    /// Executes the query with pagination and returns a BaseEntityCollection - optimized for large datasets
    /// </summary>
    public static async Task<TCollection> ToPagedCollectionAsync<T, TCollection>(this IMongoQuery<T> query, int pageIndex, int pageSize) 
        where T : BaseEntity 
        where TCollection : BaseEntityCollection<T>, new()
    {
        var (items, totalCount) = await query.ToPagedListAsync(pageIndex, pageSize);
        var collection = new TCollection();
        collection.AddRange(items);
        collection.TotalRecordsCount = (int)totalCount;
        return collection;
    }

    /// <summary>
    /// Executes the query and returns distinct values for a specific field using MongoDB aggregation
    /// </summary>
    public static async Task<List<TValue>> DistinctAsync<T, TValue>(this IMongoQuery<T> query, Expression<Func<T, TValue>> fieldSelector) where T : BaseEntity
    {
        // Get the underlying collection from the query
        var collection = GetCollectionFromQuery(query);
        var fieldName = ExpressionToMongoPipeline.GetFieldNameForAggregation(fieldSelector);
        
        // Build aggregation pipeline
        var pipeline = new List<BsonDocument>();
        
        // Add match stage if there are filters
        var matchStage = GetMatchStageFromQuery(query);
        if (matchStage != null)
        {
            pipeline.Add(matchStage);
        }
        
        // Add group stage to get distinct values
        pipeline.Add(new BsonDocument("$group", new BsonDocument("_id", $"${fieldName}")));
        
        // Add project stage to rename _id to the field name
        pipeline.Add(new BsonDocument("$project", new BsonDocument(fieldName, "$_id")));
        
        // Add sort stage if there are sorts
        var sortStage = GetSortStageFromQuery(query);
        if (sortStage != null)
        {
            pipeline.Add(sortStage);
        }
        
        // Add limit and skip stages
        var limitSkipStages = ExpressionToMongoPipeline.CreateLimitSkipStages(GetLimitFromQuery(query), GetSkipFromQuery(query));
        pipeline.AddRange(limitSkipStages);
        
        // Execute aggregation
        var cursor = await collection.AggregateAsync<BsonDocument>(pipeline);
        var bsonResults = await cursor.ToListAsync();
        
        // Convert results to list
        var results = new List<TValue>();
        foreach (var bsonDoc in bsonResults)
        {
            if (bsonDoc.Contains(fieldName))
            {
                var value = ConvertBsonValue<TValue>(bsonDoc[fieldName]);
                if (value != null)
                {
                    results.Add(value);
                }
            }
        }
        
        return results;
    }

    /// <summary>
    /// Executes the query and returns the maximum value for a specific field using MongoDB aggregation
    /// </summary>
    public static async Task<TValue?> MaxAsync<T, TValue>(this IMongoQuery<T> query, Expression<Func<T, TValue>> fieldSelector) where T : BaseEntity
    {
        var fieldName = ExpressionToMongoPipeline.GetFieldNameForAggregation(fieldSelector);
        return await ExecuteAggregationAsync<T, TValue>(query, new BsonDocument("$max", $"${fieldName}"));
    }

    /// <summary>
    /// Executes the query and returns the minimum value for a specific field using MongoDB aggregation
    /// </summary>
    public static async Task<TValue?> MinAsync<T, TValue>(this IMongoQuery<T> query, Expression<Func<T, TValue>> fieldSelector) where T : BaseEntity
    {
        var fieldName = ExpressionToMongoPipeline.GetFieldNameForAggregation(fieldSelector);
        return await ExecuteAggregationAsync<T, TValue>(query, new BsonDocument("$min", $"${fieldName}"));
    }

    /// <summary>
    /// Executes the query and returns the average value for a specific field using MongoDB aggregation
    /// </summary>
    public static async Task<double> AverageAsync<T>(this IMongoQuery<T> query, Expression<Func<T, double>> fieldSelector) where T : BaseEntity
    {
        var fieldName = ExpressionToMongoPipeline.GetFieldNameForAggregation(fieldSelector);
        var result = await ExecuteAggregationAsync<T, BsonValue>(query, new BsonDocument("$avg", $"${fieldName}"));
        return result?.AsDouble ?? 0.0;
    }

    /// <summary>
    /// Executes the query and returns the sum of values for a specific field using MongoDB aggregation
    /// </summary>
    public static async Task<decimal> SumAsync<T>(this IMongoQuery<T> query, Expression<Func<T, decimal>> fieldSelector) where T : BaseEntity
    {
        var fieldName = ExpressionToMongoPipeline.GetFieldNameForAggregation(fieldSelector);
        var result = await ExecuteAggregationAsync<T, BsonValue>(query, new BsonDocument("$sum", $"${fieldName}"));
        return result?.AsDecimal ?? 0m;
    }

    #region Helper Methods

    /// <summary>
    /// Gets the underlying MongoDB collection from a MongoQuery
    /// </summary>
    private static IMongoCollection<T> GetCollectionFromQuery<T>(IMongoQuery<T> query) where T : BaseEntity
    {
        // Use reflection to access the private _collection field
        var field = query.GetType().GetField("_collection", BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null)
        {
            return (IMongoCollection<T>)field.GetValue(query)!;
        }
        
        // Fallback: try to get from property
        var property = query.GetType().GetProperty("Collection", BindingFlags.NonPublic | BindingFlags.Instance);
        if (property != null)
        {
            return (IMongoCollection<T>)property.GetValue(query)!;
        }
        
        throw new InvalidOperationException("Cannot access underlying MongoDB collection from MongoQuery");
    }

    /// <summary>
    /// Gets the match stage from a MongoQuery's filters
    /// </summary>
    private static BsonDocument? GetMatchStageFromQuery<T>(IMongoQuery<T> query) where T : BaseEntity
    {
        // Use reflection to access the private _filters field
        var field = query.GetType().GetField("_filters", BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null)
        {
            var filters = (List<FilterDefinition<T>>)field.GetValue(query)!;
            if (filters.Count > 0)
            {
                var combinedFilter = filters.Count == 1 ? filters[0] : Builders<T>.Filter.And(filters);
                var filterDoc = combinedFilter.Render(
                    BsonSerializer.SerializerRegistry.GetSerializer<T>(),
                    BsonSerializer.SerializerRegistry);
                return new BsonDocument("$match", filterDoc);
            }
        }
        return null;
    }

    /// <summary>
    /// Gets the sort stage from a MongoQuery's sorts
    /// </summary>
    private static BsonDocument? GetSortStageFromQuery<T>(IMongoQuery<T> query) where T : BaseEntity
    {
        // Use reflection to access the private _sorts field
        var field = query.GetType().GetField("_sorts", BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null)
        {
            var sorts = (List<SortDefinition<T>>)field.GetValue(query)!;
            if (sorts.Count > 0)
            {
                var combinedSort = sorts.Count == 1 ? sorts[0] : Builders<T>.Sort.Combine(sorts);
                var sortDoc = combinedSort.Render(
                    BsonSerializer.SerializerRegistry.GetSerializer<T>(),
                    BsonSerializer.SerializerRegistry);
                return new BsonDocument("$sort", sortDoc);
            }
        }
        return null;
    }

    /// <summary>
    /// Gets the limit value from a MongoQuery
    /// </summary>
    private static int? GetLimitFromQuery<T>(IMongoQuery<T> query) where T : BaseEntity
    {
        var field = query.GetType().GetField("_limit", BindingFlags.NonPublic | BindingFlags.Instance);
        return field?.GetValue(query) as int?;
    }

    /// <summary>
    /// Gets the skip value from a MongoQuery
    /// </summary>
    private static int? GetSkipFromQuery<T>(IMongoQuery<T> query) where T : BaseEntity
    {
        var field = query.GetType().GetField("_skip", BindingFlags.NonPublic | BindingFlags.Instance);
        return field?.GetValue(query) as int?;
    }

    /// <summary>
    /// Executes a simple aggregation operation (max, min, avg, sum)
    /// </summary>
    private static async Task<TResult?> ExecuteAggregationAsync<T, TResult>(IMongoQuery<T> query, BsonDocument groupOperation) where T : BaseEntity
    {
        var collection = GetCollectionFromQuery(query);
        
        var pipeline = new List<BsonDocument>();
        
        // Add match stage if there are filters
        var matchStage = GetMatchStageFromQuery(query);
        if (matchStage != null)
        {
            pipeline.Add(matchStage);
        }
        
        // Add group stage with the operation
        pipeline.Add(new BsonDocument("$group", new BsonDocument("result", groupOperation)));
        
        // Execute aggregation
        var cursor = await collection.AggregateAsync<BsonDocument>(pipeline);
        var result = await cursor.FirstOrDefaultAsync();
        
        if (result != null && result.Contains("result"))
        {
            return ConvertBsonValue<TResult>(result["result"]);
        }
        
        return default(TResult);
    }

    /// <summary>
    /// Converts a BsonValue to a specific type
    /// </summary>
    private static TResult? ConvertBsonValue<TResult>(BsonValue bsonValue)
    {
        if (bsonValue.IsBsonNull)
            return default(TResult);

        try
        {
            if (typeof(TResult) == typeof(string))
                return (TResult)(object)bsonValue.AsString;
            else if (typeof(TResult) == typeof(int))
                return (TResult)(object)bsonValue.AsInt32;
            else if (typeof(TResult) == typeof(long))
                return (TResult)(object)bsonValue.AsInt64;
            else if (typeof(TResult) == typeof(double))
                return (TResult)(object)bsonValue.AsDouble;
            else if (typeof(TResult) == typeof(decimal))
                return (TResult)(object)bsonValue.AsDecimal;
            else if (typeof(TResult) == typeof(bool))
                return (TResult)(object)bsonValue.AsBoolean;
            else if (typeof(TResult) == typeof(DateTime))
                return (TResult)(object)bsonValue.ToUniversalTime();
            else if (typeof(TResult) == typeof(DbObjectId))
                return (TResult)(object)new DbObjectId(bsonValue.AsObjectId.ToString());
            else
            {
                // Use BsonSerializer for complex types
                return BsonSerializer.Deserialize<TResult>(bsonValue.ToBsonDocument());
            }
        }
        catch
        {
            return default(TResult);
        }
    }

    /// <summary>
    /// Converts a BsonDocument to an anonymous object
    /// </summary>
    private static object ConvertBsonToAnonymous(BsonDocument bsonDoc)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var element in bsonDoc)
        {
            dict[element.Name] = ConvertBsonValue<object>(element.Value);
        }
        return dict;
    }

    /// <summary>
    /// Converts an anonymous object to a specific type
    /// </summary>
    private static TResult ConvertAnonymousToTResult<TResult>(object anonymousObj)
    {
        if (anonymousObj is Dictionary<string, object?> dict)
        {
            // Create a BsonDocument from the dictionary and deserialize
            var bsonDoc = new BsonDocument();
            foreach (var kvp in dict)
            {
                bsonDoc[kvp.Key] = BsonValue.Create(kvp.Value);
            }
            return BsonSerializer.Deserialize<TResult>(bsonDoc);
        }
        
        // Fallback: try direct conversion
        return (TResult)anonymousObj;
    }

    /// <summary>
    /// Executes the query and returns the first result or null - equivalent to FirstOrDefaultAsync
    /// </summary>
    public static async Task<T?> FetchAsync<T>(this IMongoQuery<T> query) where T : BaseEntity
    {
        return await query.FirstOrDefaultAsync();
    }

    /// <summary>
    /// Executes the query and returns results as a BaseEntityCollection - equivalent to ToListAsync with collection wrapper
    /// </summary>
    public static async Task<TCollection> FetchAsync<T, TCollection>(this IMongoQuery<T> query, bool returnTotalCount = false) 
        where T : BaseEntity 
        where TCollection : BaseEntityCollection<T>, new()
    {
        var collection = new TCollection();
        
        if (returnTotalCount)
        {
            // Execute count and data queries in parallel for better performance
            var countTask = query.CountAsync();
            var dataTask = query.ToListAsync();
            
            await Task.WhenAll(countTask, dataTask);
            
            collection.AddRange(await dataTask);
            collection.TotalRecordsCount = (int)await countTask;
        }
        else
        {
            // Skip count query for better performance when total count is not needed
            var results = await query.ToListAsync();
            collection.AddRange(results);
        }
        
        return collection;
    }

    /// <summary>
    /// Executes the query with pagination and returns results as a BaseEntityCollection with optional total count
    /// </summary>
    public static async Task<TCollection> FetchAsync<T, TCollection>(this IMongoQuery<T> query, int pageIndex, int pageSize, bool returnTotalCount = false)
        where T : BaseEntity
        where TCollection : BaseEntityCollection<T>, new()
    {
        var collection = new TCollection();

        if (returnTotalCount)
        {
            // Use existing optimized pagination method that executes count and data queries efficiently
            var (items, totalCount) = await query.ToPagedListAsync(pageIndex, pageSize);
            collection.AddRange(items);
            collection.TotalRecordsCount = (int)totalCount;
        }
        else
        {
            // Skip count query for better performance when total count is not needed
            var results = await query.Skip(pageIndex * pageSize).Take(pageSize).ToListAsync();
            collection.AddRange(results);
        }

        return collection;
    }

    /// <summary>
    /// Inserts a new entity into the collection
    /// </summary>
    public static async Task InsertAsync<T>(this IMongoQuery<T> query, T entity) where T : BaseEntity
    {
        var collection = GetCollectionFromQuery(query);
        await collection.InsertOneAsync(entity);
    }

    /// <summary>
    /// Inserts multiple entities into the collection
    /// </summary>
    public static async Task InsertManyAsync<T>(this IMongoQuery<T> query, IEnumerable<T> entities) where T : BaseEntity
    {
        var collection = GetCollectionFromQuery(query);
        await collection.InsertManyAsync(entities);
    }

    /// <summary>
    /// Replaces an entity matching the filter with a new entity (with upsert option)
    /// </summary>
    public static async Task<ReplaceOneResult> ReplaceAsync<T>(this IMongoQuery<T> query, T entity, bool isUpsert = false) where T : BaseEntity
    {
        var collection = GetCollectionFromQuery(query);
        return await collection.ReplaceOneAsync(e => e.Id == entity.Id, entity, isUpsert);
    }

    /// <summary>
    /// Updates entities matching the query filters using a fluent UpdateBuilder for high performance
    /// </summary>
    public static async Task<UpdateResult> UpdateAsync<T>(this IMongoQuery<T> query, Func<UpdateBuilder<T>, UpdateBuilder<T>> updateBuilder) where T : BaseEntity
    {
        var builder = new UpdateBuilder<T>();
        var configuredBuilder = updateBuilder(builder);
        var update = configuredBuilder.Build();
        return await query.UpdateAsync(update);
    }

    /// <summary>
    /// Updates entities matching the query filters with the specified update definition
    /// </summary>
    public static async Task<UpdateResult> UpdateAsync<T>(this IMongoQuery<T> query, UpdateDefinition<T> update) where T : BaseEntity
    {
        var collection = GetCollectionFromQuery(query);

        // Get filters from the query
        var matchStage = GetMatchStageFromQuery(query);
        if (matchStage != null)
        {
            // Extract filter from match stage
            var field = query.GetType().GetField("_filters", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                var filters = (List<FilterDefinition<T>>)field.GetValue(query)!;
                var combinedFilter = filters.Count == 1 ? filters[0] : Builders<T>.Filter.And(filters);
                return await collection.UpdateManyAsync(combinedFilter, update);
            }
        }

        // No filters - update all (dangerous, should we throw?)
        return await collection.UpdateManyAsync(Builders<T>.Filter.Empty, update);
    }

    /// <summary>
    /// Deletes all entities matching the query filters
    /// </summary>
    public static async Task<DeleteResult> DeleteAsync<T>(this IMongoQuery<T> query) where T : BaseEntity
    {
        var collection = GetCollectionFromQuery(query);

        // Get filters from the query
        var field = query.GetType().GetField("_filters", BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null)
        {
            var filters = (List<FilterDefinition<T>>)field.GetValue(query)!;
            if (filters.Count > 0)
            {
                var combinedFilter = filters.Count == 1 ? filters[0] : Builders<T>.Filter.And(filters);
                return await collection.DeleteManyAsync(combinedFilter);
            }
        }

        // No filters - throw exception to prevent accidental deletion of all documents
        throw new InvalidOperationException("Cannot delete without filters. Use Where() to specify which documents to delete.");
    }

    /// <summary>
    /// Deletes a single entity by ID
    /// </summary>
    public static async Task<DeleteResult> DeleteByIdAsync<T>(this IMongoQuery<T> query, DbObjectId id) where T : BaseEntity
    {
        var collection = GetCollectionFromQuery(query);
        return await collection.DeleteOneAsync(e => e.Id == id);
    }

    /// <summary>
    /// Convenience method for setting a single field value with high performance
    /// </summary>
    public static async Task<UpdateResult> SetAsync<T, TField>(this IMongoQuery<T> query, Expression<Func<T, TField>> field, TField value) where T : BaseEntity
    {
        return await query.UpdateAsync(u => u.Set(field, value));
    }

    // Note: UpdateManyAsync and ReplaceOneAsync methods removed to avoid exposing MongoDB.Driver types
    // These operations can be performed using the existing UpdateAsync and ReplaceAsync methods

    /// <summary>
    /// Convenience method for incrementing a numeric field with high performance
    /// </summary>
    public static async Task<UpdateResult> IncrementAsync<T, TField>(this IMongoQuery<T> query, Expression<Func<T, TField>> field, TField value) where T : BaseEntity where TField : struct
    {
        return await query.UpdateAsync(u => u.Inc(field, value));
    }

    /// <summary>
    /// Convenience method for updating timestamp fields
    /// </summary>
    public static async Task<UpdateResult> TouchAsync<T>(this IMongoQuery<T> query, Expression<Func<T, DateTime>> timestampField) where T : BaseEntity
    {
        return await query.UpdateAsync(u => u.Set(timestampField, DateTime.UtcNow));
    }

    /// <summary>
    /// MongoDB-free aggregation API that returns Dictionary results without exposing MongoDB types
    /// </summary>
    public static async Task<List<Dictionary<string, object>>> AggregateToDictionaryAsync<T>(this IMongoQuery<T> query, Dictionary<string, object>[] pipeline) where T : BaseEntity
    {
        var collection = ((MongoQuery<T>)query).Collection;
        var bsonDocuments = pipeline.Select(dict => new MongoDB.Bson.BsonDocument(dict)).ToArray();
        var cursor = await collection.AggregateAsync<MongoDB.Bson.BsonDocument>(bsonDocuments);
        var bsonResults = await cursor.ToListAsync();

        // Convert BsonDocument to Dictionary<string, object> to avoid exposing MongoDB types
        return bsonResults.Select(ConvertBsonToDict).ToList();
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

    /// <summary>
    /// Convenience method for batch field updates with optimal performance
    /// </summary>
    public static async Task<UpdateResult> SetFieldsAsync<T>(this IMongoQuery<T> query, params (Expression<Func<T, object>> field, object value)[] updates) where T : BaseEntity
    {
        return await query.UpdateAsync(u => u.Set(updates));
    }

    #endregion
}

/// <summary>
/// A deferred projection query that wraps an existing query with a projection selector
/// Executes the projection when FetchAsync() or other execution methods are called
/// </summary>
public class ProjectionQuery<T, TResult> where T : BaseEntity
{
    private readonly IMongoQuery<T> _sourceQuery;
    private readonly Expression<Func<T, TResult>> _selector;

    public ProjectionQuery(IMongoQuery<T> sourceQuery, Expression<Func<T, TResult>> selector)
    {
        _sourceQuery = sourceQuery;
        _selector = selector;
    }

    /// <summary>
    /// Execute the query and return the projected result
    /// </summary>
    public async Task<TResult?> FetchAsync()
    {
        var sourceResult = await _sourceQuery.FetchAsync();
        return sourceResult != null ? _selector.Compile()(sourceResult) : default(TResult);
    }

    /// <summary>
    /// Execute the query and return all projected results
    /// </summary>
    public async Task<List<TResult>> ToListAsync()
    {
        var sourceResults = await _sourceQuery.ToListAsync();
        return sourceResults.Select(_selector.Compile()).ToList();
    }

    /// <summary>
    /// Execute the query and return the first projected result
    /// </summary>
    public async Task<TResult?> FirstOrDefaultAsync()
    {
        var sourceResult = await _sourceQuery.FirstOrDefaultAsync();
        return sourceResult != null ? _selector.Compile()(sourceResult) : default(TResult);
    }

    /// <summary>
    /// Count the number of matching records
    /// </summary>
    public async Task<long> CountAsync()
    {
        return await _sourceQuery.CountAsync();
    }

    /// <summary>
    /// Check if any records match the query
    /// </summary>
    public async Task<bool> AnyAsync()
    {
        return await _sourceQuery.AnyAsync();
    }

    /// <summary>
    /// Add additional filtering to the query
    /// </summary>
    public ProjectionQuery<T, TResult> Where(Expression<Func<T, bool>> filter)
    {
        _sourceQuery.Query(filter);
        return this;
    }

    /// <summary>
    /// Add sorting to the query
    /// </summary>
    public ProjectionQuery<T, TResult> OrderBy<TKey>(Expression<Func<T, TKey>> keySelector)
    {
        // Convert Expression<Func<T, TKey>> to Expression<Func<T, object>>
        var objectSelector = Expression.Lambda<Func<T, object>>(
            Expression.Convert(keySelector.Body, typeof(object)),
            keySelector.Parameters);
        _sourceQuery.Sort(objectSelector, true);
        return this;
    }

    /// <summary>
    /// Add descending sorting to the query
    /// </summary>
    public ProjectionQuery<T, TResult> OrderByDescending<TKey>(Expression<Func<T, TKey>> keySelector)
    {
        // Convert Expression<Func<T, TKey>> to Expression<Func<T, object>>
        var objectSelector = Expression.Lambda<Func<T, object>>(
            Expression.Convert(keySelector.Body, typeof(object)),
            keySelector.Parameters);
        _sourceQuery.Sort(objectSelector, false);
        return this;
    }

    /// <summary>
    /// Limit the number of results
    /// </summary>
    public ProjectionQuery<T, TResult> Take(int count)
    {
        _sourceQuery.Limit(count);
        return this;
    }

    /// <summary>
    /// Skip a number of results
    /// </summary>
    public ProjectionQuery<T, TResult> Skip(int count)
    {
        _sourceQuery.Skip(count);
        return this;
    }
}

