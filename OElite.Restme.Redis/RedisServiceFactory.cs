using System;
using OElite.Abstractions;

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
        public ICacheProvider CreateCacheProvider(string connectionString, RestConfig config)
        {
            return new RedisCacheProvider(connectionString, config);
        }

        public IQueueProvider CreateQueueProvider(string connectionString, RestConfig config)
        {
            throw new NotImplementedException("Queue operations not supported by Redis provider. Use RabbitMQ provider instead.");
        }

        public IStorageProvider CreateStorageProvider(string connectionString, RestConfig config)
        {
            throw new NotImplementedException("Storage operations not supported by Redis provider. Use Azure or S3 provider instead.");
        }

        public IHttpProvider CreateHttpProvider(RestConfig config)
        {
            throw new NotImplementedException("HTTP operations not supported by Redis provider. Use HTTP provider instead.");
        }

        public ILogProvider CreateLogProvider(RestConfig config)
        {
            return new DefaultLogProvider();
        }

        public IColumnarProvider CreateColumnarProvider(string connectionString, RestConfig config)
        {
            throw new NotImplementedException("Columnar operations not supported by Redis provider. Use ClickHouse provider instead.");
        }

        public IStreamingProvider CreateStreamingProvider(string connectionString, RestConfig config)
        {
            throw new NotImplementedException("Streaming operations not supported by Redis provider. Use Kafka provider instead.");
        }

        public ISearchProvider CreateSearchProvider(string connectionString, RestConfig config)
        {
            throw new NotImplementedException("Search operations not supported by Redis provider. Use OpenSearch provider instead.");
        }
    }
}
