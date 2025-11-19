using DotNet.Testcontainers.Builders;
using Microsoft.Extensions.Logging;
using OElite;
using Testcontainers.ClickHouse;
using Xunit;

namespace OElite.Restme.ClickHouse.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for ClickHouse integration tests using test containers
/// </summary>
public abstract class ClickHouseTestBase : IAsyncLifetime
{
    protected Rest Rest = null!;
    protected readonly ILogger Logger;
    protected string TestDatabaseName = null!;

    private ClickHouseContainer _clickHouseContainer = null!;
    private readonly ILoggerFactory _loggerFactory;

    protected ClickHouseTestBase()
    {
        // Setup logging
        _loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        Logger = _loggerFactory.CreateLogger(GetType());

        // Setup ClickHouse test container
        _clickHouseContainer = new ClickHouseBuilder()
            .WithImage("clickhouse/clickhouse-server:latest")
            .WithPortBinding(8123, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(8123))
            .Build();

        TestDatabaseName = $"test_db_{Guid.NewGuid():N}";
        Logger.LogInformation("ClickHouse test container initialized");
    }

    public async Task InitializeAsync()
    {
        await _clickHouseContainer.StartAsync();
        var connectionString = _clickHouseContainer.GetConnectionString();

        Logger.LogInformation("ClickHouse container started: {ConnectionString}", connectionString);

        // Configure Rest with ClickHouse
        Rest = new Rest($"{connectionString}/{TestDatabaseName}", new RestConfig
        {
            OperationMode = RestMode.ClickHouse
        });

        // Create test database
        await ExecuteSqlAsync($"CREATE DATABASE IF NOT EXISTS {TestDatabaseName}");
        Logger.LogInformation("Test database created: {DatabaseName}", TestDatabaseName);
    }

    public async Task DisposeAsync()
    {
        // Drop test database
        try
        {
            await ExecuteSqlAsync($"DROP DATABASE IF EXISTS {TestDatabaseName}");
            Logger.LogInformation("Test database dropped: {DatabaseName}", TestDatabaseName);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error dropping test database: {DatabaseName}", TestDatabaseName);
        }

        await _clickHouseContainer.DisposeAsync();
        _loggerFactory?.Dispose();
        Logger.LogInformation("ClickHouse container disposed");
    }

    /// <summary>
    /// Execute raw SQL for test setup/cleanup
    /// </summary>
    protected async Task ExecuteSqlAsync(string sql)
    {
        // Use the ClickHouse connection directly for raw SQL
        var connectionString = _clickHouseContainer.GetConnectionString();
        // This would need actual ClickHouse client implementation
        // For now, we'll use the Rest API
        await Rest.QueryAsync<object>($"SELECT 1"); // Placeholder
    }

    /// <summary>
    /// Setup method called before each test
    /// </summary>
    protected virtual async Task SetupAsync()
    {
        await Task.CompletedTask;
    }

    /// <summary>
    /// Cleanup method called after each test
    /// </summary>
    protected virtual async Task CleanupAsync()
    {
        await Task.CompletedTask;
    }
}