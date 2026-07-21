using MongoDB.Bson;
using MongoDB.Driver;

namespace OElite.Restme.MongoDb;

public abstract class MongoDbCentre : IDisposable
{
    private readonly MongoDbAdapter _adapter;
    private bool _disposed = false;
    private static readonly object _classMapLock = new object();
    private static readonly HashSet<Type> _configuredTypes = new HashSet<Type>();

    /// <summary>
    /// Creates a DbCentre using a specific MongoDB connection string
    /// </summary>
    /// <param name="mongoDbConnectionString">Full MongoDB connection string with database name</param>
    public MongoDbCentre(string mongoDbConnectionString)
    {
        var (connectionString, databaseName) = ParseMongoDbConnectionString(mongoDbConnectionString);
        _adapter = new MongoDbAdapter(connectionString, databaseName);
    }

    /// <summary>
    /// Get a database transaction - mimics SQL Server transaction pattern
    /// </summary>
    public async Task<IMongoTransaction> GetDbTransactionAsync()
    {
        return await _adapter.CreateTransactionAsync();
    }

    /// <summary>
    /// Get a query for a specific entity collection
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public IMongoQuery<T> GetQuery<T>() where T : BaseEntity
    {
        return new MongoQuery<T>(_adapter.GetCollection<T>());
    }

    /// <summary>
    /// Get a collection for a specific entity type
    /// </summary>
    public IMongoCollection<T> GetCollection<T>(string collectionName = null) where T : BaseEntity
    {
        return _adapter.GetCollection<T>(collectionName);
    }

    /// <summary>
    /// Get a collection for any type (used by migration services)
    /// </summary>
    public IMongoCollection<T> GetCollectionForMigration<T>(string collectionName)
    {
        ConfigureClassMapForType<T>();
        return _adapter.Database.GetCollection<T>(collectionName);
    }

    /// <summary>
    /// Get a collection for BsonDocument operations
    /// </summary>
    public IMongoCollection<BsonDocument> GetBsonCollection(string collectionName)
    {
        return _adapter.Database.GetCollection<BsonDocument>(collectionName);
    }

    /// <summary>
    /// Get a MongoDB-free collection interface for document operations
    /// This completely abstracts away MongoDB types and provides clean Dictionary/MongoDbDocument operations
    /// </summary>
    public IMongoDbCollection GetMongoDbCollection(string collectionName)
    {
        var bsonCollection = _adapter.Database.GetCollection<BsonDocument>(collectionName);
        return new MongoDbCollectionImplementation(bsonCollection);
    }

    /// <summary>
    /// Get a MongoDB-free typed collection interface for entity operations
    /// This provides strongly-typed operations without exposing MongoDB types
    /// </summary>
    public IMongoDbCollection<T> GetMongoDbCollection<T>(string collectionName = null) where T : BaseEntity
    {
        var mongoCollection = _adapter.GetCollection<T>(collectionName);
        return new MongoDbCollectionImplementation<T>(mongoCollection);
    }

    /// <summary>
    /// Get the MongoDB database instance
    /// </summary>
    public IMongoDatabase GetDatabase()
    {
        return _adapter.Database;
    }

    /// <summary>
    /// Parse MongoDB connection string to extract database name and clean connection string
    /// </summary>
    private static (string connectionString, string databaseName) ParseMongoDbConnectionString(
        string fullConnectionString)
    {
        if (string.IsNullOrEmpty(fullConnectionString))
        {
            throw new ArgumentException("MongoDB connection string cannot be null or empty",
                nameof(fullConnectionString));
        }

        // MongoDB connection string format: mongodb://[username:password@]host[:port][/database][?options]
        // We need to extract the database name and remove it from the connection string

        var uri = new Uri(fullConnectionString);
        var databaseName = uri.AbsolutePath.TrimStart('/');

        // If no database specified in path, try to extract from query parameters
        if (string.IsNullOrEmpty(databaseName))
        {
            var query = global::System.Web.HttpUtility.ParseQueryString(uri.Query);
            databaseName = query["database"] ?? query["db"];
        }

        // If still no database name, use a default
        if (string.IsNullOrEmpty(databaseName))
        {
            databaseName = "oelite"; // Default database name
        }

        // Create clean connection string without database path
        var userInfo = string.IsNullOrEmpty(uri.UserInfo) ? "" : $"{uri.UserInfo}@";
        var cleanConnectionString = $"{uri.Scheme}://{userInfo}{uri.Host}:{uri.Port}";

        // Add query parameters if they exist (excluding database-related ones)
        if (!string.IsNullOrEmpty(uri.Query))
        {
            var query = global::System.Web.HttpUtility.ParseQueryString(uri.Query);
            var cleanQueryParams = new List<string>();

            foreach (string key in query.AllKeys)
            {
                if (key != null && !key.Equals("database", StringComparison.OrdinalIgnoreCase) &&
                    !key.Equals("db", StringComparison.OrdinalIgnoreCase))
                {
                    cleanQueryParams.Add($"{key}={query[key]}");
                }
            }

            if (cleanQueryParams.Count > 0)
            {
                cleanConnectionString += "?" + string.Join("&", cleanQueryParams);
            }
        }

        return (cleanConnectionString, databaseName);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Dispose managed resources
            }

            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Configures MongoDB class mapping for the given type to handle property conflicts
    /// </summary>
    private static void ConfigureClassMapForType<T>()
    {
        lock (_classMapLock)
        {
            var type = typeof(T);
            MongoClassMapConfigurator.ConfigureClassMappingForType(type, _configuredTypes);
        }
    }
}