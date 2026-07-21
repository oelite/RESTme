using System;
using OElite.Restme;
using OElite.Restme.Abstractions;

namespace OElite.Providers
{
    /// <summary>
    /// Service factory for S3 providers
    /// </summary>
    public class S3ServiceFactory : IServiceFactory
    {
        static S3ServiceFactory()
        {
            // Auto-register this factory when the assembly is loaded
            ServiceLocator.RegisterFactory("s3", new S3ServiceFactory());
        }

        /// <summary>
        /// S3 supports Cache and Storage capabilities
        /// </summary>
        public ProviderCapabilities SupportedCapabilities => ProviderCapabilities.Cache | ProviderCapabilities.Storage;

        /// <summary>
        /// Checks if this factory can create the requested provider type
        /// </summary>
        public bool CanCreateProvider<T>() where T : class, IRestmeProvider
        {
            var requestedType = typeof(T);
            return requestedType == typeof(ICacheProvider) || requestedType == typeof(IStorageProvider);
        }

        /// <summary>
        /// Generic provider creation with capability detection
        /// </summary>
        public T? CreateProvider<T>(RestConfig config) where T : class, IRestmeProvider
        {
            var requestedType = typeof(T);

            if (requestedType == typeof(ICacheProvider))
            {
                return new S3CacheProvider(config) as T;
            }

            if (requestedType == typeof(IStorageProvider))
            {
                return new S3StorageProvider(config) as T;
            }

            return null;
        }

        public ICacheProvider? CreateCacheProvider(RestConfig config)
        {
            return new S3CacheProvider(config);
        }

        public IQueueProvider? CreateQueueProvider(RestConfig config)
        {
            return null;
        }

        public IStorageProvider? CreateStorageProvider(RestConfig config)
        {
            return new S3StorageProvider(config);
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
