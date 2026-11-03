using Microsoft.Extensions.Logging;
using Xunit;

namespace OElite.Restme.OpenSearch.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for OpenSearch integration tests against the shared local stack
/// </summary>
public abstract class OpenSearchTestBase : IAsyncLifetime
{
    protected Rest Rest = null!;
    protected readonly ILogger Logger;

    private readonly ILoggerFactory _loggerFactory;

    // Integration tests use the shared local OpenSearch stack.
    private const string SharedOpenSearchConnectionString = "https://localhost:9200";

    protected OpenSearchTestBase()
    {
        // Setup logging
        _loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        Logger = _loggerFactory.CreateLogger(GetType());

        Logger.LogInformation("OpenSearch integration tests will target the shared local stack");
    }

    public async Task InitializeAsync()
    {
        // Configure Rest with OpenSearch against the shared stack; credentials are
        // injected via OELITE_TEST_OPENSEARCH_PASSWORD so they never appear in source.
        var password = Environment.GetEnvironmentVariable("OELITE_TEST_OPENSEARCH_PASSWORD")
                       ?? throw new InvalidOperationException(
                           "OELITE_TEST_OPENSEARCH_PASSWORD must be set to the shared-stack OpenSearch admin password.");
        var username = Environment.GetEnvironmentVariable("OELITE_TEST_OPENSEARCH_USERNAME") ?? "admin";

        Rest = new Rest(SharedOpenSearchConnectionString, new RestConfig(RestMode.OpenSearch)
        {
            AuthKey = username,
            AuthSecret = password
        });

        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        Rest.Dispose();
        _loggerFactory?.Dispose();
        Logger.LogInformation("OpenSearch integration test context disposed");
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