using DotNet.Testcontainers.Builders;
using Microsoft.Extensions.Logging;
using OElite;
using OElite.Restme.Abstractions;
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

    // We use IContainer here instead of KafkaContainer since we are customizing the builder significantly.
    // However, since KafkaBuilder wraps the ContainerBuilder, we can stick to KafkaContainer.
    private KafkaContainer _kafkaContainer = null!;
    private readonly ILoggerFactory _loggerFactory;

    protected KafkaTestBase()
    {
        // Setup logging
        _loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        Logger = _loggerFactory.CreateLogger(GetType());

        // --- KRaft Configuration Constants ---
        const int ClientPort = 9092;
        const int ControllerPort = 9093;
        const int BrokerInternalPort = 9094; // Dedicated port for the BROKER listener
        const string ClusterId = "4tN_Q9F_T2h9mS4nB7d8aQ";
        const string LogDirs = "/var/lib/kafka/data";

        // 🔑 CRITICAL FIX: Explicit command sequence: Format storage, then start the server.
        // This overrides the Confluent entrypoint to guarantee the format step runs first.
        string startupCommand =
            $"kafka-storage format --ignore-formatted -t {ClusterId} -c /etc/kafka/server.properties && " +
            "exec /etc/confluent/docker/run";

        _kafkaContainer = new KafkaBuilder()
            .WithImage("confluentinc/cp-kafka:7.9.0")
            // Fix replication factor for single-node testing
            .WithEnvironment("KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR", "1")
            .WithEnvironment("KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR", "1")
            .WithEnvironment("KAFKA_TRANSACTION_STATE_LOG_MIN_ISR", "1")
            .WithEnvironment("KAFKA_DEFAULT_REPLICATION_FACTOR", "1")
            .WithEnvironment("KAFKA_MIN_INSYNC_REPLICAS", "1")
            .Build();
        //
        // // 1. COMMAND OVERRIDE: Forces the storage format before running the main script.
        // .WithCommand("sh", "-c", startupCommand)
        //
        // // --- 2. Port Exposure (We expose all required ports) ---
        // // Note: Since we are using KafkaBuilder, we must use WithPortBinding for port mapping.
        // .WithExposedPort(ClientPort)
        // .WithExposedPort(ControllerPort)
        // .WithExposedPort(BrokerInternalPort)
        //
        // // --- 3. KRaft Required Configuration ---
        // .WithEnvironment("KAFKA_NODE_ID", "1")
        // .WithEnvironment("KAFKA_PROCESS_ROLES", "broker,controller")
        // .WithEnvironment("KAFKA_CONTROLLER_QUORUM_VOTERS", $"1@127.0.0.1:{ControllerPort}")
        // .WithEnvironment("CLUSTER_ID", ClusterId)
        // .WithEnvironment("KAFKA_LOG_DIRS", LogDirs)
        // .WithEnvironment("KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR", "1")
        // .WithEnvironment("KAFKA_CONTROLLER_LISTENER_NAMES", "CONTROLLER")
        //
        // // --- 4. Listener Configuration (Stable and validated) ---
        // .WithEnvironment("KAFKA_LISTENERS",
        //     $"PLAINTEXT://0.0.0.0:{ClientPort},CONTROLLER://0.0.0.0:{ControllerPort},BROKER://0.0.0.0:{BrokerInternalPort}")
        // .WithEnvironment("KAFKA_ADVERTISED_LISTENERS",
        //     $"PLAINTEXT://localhost:{ClientPort},BROKER://localhost:{BrokerInternalPort}")
        // .WithEnvironment("KAFKA_LISTENER_SECURITY_PROTOCOL_MAP",
        //     "PLAINTEXT:PLAINTEXT,CONTROLLER:PLAINTEXT,BROKER:PLAINTEXT")
        //
        // // 5. Port Binding (Map the ClientPort to the host)
        // .WithPortBinding(ClientPort, true)
        //
        // // 6. Wait Strategy (Wait for the success log message)
        // .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(9092))
        //
        //
        // .Build();

        Logger.LogInformation("Kafka test container initialized with KRaft configuration");
    }

    public async Task InitializeAsync()
    {
        try
        {
            await _kafkaContainer.StartAsync();
            var bootstrapServers = _kafkaContainer.GetBootstrapAddress();

            Logger.LogInformation("Kafka container started: {BootstrapServers}", bootstrapServers);

            // Configure Rest with Kafka
            Rest = new Rest(new RestConfig(RestMode.Kafka)
            {
                ConnectionString = $"kafka://{bootstrapServers}"
            });

            Logger.LogInformation("Rest instance created. StreamingProvider: {ProviderType}",
                Rest.GetProvider<IEventStreamProvider>()?.GetType().Name ?? "null");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to start Kafka container");

            // Try to get logs for debugging
            try
            {
                var logs = await _kafkaContainer.GetLogsAsync();
                Logger.LogError("Kafka container stdout: {Stdout}", logs.Stdout);
                Logger.LogError("Kafka container stderr: {Stderr}", logs.Stderr);
            }
            catch
            {
                Logger.LogError("Could not retrieve container logs");
            }

            throw;
        }
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