using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using OElite.Restme.MongoDb;

namespace OElite.Restme.MongoDb.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for all integration tests providing MongoDB setup and cleanup
/// </summary>
public abstract class TestBase : IDisposable
{
    protected readonly IMongoDatabase Database;
    protected readonly TestMongoDbCentre DbCentre;
    protected readonly ILogger Logger;
    protected readonly string TestDatabaseName;

    private readonly MongoClient _mongoClient;
    private readonly ILoggerFactory _loggerFactory;
    private bool _disposed;

    protected TestBase()
    {
        // Load configuration
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        var connectionString = configuration.GetConnectionString("MongoDB")
            ?? throw new InvalidOperationException("MongoDB connection string not found in configuration");

        // Setup logging
        _loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        Logger = _loggerFactory.CreateLogger(GetType());

        // Setup MongoDB client
        _mongoClient = new MongoClient(connectionString);

        // Create unique test database name to avoid conflicts
        TestDatabaseName = $"integration_tests_{Guid.NewGuid():N}";
        Database = _mongoClient.GetDatabase(TestDatabaseName);

        // Initialize TestMongoDbCentre
        // Replace the database name in the connection string with our test database name
        var builder = new UriBuilder(connectionString);
        builder.Path = $"/{TestDatabaseName}";
        var testConnectionString = builder.ToString();
        DbCentre = new TestMongoDbCentre(testConnectionString);

        Logger.LogInformation("Test database created: {DatabaseName}", TestDatabaseName);
    }

    /// <summary>
    /// Setup method called before each test
    /// </summary>
    protected virtual async Task SetupAsync()
    {
        // Override in derived classes for test-specific setup
        await Task.CompletedTask;
    }

    /// <summary>
    /// Cleanup method called after each test
    /// </summary>
    protected virtual async Task CleanupAsync()
    {
        // Override in derived classes for test-specific cleanup
        await Task.CompletedTask;
    }

    /// <summary>
    /// Drops all collections in the test database
    /// </summary>
    protected async Task DropAllCollectionsAsync()
    {
        var collections = await Database.ListCollectionNamesAsync();
        var collectionsList = await collections.ToListAsync();
        foreach (var collectionName in collectionsList)
        {
            await Database.DropCollectionAsync(collectionName);
        }
    }

    /// <summary>
    /// Gets a collection by name
    /// </summary>
    protected IMongoCollection<T> GetCollection<T>(string collectionName)
    {
        return Database.GetCollection<T>(collectionName);
    }

    /// <summary>
    /// Gets a strongly-typed IMongoDbCollection for testing
    /// </summary>
    protected IMongoDbCollection<T> GetDbCollection<T>() where T : OElite.BaseEntity
    {
        return DbCentre.GetMongoDbCollection<T>();
    }

    /// <summary>
    /// Gets an untyped IMongoDbCollection for testing
    /// </summary>
    protected IMongoDbCollection GetDbCollection(string collectionName)
    {
        return DbCentre.GetMongoDbCollection(collectionName);
    }

    /// <summary>
    /// Gets a raw MongoDB collection for direct operations (for complex test scenarios)
    /// </summary>
    protected MongoDB.Driver.IMongoCollection<T> GetRawMongoCollection<T>(string collectionName = null) where T : OElite.BaseEntity
    {
        return DbCentre.GetCollection<T>(collectionName);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            try
            {
                // Drop the test database
                _mongoClient.DropDatabase(TestDatabaseName);
                Logger.LogInformation("Test database dropped: {DatabaseName}", TestDatabaseName);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error dropping test database: {DatabaseName}", TestDatabaseName);
            }

            _loggerFactory?.Dispose();
            _disposed = true;
        }
    }
}