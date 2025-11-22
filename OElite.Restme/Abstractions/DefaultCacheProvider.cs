using System;
using System.Threading;
using System.Threading.Tasks;

namespace OElite.Restme.Abstractions;

/// <summary>
/// Default cache provider that throws helpful error
/// </summary>
public class DefaultCacheProvider : ICacheProvider
{
    /// <summary>
    /// Provider name for debugging and logging
    /// </summary>
    public string ProviderName => "DefaultCache";

    /// <summary>
    /// Configuration used to create this provider
    /// </summary>
    public RestConfig Configuration => new(RestMode.Memory);

    /// <summary>
    /// Capabilities supported by this provider
    /// </summary>
    public ProviderCapabilities Capabilities => ProviderCapabilities.Cache;

    public void Dispose() { }

    public Task<T?> GetAsync<T>(string key) where T : class
    {
        throw new NotImplementedException("Redis provider not loaded. Please reference OElite.Restme.Redis package.");
    }

    public Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class
    {
        throw new NotImplementedException("Redis provider not loaded. Please reference OElite.Restme.Redis package.");
    }

    public Task<bool> RemoveAsync(string key)
    {
        throw new NotImplementedException("Redis provider not loaded. Please reference OElite.Restme.Redis package.");
    }

    public Task<bool> ExistsAsync(string key)
    {
        throw new NotImplementedException("Redis provider not loaded. Please reference OElite.Restme.Redis package.");
    }

    public Task<bool> SetExpiryAsync(string key, TimeSpan expiry)
    {
        throw new NotImplementedException("Redis provider not loaded. Please reference OElite.Restme.Redis package.");
    }


    // Cancellation token overloads
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        throw new NotImplementedException("Redis provider not loaded. Please reference OElite.Restme.Redis package.");
    }

    public Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default) where T : class
    {
        throw new NotImplementedException("Redis provider not loaded. Please reference OElite.Restme.Redis package.");
    }

    public Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Redis provider not loaded. Please reference OElite.Restme.Redis package.");
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Redis provider not loaded. Please reference OElite.Restme.Redis package.");
    }

    public Task<bool> SetExpiryAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Redis provider not loaded. Please reference OElite.Restme.Redis package.");
    }
}