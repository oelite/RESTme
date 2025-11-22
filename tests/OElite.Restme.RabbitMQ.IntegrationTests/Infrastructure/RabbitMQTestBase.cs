using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Logging;
using OElite;
using OElite.Restme;
using OElite.Restme.Abstractions;
using Testcontainers.RabbitMq;
using Xunit;

namespace OElite.Restme.RabbitMQ.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for RabbitMQ integration tests using test containers
/// Enterprise-grade testing infrastructure with comprehensive setup and teardown
/// </summary>
public abstract class RabbitMQTestBase : IAsyncLifetime
{
    protected Rest Rest = null!;
    protected IQueueProvider QueueProvider = null!;
    protected readonly ILogger Logger;

    private RabbitMqContainer _rabbitMqContainer = null!;
    private readonly ILoggerFactory _loggerFactory;

    // Connection validation properties
    protected string ConnectionString => _rabbitMqContainer?.GetConnectionString() ?? string.Empty;
    protected bool IsContainerRunning => _rabbitMqContainer?.State == TestcontainersStates.Running;

    protected RabbitMQTestBase()
    {
        // Setup comprehensive logging for debugging
        _loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole()
                   .SetMinimumLevel(LogLevel.Information));
        Logger = _loggerFactory.CreateLogger(GetType());

        // Initialize RabbitMQ test container with enterprise configuration
        _rabbitMqContainer = new RabbitMqBuilder()
            .WithImage("rabbitmq:3.13-management")
            .WithUsername("testuser")
            .WithPassword("testpass")
            // Expose both AMQP and management ports
            .WithPortBinding(5672, true)
            .WithPortBinding(15672, true)
            // Wait for RabbitMQ to be ready
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Server startup complete"))
            // Add health check configuration
            .WithEnvironment("RABBITMQ_DEFAULT_USER", "testuser")
            .WithEnvironment("RABBITMQ_DEFAULT_PASS", "testpass")
            .WithEnvironment("RABBITMQ_DEFAULT_VHOST", "/")
            // Enable management plugin for monitoring
            .WithEnvironment("RABBITMQ_ENABLED_PLUGINS", "rabbitmq_management")
            .Build();

