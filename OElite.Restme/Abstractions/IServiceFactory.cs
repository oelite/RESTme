using System;

namespace OElite.Abstractions
{
    /// <summary>
    /// Factory interface for creating service providers
    /// </summary>
    public interface IServiceFactory
    {
        /// <summary>
        /// Create cache provider
        /// </summary>
        ICacheProvider CreateCacheProvider(string connectionString, RestConfig config);
        
        /// <summary>
        /// Create queue provider
        /// </summary>
        IQueueProvider CreateQueueProvider(string connectionString, RestConfig config);
        
        /// <summary>
        /// Create storage provider
        /// </summary>
        IStorageProvider CreateStorageProvider(string connectionString, RestConfig config);
        
        /// <summary>
        /// Create HTTP provider
        /// </summary>
        IHttpProvider CreateHttpProvider(RestConfig config);
        
        /// <summary>
        /// Create log provider
        /// </summary>
        ILogProvider CreateLogProvider(RestConfig config);
    }
}
