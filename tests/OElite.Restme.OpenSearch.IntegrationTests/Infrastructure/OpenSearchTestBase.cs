using DotNet.Testcontainers.Builders;
using Microsoft.Extensions.Logging;
using OElite;
using Testcontainers.Elasticsearch;
using Xunit;

namespace OElite.Restme.OpenSearch.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for OpenSearch integration tests using test containers
/// </summary>
public abstract class OpenSearchTestBase : IAsyncLifetime
{
    protected Rest Rest = null!;
    protected readonly ILogger Logger;

    private ElasticsearchContainer _openSearchContainer = null!;
    private readonly ILoggerFactory _loggerFactory;

    protected OpenSearchTestBase()
    {
        // Setup logging
        _loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        Logger = _loggerFactory.CreateLogger(GetType());

        // Setup OpenSearch test container
        _openSearchContainer = new ElasticsearchBuilder()
            .WithImage("opensearchproject/opensearch:3.3.2")
            .WithPortBinding(9200, true)
            .WithEnvironment("discovery.type", "single-node")
            .WithEnvironment("OPENSEARCH_JAVA_OPTS", "-Xms512m -Xmx512m")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(9200))
            .Build();

        Logger.LogInformation("OpenSearch test container initialized");
    }

    public async Task InitializeAsync()
    {
        await _openSearchContainer.StartAsync();
        var connectionString = _openSearchContainer.GetConnectionString();

        Logger.LogInformation("OpenSearch container started: {ConnectionString}", connectionString);

        // Configure Rest with OpenSearch
        Rest = new Rest($"{connectionString}", new RestConfig
        {
            OperationMode = RestMode.OpenSearch
        });
    }

    public async Task DisposeAsync()
    {
        await _openSearchContainer.DisposeAsync();
        _loggerFactory?.Dispose();
        Logger.LogInformation("OpenSearch container disposed");
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