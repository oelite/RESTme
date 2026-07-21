using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OElite.Restme.Base;
using OElite.Restme.Utils;

namespace OElite.Restme.Abstractions;

/// <summary>
/// Default HTTP provider implementation using HttpClientProvider
/// </summary>
internal class DefaultHttpProvider : IHttpProvider
{
    private readonly IHttpProvider _httpProvider;

    /// <summary>
    /// Provider name for debugging and logging
    /// </summary>
    public string ProviderName => "DefaultHttp";

    /// <summary>
    /// Configuration used to create this provider
    /// </summary>
    public RestConfig Configuration => new (RestMode.Http);

    /// <summary>
    /// Capabilities supported by this provider
    /// </summary>
    public ProviderCapabilities Capabilities => ProviderCapabilities.None;

    public DefaultHttpProvider(RestConfig config, ILogger? logger = null)
    {
        // Use the existing HttpClientProvider implementation
        _httpProvider = new HttpClientProvider(config, logger);
    }

    public void Dispose()
    {
        _httpProvider.Dispose();
    }

    public Task<T?> HttpRequestAsync<T>(HttpMethod method, string? keyOrRelativePath, HttpRequestContext context)
    {
        return _httpProvider.HttpRequestAsync<T>(method, keyOrRelativePath, context);
    }

    public Task<HttpResponseMessage?> HttpRequestFullAsync<T>(HttpMethod method, string? keyOrRelativePath, HttpRequestContext context)
    {
        return _httpProvider.HttpRequestFullAsync<T>(method, keyOrRelativePath, context);
    }

    public Task<T?> HttpRequestAsync<T>(HttpMethod method, string? keyOrRelativePath = null, object? dataObject = null)
    {
        return _httpProvider.HttpRequestAsync<T>(method, keyOrRelativePath, dataObject);
    }

    public Task<HttpResponseMessage?> HttpRequestFullAsync<T>(HttpMethod method, string? keyOrRelativePath = null, object? dataObject = null)
    {
        return _httpProvider.HttpRequestFullAsync<T>(method, keyOrRelativePath, dataObject);
    }

    public Task<T?> GetAsync<T>(string? keyOrRelativePath = null, object? dataObject = null)
    {
        return _httpProvider.GetAsync<T>(keyOrRelativePath, dataObject);
    }

    public Task<T?> PostAsync<T>(string? keyOrRelativePath = null, object? dataObject = null, TimeSpan? expiryInMinutes = null)
    {
        return _httpProvider.PostAsync<T>(keyOrRelativePath, dataObject, expiryInMinutes);
    }

    public Task<T?> PutAsync<T>(string? keyOrRelativePath = null, object? dataObject = null, TimeSpan? expiryInMinutes = null)
    {
        return _httpProvider.PutAsync<T>(keyOrRelativePath, dataObject, expiryInMinutes);
    }

    public Task<T?> DeleteAsync<T>(string? keyOrRelativePath = null)
    {
        return _httpProvider.DeleteAsync<T>(keyOrRelativePath);
    }

    public Task<HttpResponseMessage<T>?> HttpRequestFullWithDetailsAsync<T>(HttpMethod method, string? keyOrRelativePath, HttpRequestContext context)
    {
        return _httpProvider.HttpRequestFullWithDetailsAsync<T>(method, keyOrRelativePath, context);
    }

    public Task<HttpResponseMessage<T>?> HttpRequestFullWithDetailsAsync<T>(HttpMethod method, string? keyOrRelativePath = null, object? dataObject = null)
    {
        return _httpProvider.HttpRequestFullWithDetailsAsync<T>(method, keyOrRelativePath, dataObject);
    }
}