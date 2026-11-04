using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using OElite.Restme.MongoDb;
using Xunit;

namespace OElite.Restme.MongoDb.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for all integration tests providing MongoDB setup and cleanup.
///
/// IMPORTANT (shared-infra standard 16, PROHIBITED rule 7): this base class does NOT
/// spin up a per-repo/per-test MongoDB container. It connects to the machine-wide
/// shared oelite-stack mongos router at <c>mongodb://localhost:27017/?directConnection=true</c>
/// (see <c>coding-standards/infrastructure/oelite-stack/oelite-stack.sh health</c>).
/// A Testcontainers fixture would be a second instance and would also collide with the
/// shared mongos that already owns host port 27017.
///
/// Per-project isolation is by database name only. Each test class gets its own
/// database so concurrent test classes never touch the same collection.
/// </summary>
public abstract class TestBase : IAsyncLifetime
{
    protected IMongoDatabase Database = null!;
    protected TestMongoDbCentre DbCentre = null!;
    protected readonly ILogger Logger;
    protected string TestDatabaseName = null!;

    private MongoClient _mongoClient = null!;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// Canonical shared-stack connection string. The mongos router is the only entry
    /// point for app code (standard 16, PROHIBITED rule 4: never connect directly to
    /// a configsvr or shard mongod). <c>directConnection=true</c> tells the driver to
    /// skip replica-set discovery and go straight to mongos.
    /// </summary>
    private const string SharedMongoConnectionString =
        "mongodb://localhost:27017/?directConnection=true";

    protected TestBase()
    {
        _loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        Logger = _loggerFactory.CreateLogger(GetType());
    }

    public async Task InitializeAsync()
    {
        Logger.LogInformation("Connecting to shared oelite-stack mongos: {ConnectionString}", SharedMongoConnectionString);
        _mongoClient = new MongoClient(SharedMongoConnectionString);

        // Verify the shared stack is actually reachable before any test runs.
        try
        {
            var ping = await _mongoClient.GetDatabase("admin").RunCommandAsync<BsonDocument>(
                new BsonDocument("ping", 1));
            if (ping["ok"].ToInt32() != 1)
            {
                throw new InvalidOperationException(
                    "Shared oelite-stack mongos ping failed. Start it with: " +
                    "cd coding-standards/infrastructure/oelite-stack && ./oelite-stack.sh up");
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                "Shared oelite-stack mongos is not reachable on localhost:27017. " +
                "Start it with: cd coding-standards/infrastructure/oelite-stack && ./oelite-stack.sh up",
                ex);
        }

        // Per-project isolation by database name only.
        TestDatabaseName = $"integration_tests_{Guid.NewGuid():N}";
        Database = _mongoClient.GetDatabase(TestDatabaseName);

        // MongoDbCentre parses the connection string and extracts the database name,
        // so pass the database explicitly in the path.
        var fullConnectionString = $"mongodb://localhost:27017/{TestDatabaseName}?directConnection=true";
        DbCentre = new TestMongoDbCentre(fullConnectionString);

        Logger.LogInformation("Test database created: {DatabaseName}", TestDatabaseName);
    }

    public async Task DisposeAsync()
    {
        // Drop the test database so concurrent test classes never collide on collection
        // names. The shared stack itself is left running.
        try
        {
            await _mongoClient.DropDatabaseAsync(TestDatabaseName);
        }
        catch
        {
            // Best-effort cleanup; never let cleanup failures fail the test run.
        }
        _loggerFactory.Dispose();
        Logger.LogInformation("Test database dropped: {DatabaseName}", TestDatabaseName);
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
        if (disposing)
        {
            _loggerFactory?.Dispose();
        }
    }
}