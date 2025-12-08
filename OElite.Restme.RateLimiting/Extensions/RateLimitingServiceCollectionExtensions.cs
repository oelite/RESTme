using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using OElite.Restme.RateLimiting.Interfaces;
using OElite.Restme.RateLimiting.Models;
using OElite.Restme.RateLimiting.Services;
using OElite.Restme.RateLimiting.Storage;
using StackExchange.Redis;

namespace OElite.Restme.RateLimiting.Extensions;

/// <summary>
/// Extension methods for configuring rate limiting services
/// </summary>
public static class RateLimitingServiceCollectionExtensions
{
    /// <summary>
    /// Adds rate limiting services with intelligent storage selection.
    /// Automatically detects Redis from existing ICacheProvider or IConnectionMultiplexer in DI, otherwise falls back to memory storage.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to</param>
    /// <param name="configureOptions">The action used to configure the rate limiting options</param>
    /// <returns>The IServiceCollection so that additional calls can be chained</returns>
    /// <remarks>
    /// This method will:
    /// 1. Check if ICacheProvider (from OElite.Restme.Redis) is registered in DI
    /// 2. If Redis cache provider found, extract IConnectionMultiplexer and use OptimizedRedisRateLimitStore
    /// 3. If IConnectionMultiplexer is directly registered, use OptimizedRedisRateLimitStore
    /// 4. Otherwise, fall back to MemoryRateLimitStore
    /// 
    /// For explicit storage selection, use overloads with redisConnectionString parameter or AddRateLimitingWithCustomStorage.
    /// </remarks>
    public static IServiceCollection AddRateLimiting(this IServiceCollection services, Action<RateLimitOptions>? configureOptions = null)
    {
        // Configure options
        services.Configure<RateLimitOptions>(options =>
        {
            configureOptions?.Invoke(options);
        });

        // Register storage with intelligent fallback
        services.TryAddSingleton<IRateLimitStore>(provider =>
        {
            var logger = provider.GetRequiredService<ILoggerFactory>();
            IConnectionMultiplexer? redis = null;
            
            // Strategy 1: Try to get Redis from ICacheProvider (most common in OElite ecosystem)
            // This checks for OElite.Restme.Redis RedisCacheProvider
            var cacheProvider = provider.GetService<Abstractions.ICacheProvider>();
            if (cacheProvider != null && cacheProvider.ProviderName == "RedisCache")
            {
                // RedisCacheProvider has Configuration.ConnectionString we can reuse
                var redisConnectionString = cacheProvider.Configuration?.ConnectionString;
                
                if (!string.IsNullOrWhiteSpace(redisConnectionString))
                {
                    try
                    {
                        // Create a new connection for rate limiting (connection multiplexer is thread-safe and handles pooling)
                        var configuration = ConfigurationOptions.Parse(redisConnectionString);
                        configuration.ConnectTimeout = 10000;
                        configuration.SyncTimeout = 5000;
                        configuration.AsyncTimeout = 5000;
                        configuration.AbortOnConnectFail = false;
                        configuration.ConnectRetry = 3;
                        configuration.ReconnectRetryPolicy = new ExponentialRetry(1000);
                        
                        redis = ConnectionMultiplexer.Connect(configuration);
                        
                        if (redis.IsConnected)
                        {
                            var redisLogger = logger.CreateLogger<OptimizedRedisRateLimitStore>();
                            redisLogger.LogInformation("Rate limiting using Redis storage (reusing connection string from ICacheProvider: {Endpoints})", 
                                string.Join(", ", configuration.EndPoints));
                            return new OptimizedRedisRateLimitStore(redis, redisLogger);
                        }
                    }
                    catch (Exception ex)
                    {
                        var warnLogger = logger.CreateLogger<OptimizedRedisRateLimitStore>();
                        warnLogger.LogWarning(ex, "Failed to connect to Redis from ICacheProvider connection string, falling back to memory storage");
                    }
                }
            }
            
            // Strategy 2: Try to get IConnectionMultiplexer directly from DI
            redis = provider.GetService<IConnectionMultiplexer>();
            if (redis != null && redis.IsConnected)
            {
                var redisLogger = logger.CreateLogger<OptimizedRedisRateLimitStore>();
                redisLogger.LogInformation("Rate limiting using Redis storage (from IConnectionMultiplexer in DI)");
                return new OptimizedRedisRateLimitStore(redis, redisLogger);
            }
            
            // Strategy 3: Fall back to memory storage
            var memoryLogger = logger.CreateLogger<MemoryRateLimitStore>();
            memoryLogger.LogInformation("Rate limiting using in-memory storage (Redis not available)");
            return new MemoryRateLimitStore(memoryLogger);
        });

        // Register core services
        services.TryAddSingleton<IRateLimitService, HighPerformanceRateLimitService>();
        services.TryAddSingleton<IRateLimitKeyGenerator, DefaultRateLimitKeyGenerator>();
        services.TryAddSingleton<IRateLimitResponseBuilder, DefaultRateLimitResponseBuilder>();

        return services;
    }

