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
    /// Adds rate limiting services with memory storage to the specified IServiceCollection
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to</param>
    /// <param name="configureOptions">The action used to configure the rate limiting options</param>
    /// <returns>The IServiceCollection so that additional calls can be chained</returns>
    public static IServiceCollection AddRateLimiting(this IServiceCollection services, Action<RateLimitOptions>? configureOptions = null)
    {
        return services.AddRateLimitingWithMemoryStorage(configureOptions);
    }

    /// <summary>
    /// Adds rate limiting services with memory storage to the specified IServiceCollection
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to</param>
    /// <param name="configureOptions">The action used to configure the rate limiting options</param>
    /// <returns>The IServiceCollection so that additional calls can be chained</returns>
    public static IServiceCollection AddRateLimitingWithMemoryStorage(this IServiceCollection services, Action<RateLimitOptions>? configureOptions = null)
    {
        // Configure options
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }
        else
        {
            services.Configure<RateLimitOptions>(options => { });
        }

        // Register rate limiting services
        services.TryAddSingleton<IRateLimitStore, MemoryRateLimitStore>();
        services.TryAddSingleton<IRateLimitService, HighPerformanceRateLimitService>();
        services.TryAddSingleton<IRateLimitKeyGenerator, DefaultRateLimitKeyGenerator>();
        services.TryAddSingleton<IRateLimitResponseBuilder, DefaultRateLimitResponseBuilder>();

        return services;
    }

    /// <summary>
    /// Adds rate limiting services with Redis storage to the specified IServiceCollection
    /// Uses shared Redis connection multiplexer for optimal performance
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to</param>
    /// <param name="redisConnectionString">The Redis connection string</param>
    /// <param name="configureOptions">The action used to configure the rate limiting options</param>
    /// <returns>The IServiceCollection so that additional calls can be chained</returns>
    public static IServiceCollection AddRateLimitingWithRedisStorage(this IServiceCollection services, string redisConnectionString, Action<RateLimitOptions>? configureOptions = null)
    {
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
            var configuration = ConfigurationOptions.Parse(redisConnectionString);
            // Optimize for rate limiting workload
            configuration.ConnectTimeout = 10000;
            configuration.SyncTimeout = 5000;
            configuration.AsyncTimeout = 5000;
            configuration.AbortOnConnectFail = false;
            configuration.ConnectRetry = 3;
            configuration.ReconnectRetryPolicy = new ExponentialRetry(1000);

            return ConnectionMultiplexer.Connect(configuration);
        });

        // Register high-performance Redis rate limit store with shared connection
        services.TryAddSingleton<IRateLimitStore>(provider =>
        {
            var redis = provider.GetRequiredService<IConnectionMultiplexer>();
            var logger = provider.GetRequiredService<ILogger<OptimizedRedisRateLimitStore>>();
            return new OptimizedRedisRateLimitStore(redis, logger);
        });

        services.TryAddSingleton<IRateLimitService, HighPerformanceRateLimitService>();
        services.TryAddSingleton<IRateLimitKeyGenerator, DefaultRateLimitKeyGenerator>();
        services.TryAddSingleton<IRateLimitResponseBuilder, DefaultRateLimitResponseBuilder>();

        return services;
    }

    /// <summary>
    /// Adds rate limiting services with an existing Redis connection multiplexer
    /// Use this when you already have a shared Redis connection in your DI container
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to</param>
    /// <param name="configureOptions">The action used to configure the rate limiting options</param>
    /// <returns>The IServiceCollection so that additional calls can be chained</returns>
    public static IServiceCollection AddRateLimitingWithExistingRedis(this IServiceCollection services, Action<RateLimitOptions>? configureOptions = null)
    {
        // Configure options
        services.Configure<RateLimitOptions>(options =>
        {
            options.StorageType = RateLimitStorageType.Redis;
            configureOptions?.Invoke(options);
        });

        // Register high-performance Redis rate limit store with existing connection
        services.TryAddSingleton<IRateLimitStore>(provider =>
        {
            var redis = provider.GetRequiredService<IConnectionMultiplexer>();
            var logger = provider.GetRequiredService<ILogger<OptimizedRedisRateLimitStore>>();
            return new OptimizedRedisRateLimitStore(redis, logger);
        });

        services.TryAddSingleton<IRateLimitService, HighPerformanceRateLimitService>();
        services.TryAddSingleton<IRateLimitKeyGenerator, DefaultRateLimitKeyGenerator>();
        services.TryAddSingleton<IRateLimitResponseBuilder, DefaultRateLimitResponseBuilder>();

        return services;
    }

    /// <summary>
    /// Adds high-performance rate limiting services with optimized Redis operations
    /// Uses shared connection pool and atomic Lua scripts for maximum performance
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to</param>
    /// <param name="redisConnectionString">The Redis connection string</param>
    /// <param name="configureOptions">The action used to configure the rate limiting options</param>
    /// <returns>The IServiceCollection so that additional calls can be chained</returns>
    public static IServiceCollection AddHighPerformanceRateLimiting(this IServiceCollection services, string redisConnectionString, Action<RateLimitOptions>? configureOptions = null)
    {
        // Configure options with Redis settings
        services.Configure<RateLimitOptions>(options =>
        {
            options.StorageType = RateLimitStorageType.Redis;
            options.RedisConnectionString = redisConnectionString;
            configureOptions?.Invoke(options);
        });

        // Register shared Redis connection multiplexer with optimized settings
        services.TryAddSingleton<IConnectionMultiplexer>(provider =>
        {
            var configuration = ConfigurationOptions.Parse(redisConnectionString);
            // Optimize for high-performance rate limiting
            configuration.ConnectTimeout = 10000;
            configuration.SyncTimeout = 5000;
            configuration.AsyncTimeout = 5000;
            configuration.AbortOnConnectFail = false;
            configuration.ConnectRetry = 3;
            configuration.ReconnectRetryPolicy = new ExponentialRetry(1000);

            return ConnectionMultiplexer.Connect(configuration);
        });

        // Register optimized Redis rate limit store
        services.TryAddSingleton<IRateLimitStore>(provider =>
        {
            var redis = provider.GetRequiredService<IConnectionMultiplexer>();
            var logger = provider.GetRequiredService<ILogger<OptimizedRedisRateLimitStore>>();
            return new OptimizedRedisRateLimitStore(redis, logger);
        });

        // Register high-performance rate limit service
        services.TryAddSingleton<IRateLimitService, HighPerformanceRateLimitService>();
        services.TryAddSingleton<IRateLimitKeyGenerator, DefaultRateLimitKeyGenerator>();
        services.TryAddSingleton<IRateLimitResponseBuilder, DefaultRateLimitResponseBuilder>();

        return services;
    }

    /// <summary>
    /// Adds rate limiting services with custom implementations
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to</param>
    /// <param name="configureOptions">The action used to configure the rate limiting options</param>
    /// <returns>The IServiceCollection so that additional calls can be chained</returns>
    public static IServiceCollection AddRateLimitingWithCustomStorage<TStore, TKeyGenerator, TResponseBuilder>(
        this IServiceCollection services,
        Action<RateLimitOptions>? configureOptions = null)
        where TStore : class, IRateLimitStore
        where TKeyGenerator : class, IRateLimitKeyGenerator
        where TResponseBuilder : class, IRateLimitResponseBuilder
    {
        // Configure options
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }
        else
        {
            services.Configure<RateLimitOptions>(options => { });
        }

        // Register custom implementations
        services.TryAddSingleton<IRateLimitStore, TStore>();
        services.TryAddSingleton<IRateLimitService, HighPerformanceRateLimitService>();
        services.TryAddSingleton<IRateLimitKeyGenerator, TKeyGenerator>();
        services.TryAddSingleton<IRateLimitResponseBuilder, TResponseBuilder>();

        return services;
    }
}