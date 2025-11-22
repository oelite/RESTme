using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using OElite.Restme.MongoDb;
using Testcontainers.MongoDb;
using DotNet.Testcontainers.Builders;
using Xunit;

namespace OElite.Restme.MongoDb.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for all integration tests providing MongoDB setup and cleanup
/// </summary>
public abstract class TestBase : IAsyncLifetime
{
    protected IMongoDatabase Database = null!;
    protected TestMongoDbCentre DbCentre = null!;
    protected readonly ILogger Logger;
    protected string TestDatabaseName = null!;

    private MongoClient _mongoClient = null!;
    private readonly ILoggerFactory _loggerFactory;
    private readonly MongoDbContainer _mongoContainer;
    private bool _disposed;

    protected TestBase()
    {
        // Setup logging first
        _loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        Logger = _loggerFactory.CreateLogger(GetType());

        // Setup MongoDB test container
        _mongoContainer = new MongoDbBuilder()
            .WithImage("mongo:8.0.15")
            .WithPortBinding(27017, true)
            .Build();

        Logger.LogInformation("MongoDB test container initialized");
    }

    public async Task InitializeAsync()
    {
        await _mongoContainer.StartAsync();
        var connectionString = _mongoContainer.GetConnectionString();

        Logger.LogInformation("MongoDB container started: {ConnectionString}", connectionString);

        // Setup MongoDB client
        _mongoClient = new MongoClient(connectionString);

        // Create unique test database name to avoid conflicts
        TestDatabaseName = $"integration_tests_{Guid.NewGuid():N}";
        Database = _mongoClient.GetDatabase(TestDatabaseName);

        // Initialize TestMongoDbCentre with the full connection string including database
        // But we need to construct it properly to avoid URI parsing issues
        var baseUri = new Uri(connectionString);

        var fullConnectionString =
            $"{baseUri.Scheme}://{baseUri.UserInfo}@{baseUri.Host}:{baseUri.Port}/{TestDatabaseName}";
        if (!string.IsNullOrEmpty(baseUri.Query))
        {
            fullConnectionString += baseUri.Query;
        }

        Logger.LogInformation("Full connection string for TestMongoDbCentre: {FullConnectionString}",
            fullConnectionString);
        DbCentre = new TestMongoDbCentre(fullConnectionString);

        Logger.LogInformation("Test database created: {DatabaseName}", TestDatabaseName);
    }

    public async Task DisposeAsync()
    {
        await _mongoContainer.DisposeAsync();
        Logger.LogInformation("MongoDB container disposed");
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
    protected MongoDB.Driver.IMongoCollection<T> GetRawMongoCollection<T>(string collectionName = null)
        where T : OElite.BaseEntity
    {
        return DbCentre.GetCollection<T>(collectionName);
    }

    // IAsyncLifetime handles disposal via DisposeAsync()
    // Keeping IDisposable for backward compatibility with test framework
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _loggerFactory?.Dispose();
            _disposed = true;
        }
    }
}