    /// <summary>
    /// Adds rate limiting services with Redis distributed storage.
    /// Creates and manages a dedicated Redis connection for rate limiting with optimized settings.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to</param>
    /// <param name="redisConnectionString">The Redis connection string (e.g., "localhost:6379" or "redis.example.com:6379,password=secret")</param>
    /// <param name="configureOptions">The action used to configure the rate limiting options</param>
    /// <returns>The IServiceCollection so that additional calls can be chained</returns>
    /// <remarks>
    /// This method creates a new IConnectionMultiplexer optimized for rate limiting workloads with:
    /// - ConnectTimeout: 10000ms
    /// - SyncTimeout: 5000ms
    /// - AsyncTimeout: 5000ms
    /// - AbortOnConnectFail: false
    /// - ConnectRetry: 3
    /// - ExponentialRetry: 1000ms base interval
    /// 
    /// Uses atomic Lua scripts for high-performance rate limit operations.
    /// Suitable for distributed scenarios and high-throughput applications.
    /// </remarks>
    public static IServiceCollection AddRateLimiting(
        this IServiceCollection services,
        string redisConnectionString,
        Action<RateLimitOptions>? configureOptions = null)
    {
        if (string.IsNullOrWhiteSpace(redisConnectionString))
        {
            throw new ArgumentException("Redis connection string cannot be null or empty", nameof(redisConnectionString));
        }

        // Configure options with Redis settings
        services.Configure<RateLimitOptions>(options =>
        {
            options.StorageType = RateLimitStorageType.Redis;
            options.RedisConnectionString = redisConnectionString;
            configureOptions?.Invoke(options);
        });

        // Register shared Redis connection multiplexer if not already registered
        services.TryAddSingleton<IConnectionMultiplexer>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<OptimizedRedisRateLimitStore>>();
            
            try
            {
                var configuration = ConfigurationOptions.Parse(redisConnectionString);
                
                // Optimize for rate limiting workload
                configuration.ConnectTimeout = 10000;
                configuration.SyncTimeout = 5000;
                configuration.AsyncTimeout = 5000;
                configuration.AbortOnConnectFail = false;
                configuration.ConnectRetry = 3;
                configuration.ReconnectRetryPolicy = new ExponentialRetry(1000);

                logger.LogInformation("Connecting to Redis for rate limiting: {Endpoints}", 
                    string.Join(", ", configuration.EndPoints));
                
                return ConnectionMultiplexer.Connect(configuration);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to connect to Redis at {ConnectionString}. Rate limiting will not function correctly.", 
                    redisConnectionString);
                throw;
            }
        });

        // Register optimized Redis rate limit store
        services.TryAddSingleton<IRateLimitStore>(provider =>
        {
            var redis = provider.GetRequiredService<IConnectionMultiplexer>();
            var logger = provider.GetRequiredService<ILogger<OptimizedRedisRateLimitStore>>();
            return new OptimizedRedisRateLimitStore(redis, logger);
        });

        // Register core services
        services.TryAddSingleton<IRateLimitService, HighPerformanceRateLimitService>();
        services.TryAddSingleton<IRateLimitKeyGenerator, DefaultRateLimitKeyGenerator>();
        services.TryAddSingleton<IRateLimitResponseBuilder, DefaultRateLimitResponseBuilder>();

        return services;
    }

    /// <summary>
    /// Adds rate limiting services with custom storage, key generator, and response builder implementations.
    /// Use this for advanced scenarios requiring custom rate limiting behavior.
    /// </summary>
    /// <typeparam name="TStore">The custom rate limit store implementation</typeparam>
    /// <typeparam name="TKeyGenerator">The custom key generator implementation</typeparam>
    /// <typeparam name="TResponseBuilder">The custom response builder implementation</typeparam>
    /// <param name="services">The IServiceCollection to add services to</param>
    /// <param name="configureOptions">The action used to configure the rate limiting options</param>
    /// <returns>The IServiceCollection so that additional calls can be chained</returns>
    /// <remarks>
    /// Use this method when you need:
    /// - Custom storage backend (e.g., database, distributed cache)
    /// - Custom rate limit key generation logic (e.g., user-based, tenant-based)
    /// - Custom HTTP response formatting
    /// 
    /// Example:
    /// services.AddRateLimitingWithCustomStorage&lt;DatabaseRateLimitStore, UserIdKeyGenerator, JsonResponseBuilder&gt;();
    /// </remarks>
    public static IServiceCollection AddRateLimitingWithCustomStorage<TStore, TKeyGenerator, TResponseBuilder>(
        this IServiceCollection services,
        Action<RateLimitOptions>? configureOptions = null)
        where TStore : class, IRateLimitStore
        where TKeyGenerator : class, IRateLimitKeyGenerator
        where TResponseBuilder : class, IRateLimitResponseBuilder
    {
        // Configure options
        services.Configure<RateLimitOptions>(options =>
        {
            configureOptions?.Invoke(options);
        });

        // Register custom implementations
        services.TryAddSingleton<IRateLimitStore, TStore>();
        services.TryAddSingleton<IRateLimitService, HighPerformanceRateLimitService>();
        services.TryAddSingleton<IRateLimitKeyGenerator, TKeyGenerator>();
        services.TryAddSingleton<IRateLimitResponseBuilder, TResponseBuilder>();

        return services;
    }
}