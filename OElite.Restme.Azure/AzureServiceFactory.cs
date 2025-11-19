using System;
using OElite.Abstractions;

namespace OElite.Providers
{
    /// <summary>
    /// Service factory for Azure providers
    /// </summary>
    public class AzureServiceFactory : IServiceFactory
    {
        static AzureServiceFactory()
        {
            // Auto-register this factory when the assembly is loaded
            ServiceLocator.RegisterFactory("azure", new AzureServiceFactory());
        }
        public ICacheProvider CreateCacheProvider(string connectionString, RestConfig config)
        {
            return new AzureCacheProvider(connectionString, config);
        }

        public IQueueProvider CreateQueueProvider(string connectionString, RestConfig config)
        {
            throw new NotImplementedException("Queue operations not supported by Azure provider. Use RabbitMQ provider instead.");
        }

        public IStorageProvider CreateStorageProvider(string connectionString, RestConfig config)
        {
            return new AzureStorageProvider(connectionString, config);
        }

        public IHttpProvider CreateHttpProvider(RestConfig config)
        {
            throw new NotImplementedException("HTTP operations not supported by Azure provider. Use HTTP provider instead.");
        }

        public ILogProvider CreateLogProvider(RestConfig config)
        {
            return new DefaultLogProvider();
        }

        public IColumnarProvider CreateColumnarProvider(string connectionString, RestConfig config)
        {
            throw new NotImplementedException("Columnar operations not supported by Azure provider. Use ClickHouse provider instead.");
        }

        public IStreamingProvider CreateStreamingProvider(string connectionString, RestConfig config)
        {
            throw new NotImplementedException("Streaming operations not supported by Azure provider. Use Kafka provider instead.");
        }

        public ISearchProvider CreateSearchProvider(string connectionString, RestConfig config)
        {
            throw new NotImplementedException("Search operations not supported by Azure provider. Use OpenSearch provider instead.");
        }
    }
}
