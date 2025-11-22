using System;
using OElite.Providers;
using OElite.Restme.Abstractions;

namespace OElite.Restme.Azure
{
    /// <summary>
    /// Service factory for Azure providers
    /// Supports Cache and Storage capabilities via Azure Blob Storage
    /// </summary>
    public class AzureServiceFactory : IServiceFactory
    {
        static AzureServiceFactory()
        {
            // Auto-register this factory when the assembly is loaded
            ServiceLocator.RegisterFactory("azure", new AzureServiceFactory());
        }

        /// <summary>
        /// Azure provider supports Cache and Storage capabilities
        /// </summary>
        public ProviderCapabilities SupportedCapabilities => ProviderCapabilities.Cache | ProviderCapabilities.Storage;

        /// <summary>
        /// Check if this factory can create a specific provider type
        /// </summary>
        public bool CanCreateProvider<T>() where T : class, IRestmeProvider
        {
            var type = typeof(T);
            return type == typeof(ICacheProvider) || type == typeof(IStorageProvider);
        }

        /// <summary>
        /// Create a provider of the specified type if supported
        /// </summary>
        public T? CreateProvider<T>(RestConfig config) where T : class, IRestmeProvider
        {
            var type = typeof(T);

            if (type == typeof(ICacheProvider))
                return new AzureCacheProvider(config) as T;

            if (type == typeof(IStorageProvider))
                return new AzureStorageProvider(config) as T;

            return null;
        }

        // Legacy methods - return null for unsupported providers

        public ICacheProvider? CreateCacheProvider(RestConfig config)
        {
            return new AzureCacheProvider(config);
        }

        public IQueueProvider? CreateQueueProvider(RestConfig config)
        {
            return null;
        }

        public IStorageProvider? CreateStorageProvider(RestConfig config)
        {
            return new AzureStorageProvider(config);
        }

        public IHttpProvider? CreateHttpProvider(RestConfig config)
        {
            return null;
        }

        public ILogProvider? CreateLogProvider(RestConfig config)
        {
            return null;
        }

        public IColumnarProvider? CreateColumnarProvider(RestConfig config)
        {
            return null;
        }

        public IStreamingProvider? CreateStreamingProvider(RestConfig config)
        {
            return null;
        }

        public ISearchProvider? CreateSearchProvider(RestConfig config)
        {
            return null;
        }
    }
}