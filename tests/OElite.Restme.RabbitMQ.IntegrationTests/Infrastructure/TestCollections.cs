using System.Threading.Tasks;
using Xunit;

namespace OElite.Restme.RabbitMQ.IntegrationTests.Infrastructure;

/// <summary>
/// Test collection definition for RabbitMQ integration tests
/// Ensures tests run sequentially to avoid container conflicts
/// </summary>
[CollectionDefinition("RabbitMQIntegration")]
public class RabbitMQIntegrationCollection : ICollectionFixture<RabbitMQTestFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}

/// <summary>
/// Shared test fixture for RabbitMQ integration tests
/// Provides common setup and teardown for the entire test collection
/// </summary>
public class RabbitMQTestFixture : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        // Global setup for the test collection if needed
        // Currently handled by individual test base classes
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        // Global cleanup for the test collection if needed
        await Task.CompletedTask;
    }
}