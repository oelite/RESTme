using System;
using System.Threading;
using System.Threading.Tasks;

namespace OElite.Restme.Abstractions
{
    /// <summary>
    /// Interface for cache operations (Redis, etc.)
    /// </summary>
    public interface ICacheProvider : IRestmeProvider
    {
        /// <summary>
        /// Get cached data by key
        /// </summary>
        Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Set cached data with optional expiry
        /// </summary>
        Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Remove cached data by key
        /// </summary>
        Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if key exists in cache
        /// </summary>
        Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Set expiry for existing key
        /// </summary>
        Task<bool> SetExpiryAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get original data from ResponseMessage wrapper
        /// </summary>
        T? GetOriginalData<T>(ResponseMessage? responseMessage) where T : class;
    }
}
