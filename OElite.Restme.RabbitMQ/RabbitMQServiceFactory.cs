using System;
using OElite.Abstractions;

namespace OElite.Providers
{
    /// <summary>
    /// Service factory for RabbitMQ providers
    /// </summary>
    public class RabbitMQServiceFactory : IServiceFactory
    {
        public ICacheProvider CreateCacheProvider(string connectionString, RestConfig config)
        {
            throw new NotImplementedException("Cache operations not supported by RabbitMQ provider. Use Redis provider instead.");
        }

        public IQueueProvider CreateQueueProvider(string connectionString, RestConfig config)
        {
            return new RabbitMQProvider(connectionString, config);
        }

        public IStorageProvider CreateStorageProvider(string connectionString, RestConfig config)
        {
            throw new NotImplementedException("Storage operations not supported by RabbitMQ provider. Use Azure or S3 provider instead.");
        }

        public IHttpProvider CreateHttpProvider(RestConfig config)
        {
            throw new NotImplementedException("HTTP operations not supported by RabbitMQ provider. Use HTTP provider instead.");
        }

        public ILogProvider CreateLogProvider(RestConfig config)
        {
            return new DefaultLogProvider();
        }
    }
}
