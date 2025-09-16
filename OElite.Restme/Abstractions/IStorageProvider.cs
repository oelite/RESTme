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
        Task<T?> GetAsync<T>(string objectKey) where T : class;
        
        /// <summary>
        /// Put data to storage
        /// </summary>
        Task<T?> PutAsync<T>(string objectKey, T value) where T : class;
        
        /// <summary>
        /// Delete data from storage
        /// </summary>
        Task<bool> DeleteAsync(string objectKey);
        
        /// <summary>
        /// Check if data exists in storage
        /// </summary>
        Task<bool> ExistsAsync(string objectKey);
        
        /// <summary>
        /// Get string data from storage
        /// </summary>
        Task<string?> GetStringAsync(string objectKey);
        
        /// <summary>
        /// Put string data to storage
        /// </summary>
        Task<string?> PutStringAsync(string objectKey, string value);
        
        /// <summary>
        /// Get stream data from storage
        /// </summary>
        Task<Stream?> GetStreamAsync(string objectKey);
        
        /// <summary>
        /// Put stream data to storage
        /// </summary>
        Task<bool> PutStreamAsync(string objectKey, Stream stream);
        
        /// <summary>
        /// Get stream data as specific stream type
        /// </summary>
        Task<T> GetStreamAsync<T>(string objectKey) where T : Stream;
    }
}
