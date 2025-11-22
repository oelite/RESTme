using System;
using Microsoft.Extensions.Logging;

namespace OElite.Restme.Abstractions
{
    /// <summary>
    /// Default service factory that provides fallback implementations
    /// This maintains backward compatibility when no specific providers are registered
    /// </summary>
    public class DefaultServiceFactory : IServiceFactory
    {
        private readonly ILogger? _logger;

        public DefaultServiceFactory(ILogger? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// Default factory supports no capabilities
        /// </summary>
        public ProviderCapabilities SupportedCapabilities => ProviderCapabilities.None;

        /// <summary>
        /// Check if this factory can create a specific provider type
        /// Default factory cannot create any providers
        /// </summary>
        public bool CanCreateProvider<T>() where T : class, IRestmeProvider
        {
            return false;
        }

        /// <summary>
        /// Create a provider of the specified type if supported
        /// Default factory returns null for all provider types
        /// </summary>
        public T? CreateProvider<T>(RestConfig config) where T : class, IRestmeProvider
        {
            return null;
        }

        public ICacheProvider? CreateCacheProvider(RestConfig config)
        {
            // This will be implemented by the Redis provider project
            throw new NotImplementedException(
                "Redis provider not loaded. Please reference OElite.Restme.Redis package.");
        }

        public IQueueProvider CreateQueueProvider(RestConfig config)
        {
            // This will be implemented by the RabbitMQ provider project
            throw new NotImplementedException(
                "RabbitMQ provider not loaded. Please reference OElite.Restme.RabbitMQ package.");
        }

        public IStorageProvider CreateStorageProvider(RestConfig config)
        {
            // This will be implemented by the Azure/S3 provider projects
            throw new NotImplementedException(
                "Storage provider not loaded. Please reference OElite.Restme.Azure or OElite.Restme.S3 package.");
        }

        public IHttpProvider CreateHttpProvider(RestConfig config)
        {
            // HTTP provider can be implemented in the main project or separate project
            return new DefaultHttpProvider(config, _logger);
        }

        public ILogProvider CreateLogProvider(RestConfig config)
        {
            return new DefaultLogProvider(_logger);
        }

        public IColumnarProvider CreateColumnarProvider(RestConfig config)
        {
            throw new NotImplementedException(
                "ClickHouse provider not loaded. Please reference OElite.Restme.ClickHouse package.");
        }

        public IStreamingProvider CreateStreamingProvider(RestConfig config)
        {
            throw new NotImplementedException(
                "Kafka provider not loaded. Please reference OElite.Restme.Kafka package.");
        }

        public ISearchProvider CreateSearchProvider(RestConfig config)
        {
            throw new NotImplementedException(
                "OpenSearch provider not loaded. Please reference OElite.Restme.OpenSearch package.");
        }
    }
}