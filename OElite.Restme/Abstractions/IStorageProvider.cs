using System;
using System.IO;
using System.Threading.Tasks;

namespace OElite.Abstractions
{
    /// <summary>
    /// Interface for storage operations (Azure Blob, S3, etc.)
    /// </summary>
    public interface IStorageProvider : IDisposable
    {
        /// <summary>
        /// Get data from storage
        /// </summary>
        Task<T?> GetAsync<T>(string key) where T : class;
        
        /// <summary>
        /// Put data to storage
        /// </summary>
        Task<T?> PutAsync<T>(string key, T value) where T : class;
        
        /// <summary>
        /// Delete data from storage
        /// </summary>
        Task<bool> DeleteAsync(string key);
        
        /// <summary>
        /// Check if data exists in storage
        /// </summary>
        Task<bool> ExistsAsync(string key);
        
        /// <summary>
        /// Get string data from storage
        /// </summary>
        Task<string?> GetStringAsync(string key);
        
        /// <summary>
        /// Put string data to storage
        /// </summary>
        Task<string?> PutStringAsync(string key, string value);
        
        /// <summary>
        /// Get stream data from storage
        /// </summary>
        Task<Stream?> GetStreamAsync(string key);
        
        /// <summary>
        /// Put stream data to storage
        /// </summary>
        Task<bool> PutStreamAsync(string key, Stream stream);
        
        /// <summary>
        /// Get stream data as specific stream type
        /// </summary>
        Task<T?> GetStreamAsync<T>(string key) where T : Stream;
    }
}
