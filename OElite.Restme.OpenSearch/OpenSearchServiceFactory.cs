using OElite.Restme.Abstractions;

namespace OElite.Restme.OpenSearch
{
    /// <summary>
    /// Service factory for OpenSearch provider
    /// </summary>
    public class OpenSearchServiceFactory : IServiceFactory
    {
        static OpenSearchServiceFactory()
        {
            ServiceLocator.RegisterFactory("opensearch", new OpenSearchServiceFactory());
        }

        /// <summary>
        /// OpenSearch supports Search capability only
        /// </summary>
        public ProviderCapabilities SupportedCapabilities => ProviderCapabilities.Search;

        /// <summary>
        /// Checks if this factory can create the requested provider type
        /// </summary>
        public bool CanCreateProvider<T>() where T : class, IRestmeProvider
        {
            return typeof(T) == typeof(ISearchProvider);
        }

        /// <summary>
        /// Generic provider creation with capability detection
        /// </summary>
        public T? CreateProvider<T>(RestConfig config) where T : class, IRestmeProvider
        {
            if (typeof(T) == typeof(ISearchProvider))
            {
                return new OpenSearchProvider(config) as T;
            }

            return null;
        }

        public ICacheProvider? CreateCacheProvider(RestConfig config)
        {
            return null;
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

        public ILogProvider? CreateLogProvider(RestConfig config)
        {
            return null;
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
            return new OpenSearchProvider(config);
        }
    }
}