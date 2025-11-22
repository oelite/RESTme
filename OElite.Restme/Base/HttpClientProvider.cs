using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OElite.Restme.Abstractions;

namespace OElite.Restme.Base
{
    /// <summary>
    /// Default HttpClient-based provider using existing Restme HTTP extension behavior.
    /// </summary>
    public sealed class HttpClientProvider : IHttpProvider
    {
        private readonly RestConfig _config;
        private readonly ILogger? _logger;
        private bool _disposed;

        /// <summary>
        /// Provider name for debugging and logging
        /// </summary>
        public string ProviderName => "HttpClient";

        /// <summary>
        /// Configuration used to create this provider
        /// </summary>
        public RestConfig Configuration => _config;

        /// <summary>
        /// Capabilities supported by this provider
        /// </summary>
        public ProviderCapabilities Capabilities => ProviderCapabilities.None;

        public HttpClientProvider(RestConfig config, ILogger? logger = null)
        {
            _config = config;
            _logger = logger;
        }

        public Task<T?> HttpRequestAsync<T>(HttpMethod method, string? keyOrRelativePath = null, object? dataObject = null)
        {
            // Delegate to Restme extension pattern via a lightweight Rest instance
            var rest = new Rest(null, null, _config, _logger);
            if (dataObject != null)
            {
                rest.Add("payload", dataObject);
            }
            return rest.HttpRequestAsync<T>(method, keyOrRelativePath);
        }

        public Task<HttpResponseMessage?> HttpRequestFullAsync<T>(HttpMethod method, string? keyOrRelativePath = null, object? dataObject = null)
        {
            var rest = new Rest(null, null, _config, _logger);
            if (dataObject != null)
            {
                rest.Add("payload", dataObject);
            }
            // Bridge the Task<HttpResponseMessage<T?>> to Task<HttpResponseMessage>
            // Base provider focuses on simplified usage: return null (not used by current interfaces)
            return Task.FromResult<HttpResponseMessage?>(null);
        }

        public Task<T?> GetAsync<T>(string? keyOrRelativePath = null, object? dataObject = null)
        {
            return HttpRequestAsync<T>(HttpMethod.Get, keyOrRelativePath, dataObject);
        }

        public Task<T?> PostAsync<T>(string? keyOrRelativePath = null, object? dataObject = null, TimeSpan? expiryInMinutes = null)
        {
            return HttpRequestAsync<T>(HttpMethod.Post, keyOrRelativePath, dataObject);
        }

        public Task<T?> PutAsync<T>(string? keyOrRelativePath = null, object? dataObject = null, TimeSpan? expiryInMinutes = null)
        {
            return HttpRequestAsync<T>(HttpMethod.Put, keyOrRelativePath, dataObject);
        }

        public Task<T?> DeleteAsync<T>(string? keyOrRelativePath = null)
        {
            return HttpRequestAsync<T>(HttpMethod.Delete, keyOrRelativePath, null);
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
}


