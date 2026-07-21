using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using OElite.Restme.Utils;

namespace OElite.Restme.Abstractions
{
    /// <summary>
    /// HTTP request context containing all necessary information for HTTP operations
    /// </summary>
    public class HttpRequestContext
    {
        public Uri? BaseUri { get; set; }
        public Dictionary<string, string> Parameters { get; set; } = new();
        public Dictionary<string, List<string>> Headers { get; set; } = new();
        public object? DataObject { get; set; }
        public int TimeoutMs { get; set; }
    }

    /// <summary>
    /// Interface for HTTP operations
    /// </summary>
    public interface IHttpProvider : IRestmeProvider
    {
        /// <summary>
        /// Perform HTTP request with context
        /// </summary>
        Task<T?> HttpRequestAsync<T>(HttpMethod method, string? keyOrRelativePath, HttpRequestContext context);

        /// <summary>
        /// Perform HTTP request with full response
        /// </summary>
        Task<HttpResponseMessage?> HttpRequestFullAsync<T>(HttpMethod method, string? keyOrRelativePath, HttpRequestContext context);

        /// <summary>
        /// Enhanced HTTP request with detailed response information
        /// </summary>
        Task<HttpResponseMessage<T>?> HttpRequestFullWithDetailsAsync<T>(HttpMethod method, string? keyOrRelativePath, HttpRequestContext context);
        Task<HttpResponseMessage<T>?> HttpRequestFullWithDetailsAsync<T>(HttpMethod method, string? keyOrRelativePath = null, object? dataObject = null);

        /// <summary>
        /// Simplified HTTP request methods for backward compatibility
        /// </summary>
        Task<T?> HttpRequestAsync<T>(HttpMethod method, string? keyOrRelativePath = null, object? dataObject = null);
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
