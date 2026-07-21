using System;
using OElite.Restme;
using OElite.Restme.Abstractions;

namespace OElite.Providers
{
    /// <summary>
    /// Service factory for Redis providers
    /// </summary>
    public class RedisServiceFactory : IServiceFactory
    {
        static RedisServiceFactory()
        {
            // Auto-register this factory when the assembly is loaded
            ServiceLocator.RegisterFactory("redis", new RedisServiceFactory());
        }

        /// <summary>
        /// Redis supports Cache capability only
        /// </summary>
        public ProviderCapabilities SupportedCapabilities => ProviderCapabilities.Cache;

        /// <summary>
        /// Checks if this factory can create the requested provider type
        /// </summary>
        public bool CanCreateProvider<T>() where T : class, IRestmeProvider
        {
            return typeof(T) == typeof(ICacheProvider);
        }

        /// <summary>
        /// Generic provider creation with capability detection
        /// </summary>
        public T? CreateProvider<T>(RestConfig config) where T : class, IRestmeProvider
        {
            if (typeof(T) == typeof(ICacheProvider))
            {
                return new RedisCacheProvider(config) as T;
            }

            return null;
        }

        public ICacheProvider? CreateCacheProvider(RestConfig config)
        {
            return new RedisCacheProvider(config);
        }

        public IQueueProvider? CreateQueueProvider(RestConfig config)
        {
            return null;
        }

        public IStorageProvider? CreateStorageProvider(RestConfig config)
        {
            return null;
        }

        public IHttpProvider? CreateHttpProvider(RestConfig config)
        {
            return null;
        }

        public ILogProvider CreateLogProvider(RestConfig config)
        {
            return new DefaultLogProvider();
        }

        public IColumnarProvider? CreateColumnarProvider(RestConfig config)
        {
            return null;
        }

        public IEventStreamProvider? CreateStreamingProvider(RestConfig config)
        {
            return null;
        }

        public ISearchProvider? CreateSearchProvider(RestConfig config)
        {
            return null;
        }
    }
}
