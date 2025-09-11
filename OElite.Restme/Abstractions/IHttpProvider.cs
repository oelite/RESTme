using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace OElite.Abstractions
{
    /// <summary>
    /// Interface for HTTP operations
    /// </summary>
    public interface IHttpProvider : IDisposable
    {
        /// <summary>
        /// Perform HTTP request
        /// </summary>
        Task<T?> HttpRequestAsync<T>(HttpMethod method, string? keyOrRelativePath = null, object? dataObject = null);
        
        /// <summary>
        /// Perform HTTP request with full response
        /// </summary>
        Task<HttpResponseMessage?> HttpRequestFullAsync<T>(HttpMethod method, string? keyOrRelativePath = null, object? dataObject = null);
        
        /// <summary>
        /// GET request
        /// </summary>
        Task<T?> GetAsync<T>(string? keyOrRelativePath = null, object? dataObject = null);
        
        /// <summary>
        /// POST request
        /// </summary>
        Task<T?> PostAsync<T>(string? keyOrRelativePath = null, object? dataObject = null, TimeSpan? expiryInMinutes = null);
        
        /// <summary>
        /// PUT request
        /// </summary>
        Task<T?> PutAsync<T>(string? keyOrRelativePath = null, object? dataObject = null, TimeSpan? expiryInMinutes = null);
        
        /// <summary>
        /// DELETE request
        /// </summary>
        Task<T?> DeleteAsync<T>(string? keyOrRelativePath = null);
    }
}
