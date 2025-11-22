using DotNet.Testcontainers.Builders;
using Microsoft.Extensions.Logging;
using Testcontainers.OpenSearch;
using Xunit;

namespace OElite.Restme.OpenSearch.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for OpenSearch integration tests using test containers
/// </summary>
public abstract class OpenSearchTestBase : IAsyncLifetime
{
    protected Rest Rest = null!;
    protected readonly ILogger Logger;

    private OpenSearchContainer _openSearchContainer = null!;
    private readonly ILoggerFactory _loggerFactory;

    protected OpenSearchTestBase()
    {
        // Setup logging
        _loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        Logger = _loggerFactory.CreateLogger(GetType());

        // Setup OpenSearch test container with security plugin properly configured
        _openSearchContainer = new OpenSearchBuilder()
            .WithImage("opensearchproject/opensearch:2.8.0")
            .WithPassword("admin")
            // .WithPortBinding(9200, true)
            // .WithEnvironment("discovery.type", "single-node")
            // .WithEnvironment("bootstrap.memory_lock", "true")
            // .WithEnvironment("OPENSEARCH_JAVA_OPTS", "-Xms256m -Xmx256m")
            // .WithWaitStrategy(Wait.ForUnixContainer()
            //     .UntilInternalTcpPortIsAvailable(9200)
            //     .UntilHttpRequestIsSucceeded(request =>
            //         request.ForPort(9200).ForPath("/_cluster/health")))
            .Build();

        Logger.LogInformation("OpenSearch test container initialized");
    }

    public async Task InitializeAsync()
    {
        await _openSearchContainer.StartAsync();
        var connectionString = _openSearchContainer.GetConnectionString();

        // Configure Rest with OpenSearch with authentication and explicit mode
        Logger.LogInformation("Container connection string: {ConnectionString}", connectionString);
        Rest = new Rest(connectionString, new RestConfig
        {
            OperationMode = RestMode.OpenSearch,
            AuthKey = "admin",
            AuthSecret = "admin"
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