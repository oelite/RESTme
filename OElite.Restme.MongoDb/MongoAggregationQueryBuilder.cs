using MongoDB.Bson;
using MongoDB.Driver;
using OElite.Common;

namespace OElite.Restme.MongoDb;

/// <summary>
/// Generic MongoDB aggregation query builder for complex filtering scenarios
/// </summary>
public class MongoAggregationQueryBuilder<T> where T : BaseEntity
{
    private readonly List<BsonDocument> _pipeline = new();
    private readonly IMongoCollection<T> _collection;

    public MongoAggregationQueryBuilder(IMongoCollection<T> collection)
    {
        _collection = collection;
    }

    /// <summary>
    /// Adds a $lookup stage to join with another collection
    /// </summary>
    public MongoAggregationQueryBuilder<T> Lookup<TForeign>(
        string foreignCollection,
        string localField,
        string foreignField,
        string aliasField)
    {
        var lookupStage = new BsonDocument
        {
            ["$lookup"] = new BsonDocument
            {
                ["from"] = foreignCollection,
                ["localField"] = localField,
                ["foreignField"] = foreignField,
                ["as"] = aliasField
            }
        };
        _pipeline.Add(lookupStage);
        return this;
    }

    /// <summary>
    /// Adds a $match stage for filtering
    /// </summary>
    public MongoAggregationQueryBuilder<T> Match(BsonDocument filter)
    {
        if (filter != null && filter.ElementCount > 0)
        {
            _pipeline.Add(new BsonDocument("$match", filter));
        }
        return this;
    }

    /// <summary>
    /// Adds a $match stage for filtering with dictionary
    /// </summary>
    public MongoAggregationQueryBuilder<T> Match(Dictionary<string, object> filter)
    {
        if (filter != null && filter.Count > 0)
        {
            var bsonFilter = new BsonDocument();
            foreach (var kvp in filter)
            {
                bsonFilter[kvp.Key] = MongoDbCollectionImplementation.ConvertToBsonValue(kvp.Value);
            }
            _pipeline.Add(new BsonDocument("$match", bsonFilter));
        }
        return this;
    }

    /// <summary>
    /// Adds a $project stage to select specific fields
    /// </summary>
    public MongoAggregationQueryBuilder<T> Project(BsonDocument projection)
    {
        _pipeline.Add(new BsonDocument("$project", projection));
        return this;
    }

    /// <summary>
    /// Adds a $unwind stage to flatten arrays
    /// </summary>
    public MongoAggregationQueryBuilder<T> Unwind(string fieldPath, bool preserveNullAndEmptyArrays = false)
    {
        var unwindStage = new BsonDocument("$unwind", fieldPath);
        if (preserveNullAndEmptyArrays)
        {
            unwindStage = new BsonDocument
            {
                ["$unwind"] = new BsonDocument
                {
                    ["path"] = fieldPath,
                    ["preserveNullAndEmptyArrays"] = preserveNullAndEmptyArrays
                }
            };
        }
        _pipeline.Add(unwindStage);
        return this;
    }

    /// <summary>
    /// Adds a $group stage for aggregation
    /// </summary>
    public MongoAggregationQueryBuilder<T> Group(BsonDocument groupDefinition)
    {
        _pipeline.Add(new BsonDocument("$group", groupDefinition));
        return this;
    }

    /// <summary>
    /// Adds a $sort stage
    /// </summary>
    public MongoAggregationQueryBuilder<T> Sort(BsonDocument sortDefinition)
    {
        _pipeline.Add(new BsonDocument("$sort", sortDefinition));
        return this;
    }

    /// <summary>
    /// Adds a $limit stage
    /// </summary>
    public MongoAggregationQueryBuilder<T> Limit(int limit)
    {
        _pipeline.Add(new BsonDocument("$limit", limit));
        return this;
    }

