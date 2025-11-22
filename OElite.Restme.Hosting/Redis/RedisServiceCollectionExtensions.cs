using System;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OElite.Providers;
using OElite.Restme.Abstractions;

namespace OElite.Restme.Hosting.Redis
{
    /// <summary>
    /// ASP.NET Core DI extension methods for OElite.Restme.Redis
    /// Provides seamless integration with Microsoft.Extensions.Caching.Distributed
    /// </summary>
    public static class RedisServiceCollectionExtensions
    {
        /// <summary>
        /// Add OElite Redis distributed cache to the service collection
        /// This replaces the need for Microsoft.Extensions.Caching.StackExchangeRedis
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="connectionString">Redis connection string (e.g., "localhost:6379")</param>
        /// <param name="instanceName">Optional instance prefix for cache keys (e.g., "kortex:")</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddRestmeRedisCache(
            this IServiceCollection services,
            string connectionString,
            string? instanceName = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            // Register the OElite Redis cache provider
            // Instance name prefix will be handled by prepending to keys in the adapter
            var cacheProvider = new RedisCacheProvider(new RestConfig(RestMode.Redis)
            {
                ConnectionString = connectionString,
                InstanceName = instanceName
            });

            services.AddSingleton<RedisCacheProvider>(_ => cacheProvider);
            services.AddSingleton<ICacheProvider>(_ => cacheProvider);

            // Register the IDistributedCache adapter with instance prefix
            services.TryAddSingleton<IDistributedCache>(provider =>
                new RedisDistributedCache(cacheProvider, instanceName));

            return services;
        }

        /// <summary>
        /// Add OElite Redis distributed cache to the service collection with configuration action
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="setupAction">Configuration action for Redis options</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddRestmeRedisCache(
            this IServiceCollection services,
            Action<RedisOptions> setupAction)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            if (setupAction == null)
                throw new ArgumentNullException(nameof(setupAction));

            var options = new RedisOptions();
            setupAction(options);

            if (string.IsNullOrEmpty(options.ConnectionString))
                throw new InvalidOperationException("Redis connection string must be configured");

            return AddRestmeRedisCache(services, options.ConnectionString, options.InstanceName);
        }
    }

    /// <summary>
    /// Configuration options for OElite Redis cache
    /// </summary>
    public class RedisOptions
    {
        /// <summary>
        /// Redis connection string (e.g., "localhost:6379" or "redis.example.com:6379,password=secret,db=0")
        /// </summary>
        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Optional instance prefix for cache keys (e.g., "kortex:")
        /// Useful for multi-tenant or multi-application scenarios
        /// </summary>
        public string? InstanceName { get; set; }
    }
}