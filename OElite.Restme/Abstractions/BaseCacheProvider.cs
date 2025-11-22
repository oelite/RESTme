using System;
using System.Threading;
using System.Threading.Tasks;

namespace OElite.Restme.Abstractions
{
    /// <summary>
    /// Base implementation for cache providers with common functionality
    /// </summary>
    public abstract class BaseCacheProvider : ICacheProvider
    {
        protected readonly RestConfig Config;

        protected BaseCacheProvider(RestConfig config)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Provider name for debugging and logging
        /// </summary>
        public abstract string ProviderName { get; }

        /// <summary>
        /// Configuration used to create this provider
        /// </summary>
        public RestConfig Configuration => Config;

        /// <summary>
        /// Capabilities supported by this provider
        /// </summary>
        public abstract ProviderCapabilities Capabilities { get; }

        // Methods with cancellation token support (default parameter provides backward compatibility)
        public abstract Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;
        public abstract Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default) where T : class;
        public abstract Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default);
        public abstract Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
        public abstract Task<bool> SetExpiryAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default);

        public abstract void Dispose();

        /// <summary>
        /// Validates cache key input
        /// </summary>
        protected static void ValidateKey(string key, string operation)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException($"Cache key cannot be null, empty, or whitespace for {operation}", nameof(key));
        }

        /// <summary>
        /// Validates cache value input
        /// </summary>
        protected static void ValidateValue<T>(T value, string operation) where T : class
        {
            if (value == null)
                throw new ArgumentException($"Cache value cannot be null for {operation}", nameof(value));
        }
    }
}
