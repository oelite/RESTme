using OElite.Restme.Abstractions;
using System.Runtime.CompilerServices;

namespace OElite.Restme.ClickHouse
{
    /// <summary>
    /// Service factory for ClickHouse provider
    /// </summary>
    public class ClickHouseServiceFactory : IServiceFactory
    {
        static ClickHouseServiceFactory()
        {
            ServiceLocator.RegisterFactory("clickhouse", new ClickHouseServiceFactory());
        }

        /// <summary>
        /// Module initializer to ensure factory registration happens when assembly loads
        /// </summary>
        [ModuleInitializer]
        public static void Initialize()
        {
            // Force static constructor to run
            ServiceLocator.RegisterFactory("clickhouse", new ClickHouseServiceFactory());
        }

        /// <summary>
        /// ClickHouse supports Columnar capability only
        /// </summary>
        public ProviderCapabilities SupportedCapabilities => ProviderCapabilities.Columnar;

        /// <summary>
        /// Checks if this factory can create the requested provider type
        /// </summary>
        public bool CanCreateProvider<T>() where T : class, IRestmeProvider
        {
            return typeof(T) == typeof(IColumnarProvider);
        }

        /// <summary>
        /// Generic provider creation with capability detection
        /// </summary>
        public T? CreateProvider<T>(RestConfig config) where T : class, IRestmeProvider
        {
            if (typeof(T) == typeof(IColumnarProvider))
            {
                return new ClickHouseProvider(config) as T;
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
            return new ClickHouseProvider(config);
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