using System;
using OElite.Abstractions;

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
        public ICacheProvider CreateCacheProvider(string connectionString, RestConfig config)
        {
            return new S3CacheProvider(connectionString, config);
        }

        public IQueueProvider CreateQueueProvider(string connectionString, RestConfig config)
        {
            throw new NotImplementedException("Queue operations not supported by S3 provider. Use RabbitMQ provider instead.");
        }

        public IStorageProvider CreateStorageProvider(string connectionString, RestConfig config)
        {
            return new S3StorageProvider(connectionString, config);
        }

        public IHttpProvider CreateHttpProvider(RestConfig config)
        {
            throw new NotImplementedException("HTTP operations not supported by S3 provider. Use HTTP provider instead.");
        }

        public ILogProvider CreateLogProvider(RestConfig config)
        {
            return new DefaultLogProvider();
        }

        public IColumnarProvider CreateColumnarProvider(string connectionString, RestConfig config)
        {
            throw new NotImplementedException("Columnar operations not supported by S3 provider. Use ClickHouse provider instead.");
        }

        public IStreamingProvider CreateStreamingProvider(string connectionString, RestConfig config)
        {
            throw new NotImplementedException("Streaming operations not supported by S3 provider. Use Kafka provider instead.");
        }

        public ISearchProvider CreateSearchProvider(string connectionString, RestConfig config)
        {
            throw new NotImplementedException("Search operations not supported by S3 provider. Use OpenSearch provider instead.");
        }
    }
}
