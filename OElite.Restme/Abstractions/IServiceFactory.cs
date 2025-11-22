namespace OElite.Restme.Abstractions
{
    /// <summary>
    /// Factory interface for creating service providers with capability detection
    /// </summary>
    public interface IServiceFactory
    {
        /// <summary>
        /// Get the capabilities supported by this factory
        /// </summary>
        ProviderCapabilities SupportedCapabilities { get; }

        /// <summary>
        /// Check if this factory can create a specific provider type
        /// </summary>
        bool CanCreateProvider<T>() where T : class, IRestmeProvider;

        /// <summary>
        /// Create a provider of the specified type if supported
        /// Returns null if the provider type is not supported by this factory
        /// </summary>
        T? CreateProvider<T>(RestConfig config) where T : class, IRestmeProvider;

        // Legacy methods for backward compatibility - will be deprecated
        
        /// <summary>
        /// Create cache provider (returns null if not supported)
        /// </summary>
        ICacheProvider? CreateCacheProvider(RestConfig config);
        
        /// <summary>
        /// Create queue provider (returns null if not supported)
        /// </summary>
        IQueueProvider? CreateQueueProvider(RestConfig config);
        
        /// <summary>
        /// Create storage provider (returns null if not supported)
        /// </summary>
        IStorageProvider? CreateStorageProvider(RestConfig config);
        
        /// <summary>
        /// Create HTTP provider (returns null if not supported)
        /// </summary>
        IHttpProvider? CreateHttpProvider(RestConfig config);
        
        /// <summary>
        /// Create log provider (returns null if not supported)
        /// </summary>
        ILogProvider? CreateLogProvider(RestConfig config);

        /// <summary>
        /// Create columnar provider (returns null if not supported)
        /// </summary>
        IColumnarProvider? CreateColumnarProvider(RestConfig config);

        /// <summary>
        /// Create streaming provider (returns null if not supported)
        /// </summary>
        IStreamingProvider? CreateStreamingProvider(RestConfig config);

        /// <summary>
        /// Create search provider (returns null if not supported)
        /// </summary>
        ISearchProvider? CreateSearchProvider(RestConfig config);
    }
}
