using System;
using System.Threading.Tasks;

namespace OElite.Abstractions
{
    /// <summary>
    /// Interface for cache operations (Redis, etc.)
    /// </summary>
    public interface ICacheProvider : IDisposable
    {
        /// <summary>
        /// Get cached data by key
        /// </summary>
        Task<T?> GetAsync<T>(string key) where T : class;
        
        /// <summary>
        /// Set cached data with optional expiry
        /// </summary>
        Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class;
        
        /// <summary>
        /// Remove cached data by key
        /// </summary>
        Task<bool> RemoveAsync(string key);
        
        /// <summary>
        /// Check if key exists in cache
        /// </summary>
        Task<bool> ExistsAsync(string key);
        
        /// <summary>
        /// Set expiry for existing key
        /// </summary>
        Task<bool> SetExpiryAsync(string key, TimeSpan expiry);
        
        /// <summary>
        /// Get original data from ResponseMessage wrapper
        /// </summary>
        T? GetOriginalData<T>(ResponseMessage? responseMessage) where T : class;
    }
}
