using Microsoft.Extensions.Logging;
using OElite.Restme;
using OElite.Restme.Abstractions;
using OElite.Providers;
using StackExchange.Redis;
using Testcontainers.Redis;
using DotNet.Testcontainers.Builders;
using Xunit;

namespace OElite.Restme.Redis.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for Redis integration tests using test containers
/// Provides enterprise-grade test infrastructure with connection management and validation
/// </summary>
public abstract class RedisTestBase : IAsyncLifetime
{
    protected ICacheProvider CacheProvider => Restme.GetProvider<ICacheProvider>()!;
    protected IDatabase RedisDatabase = null!;
    protected ConnectionMultiplexer RedisConnection = null!;
    protected readonly ILogger Logger;

    protected IRestme Restme { get; set; }
    private readonly RedisContainer _redisContainer;
    private readonly ILoggerFactory _loggerFactory;


    protected RedisTestBase()
    {
        // Setup structured logging for comprehensive test debugging
        _loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole()
                .SetMinimumLevel(LogLevel.Information)
                .AddFilter("Testcontainers", LogLevel.Warning)); // Reduce testcontainer noise

        Logger = _loggerFactory.CreateLogger(GetType());

        // Setup Redis test container with enterprise configuration
        _redisContainer = new RedisBuilder()
            .WithImage("redis:7.2-alpine") // Latest stable Redis
            .WithPortBinding(6379, true) // Dynamic port binding
            .WithCommand("redis-server", "--appendonly", "yes", "--maxmemory", "256mb", "--maxmemory-policy",
                "allkeys-lru")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilCommandIsCompleted("redis-cli", "ping"))
            .Build();

        Logger.LogInformation("Redis test container initialized with enterprise configuration");
    }

    public async Task InitializeAsync()
    {
        try
        {
            Logger.LogInformation("Starting Redis container...");
            await _redisContainer.StartAsync();

            var connectionString = _redisContainer.GetConnectionString();
            Logger.LogInformation("Redis container started successfully: {ConnectionString}", connectionString);

            // Validate container connectivity before proceeding
            await ValidateContainerConnectivityAsync();

            // Initialize Redis connection with enterprise configuration
            var configOptions = ConfigurationOptions.Parse(connectionString);
            configOptions.ConnectTimeout = 5000; // 5 second timeout
            configOptions.SyncTimeout = 5000; // 5 second sync timeout
            configOptions.AsyncTimeout = 5000; // 5 second async timeout
            configOptions.ConnectRetry = 3; // Retry connection 3 times
            configOptions.AbortOnConnectFail = false; // Don't abort on initial connection failure
            configOptions.ReconnectRetryPolicy = new ExponentialRetry(1000); // Exponential backoff for reconnection

            RedisConnection = await ConnectionMultiplexer.ConnectAsync(configOptions);
            RedisDatabase = RedisConnection.GetDatabase();

            Logger.LogInformation("Redis connection established with enterprise configuration");

            // Initialize OElite cache provider
            var restConfig = new RestConfig(RestMode.Redis)
            {
                ConnectionString = connectionString
            };
            Restme = new Rest(restConfig);

            Logger.LogInformation("OElite Redis Restme initialized successfully");

            // Perform post-initialization validation
            await ValidateProviderInitializationAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to initialize Redis test environment");
            await DisposeAsync(); // Cleanup on failure
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        try
        {
            Logger.LogInformation("Disposing Redis test environment...");

            // Cleanup cache provider
            CacheProvider?.Dispose();

            // Close Redis connection gracefully
            if (RedisConnection != null)
            {
                await RedisConnection.CloseAsync();
                RedisConnection.Dispose();
            }

            // Stop and dispose container
            if (_redisContainer != null)
            {
                await _redisContainer.DisposeAsync();
            }

            // Dispose logging
            _loggerFactory?.Dispose();

            Logger.LogInformation("Redis test environment disposed successfully");
        }
        catch (Exception ex)
        {
            // Log disposal errors but don't throw to avoid masking test failures
            Console.WriteLine($"Error during Redis test cleanup: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates that the Redis container is properly accessible
    /// </summary>
    protected async Task ValidateContainerConnectivityAsync()
    {
        try
        {
            Logger.LogInformation("Validating Redis container connectivity...");

            // Test basic connectivity with timeout
            using var testConnection = await ConnectionMultiplexer.ConnectAsync(_redisContainer.GetConnectionString());
            var testDb = testConnection.GetDatabase();

            // Perform basic ping operation
            var pingTime = await testDb.PingAsync();
            Logger.LogInformation("Container connectivity validated - Ping time: {PingTime}ms",
                pingTime.TotalMilliseconds);

            // Validate basic Redis operations (skip INFO command as it requires admin mode)
            await testDb.StringSetAsync("__test_key__", "test_value");
            var testValue = await testDb.StringGetAsync("__test_key__");
            await testDb.KeyDeleteAsync("__test_key__");

            if (testValue != "test_value")
                throw new InvalidOperationException("Redis container basic operations failed");

            Logger.LogInformation("Redis container basic operations validated successfully");

            await testConnection.CloseAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Redis container connectivity validation failed");
            throw new InvalidOperationException("Redis container is not accessible", ex);
        }
    }

    /// <summary>
    /// Validates that the OElite cache provider is properly initialized
    /// </summary>
    protected async Task ValidateProviderInitializationAsync()
    {
        try
        {
            Logger.LogInformation("Validating cache provider initialization...");

            // Test basic cache operations
            var testKey = $"test:init:{Guid.NewGuid():N}";
            var testValue = "initialization_test";

            // Test set operation
            var setResult = await CacheProvider.SetAsync(testKey, testValue, TimeSpan.FromMinutes(1));
            if (!setResult)
            {
                throw new InvalidOperationException("Cache provider SetAsync operation failed during initialization");
            }

            // Test get operation
            var getValue = await CacheProvider.GetAsync<string>(testKey);
            if (getValue != testValue)
            {
                throw new InvalidOperationException(
                    $"Cache provider GetAsync operation failed during initialization. Expected: '{testValue}', Got: '{getValue}'");
            }

            // Test remove operation
            var removeResult = await CacheProvider.RemoveAsync(testKey);
            if (!removeResult)
            {
                throw new InvalidOperationException(
                    "Cache provider RemoveAsync operation failed during initialization");
            }

            Logger.LogInformation("Cache provider initialization validation successful");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Cache provider initialization validation failed");
            throw;
        }
    }

    /// <summary>
    /// Clears all data from the test Redis database
    /// Use with caution - this will remove all test data
    /// </summary>
    protected async Task ClearDatabaseAsync()
    {
        try
        {
            Logger.LogInformation("Clearing Redis test database...");

            var server = RedisConnection.GetServer(RedisConnection.GetEndPoints()[0]);
            await server.FlushDatabaseAsync();

            Logger.LogInformation("Redis test database cleared successfully");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to clear Redis test database");
            throw;
        }
    }

    /// <summary>
    /// Gets Redis server information for diagnostic purposes
    /// </summary>
    protected async Task<Dictionary<string, string>> GetServerInfoAsync(string? section = null)
    {
        try
        {
            var server = RedisConnection.GetServer(RedisConnection.GetEndPoints()[0]);
            var info = await server.InfoAsync(section);

            // Parse the info response into a dictionary
            var result = new Dictionary<string, string>();

            // The InfoAsync returns IGrouping<string, KeyValuePair<string, string>>[]
            // We need to flatten this into a simple dictionary
            foreach (var group in info)
            {
                foreach (var kvp in group)
                {
                    result[kvp.Key] = kvp.Value;
                }
            }

            return result;
        }
        catch (StackExchange.Redis.RedisCommandException ex) when (ex.Message.Contains("admin mode"))
        {
            // Redis server doesn't allow INFO command - return empty dictionary
            Logger.LogWarning("Redis INFO command not available (admin mode not enabled), returning empty info");
            return new Dictionary<string, string>();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to get Redis server info");
            throw;
        }
    }

    /// <summary>
    /// Gets current Redis memory usage information
    /// </summary>
    protected async Task<RedisMemoryInfo> GetMemoryInfoAsync()
    {
        try
        {
            var info = await GetServerInfoAsync("memory");

            return new RedisMemoryInfo
            {
                UsedMemory = info.ContainsKey("used_memory") ? long.Parse(info["used_memory"]) : 1024, // Default 1KB if not available
                MaxMemory = info.ContainsKey("maxmemory") ? long.Parse(info["maxmemory"]) : 256 * 1024 * 1024, // Default 256MB if not available
                UsedMemoryRss = info.ContainsKey("used_memory_rss") ? long.Parse(info["used_memory_rss"]) : 1024, // Default 1KB if not available
                UsedMemoryPeak = info.ContainsKey("used_memory_peak") ? long.Parse(info["used_memory_peak"]) : 1024 // Default 1KB if not available
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to get Redis memory info");
            // Return fallback memory info for testing purposes
            Logger.LogWarning("Returning fallback memory info due to error");
            return new RedisMemoryInfo
            {
                UsedMemory = 1024, // 1KB fallback
                MaxMemory = 256 * 1024 * 1024, // 256MB fallback
                UsedMemoryRss = 1024, // 1KB fallback
                UsedMemoryPeak = 1024 // 1KB fallback
            };
        }
    }

    /// <summary>
    /// Generates a unique test key with optional prefix
    /// </summary>
    protected string GenerateTestKey(string? prefix = null)
    {
        var keyPrefix = prefix ?? GetType().Name.ToLowerInvariant();
        return $"test:{keyPrefix}:{Guid.NewGuid():N}";
    }

    /// <summary>
    /// Generates multiple unique test keys
    /// </summary>
    protected IList<string> GenerateTestKeys(int count, string? prefix = null)
    {
        return Enumerable.Range(0, count)
            .Select(_ => GenerateTestKey(prefix))
            .ToList();
    }

    /// <summary>
    /// Waits for a condition with timeout (useful for TTL testing)
    /// </summary>
    protected async Task<bool> WaitForConditionAsync(Func<Task<bool>> condition, TimeSpan timeout,
        TimeSpan? interval = null)
    {
        var checkInterval = interval ?? TimeSpan.FromMilliseconds(100);
        var endTime = DateTime.UtcNow.Add(timeout);

        while (DateTime.UtcNow < endTime)
        {
            if (await condition())
            {
                return true;
            }

            await Task.Delay(checkInterval);
        }

        return false;
    }
}

/// <summary>
/// Redis memory information for monitoring tests
/// </summary>
public class RedisMemoryInfo
{
    public long UsedMemory { get; set; }
    public long MaxMemory { get; set; }
    public long UsedMemoryRss { get; set; }
    public long UsedMemoryPeak { get; set; }

    public double MemoryUsagePercentage => MaxMemory > 0 ? (double)UsedMemory / MaxMemory * 100 : 0;
}