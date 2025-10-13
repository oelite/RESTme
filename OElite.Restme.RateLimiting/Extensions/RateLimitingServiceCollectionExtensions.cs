using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        // Add required services
        services.AddMemoryCache();

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
        services.TryAddSingleton<IRateLimitKeyGenerator, DefaultRateLimitKeyGenerator>();
        services.TryAddSingleton<IRateLimitResponseBuilder, DefaultRateLimitResponseBuilder>();

        return services;
    }

    /// <summary>
    /// Adds rate limiting services with Redis storage to the specified IServiceCollection
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to</param>
    /// <param name="redisConnectionString">The Redis connection string</param>
    /// <param name="configureOptions">The action used to configure the rate limiting options</param>
    /// <returns>The IServiceCollection so that additional calls can be chained</returns>
    public static IServiceCollection AddRateLimitingWithRedisStorage(this IServiceCollection services, string redisConnectionString, Action<RateLimitOptions>? configureOptions = null)
    {
        // Add Redis connection
        services.AddSingleton<IConnectionMultiplexer>(provider =>
            ConnectionMultiplexer.Connect(redisConnectionString));

        // Configure options with Redis settings
        services.Configure<RateLimitOptions>(options =>
        {
            options.StorageType = RateLimitStorageType.Redis;
            options.RedisConnectionString = redisConnectionString;
            configureOptions?.Invoke(options);
        });

        // Register rate limiting services
        services.TryAddSingleton<IRateLimitStore, RedisRateLimitStore>();
        services.TryAddSingleton<IRateLimitKeyGenerator, DefaultRateLimitKeyGenerator>();
        services.TryAddSingleton<IRateLimitResponseBuilder, DefaultRateLimitResponseBuilder>();

        return services;
    }

    /// <summary>
    /// Adds rate limiting services with Redis storage using an existing IConnectionMultiplexer
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to</param>
    /// <param name="configureOptions">The action used to configure the rate limiting options</param>
    /// <returns>The IServiceCollection so that additional calls can be chained</returns>
    public static IServiceCollection AddRateLimitingWithRedisStorage(this IServiceCollection services, Action<RateLimitOptions>? configureOptions = null)
    {
        // Configure options
        services.Configure<RateLimitOptions>(options =>
        {
            options.StorageType = RateLimitStorageType.Redis;
            configureOptions?.Invoke(options);
        });

        // Register rate limiting services (assumes IConnectionMultiplexer is already registered)
        services.TryAddSingleton<IRateLimitStore, RedisRateLimitStore>();
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
        services.TryAddSingleton<IRateLimitKeyGenerator, TKeyGenerator>();
        services.TryAddSingleton<IRateLimitResponseBuilder, TResponseBuilder>();

        return services;
    }
}