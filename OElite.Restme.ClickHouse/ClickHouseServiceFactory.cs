using System;
using OElite.Abstractions;

namespace OElite.Restme.ClickHouse
{
    /// <summary>
    /// Service factory for ClickHouse provider
    /// </summary>
    public class ClickHouseServiceFactory : IServiceFactory
    {
        public ICacheProvider CreateCacheProvider(string connectionString, RestConfig config)
        {
            throw new NotImplementedException("ClickHouse does not provide caching services");
        }

        public IQueueProvider CreateQueueProvider(string connectionString, RestConfig config)
        {
            throw new NotImplementedException("ClickHouse does not provide queuing services");
        }

        public IStorageProvider CreateStorageProvider(string connectionString, RestConfig config)
        {
            throw new NotImplementedException("ClickHouse does not provide storage services");
        }

        public IHttpProvider CreateHttpProvider(RestConfig config)
        {
            throw new NotImplementedException("ClickHouse does not provide HTTP services");
        }

        public ILogProvider CreateLogProvider(RestConfig config)
        {
            throw new NotImplementedException("ClickHouse does not provide logging services");
        }

        public IColumnarProvider CreateColumnarProvider(string connectionString, RestConfig config)
        {
            return new ClickHouseProvider(connectionString, config);
        }

        public IStreamingProvider CreateStreamingProvider(string connectionString, RestConfig config)
        {
            throw new NotImplementedException("ClickHouse does not provide streaming services");
        }

        public ISearchProvider CreateSearchProvider(string connectionString, RestConfig config)
        {
            throw new NotImplementedException("ClickHouse does not provide search services");
        }
    }
}