    /// <summary>
    /// Adds a $skip stage
    /// </summary>
    public MongoAggregationQueryBuilder<T> Skip(int skip)
    {
        _pipeline.Add(new BsonDocument("$skip", skip));
        return this;
    }

    /// <summary>
    /// Adds a $addFields stage to add computed fields
    /// </summary>
    public MongoAggregationQueryBuilder<T> AddFields(BsonDocument fields)
    {
        _pipeline.Add(new BsonDocument("$addFields", fields));
        return this;
    }

    /// <summary>
    /// Adds a custom pipeline stage
    /// </summary>
    public MongoAggregationQueryBuilder<T> AddStage(BsonDocument stage)
    {
        _pipeline.Add(stage);
        return this;
    }

    /// <summary>
    /// Executes the aggregation pipeline and returns results
    /// </summary>
    public async Task<List<TResult>> ExecuteAsync<TResult>()
    {
        var pipeline = PipelineDefinition<T, TResult>.Create(_pipeline);
        var cursor = await _collection.AggregateAsync(pipeline);
        return await cursor.ToListAsync();
    }

    /// <summary>
    /// Executes the aggregation pipeline and returns the first result
    /// </summary>
    public async Task<TResult?> FirstOrDefaultAsync<TResult>()
    {
        var pipeline = PipelineDefinition<T, TResult>.Create(_pipeline);
        var cursor = await _collection.AggregateAsync(pipeline);
        return await cursor.FirstOrDefaultAsync();
    }

    /// <summary>
    /// Executes the aggregation pipeline and returns count
    /// </summary>
    public async Task<long> CountAsync()
    {
        // Add count stage
        _pipeline.Add(new BsonDocument("$count", "total"));
        var pipeline = PipelineDefinition<T, BsonDocument>.Create(_pipeline);
        var cursor = await _collection.AggregateAsync(pipeline);
        var result = await cursor.FirstOrDefaultAsync();
        return result?.GetValue("total", 0).AsInt64 ?? 0;
    }

    /// <summary>
    /// Gets the current pipeline as BsonDocument array for debugging
    /// </summary>
    public BsonArray GetPipeline()
    {
        return new BsonArray(_pipeline);
    }
}

/// <summary>
/// Extension methods for creating complex aggregation queries
/// </summary>
public static class MongoAggregationExtensions
{
    /// <summary>
    /// Creates a new aggregation query builder
    /// </summary>
    public static MongoAggregationQueryBuilder<T> CreateAggregation<T>(this IMongoCollection<T> collection) 
        where T : BaseEntity
    {
        return new MongoAggregationQueryBuilder<T>(collection);
    }

    /// <summary>
    /// Creates a filter for products by account ID using aggregation
    /// </summary>
    public static BsonDocument CreateProductByAccountFilter(DbObjectId accountId)
    {
        return new BsonDocument
        {
            ["$lookup"] = new BsonDocument
            {
                ["from"] = "products",
                ["localField"] = "ProductId",
                ["foreignField"] = "_id",
                ["as"] = "product"
            }
        };
    }

    /// <summary>
    /// Creates a filter for products by status using aggregation
    /// </summary>
    public static BsonDocument CreateProductByStatusFilter(int[] statusValues)
    {
        return new BsonDocument
        {
            ["$lookup"] = new BsonDocument
            {
                ["from"] = "products",
                ["localField"] = "ProductId",
                ["foreignField"] = "_id",
                ["as"] = "product"
            }
        };
    }

    /// <summary>
    /// Creates a filter for products by category hierarchy using aggregation
    /// </summary>
    public static BsonDocument CreateProductByCategoryFilter(DbObjectId[] categoryIds, bool isSiteQuery = false)
    {
        var categoryCollection = isSiteQuery ? "entity_categories" : "product_categories";
        
        return new BsonDocument
        {
            ["$lookup"] = new BsonDocument
            {
                ["from"] = "products",
                ["localField"] = "ProductId",
                ["foreignField"] = "_id",
                ["as"] = "product"
            }
        };
    }
}
