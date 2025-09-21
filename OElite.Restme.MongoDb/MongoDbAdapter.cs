using MongoDB.Driver;
using MongoDB.Bson;

namespace OElite.Restme.MongoDb;

/// <summary>
/// MongoDB adapter that provides a SQL-like interface for MongoDB operations
/// This allows gradual migration from SQL to MongoDB
/// </summary>
public class MongoDbAdapter
{
    private readonly IMongoDatabase _database;
    private readonly IMongoClient _client;
    private readonly string _connectionString;
    private readonly string _databaseName;

    /// <summary>
    /// Gets the MongoDB database instance
    /// </summary>
    public IMongoDatabase Database => _database;

    public MongoDbAdapter(string connectionString, string databaseName)
    {
        _connectionString = connectionString;
        _databaseName = databaseName;
        _client = new MongoClient(connectionString);
        _database = _client.GetDatabase(databaseName);

        // Register custom serializers
        DbObjectIdSerializerProvider.RegisterSerializer();

        // Configure class mappings
        MongoClassMapConfigurator.ConfigureClassMappings();
    }

    /// <summary>
    /// Get a collection for a specific entity type
    /// </summary>
    public IMongoCollection<T> GetCollection<T>(string collectionName = null) where T : BaseEntity
    {
        // Ensure class mapping is registered for this type
        MongoClassMapConfigurator.RegisterClassMapping<T>();

        if (string.IsNullOrEmpty(collectionName))
        {
            collectionName = MongoDbAttributeMapper.GetCollectionName(typeof(T)) ?? typeof(T).Name.ToLowerInvariant();
        }

        return _database.GetCollection<T>(collectionName);
    }

    /// <summary>
    /// Get a collection for a specific entity type with transaction support
    /// </summary>
    public IMongoCollection<T> GetCollection<T>(IClientSessionHandle session, string collectionName = null)
        where T : BaseEntity
    {
        // Ensure class mapping is registered for this type
        MongoClassMapConfigurator.RegisterClassMapping<T>();

        if (string.IsNullOrEmpty(collectionName))
        {
            collectionName = MongoDbAttributeMapper.GetCollectionName(typeof(T)) ?? typeof(T).Name.ToLowerInvariant();
        }

        return _database.GetCollection<T>(collectionName);
    }

    /// <summary>
    /// Start a new transaction session
    /// </summary>
    public async Task<IClientSessionHandle> StartSessionAsync()
    {
        return await _client.StartSessionAsync();
    }

    /// <summary>
    /// Create a new transaction
    /// </summary>
    public async Task<IMongoTransaction> CreateTransactionAsync(TransactionOptions options = null)
    {
        var session = await StartSessionAsync();
        return new MongoTransaction(session, options);
    }

    /// <summary>
    /// Execute a simple query with filters
    /// </summary>
    public async Task<List<T>> QueryAsync<T>(string collectionName, Dictionary<string, object> filters = null,
        int limit = 0, int skip = 0) where T : BaseEntity
    {
        var collection = GetCollection<T>(collectionName);
        var filterBuilder = Builders<T>.Filter;
        FilterDefinition<T> filter = filterBuilder.Empty;

        if (filters != null && filters.Any())
        {
            var filterDefinitions = new List<FilterDefinition<T>>();

            foreach (var kvp in filters)
            {
                if (kvp.Value is string stringValue)
                {
                    filterDefinitions.Add(filterBuilder.Eq(kvp.Key, stringValue));
                }
                else if (kvp.Value is int intValue)
                {
                    filterDefinitions.Add(filterBuilder.Eq(kvp.Key, intValue));
                }
                else if (kvp.Value is long longValue)
                {
                    filterDefinitions.Add(filterBuilder.Eq(kvp.Key, longValue));
                }
                else if (kvp.Value is bool boolValue)
                {
                    filterDefinitions.Add(filterBuilder.Eq(kvp.Key, boolValue));
                }
                else if (kvp.Value is DateTime dateValue)
                {
                    filterDefinitions.Add(filterBuilder.Eq(kvp.Key, dateValue));
                }
                else if (kvp.Value is IEnumerable<object> arrayValue)
                {
                    filterDefinitions.Add(filterBuilder.In(kvp.Key, arrayValue));
                }
            }

            if (filterDefinitions.Any())
            {
                filter = filterBuilder.And(filterDefinitions);
            }
        }

        var options = new FindOptions<T>();
        if (limit > 0) options.Limit = limit;
        if (skip > 0) options.Skip = skip;

        var cursor = await collection.FindAsync(filter, options);
        return await cursor.ToListAsync();
    }

