using System;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OElite.Abstractions;
using OElite.Base;

namespace OElite.Restme.Hosting.Memory
{
    /// <summary>
    /// ASP.NET Core DI extension methods for OElite.Restme.Memory
    /// Provides seamless integration with Microsoft.Extensions.Caching.Memory and Microsoft.Extensions.Caching.Distributed
    /// </summary>
    public static class MemoryServiceCollectionExtensions
    {
        /// <summary>
        /// Add OElite memory cache to the service collection
        /// This replaces the need for Microsoft.Extensions.Caching.Memory.AddMemoryCache()
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="instanceName">Optional instance prefix for cache keys (e.g., "oelite:")</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddRestmeMemoryCache(
            this IServiceCollection services,
            string? instanceName = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            // Register the OElite Memory cache provider
            services.AddSingleton<ICacheProvider, MemoryCacheProvider>();

            // Register the IMemoryCache adapter with instance prefix
            services.TryAddSingleton<IMemoryCache>(provider =>
            {
                var cacheProvider = provider.GetRequiredService<ICacheProvider>();
                return new RestmeMemoryCache(cacheProvider, instanceName);
            });

            return services;
        }

        /// <summary>
        /// Add OElite memory cache with distributed cache adapter to the service collection
        /// This provides both IMemoryCache and IDistributedCache implementations using the same underlying memory store
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="instanceName">Optional instance prefix for cache keys (e.g., "oelite:")</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddRestmeMemoryCacheWithDistributed(
            this IServiceCollection services,
            string? instanceName = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            // Register the OElite Memory cache provider (shared between IMemoryCache and IDistributedCache)
            services.AddSingleton<ICacheProvider, MemoryCacheProvider>();

            // Register the IMemoryCache adapter
            services.TryAddSingleton<IMemoryCache>(provider =>
            {
                var cacheProvider = provider.GetRequiredService<ICacheProvider>();
                return new RestmeMemoryCache(cacheProvider, instanceName);
            });

            // Register the IDistributedCache adapter using the same memory provider
            services.TryAddSingleton<IDistributedCache>(provider =>
            {
                var cacheProvider = provider.GetRequiredService<ICacheProvider>();
                return new MemoryDistributedCache(cacheProvider, instanceName);
            });

            return services;
        }

        /// <summary>
        /// Add OElite memory cache with configuration action
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="setupAction">Configuration action for memory cache options</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddRestmeMemoryCache(
            this IServiceCollection services,
            Action<MemoryOptions> setupAction)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            if (setupAction == null)
                throw new ArgumentNullException(nameof(setupAction));

            var options = new MemoryOptions();
            setupAction(options);

            return AddRestmeMemoryCache(services, options.InstanceName);
        }
    }

    /// <summary>
    /// Configuration options for OElite memory cache
    /// </summary>
    public class MemoryOptions
    {
        /// <summary>
        /// Optional instance prefix for cache keys (e.g., "oelite:")
        /// Useful for multi-tenant or multi-application scenarios
        /// </summary>
        public string? InstanceName { get; set; }
    }
}