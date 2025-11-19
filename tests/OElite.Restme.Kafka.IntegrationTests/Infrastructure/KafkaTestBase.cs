using DotNet.Testcontainers.Builders;
using Microsoft.Extensions.Logging;
using OElite;
using Testcontainers.Kafka;
using Xunit;

namespace OElite.Restme.Kafka.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for Kafka integration tests using test containers
/// </summary>
public abstract class KafkaTestBase : IAsyncLifetime
{
    protected Rest Rest = null!;
    protected readonly ILogger Logger;

    private KafkaContainer _kafkaContainer = null!;
    private readonly ILoggerFactory _loggerFactory;

    protected KafkaTestBase()
    {
        // Setup logging
        _loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        Logger = _loggerFactory.CreateLogger(GetType());

        // Setup Kafka test container
        _kafkaContainer = new KafkaBuilder()
            .WithImage("confluentinc/cp-kafka:latest")
            .WithPortBinding(9092, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(9092))
            .Build();

        Logger.LogInformation("Kafka test container initialized");
    }

    public async Task InitializeAsync()
    {
        await _kafkaContainer.StartAsync();
        var bootstrapServers = _kafkaContainer.GetBootstrapAddress();

        Logger.LogInformation("Kafka container started: {BootstrapServers}", bootstrapServers);

        // Configure Rest with Kafka
        Rest = new Rest($"kafka://{bootstrapServers}", new RestConfig
        {
            OperationMode = RestMode.Kafka
        });
    }

    public async Task DisposeAsync()
    {
        await _kafkaContainer.DisposeAsync();
        _loggerFactory?.Dispose();
        Logger.LogInformation("Kafka container disposed");
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