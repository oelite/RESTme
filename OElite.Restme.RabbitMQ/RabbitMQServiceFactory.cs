using System;
using System.Runtime.CompilerServices;
using OElite.Restme;
using OElite.Restme.Abstractions;

namespace OElite.Providers
{
    /// <summary>
    /// Service factory for RabbitMQ providers
    /// </summary>
    public class RabbitMQServiceFactory : IServiceFactory
    {
        static RabbitMQServiceFactory()
        {
            // Auto-register this factory when the assembly is loaded
            ServiceLocator.RegisterFactory("rabbitmq", new RabbitMQServiceFactory());
        }

        /// <summary>
        /// Module initializer to ensure registration happens when assembly is loaded
        /// </summary>
        [ModuleInitializer]
        public static void Initialize()
        {
            // Force static constructor to run
            ServiceLocator.RegisterFactory("rabbitmq", new RabbitMQServiceFactory());
        }

        /// <summary>
        /// RabbitMQ supports Queue capability only
        /// </summary>
        public ProviderCapabilities SupportedCapabilities => ProviderCapabilities.Queue;

        /// <summary>
        /// Checks if this factory can create the requested provider type
        /// </summary>
        public bool CanCreateProvider<T>() where T : class, IRestmeProvider
        {
            return typeof(T) == typeof(IQueueProvider);
        }

        /// <summary>
        /// Generic provider creation with capability detection
        /// </summary>
        public T? CreateProvider<T>(RestConfig config) where T : class, IRestmeProvider
        {
            if (typeof(T) == typeof(IQueueProvider))
            {
                return new RabbitMQProvider(config) as T;
            }

            return null;
        }

        public ICacheProvider? CreateCacheProvider(RestConfig config)
        {
            return null;
        }

        public IQueueProvider? CreateQueueProvider(RestConfig config)
        {
            return new RabbitMQProvider(config);
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