        Logger.LogInformation("RabbitMQ test container initialized with management interface");
    }

    public virtual async Task InitializeAsync()
    {
        try
        {
            Logger.LogInformation("Starting RabbitMQ container...");
            await _rabbitMqContainer.StartAsync();

            // Wait a bit for RabbitMQ to fully initialize
            await Task.Delay(2000);

            var connectionString = _rabbitMqContainer.GetConnectionString();
            Logger.LogInformation("RabbitMQ container started: {ConnectionString}", connectionString);

            // Verify container is actually running
            if (!IsContainerRunning)
            {
                throw new InvalidOperationException("RabbitMQ container failed to start properly");
            }

            // Configure Rest with RabbitMQ - ensure proper mode setting
            Rest = new Rest(connectionString, new RestConfig
            {
                OperationMode = RestMode.RabbitMq,
                AuthKey = "testuser",
                AuthSecret = "testpass"
            });

            // Get and validate QueueProvider
            QueueProvider = Rest.GetProvider<IQueueProvider>();
            if (QueueProvider == null)
            {
                throw new InvalidOperationException("Failed to initialize RabbitMQ QueueProvider");
            }

            Logger.LogInformation("Rest instance created successfully. QueueProvider: {ProviderType}",
                QueueProvider.GetType().Name);

            // Validate connection by attempting a simple operation
            await ValidateConnection();

            Logger.LogInformation("RabbitMQ integration test setup completed successfully");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to start RabbitMQ container");

            // Enhanced error logging with container state information
            await LogContainerDiagnostics();
            throw;
        }
    }

    public virtual async Task DisposeAsync()
    {
        try
        {
            Logger.LogInformation("Starting cleanup of RabbitMQ test resources...");

            // Gracefully stop consuming if active
            if (QueueProvider != null)
            {
                try
                {
                    await QueueProvider.StopConsumingAsync(CancellationToken.None);
                    Logger.LogInformation("Consumer stopped successfully");
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Error stopping consumer during cleanup");
                }

                // Dispose provider if it implements IDisposable
                if (QueueProvider is IDisposable disposableProvider)
                {
                    disposableProvider.Dispose();
                    Logger.LogInformation("QueueProvider disposed successfully");
                }
            }

            // Stop and dispose container
            if (_rabbitMqContainer != null)
            {
                await _rabbitMqContainer.StopAsync();
                await _rabbitMqContainer.DisposeAsync();
                Logger.LogInformation("RabbitMQ container disposed successfully");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during RabbitMQ test cleanup");
        }
        finally
        {
            _loggerFactory?.Dispose();
        }
    }

    /// <summary>
    /// Validates the RabbitMQ connection is working properly
    /// This is called during initialization to ensure enterprise reliability
    /// </summary>
    protected virtual async Task ValidateConnection()
    {
        try
        {
            Logger.LogInformation("Validating RabbitMQ connection...");

            // Test basic queue operations to ensure connection is working
            var testQueueName = $"connection-test-{Guid.NewGuid():N}";

            // Declare a temporary queue
            var actualQueueName = await QueueProvider.DeclareQueueAsync(testQueueName,
                isDurable: false, isExclusive: true, autoDelete: true);

            if (string.IsNullOrEmpty(actualQueueName))
            {
                throw new InvalidOperationException("Queue declaration returned empty queue name");
            }

            Logger.LogInformation("Connection validation successful - queue '{QueueName}' declared", actualQueueName);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "RabbitMQ connection validation failed");
            throw new InvalidOperationException("Failed to validate RabbitMQ connection", ex);
        }
    }

    /// <summary>
    /// Logs comprehensive container diagnostics for troubleshooting
    /// </summary>
    protected virtual async Task LogContainerDiagnostics()
    {
        try
        {
            if (_rabbitMqContainer != null)
            {
                Logger.LogError("Container State: {State}", _rabbitMqContainer.State);
                Logger.LogError("Container Image: {Image}", _rabbitMqContainer.Image.FullName);

                try
                {
                    var logs = await _rabbitMqContainer.GetLogsAsync();
                    Logger.LogError("Container stdout: {Stdout}", logs.Stdout);
                    Logger.LogError("Container stderr: {Stderr}", logs.Stderr);
                }
                catch (Exception logEx)
                {
                    Logger.LogError(logEx, "Could not retrieve container logs");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error retrieving container diagnostics");
        }
    }

    /// <summary>
    /// Setup method called before each test - override for test-specific setup
    /// </summary>
    protected virtual async Task SetupAsync()
    {
        await Task.CompletedTask;
    }

    /// <summary>
    /// Cleanup method called after each test - override for test-specific cleanup
    /// </summary>
    protected virtual async Task CleanupAsync()
    {
        await Task.CompletedTask;
    }

    /// <summary>
    /// Creates a unique queue name for test isolation
    /// </summary>
    protected virtual string CreateUniqueQueueName(string prefix = "test")
    {
        return $"{prefix}-{Guid.NewGuid():N}";
    }

    /// <summary>
    /// Creates a unique exchange name for test isolation
    /// </summary>
    protected virtual string CreateUniqueExchangeName(string prefix = "test-exchange")
    {
        return $"{prefix}-{Guid.NewGuid():N}";
    }

    /// <summary>
    /// Waits for a condition with timeout - useful for async operations validation
    /// </summary>
    protected virtual async Task<bool> WaitForConditionAsync(Func<bool> condition, TimeSpan timeout,
        TimeSpan? checkInterval = null)
    {
        var interval = checkInterval ?? TimeSpan.FromMilliseconds(100);
        var deadline = DateTime.UtcNow.Add(timeout);

        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(interval);
        }

        return false;
    }

    /// <summary>
    /// Enterprise-grade assertion that waits for async conditions
    /// Prevents race conditions common in distributed messaging tests
    /// </summary>
    protected virtual async Task AssertEventuallyAsync(Func<bool> condition, TimeSpan timeout,
        string? failureMessage = null)
    {
        var success = await WaitForConditionAsync(condition, timeout);
        if (!success)
        {
            throw new TimeoutException(failureMessage ??
                $"Condition was not met within {timeout.TotalSeconds} seconds");
        }
    }
}