    /// <summary>
    /// Count documents in a collection
    /// </summary>
    public async Task<long> CountAsync<T>(string collectionName, Dictionary<string, object>? filters = null)
        where T : BaseEntity
    {
        var collection = GetCollection<T>(collectionName);
        var filterBuilder = Builders<T>.Filter;
        FilterDefinition<T> filter = filterBuilder.Empty;

        if (filters != null && filters.Any())
        {
            var filterDefinitions = new List<FilterDefinition<T>>();

            foreach (var kvp in filters)
            {
                if (kvp.Value is string stringValue)
                {
                    filterDefinitions.Add(filterBuilder.Eq(kvp.Key, stringValue));
                }
                else if (kvp.Value is int intValue)
                {
                    filterDefinitions.Add(filterBuilder.Eq(kvp.Key, intValue));
                }
                else if (kvp.Value is long longValue)
                {
                    filterDefinitions.Add(filterBuilder.Eq(kvp.Key, longValue));
                }
                else if (kvp.Value is bool boolValue)
                {
                    filterDefinitions.Add(filterBuilder.Eq(kvp.Key, boolValue));
                }
                else if (kvp.Value is DateTime dateValue)
                {
                    filterDefinitions.Add(filterBuilder.Eq(kvp.Key, dateValue));
                }
                else if (kvp.Value is IEnumerable<object> arrayValue)
                {
                    filterDefinitions.Add(filterBuilder.In(kvp.Key, arrayValue));
                }
            }

            if (filterDefinitions.Any())
            {
                filter = filterBuilder.And(filterDefinitions);
            }
        }

        return await collection.CountDocumentsAsync(filter);
    }

    /// <summary>
    /// Insert a document
    /// </summary>
    public async Task InsertOneAsync<T>(string collectionName, T document) where T : BaseEntity
    {
        var collection = GetCollection<T>(collectionName);
        await collection.InsertOneAsync(document);
    }

    /// <summary>
    /// Insert multiple documents
    /// </summary>
    public async Task InsertManyAsync<T>(string collectionName, IEnumerable<T> documents) where T : BaseEntity
    {
        var collection = GetCollection<T>(collectionName);
        await collection.InsertManyAsync(documents);
    }

    /// <summary>
    /// Update a document
    /// </summary>
    public async Task<UpdateResult> UpdateOneAsync<T>(string collectionName, Dictionary<string, object> filter,
        Dictionary<string, object> update) where T : BaseEntity
    {
        var collection = GetCollection<T>(collectionName);
        var filterBuilder = Builders<T>.Filter;
        var updateBuilder = Builders<T>.Update;

        FilterDefinition<T> filterDef = filterBuilder.Empty;
        UpdateDefinition<T> updateDef = updateBuilder.Set("_id", ObjectId.GenerateNewId());

        // Build filter
        if (filter != null && filter.Any())
        {
            var filterDefinitions = new List<FilterDefinition<T>>();
            foreach (var kvp in filter)
            {
                if (kvp.Value is string stringValue)
                {
                    filterDefinitions.Add(filterBuilder.Eq(kvp.Key, stringValue));
                }
                else if (kvp.Value is int intValue)
                {
                    filterDefinitions.Add(filterBuilder.Eq(kvp.Key, intValue));
                }
                else if (kvp.Value is long longValue)
                {
                    filterDefinitions.Add(filterBuilder.Eq(kvp.Key, longValue));
                }
            }

            if (filterDefinitions.Any())
            {
                filterDef = filterBuilder.And(filterDefinitions);
            }
        }

        // Build update
        if (update != null && update.Any())
        {
            var updateDefinitions = new List<UpdateDefinition<T>>();
            foreach (var kvp in update)
            {
                updateDefinitions.Add(updateBuilder.Set(kvp.Key, kvp.Value));
            }

            if (updateDefinitions.Any())
            {
                updateDef = updateBuilder.Combine(updateDefinitions);
            }
        }

        return await collection.UpdateOneAsync(filterDef, updateDef);
    }

    /// <summary>
    /// Delete a document
    /// </summary>
    public async Task<DeleteResult> DeleteOneAsync<T>(string collectionName, Dictionary<string, object> filter)
        where T : BaseEntity
    {
        var collection = GetCollection<T>(collectionName);
        var filterBuilder = Builders<T>.Filter;

        FilterDefinition<T> filterDef = filterBuilder.Empty;

        if (filter != null && filter.Any())
        {
            var filterDefinitions = new List<FilterDefinition<T>>();
            foreach (var kvp in filter)
            {
                if (kvp.Value is string stringValue)
                {
                    filterDefinitions.Add(filterBuilder.Eq(kvp.Key, stringValue));
                }
                else if (kvp.Value is int intValue)
                {
                    filterDefinitions.Add(filterBuilder.Eq(kvp.Key, intValue));
                }
                else if (kvp.Value is long longValue)
                {
                    filterDefinitions.Add(filterBuilder.Eq(kvp.Key, longValue));
                }
            }

            if (filterDefinitions.Any())
            {
                filterDef = filterBuilder.And(filterDefinitions);
            }
        }

        return await collection.DeleteOneAsync(filterDef);
    }
}