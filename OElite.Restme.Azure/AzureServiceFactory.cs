using System;
using OElite.Abstractions;

namespace OElite.Providers
{
    /// <summary>
    /// Service factory for Azure providers
    /// </summary>
    public class AzureServiceFactory : IServiceFactory
    {
        public ICacheProvider CreateCacheProvider(string connectionString, RestConfig config)
        {
            throw new NotImplementedException("Cache operations not supported by Azure provider. Use Redis provider instead.");
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
    }
}
