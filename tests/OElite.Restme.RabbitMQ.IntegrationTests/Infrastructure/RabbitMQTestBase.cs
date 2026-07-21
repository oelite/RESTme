using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Logging;
using OElite;
using OElite.Restme;
using OElite.Restme.Abstractions;
using RabbitMQ.Client;
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

        // Initialize RabbitMQ test container with minimal configuration
        _rabbitMqContainer = new RabbitMqBuilder()
            .WithImage("rabbitmq:3.13-management")  // Use lightweight Alpine
            .WithPortBinding(5672, true)
            // Very basic wait strategy
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilExternalTcpPortIsAvailable(5672))
            .Build();

        Logger.LogInformation("RabbitMQ test container initialized with management interface");
    }

    public virtual async Task InitializeAsync()
    {
        try
        {
            Logger.LogInformation("Starting RabbitMQ container...");
            await _rabbitMqContainer.StartAsync();

            // Wait longer for RabbitMQ to fully initialize
            Logger.LogInformation("Waiting for RabbitMQ service to be ready...");
            await Task.Delay(5000);

            var connectionString = _rabbitMqContainer.GetConnectionString();
            Logger.LogInformation("RabbitMQ container started: {ConnectionString}", connectionString);

            // Verify container is actually running
            if (!IsContainerRunning)
            {
                throw new InvalidOperationException("RabbitMQ container failed to start properly");
            }

            // Test connection to RabbitMQ with retry logic
            await TestRabbitMQConnection(connectionString);

            // Configure Rest with RabbitMQ - ensure proper mode setting
            Rest = new Rest(connectionString, new RestConfig(RestMode.RabbitMq)
            {
                AuthKey = "rabbitmq",
                AuthSecret = "rabbitmq"
            });

            Logger.LogInformation("Rest configuration created with mode: {Mode}, Connection: {Connection}",
                Rest.Configuration.OperationMode, Rest.Configuration.ConnectionString);

            // Get and validate QueueProvider
            Logger.LogInformation("About to get QueueProvider for mode: {Mode}", Rest.Configuration.OperationMode);
            QueueProvider = Rest.GetProvider<IQueueProvider>();
            Logger.LogInformation("QueueProvider retrieved: {Provider}", QueueProvider?.GetType().Name ?? "NULL");

            if (QueueProvider == null)
            {
                // Try to force factory loading by checking if RabbitMQ assembly is loaded
                var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
                var rabbitMQAssemblies = loadedAssemblies.Where(a => a.FullName?.Contains("OElite.Restme.RabbitMQ") == true).ToList();
                Logger.LogInformation("RabbitMQ assemblies loaded: {Assemblies}", string.Join(", ", rabbitMQAssemblies.Select(a => a.GetName().Name)));

                // Check specifically for the provider assembly
                var providerAssembly = loadedAssemblies.FirstOrDefault(a => a.FullName?.Contains("OElite.Restme.RabbitMQ") == true && !a.FullName.Contains("IntegrationTests"));
                Logger.LogInformation("RabbitMQ provider assembly: {Assembly}", providerAssembly?.GetName().Name ?? "NOT FOUND");

                // Check if the factory is registered
                var factory = ServiceLocator.GetFactory("rabbitmq");
                Logger.LogInformation("RabbitMQ factory found: {Factory}", factory?.GetType().Name ?? "NOT FOUND");

                if (factory != null)
                {
                    // Test if factory can create IQueueProvider
                    var canCreateQueue = factory.CanCreateProvider<IQueueProvider>();
                    Logger.LogInformation("Factory can create IQueueProvider: {CanCreate}", canCreateQueue);

                    // Try to create provider directly
                    try
                    {
                        var provider = factory.CreateProvider<IQueueProvider>(Rest.Configuration);
                        Logger.LogInformation("Direct factory provider creation: {Provider}", provider?.GetType().Name ?? "NULL");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Factory provider creation failed");
                    }
                }

                throw new InvalidOperationException($"Failed to initialize RabbitMQ QueueProvider. Mode: {Rest.Configuration.OperationMode}, Connection: {connectionString}");
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
    /// Test RabbitMQ connection with retry logic
    /// </summary>
    protected virtual async Task TestRabbitMQConnection(string connectionString)
    {
        const int maxRetries = 5;
        var retryDelay = TimeSpan.FromSeconds(2);

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                Logger.LogInformation("Testing RabbitMQ connection (attempt {Attempt}/{MaxRetries})", attempt, maxRetries);

                var factory = new global::RabbitMQ.Client.ConnectionFactory();
                factory.Uri = new Uri(connectionString);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                using var connection = await factory.CreateConnectionAsync(cancellationToken: cts.Token);
                using var channel = await connection.CreateChannelAsync(cancellationToken: cts.Token);

                Logger.LogInformation("✅ RabbitMQ connection test successful");
                return;
            }
            catch (Exception ex)
            {
                Logger.LogWarning("❌ RabbitMQ connection test failed (attempt {Attempt}/{MaxRetries}): {Message}",
                    attempt, maxRetries, ex.Message);

                if (attempt == maxRetries)
                {
                    throw new InvalidOperationException($"Failed to connect to RabbitMQ after {maxRetries} attempts: {ex.Message}", ex);
                }

                await Task.Delay(retryDelay);
                retryDelay = TimeSpan.FromMilliseconds(retryDelay.TotalMilliseconds * 1.5); // Exponential backoff
            }
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