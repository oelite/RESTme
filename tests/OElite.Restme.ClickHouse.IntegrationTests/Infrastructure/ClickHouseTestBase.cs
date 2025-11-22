using ClickHouse.Client.ADO;
using ClickHouse.Client.ADO.Parameters;
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

    protected ClickHouseContainer _clickHouseContainer = null!;
    private readonly ILoggerFactory _loggerFactory;

    protected ClickHouseTestBase()
    {
        // Setup logging
        _loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        Logger = _loggerFactory.CreateLogger(GetType());

        // Setup ClickHouse test container with proper authentication configuration
        _clickHouseContainer = new ClickHouseBuilder()
            .WithImage("clickhouse/clickhouse-server:latest")
            .WithPortBinding(8123, true)
            .WithPortBinding(9000, true)
            .WithEnvironment("CLICKHOUSE_DB", "test_db")
            .WithEnvironment("CLICKHOUSE_USER", "test_user")
            .WithEnvironment("CLICKHOUSE_PASSWORD", "test_password")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r.ForPath("/ping").ForPort(8123)))
            .Build();

        TestDatabaseName = $"test_db_{Guid.NewGuid():N}";
        Logger.LogInformation("ClickHouse test container initialized");
    }

    public async Task InitializeAsync()
    {
        await _clickHouseContainer.StartAsync();
        var containerConnectionString = _clickHouseContainer.GetConnectionString();

        Logger.LogInformation("ClickHouse container started: {ConnectionString}", containerConnectionString);

        // Use the test database configured in the container
        TestDatabaseName = "test_db";

        // Configure Rest with ClickHouse using the container's connection string with authentication
        var mappedPort = _clickHouseContainer.GetMappedPublicPort(8123);
        var clickHouseConnectionString =
            $"clickhouse://test_user:test_password@localhost:{mappedPort}/{TestDatabaseName}";

        Rest = new Rest(clickHouseConnectionString, new RestConfig(RestMode.ClickHouse)
        {
            ConnectionString = clickHouseConnectionString,
            InstanceName = TestDatabaseName
        });

        Logger.LogInformation("Rest configured with connection: {ConnectionString}", clickHouseConnectionString);
    }

    public async Task DisposeAsync()
    {
        await _clickHouseContainer.DisposeAsync();
        _loggerFactory?.Dispose();
        Logger.LogInformation("ClickHouse container disposed");
    }

    /// <summary>
    /// Execute raw SQL for test setup/cleanup
    /// </summary>
    protected async Task ExecuteSqlAsync(string sql, string? connectionString = null)
    {
        if (connectionString == null)
        {
            // Use the same authentication configuration as our Rest client
            var mappedPort = _clickHouseContainer.GetMappedPublicPort(8123);
            connectionString =
                $"Host=localhost;Port={mappedPort};Database={TestDatabaseName};Username=test_user;Password=test_password;Compress=false";
        }

        await using var connection = new ClickHouseConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Setup method called before each test
    /// </summary>
    protected virtual async Task SetupAsync()
    {
        await Task.CompletedTask;
    }

    /// <summary>
    /// Test basic HTTP connectivity to ClickHouse container
    /// </summary>
    protected async Task<bool> TestContainerConnectivityAsync()
    {
        try
        {
            var mappedPort = _clickHouseContainer.GetMappedPublicPort(8123);
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(5);

            // Try a simple ping to ClickHouse HTTP interface
            var response = await httpClient.GetAsync($"http://localhost:{mappedPort}/ping");
            Logger.LogInformation("Container connectivity test - Status: {StatusCode}", response.StatusCode);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Container connectivity test failed");
            return false;
        }
    }

    /// <summary>
    /// Cleanup method called after each test
    /// </summary>
    protected virtual async Task CleanupAsync()
    {
        await Task.CompletedTask;
    }
}