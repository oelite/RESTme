using OElite.Restme.Abstractions;

namespace OElite.Restme.Kafka
{
    /// <summary>
    /// Service factory for Kafka provider
    /// </summary>
    public class KafkaServiceFactory : IServiceFactory
    {
        static KafkaServiceFactory()
        {
            ServiceLocator.RegisterFactory("kafka", new KafkaServiceFactory());
        }

        /// <summary>
        /// Kafka supports Streaming capability only
        /// </summary>
        public ProviderCapabilities SupportedCapabilities => ProviderCapabilities.EventStream;

        /// <summary>
        /// Checks if this factory can create the requested provider type
        /// </summary>
        public bool CanCreateProvider<T>() where T : class, IRestmeProvider
        {
            return typeof(T) == typeof(IEventStreamProvider);
        }

        /// <summary>
        /// Generic provider creation with capability detection
        /// </summary>
        public T? CreateProvider<T>(RestConfig config) where T : class, IRestmeProvider
        {
            if (typeof(T) == typeof(IEventStreamProvider))
            {
                return new KafkaProvider(config) as T;
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
            return new KafkaProvider(config);
        }

        public ISearchProvider? CreateSearchProvider(RestConfig config)
        {
            return null;
        }
    }
}